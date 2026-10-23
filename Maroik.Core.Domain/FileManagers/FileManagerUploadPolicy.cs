using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// The upload rules of the File Manager: the size one file may have, the size of each chunk, when an unfinished upload is abandoned
/// and whether an upload fits the account's quota.
/// </summary>
public static class FileManagerUploadPolicy
{
    /// <summary>
    /// The largest file accepted, 2,147,483,647 bytes (S1): ClamAV cannot scan files of 2 GB or more ("Technical design limitations
    /// prevent ClamAV from scanning files greater than 2 GB", clamd.conf.sample). A larger file is refused when its upload starts.
    /// </summary>
    public const long MaxFileBytes = int.MaxValue;

    /// <summary>
    /// The size of each upload chunk, 16 MB (P5): the site sits behind Cloudflare, which caps one request body at 100 MB, and a chunk
    /// stays well inside Kestrel's default 30 MB request limit.
    /// </summary>
    public const int ChunkBytes = 16 * 1024 * 1024;

    /// <summary>How long an upload may stay unfinished before the next upload of the same user removes it (Q5).</summary>
    public static readonly TimeSpan StaleUploadAge = TimeSpan.FromHours(24);

    /// <summary>The storage directory every File Manager file lives in, under its GUID name (P1).</summary>
    public const string StorageDirectory = "upload/FileManager";

    /// <summary>Bytes in one GB, the unit the quota is shown and refused in.</summary>
    private const decimal BytesPerGb = 1024m * 1024 * 1024;

    /// <summary>Accepts a declared file size of 0 to <see cref="MaxFileBytes"/> bytes.</summary>
    public static ErrorOr<Success> CheckSize(long size)
    {
        if (size < 0)
            return DomainError.Validation("FileManager.SizeInvalid", "The file size is not valid.");

        if (size > MaxFileBytes)
            return DomainError.Validation("FileManager.FileTooLarge", "A file larger than 2 GB cannot be uploaded.");

        return Result.Success;
    }

    /// <summary>
    /// Accepts an upload of <paramref name="requestedBytes"/> when it fits what is left of <paramref name="quotaBytes"/> after
    /// <paramref name="usedBytes"/> (the user's files, unfinished uploads included, Q4). Otherwise the refusal names the shortfall in
    /// GB, rounded up to 0.1 GB, so the user is told "N GB short". A quota already below the usage (Q3) refuses every new upload.
    /// </summary>
    public static ErrorOr<Success> CheckQuota(long quotaBytes, long usedBytes, long requestedBytes)
    {
        long shortfall = usedBytes + requestedBytes - quotaBytes;
        if (shortfall <= 0)
            return Result.Success;

        decimal shortfallGb = Math.Ceiling(shortfall / BytesPerGb * 10) / 10;
        return DomainError.Validation("FileManager.QuotaExceeded", "Not enough storage: {0} GB short.", shortfallGb);
    }

    /// <summary>Whether an upload started at <paramref name="started"/> is older than <see cref="StaleUploadAge"/>.</summary>
    public static bool IsStale(DateTime started, DateTime utcNow) => utcNow - started > StaleUploadAge;

    /// <summary>
    /// The quota in bytes for one typed in GB by an administrator (Q2): 1 GB = 1024³ bytes, rounded to a whole byte. A negative quota,
    /// or one beyond what the <c>bigint</c> column holds, is refused.
    /// </summary>
    public static ErrorOr<long> QuotaBytesFromGb(decimal gb)
    {
        if (gb < 0)
            return DomainError.Validation("Account.FileManagerQuotaNegative", "The storage quota cannot be negative.");

        decimal bytes = Math.Round(gb * BytesPerGb, MidpointRounding.AwayFromZero);
        if (bytes > long.MaxValue)
            return DomainError.Validation("FileManager.QuotaTooLarge", "The storage quota is too large.");

        return (long)bytes;
    }

    /// <summary>The quota in GB, to two decimal places, as the administrator's edit form shows it.</summary>
    public static decimal QuotaGbFromBytes(long bytes) => Math.Round(bytes / BytesPerGb, 2);
}
