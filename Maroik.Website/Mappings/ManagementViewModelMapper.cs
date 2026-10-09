using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Time;
using Maroik.Website.Models.ViewModels.Management;

namespace Maroik.Website.Mappings;

/// <summary>
/// Maps account response objects to Management-area display view models,
/// applying timezone conversion to audit timestamps.
/// </summary>
public static class ManagementViewModelMapper
{
    extension(AdminAccountResponse item)
    {
        /// <summary>Maps a single <see cref="AdminAccountResponse"/> to an <see cref="AccountOutputViewModel"/> for display.</summary>
        private AccountOutputViewModel ToDisplayViewModel(string timeZoneIanaId) => new()
        {
            Email = item.Email,
            HashedPassword = item.HashedPassword,
            Nickname = item.Nickname,
            AvatarImagePath = item.AvatarImagePath,
            Role = item.Role,
            TimeZoneIanaId = item.TimeZoneIanaId,
            Locked = item.Locked,
            LoginAttempt = item.LoginAttempt,
            EmailConfirmed = item.EmailConfirmed,
            AgreedServiceTerms = item.AgreedServiceTerms,
            RegistrationToken = item.RegistrationToken,
            ResetPasswordToken = item.ResetPasswordToken,
            Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
            Message = item.Message,
            Deleted = item.Deleted
        };
    }

    extension(IEnumerable<AdminAccountResponse> items)
    {
        /// <summary>Maps all <see cref="AdminAccountResponse"/> items to display view models.</summary>
        public List<AccountOutputViewModel> ToDisplayViewModels(string timeZoneIanaId)
            =>
            [
                .. items.Select(i => i.ToDisplayViewModel(timeZoneIanaId))
            ];
    }
}
