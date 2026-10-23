using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.FileManagers;
namespace Maroik.Core.Domain.Tests.FileManagers;

/// <summary>
/// Unit tests for <see cref="FileManagerUploadPolicy"/>: the per-file size limit ClamAV can scan (S1), the chunk size (P5), the age of
/// an abandoned upload (Q5) and the quota check (Q3, Q4).
/// </summary>
public class FileManagerUploadPolicyTests
{
    /// <summary>The fixed limits: 2 GB per file (2,147,483,647 bytes), 16 MB chunks, 24 hours.</summary>
    [Fact]
    public void TheLimits_AreTheAgreedValues()
    {
        Assert.Equal(2_147_483_647L, FileManagerUploadPolicy.MaxFileBytes);
        Assert.Equal(16 * 1024 * 1024, FileManagerUploadPolicy.ChunkBytes);
        Assert.Equal(TimeSpan.FromHours(24), FileManagerUploadPolicy.StaleUploadAge);
    }

    /// <summary>An empty file and a file up to the limit are accepted.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2_147_483_647L)]
    public void CheckSize_AcceptsAFileUpToTheLimit(long size)
    {
        Assert.False(FileManagerUploadPolicy.CheckSize(size).IsError);
    }

    /// <summary>A file one byte over the limit is refused when the upload starts; a negative size is refused too.</summary>
    [Theory]
    [InlineData(2_147_483_648L, "FileManager.FileTooLarge")]
    [InlineData(-1L, "FileManager.SizeInvalid")]
    public void CheckSize_RefusesAFileOverTheLimit(long size, string code)
    {
        Assert.Equal(code, FileManagerUploadPolicy.CheckSize(size).FirstError.Code);
    }

    /// <summary>An upload that fits the remaining quota exactly is accepted.</summary>
    [Fact]
    public void CheckQuota_AcceptsAnUploadThatFitsExactly()
    {
        Assert.False(FileManagerUploadPolicy.CheckQuota(quotaBytes: 100, usedBytes: 60, requestedBytes: 40).IsError);
    }

    /// <summary>An upload over the quota is refused, saying how many GB are missing (rounded up to 0.1 GB).</summary>
    [Fact]
    public void CheckQuota_RefusesAnUploadOverTheQuota_NamingTheShortfallInGb()
    {
        const long gb = 1024L * 1024 * 1024;

        var result = FileManagerUploadPolicy.CheckQuota(quotaBytes: 30 * gb, usedBytes: 29 * gb, requestedBytes: 2 * gb + 1);

        Assert.Equal("FileManager.QuotaExceeded", result.FirstError.Code);
        Assert.Equal([1.1m], (object[])result.FirstError.Metadata![DomainError.MessageArgsMetadataKey]);
    }

    /// <summary>With the default quota of 0 GB every non-empty upload is refused; an empty file still fits.</summary>
    [Fact]
    public void CheckQuota_UnderTheDefaultZeroQuota_RefusesEveryNonEmptyFile()
    {
        Assert.Equal("FileManager.QuotaExceeded", FileManagerUploadPolicy.CheckQuota(0, 0, 1).FirstError.Code);
        Assert.Equal([0.1m], (object[])FileManagerUploadPolicy.CheckQuota(0, 0, 1).FirstError.Metadata![DomainError.MessageArgsMetadataKey]);
        Assert.False(FileManagerUploadPolicy.CheckQuota(0, 0, 0).IsError);
    }

    /// <summary>A quota already below the usage (an administrator lowered it) blocks new uploads but nothing else is implied.</summary>
    [Fact]
    public void CheckQuota_RefusesAnUpload_WhenTheUsageIsAlreadyOverTheQuota()
    {
        Assert.Equal("FileManager.QuotaExceeded", FileManagerUploadPolicy.CheckQuota(quotaBytes: 10, usedBytes: 50, requestedBytes: 1).FirstError.Code);
    }

    /// <summary>An upload is stale once it is older than 24 hours.</summary>
    [Fact]
    public void IsStale_IsTrue_OnlyPastTwentyFourHours()
    {
        DateTime now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(FileManagerUploadPolicy.IsStale(now.AddHours(-24), now));
        Assert.True(FileManagerUploadPolicy.IsStale(now.AddHours(-24).AddSeconds(-1), now));
    }

    // -- Quota units (the administrator enters GB) -----------------------------------

    /// <summary>A quota typed in GB becomes bytes (1 GB = 1024³ bytes), fractions included and rounded to a whole byte.</summary>
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("30", 32_212_254_720L)]
    [InlineData("0.5", 536_870_912L)]
    public void QuotaBytesFromGb_ConvertsGbToBytes(string gb, long bytes)
    {
        Assert.Equal(bytes, FileManagerUploadPolicy.QuotaBytesFromGb(decimal.Parse(gb, System.Globalization.CultureInfo.InvariantCulture)).Value);
    }

    /// <summary>A negative quota, or one too large to store, is refused.</summary>
    [Theory]
    [InlineData("-0.1", "Account.FileManagerQuotaNegative")]
    [InlineData("10000000000", "FileManager.QuotaTooLarge")]
    public void QuotaBytesFromGb_RefusesANegativeOrHugeQuota(string gb, string code)
    {
        Assert.Equal(code, FileManagerUploadPolicy.QuotaBytesFromGb(decimal.Parse(gb, System.Globalization.CultureInfo.InvariantCulture)).FirstError.Code);
    }

    /// <summary>Bytes shown back in GB keep up to two decimal places (what the administrator typed comes back).</summary>
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(32_212_254_720L, "30")]
    [InlineData(536_870_912L, "0.5")]
    [InlineData(1L, "0")]
    public void QuotaGbFromBytes_ConvertsBytesToGb(long bytes, string gb)
    {
        Assert.Equal(decimal.Parse(gb, System.Globalization.CultureInfo.InvariantCulture), FileManagerUploadPolicy.QuotaGbFromBytes(bytes));
    }
}
