using System.Text;
using System.Text.Encodings.Web;
using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="IncomeSubClassOptionsExtensions"/>. Unlike
/// <c>ExpenditureSubClassOptionsExtensions</c>, income subclasses are not partitioned per main
/// class -- "LaborIncome"/"OtherIncome" are valid under both "RegularIncome" and "IrregularIncome"
/// -- so these tests specifically guard against re-introducing a duplicate &lt;option&gt; per
/// overlapping subclass.
/// </summary>
public class IncomeSubClassOptionsExtensionsTests
{
    private static IViewLocalizer MakeEchoLocalizer()
    {
        var mock = new Mock<IViewLocalizer>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        return mock.Object;
    }

    private static string Render(Microsoft.AspNetCore.Html.IHtmlContent content)
    {
        var writer = new StringWriter(new StringBuilder());
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    /// <summary>Every distinct subclass value across all main classes is rendered exactly once, even though "LaborIncome" and "OtherIncome" belong to both "RegularIncome" and "IrregularIncome".</summary>
    [Fact]
    public void IncomeSubClassOptions_RendersEachDistinctSubClassExactlyOnce()
    {
        string html = Render(((IHtmlHelper)null!).IncomeSubClassOptions(MakeEchoLocalizer()));

        Assert.Equal(1, CountOccurrences(html, "value=\"LaborIncome\""));
        Assert.Equal(1, CountOccurrences(html, "value=\"OtherIncome\""));
        Assert.Equal(1, CountOccurrences(html, "value=\"BusinessIncome\""));
        Assert.Equal(1, CountOccurrences(html, "value=\"PensionIncome\""));
        Assert.Equal(1, CountOccurrences(html, "value=\"FinancialIncome\""));
        Assert.Equal(1, CountOccurrences(html, "value=\"RentalIncome\""));
    }

    /// <summary>The first rendered subclass (matching the create-form's previously-hardcoded default) is marked selected.</summary>
    [Fact]
    public void IncomeSubClassOptions_MarksFirstSubClass_AsSelected()
    {
        string html = Render(((IHtmlHelper)null!).IncomeSubClassOptions(MakeEchoLocalizer()));

        int optionStart = html.IndexOf("<option", StringComparison.Ordinal);
        int optionEnd = html.IndexOf('>', optionStart);
        string firstOptionTag = html[optionStart..(optionEnd + 1)];
        Assert.Contains("value=\"LaborIncome\"", firstOptionTag);
        Assert.Contains("selected", firstOptionTag);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
