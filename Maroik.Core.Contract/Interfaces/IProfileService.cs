using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for user profile self-management (Management > Profile page).
/// </summary>
public interface IProfileService
{
    /// <summary>Returns the profile of the given account, or null if not found.</summary>
    Task<AccountResponse?> GetProfileAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Validates the uploaded image, stores it through the file-storage service (shared across
    /// replicas), and updates the account's avatar path.
    /// </summary>
    Task<ServiceResult> UploadAndUpdateAvatarAsync(string email, byte[] imageBytes, string extension, CancellationToken ct = default);

    /// <summary>
    /// Fetches a stored avatar's bytes from the file-storage service by file name, or
    /// <see langword="null"/> if it is missing or the fetch fails. Used to lazily repopulate a
    /// replica's local avatar cache.
    /// </summary>
    Task<byte[]?> DownloadAvatarAsync(string fileName, CancellationToken ct = default);

    /// <summary>Updates the account's preferred IANA time-zone ID.</summary>
    Task<ServiceResult> UpdateTimeZoneAsync(string email, string timeZoneIanaId, CancellationToken ct = default);

    /// <summary>
    /// Changes the account's password after verifying <paramref name="currentPassword"/> matches the stored hash.
    /// </summary>
    Task<ServiceResult> UpdatePasswordAsync(string email, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Persists the user's preferred default currency unit.
    /// Sets the unit only when it matches the currency of one of the account's non-deleted assets;
    /// otherwise clears the preference to null. Does not bump the account's <c>Updated</c> timestamp.
    /// </summary>
    Task UpdateDefaultMonetaryUnitAsync(string accountEmail, string? monetaryUnit, CancellationToken ct = default);
}
