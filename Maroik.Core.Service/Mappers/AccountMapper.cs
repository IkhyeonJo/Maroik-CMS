using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Account"/> domain objects to <see cref="AccountResponse"/> DTOs.</summary>
internal static class AccountMapper
{
    /// <summary>Copies the fields shared by every account DTO from <paramref name="account"/> onto <paramref name="target"/>.</summary>
    private static void PopulateCommon(AccountResponse target, Account account)
    {
        target.Email = account.Email.Value;
        target.Nickname = account.Nickname;
        target.AvatarImagePath = account.AvatarImagePath;
        target.Role = account.Role.Value;
        target.TimeZoneIanaId = account.TimeZone.Value;
        target.DefaultMonetaryUnit = account.DefaultMonetaryUnit?.Value;
        target.Locked = account.Locked;
        target.LoginAttempt = account.LoginAttempt;
        target.EmailConfirmed = account.EmailConfirmed;
        target.AgreedServiceTerms = account.AgreedServiceTerms;
        target.Created = account.Created;
        target.Updated = account.Updated;
        target.Message = account.Message;
        target.Deleted = account.Deleted;
        target.SecurityStamp = account.SecurityStamp;
        target.MustChangePassword = account.MustChangePassword;
    }

    /// <summary>Converts an <see cref="Account"/> domain object to its corresponding <see cref="AccountResponse"/> DTO.</summary>
    internal static AccountResponse ToResponse(Account account)
    {
        var response = new AccountResponse();
        PopulateCommon(response, account);
        return response;
    }
}
