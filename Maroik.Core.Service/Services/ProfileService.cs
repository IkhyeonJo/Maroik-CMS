using System.Diagnostics;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Media;
using Maroik.Core.Domain.ValueObjects;
using Maroik.Core.Service.Extensions;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IProfileService"/> for user self-service profile management.
/// Avatar images are image-validated locally, then stored through the file-storage service
/// (<see cref="IFileClient"/> — shared across replicas, virus-scanned there). Password changes
/// require the current password to be verified via BCrypt before the new hash is stored.
/// Time-zone updates validate the IANA ID against the system's zone database.
/// </summary>
public class ProfileService(
    IAccountRepository accountRepository,
    IAssetRepository assetRepository,
    IPasswordService passwordService,
    IFileClient fileClient,
    IImageValidatorService imageValidator,
    IOptions<ServerSetting> settings,
    IUnitOfWork unitOfWork,
    ILogger<ProfileService> logger,
    TimeProvider timeProvider) : IProfileService
{
    /// <summary>Relative storage directory (under the file-storage root) that holds uploaded avatars.</summary>
    private const string AvatarStorageDirectory = "upload/Management/Profile/Avatar";

    /// <inheritdoc />
    public async Task<AccountResponse?> GetProfileAsync(string email, CancellationToken ct = default)
    {
        Account? account = await FindByEmailAsync(email, ct);
        return account == null ? null : AccountMapper.ToResponse(account);
    }

    /// <summary>
    /// Points the account's avatar at <paramref name="avatarPath"/> (a column-scoped write of
    /// <c>AvatarImagePath</c>/<c>Updated</c>). Not part of <see cref="IProfileService"/>: it is the last
    /// step of <see cref="UploadAndUpdateAvatarAsync"/>, after the image has been validated and stored.
    /// </summary>
    public async Task<ServiceResult> UpdateAvatarAsync(string email, string avatarPath, CancellationToken ct = default)
    {
        try
        {
            Account? account = await FindByEmailAsync(email, ct);
            if (account == null)
                return ServiceResult.NotFound("Account.NotFound", "Input is invalid");

            // Column-scoped write: touch only AvatarImagePath/Updated so a concurrent timezone or
            // password change on the same account is not clobbered by a full-row overwrite.
            await accountRepository.UpdateAvatarPathAsync(email, avatarPath, timeProvider.GetUtcNow().UtcDateTime, ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update avatar for {Email}", email);
            return ServiceResult.Failure("Profile.UpdateAvatarFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateTimeZoneAsync(string email, string timeZoneIanaId, CancellationToken ct = default)
    {
        try
        {
            Account? account = await FindByEmailAsync(email, ct);
            if (account == null)
                return ServiceResult.NotFound("Account.NotFound", "Input is invalid");

            var tzResult = TimeZoneId.Create(timeZoneIanaId);
            if (tzResult.IsError)
                return ServiceResult.FromError(tzResult.FirstError);

            // Column-scoped write: touch only TimeZoneIanaId/Updated (see UpdateAvatarAsync).
            await accountRepository.UpdateTimeZoneAsync(email, tzResult.Value.Value, timeProvider.GetUtcNow().UtcDateTime, ct);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update timezone for {Email}", email);
            return ServiceResult.Failure("Profile.UpdateTimezoneFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdatePasswordAsync(string email, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        await unitOfWork.BeginAsync(ct);
        try
        {
            // The read must be locked (FOR UPDATE), not the plain FindByEmailAsync used elsewhere in
            // this class: a concurrent AccountService.ResetPasswordAsync also writes HashedPassword /
            // SecurityStamp. An unlocked read here could verify currentPassword against a
            // since-superseded hash and then blindly overwrite a password the user just reset via
            // "forgot password" on another device.
            Account? account = await accountRepository.FindByEmailForUpdateAsync(email, ct);
            if (account == null)
                return await unitOfWork.FailAsync(ServiceResult.NotFound("Account.NotFound", "Input is invalid"), ct);

            if (!passwordService.VerifyPassword(currentPassword, account.HashedPassword))
            {
                logger.LogWarning("Password change refused: wrong current password for {Email}", email);
                return await unitOfWork.FailAsync(ServiceResult.Validation("Profile.WrongPassword", "Invalid password. Please check again."), ct);
            }

            // Enforce the complexity rule server-side, before hashing — Account.ChangePassword only sees the hash.
            if (!PasswordPolicy.IsValid(newPassword))
                return await unitOfWork.FailAsync(ServiceResult.Validation("Account.PasswordPolicy", PasswordPolicy.ViolationMessage), ct);

            // Let the domain method compute the new SecurityStamp / clear MustChangePassword and any
            // pending ResetPasswordToken, then persist only those columns (see UpdateAvatarAsync for
            // why the full-row write is avoided).
            var changeResult = account.ChangePassword(passwordService.HashPassword(newPassword), timeProvider.GetUtcNow().UtcDateTime);
            if (changeResult.IsError)
            {
                // The policy above already refused a blank password, so an empty hash here is a
                // hasher fault, not user input.
                logger.LogError("Password change failed for {Email}: {ErrorCode}", email, changeResult.FirstError.Code);
                return await unitOfWork.FailAsync(ServiceResult.Failure("Profile.UpdatePasswordFailed", ServiceResult.TemporaryErrorKey), ct);
            }

            await accountRepository.UpdatePasswordAsync(
                email, account.HashedPassword, account.SecurityStamp, account.MustChangePassword, account.ResetPasswordToken,
                account.Locked, account.LoginAttempt, account.Message, account.Updated, ct);
            await unitOfWork.CommitAsync(ct);
            logger.LogInformation("Password changed for {Email}", email);
            return ServiceResult.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update password for {Email}", email);
            await unitOfWork.RollbackAsync(ct);
            return ServiceResult.Failure("Profile.UpdatePasswordFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UploadAndUpdateAvatarAsync(string email, byte[] imageBytes, string extension, CancellationToken ct = default)
    {
        try
        {
            // Re-assert the allow-list here (not just in the controller): ImageUploadPolicy is the
            // single source of truth, and a stored avatar must never end up under an arbitrary
            // caller-chosen extension even if a future caller skips the controller-side check.
            if (!ImageUploadPolicy.IsAllowedExtension(extension))
            {
                logger.LogWarning("Avatar upload refused: extension not allowed ({Extension}) for {Email}", extension, email);
                return ServiceResult.Validation("Profile.InvalidImage", "invalid-image");
            }

            // SVG first: IsValidImage only accepts JPEG/PNG, so an SVG would otherwise be reported as a
            // generic "invalid image" instead of the specific "SVG is not allowed".
            if (imageValidator.IsSvg(imageBytes))
            {
                logger.LogWarning("Avatar upload refused: SVG is not allowed for {Email}", email);
                return ServiceResult.Validation("Profile.SvgNotAllowed", "svg-not-allowed");
            }

            if (!imageValidator.IsValidImage(imageBytes))
            {
                logger.LogWarning("Avatar upload refused: not a valid image for {Email}", email);
                return ServiceResult.Validation("Profile.InvalidImage", "invalid-image");
            }

            // Re-encode without metadata: a phone photo's EXIF (GPS position, camera, capture time)
            // must not be published with the avatar.
            StrippedImage stored = imageValidator.StripMetadata(imageBytes);

            // Named after the format the image actually is, not the extension it was uploaded with.
            string avatarFile = $"{Guid.NewGuid():N}{stored.Extension}";
            // Store through the file-storage service (shared volume, ClamAV-scanned there) instead of
            // this replica's local wwwroot, so a multi-replica deployment stays consistent.
            FileUploadResult uploaded = await fileClient.UploadWithResultAsync(
                stored.Bytes, stored.ContentType,
                $"{AvatarStorageDirectory}/{avatarFile}", settings.Value.FileStorageBaseUrl ?? "", ct);

            // Tell the caller WHY the file was refused, so the user is not told "invalid input" for a
            // virus detection or for a scanner outage.
            switch (uploaded)
            {
                case FileUploadResult.Infected:
                    logger.LogWarning("Avatar upload refused: virus detected for {Email}", email);
                    return ServiceResult.Validation("Profile.VirusDetected", "virus-detected");
                case FileUploadResult.ScanUnavailable:
                    logger.LogError("Avatar upload failed: virus scanner unavailable for {Email}", email);
                    return ServiceResult.Failure("Profile.ScanUnavailable", "scan-unavailable");
                case FileUploadResult.Failed:
                    logger.LogError("Avatar upload failed: file storage refused the upload for {Email}", email);
                    return ServiceResult.Failure("Profile.UploadAvatarFailed", ServiceResult.TemporaryErrorKey);
            }

            ServiceResult updated = await UpdateAvatarAsync(email, $"/{AvatarStorageDirectory}/{avatarFile}", ct);
            if (updated.Success)
                logger.LogInformation("Avatar updated for {Email}", email);
            return updated;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to upload and update avatar for {Email}", email);
            return ServiceResult.Failure("Profile.UploadAvatarFailed", ServiceResult.TemporaryErrorKey);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadAvatarAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            byte[] bytes = await fileClient.DownloadAsync(
                $"{AvatarStorageDirectory}/{fileName}", settings.Value.FileStorageBaseUrl ?? "", ct);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to download avatar {FileName} from file storage. CorrelationId={CorrelationId}", fileName, Activity.Current?.Id);
            return null;
        }
    }

    /// <summary>Unlocked lookup of an account by email.</summary>
    private Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default)
        => accountRepository.FindByEmailAsync(email, ct);

    /// <inheritdoc />
    public async Task UpdateDefaultMonetaryUnitAsync(string accountEmail, string? monetaryUnit, CancellationToken ct = default)
    {
        List<Asset> assets = await assetRepository.GetByAccountEmailAsync(accountEmail, ct);

        // A deleted asset's currency must not remain selectable as the default monetary unit.
        string? newUnit = assets.Where(x => !x.Deleted).Select(x => x.Balance.Currency.Value).Distinct().Contains(monetaryUnit)
            ? monetaryUnit
            : null;

        // Column-scoped write: only DefaultMonetaryUnit, no Updated bump (background correction).
        // A no-op (0 rows) when the account does not exist, matching the prior silent return.
        await accountRepository.UpdateDefaultMonetaryUnitAsync(accountEmail, newUnit, ct);
    }
}
