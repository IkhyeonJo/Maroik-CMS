using Maroik.Core.Domain.Finance;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Extensions;

/// <summary>
/// Renders the &lt;option&gt; list for an expenditure "subclass" &lt;select&gt; from
/// <see cref="ExpenditureClassPolicy.SubClassesByMainClass"/> (the domain policy backing
/// <see cref="ExpenditureClassPolicy.Validate"/>) instead of hardcoding it, so the list can't drift
/// out of sync with what the server actually accepts. Only the default main class's (first key's)
/// subclasses start visible/selected; the page's own <c>ChangeCreate*MainClass</c>/
/// <c>ChangeEdit*MainClass</c> (site.ts) reveal the rest when the user picks a different main class.
/// Shared by the create/edit forms in both <c>Views/AccountBook/Expenditure.cshtml</c> and
/// <c>Views/Notice/FixedExpenditure.cshtml</c>, which otherwise repeated this identical
/// default-computation + rendering loop four times. Takes the calling view's own
/// <c>localizer</c> (rather than resolving one itself) so each page's subclass labels
/// keep resolving from that page's own resource file, exactly as before this was extracted.
/// </summary>
public static class ExpenditureSubClassOptionsExtensions
{
    /// <summary>
    /// Builds one <c>&lt;option&gt;</c> per (main class, subclass) pair; options of every main class but
    /// the default one carry <c>hidden</c>, and the default main class's first subclass is <c>selected</c>.
    /// </summary>
    /// <param name="html">The view's HTML helper (extension receiver; not otherwise used).</param>
    /// <param name="localizer">The calling view's localizer, used for each option's label.</param>
    /// <returns>The rendered option list, ready to place inside the subclass <c>&lt;select&gt;</c>.</returns>
    public static IHtmlContent ExpenditureSubClassOptions(this IHtmlHelper html, IViewLocalizer localizer)
    {
        string defaultMainClass = ExpenditureClassPolicy.SubClassesByMainClass.Keys.First();
        string defaultSubClass = ExpenditureClassPolicy.SubClassesByMainClass[defaultMainClass][0];

        var content = new HtmlContentBuilder();
        foreach ((string mainClass, IReadOnlyList<string> subClasses) in ExpenditureClassPolicy.SubClassesByMainClass)
        {
            foreach (string subClass in subClasses)
            {
                var option = new TagBuilder("option")
                {
                    Attributes =
                    {
                        ["value"] = subClass
                    }
                };
                if (mainClass != defaultMainClass)
                    option.Attributes["hidden"] = "hidden";
                if (subClass == defaultSubClass)
                    option.Attributes["selected"] = "selected";
                option.InnerHtml.Append(localizer[subClass].Value);
                content.AppendHtml(option);
            }
        }
        return content;
    }
}
