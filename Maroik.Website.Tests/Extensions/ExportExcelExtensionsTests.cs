using System.Text.RegularExpressions;
using Maroik.Website.Extensions;

namespace Maroik.Website.Tests.Extensions;

/// <summary>Unit tests for <see cref="ExportExcelExtensions"/>.</summary>
public class ExportExcelExtensionsTests
{
    /// <summary>To Excel file name starts with prefix and ends with xlsx extension.</summary>
    [Fact]
    public void ToExcelFileName_StartsWithPrefixAndEndsWithXlsxExtension()
    {
        string fileName = "AccountBook".ToExcelFileName("UTC", DateTime.UtcNow);

        Assert.StartsWith("AccountBook-", fileName);
        Assert.EndsWith(".xlsx", fileName);
    }

    /// <summary>To Excel file name matches timestamp pattern.</summary>
    [Fact]
    public void ToExcelFileName_MatchesTimestampPattern()
    {
        string fileName = "Menu".ToExcelFileName("UTC", DateTime.UtcNow);

        Assert.Matches(new Regex(@"^Menu-\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d{3}\.xlsx$"), fileName);
    }

    /// <summary>
    /// The timestamp is the given instant (not the machine's clock) shown in the viewer's time zone, to the millisecond:
    /// 2031-02-03 04:05:06.789 UTC is 13:05:06.789 in Asia/Seoul.
    /// </summary>
    [Fact]
    public void ToExcelFileName_StampsTheGivenInstantInTheViewersTimeZone()
    {
        string fileName = "Menu".ToExcelFileName("Asia/Seoul", new DateTime(2031, 2, 3, 4, 5, 6, 789, DateTimeKind.Utc));

        Assert.Equal("Menu-2031-02-03-13-05-06-789.xlsx", fileName);
    }

    /// <summary>
    /// Regression test: a caller-supplied prefix containing characters invalid in a file name (it
    /// comes straight from a query string) must not throw when the framework later builds the
    /// response's Content-Disposition header from the result — those characters are stripped instead.
    /// Built from <see cref="Path.GetInvalidFileNameChars"/> itself (platform-dependent — Windows
    /// and Linux disallow different characters) rather than a hardcoded set, so this passes on both.
    /// </summary>
    [Fact]
    public void ToExcelFileName_StripsInvalidFileNameCharacters()
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string prefix = "Bad" + new string(invalidChars) + "Name";

        string fileName = prefix.ToExcelFileName("UTC", DateTime.UtcNow);

        Assert.StartsWith("BadName-", fileName);
        foreach (char c in invalidChars)
            Assert.DoesNotContain(c, fileName);
    }
}
