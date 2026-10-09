using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IManagementAccountService"/> for admin account CRUD operations.
/// Passwords supplied in create/update operations are hashed via <see cref="IPasswordService"/>
/// before being stored. Soft-deleted accounts can be restored by the admin.
/// Errors are logged and returned to the caller as a typed <see cref="ServiceResult"/>
/// (<see cref="ServiceResult.NotFound"/> / <see cref="ServiceResult.Conflict"/> /
/// <see cref="ServiceResult.Failure"/>).
/// </summary>
public class ManagementAccountService(
    IAccountRepository accountRepository,
    IPasswordService passwordService,
    IUnitOfWork unitOfWork,
    ILogger<ManagementAccountService> logger,
    TimeProvider timeProvider) : IManagementAccountService
{
    /// <inheritdoc />
    public async Task<List<AdminAccountResponse>> GetAllAccountsAsync(CancellationToken ct = default)
        => [.. (await accountRepository.GetAllAsync(ct)).Select(AccountMapper.ToAdminResponse)];

    /// <inheritdoc />
    public async Task<List<AdminAccountResponse>> SearchAccountsAsync(string search, CancellationToken ct = default)
        => [.. (await accountRepository.SearchAsync(search, ct)).Select(AccountMapper.ToAdminResponse)];

    /// <inheritdoc />
    public async Task<AccountResponse?> GetAccountByEmailAsync(string email, CancellationToken ct = default)
    {
        Account? account = await FindByEmailAsync(email, ct);
        return account == null ? null : AccountMapper.ToResponse(account);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAccountAsync(AdminCreateAccountRequest request, string actorEmail, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            if (await FindByEmailAsync(request.Email ?? "", ct) != null)
                return ServiceResult.Conflict("Account.AlreadyExists", ServiceErrorKeys.AccountAlreadyCreated);

            // Nickname: validated + normalized like self-registration, except that an administrator
            // may deliberately use a reserved name. Uniqueness is checked ignoring case (the DB
            // constraint alone is case-sensitive).
            var nicknameResult = NicknamePolicy.Validate(request.Nickname, allowReserved: true);
            if (nicknameResult.IsError)
                return ServiceResult.FromError(nicknameResult.FirstError);
            string nickname = nicknameResult.Value;

            if (await accountRepository.NicknameExistsIgnoreCaseAsync(nickname, ct))
                return ServiceResult.Conflict("Account.NicknameAlreadyExists", ServiceErrorKeys.NicknameExists, nickname);

            // request.PlainPassword carries the raw password. The complexity rule is checked here,
            // before hashing, so the controller no longer has to — Account.Create only ever sees the hash.
            if (!PasswordPolicy.IsValid(request.PlainPassword))
                return ServiceResult.Validation("Account.PasswordPolicy", PasswordPolicy.ViolationMessage);

            var roleResult = AccountRole.Create(request.Role ?? Role.User);
            if (roleResult.IsError)
                return ServiceResult.FromError(roleResult.FirstError);

            string hashedPassword = passwordService.HashPassword(request.PlainPassword ?? "");

            var createResult = Account.Create(
                request.Email ?? "",
                hashedPassword,
                nickname,
                roleResult.Value,
                request.TimeZoneIanaId ?? "UTC",
                defaultMonetaryUnit: null,
                registrationToken: null,
                request.AgreedServiceTerms, utcNow,
                allowReservedNickname: true);

            if (createResult.IsError)
                return ServiceResult.FromError(createResult.FirstError);

            var account = createResult.Value;
            account.SetMessage(request.Message, utcNow);

            if (request.EmailConfirmed)
                account.ForceConfirmEmail(utcNow);

            try
            {
                await accountRepository.CreateAsync(account, ct);
            }
            catch (Exception e) when (e.IsPostgresUniqueViolation())
            {
                // The FindByEmailAsync / NicknameExistsIgnoreCaseAsync checks above are read-then-write
                // races: two concurrent admin creates for the same not-yet-registered email or
                // nickname can both pass them. Email is the actual primary key (Account_pk), while
                // Nickname has its own separate unique constraint (Account_Nickname_unique) — report
                // whichever one actually collided, same classification RegisterAsync uses.
                bool isNicknameConflict = e.IsAccountNicknameUniqueViolation();
                return isNicknameConflict
                    ? ServiceResult.Conflict("Account.NicknameAlreadyExists", ServiceErrorKeys.NicknameExists, nickname)
                    : ServiceResult.Conflict("Account.AlreadyExists", ServiceErrorKeys.AccountAlreadyCreated);
            }

 #pragma warning disable CA1873
            logger.LogInformation("Admin created account {Email} with role {Role} by admin {Admin}", account.Email.Value, account.Role, actorEmail);
 #pragma warning restore CA1873
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create account for {Email}", request.Email);
            return ServiceResult.Failure("ManagementAccount.CreateFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAccountAsync(AdminUpdateAccountRequest request, string? newPassword, string actorEmail, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        // An admin-set password always forces the account to pick its own new one (meeting
        // PasswordPolicy) at next login — see Account.AdminResetPassword. It must still meet the
        // policy itself so there is no weak-password window before that forced change. Hash it up
        // front, outside the transaction, so the row lock below isn't held across the (deliberately
        // slow) BCrypt work.
        string? newHashedPassword = null;
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (!PasswordPolicy.IsValid(newPassword))
                return ServiceResult.Validation("Account.PasswordPolicy", PasswordPolicy.ViolationMessage);

            newHashedPassword = passwordService.HashPassword(newPassword);
        }

        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            // FOR UPDATE: hold the row lock from read to commit so a concurrent self-service
            // column-scoped write (profile avatar/timezone/password change) on the same account
            // serializes behind this admin edit instead of being silently reverted by the full-row
            // write below.
            Account? account = await accountRepository.FindByEmailForUpdateAsync(request.Email ?? "", ct);
            if (account == null)
            {
                await unitOfWork.RollbackAsync(ct);
                logger.LogWarning("Admin update failed: account not found for {Email} by admin {Admin}", request.Email, actorEmail);
                return ServiceResult.NotFound("Account.NotFound", ServiceErrorKeys.EmailAddressWrong);
            }

            AccountRole oldRole = account.Role;
            bool oldLocked = account.Locked;
            bool oldDeleted = account.Deleted;

            if (newHashedPassword != null)
            {
                var resetResult = account.AdminResetPassword(newHashedPassword, utcNow);
                if (resetResult.IsError)
                {
                    // The policy above already refused a blank password, so an empty hash here is
                    // a hasher fault, not admin input.
                    await unitOfWork.RollbackAsync(ct);
                    logger.LogError("Admin password reset failed for {Email} by admin {Admin}: {ErrorCode}", account.Email.Value, actorEmail, resetResult.FirstError.Code);
                    return ServiceResult.Failure("ManagementAccount.UpdateFailed", ServiceResult.TemporaryErrorKey);
                }
            }

            var updateResult = ApplyAdminChanges(account, request, utcNow);
            if (updateResult.IsError)
            {
                await unitOfWork.RollbackAsync(ct);
                return ServiceResult.FromError(updateResult.FirstError);
            }

            await accountRepository.UpdateEntityAsync(account, ct);
            await unitOfWork.CommitAsync(ct);
 #pragma warning disable CA1873
            logger.LogInformation(
 #pragma warning restore CA1873
                "Admin updated account {Email}: role {OldRole} -> {NewRole}, locked {OldLocked} -> {NewLocked}, deleted {OldDeleted} -> {NewDeleted}, password reset {PasswordReset} by admin {Admin}",
                account.Email.Value, oldRole, account.Role, oldLocked, account.Locked, oldDeleted, account.Deleted, newHashedPassword != null, actorEmail);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update account for {Email}", request.Email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("ManagementAccount.UpdateFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <summary>
    /// Turns the admin form's desired state into the account's own operations. A null role or time
    /// zone keeps the current value. Saving an account as unlocked always clears its failed-login
    /// counter (<see cref="Account.Unlock"/>); locking keeps the counter. The admin's message is
    /// applied last so it is what the account ends up showing, whatever lock/unlock wrote.
    /// </summary>
    private static ErrorOr<Success> ApplyAdminChanges(Account account, AdminUpdateAccountRequest request, DateTime utcNow)
    {
        if (request.TimeZoneIanaId != null)
        {
            var tzResult = account.ChangeTimeZone(request.TimeZoneIanaId, utcNow);
            if (tzResult.IsError) return tzResult.Errors;
        }

        if (request.Role != null)
        {
            var roleResult = AccountRole.Create(request.Role);
            if (roleResult.IsError) return roleResult.Errors;
            account.ChangeRole(roleResult.Value, utcNow);
        }

        switch (request.Locked)
        {
            // Locking an unlocked account is the admin's sanction and ends its sessions; re-saving an account that is
            // already locked (e.g. by failed logins) keeps them, as that lock only refuses new logins.
            case true when account.Locked:
                account.Lock(utcNow);
                break;
            case true:
                account.LockAndEndSessions(utcNow);
                break;
            default:
                account.Unlock(utcNow);
                break;
        }

        switch (request.EmailConfirmed)
        {
            case true when !account.EmailConfirmed:
                account.ForceConfirmEmail(utcNow);
                break;
            case false when account.EmailConfirmed:
                account.RevokeEmailConfirmation(utcNow);
                break;
        }

        if (request.AgreedServiceTerms)
            account.AcceptServiceTerms(utcNow);
        else
            account.RevokeServiceTerms(utcNow);

        if (request.Deleted)
            account.SoftDelete(utcNow);
        else
            account.Restore(utcNow);

        account.SetMessage(request.Message, utcNow);
        return Result.Success;
    }

    /// <summary>Unlocked lookup of an account by email.</summary>
    private Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default)
        => accountRepository.FindByEmailAsync(email, ct);

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAccountAsync(string email, string actorEmail, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.BeginAsync(ct: ct);
        try
        {
            // FOR UPDATE, same reason as UpdateAccountAsync: SoftDelete is persisted as a full-row
            // write, so the row must stay locked from read to commit or a concurrent self-service
            // column-scoped change would be reverted.
            Account? account = await accountRepository.FindByEmailForUpdateAsync(email, ct);
            if (account == null)
            {
                await unitOfWork.RollbackAsync(ct);
                logger.LogWarning("Admin delete failed: account not found for {Email} by admin {Admin}", email, actorEmail);
                return ServiceResult.NotFound("Account.NotFound", ServiceErrorKeys.AccountNotFoundByEmail);
            }

            account.SoftDelete(utcNow);

            await accountRepository.UpdateEntityAsync(account, ct);
            await unitOfWork.CommitAsync(ct);
 #pragma warning disable CA1873
            logger.LogInformation("Admin deleted account {Email} by admin {Admin}", account.Email.Value, actorEmail);
 #pragma warning restore CA1873
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete account for {Email}", email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("ManagementAccount.DeleteFailed", ServiceResult.TemporaryErrorKey);
        }
    }
}
