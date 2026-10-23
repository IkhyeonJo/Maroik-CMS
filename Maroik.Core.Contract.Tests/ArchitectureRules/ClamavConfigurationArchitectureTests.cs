using System.Text.RegularExpressions;
using Maroik.Core.Contract.Tests.Fixtures;

namespace Maroik.Core.Contract.Tests.ArchitectureRules;

/// <summary>
/// clamd's limits in both compose files (File Manager requirement S3). The image's defaults (StreamMaxLength / MaxFileSize 100 MB,
/// MaxScanSize 400 MB) would turn away every File Manager file over 100 MB, and with <c>AlertExceedsMax</c> off clamd may pass a file it
/// did not scan whole as "OK". So both files raise the three size limits to 2 GB (2048M — the largest file ClamAV can scan), switch
/// <c>AlertExceedsMax</c> on (an unscanned remainder is reported, and the client calls it "could not be scanned"), and give a scan up to
/// 10 minutes (<c>MaxScanTime</c>, ms) so a 2 GB file is not cut off by the 2-minute default. The <c>clamav/clamav</c> image writes every
/// <c>CLAMD_CONF_*</c> variable into clamd.conf at start-up.
/// </summary>
public class ClamavConfigurationArchitectureTests
{
    /// <summary>The clamd settings each compose file must set, and their values.</summary>
    public static TheoryData<string, string, string> Settings()
    {
        var data = new TheoryData<string, string, string>();
        foreach (string file in new[] { "docker-compose.prod.yml", "docker-compose.debug.yml" })
        {
            data.Add(file, "StreamMaxLength", "2048M");
            data.Add(file, "MaxFileSize", "2048M");
            data.Add(file, "MaxScanSize", "2048M");
            data.Add(file, "AlertExceedsMax", "yes");
            data.Add(file, "MaxScanTime", "600000");
        }
        return data;
    }

    /// <summary>The clamav service's block of <paramref name="file"/> (up to the next service at the same indentation).</summary>
    private static string ClamavService(string file)
    {
        string compose = File.ReadAllText(Path.Combine(RepositoryPaths.Root, file));
        Match block = Regex.Match(compose, @"^(?<indent>\s+)clamav:\s*\n(?<body>(?:\k<indent>\s+.*\n|\s*\n)+)", RegexOptions.Multiline);
        Assert.True(block.Success, $"{file}: no clamav service");
        return block.Groups["body"].Value;
    }

    /// <summary>The clamav service of each compose file sets each limit.</summary>
    [Theory]
    [MemberData(nameof(Settings))]
    public void TheClamavService_SetsTheFileManagerLimits(string file, string setting, string value)
    {
        Assert.Matches($@"\n\s+CLAMD_CONF_{setting}:\s*""?{Regex.Escape(value)}""?\s*(#.*)?\n", ClamavService(file));
    }
}
