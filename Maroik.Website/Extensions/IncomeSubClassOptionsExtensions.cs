using Maroik.Core.Domain.Finance;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Extensions;

/// <summary>
/// Renders the &lt;option&gt; list for an income "subclass" &lt;select&gt; from
/// <see cref="IncomeClassPolicy.SubClassesByMainClass"/> (the domain policy backing
/// <see cref="IncomeClassPolicy.Validate"/>) instead of hardcoding it, so the list can't drift
/// out of sync with what the server actually accepts.
/// <para>
/// Unlike <see cref="ExpenditureSubClassOptionsExtensions.ExpenditureSubClassOptions"/>, income
/// subclasses are not partitioned per main class -- e.g. "LaborIncome" and "OtherIncome" are valid
/// under both "RegularIncome" and "IrregularIncome" -- so this renders each distinct subclass
/// exactly once (in policy order) instead of one group per main class, avoiding duplicate
/// <c>&lt;option&gt;</c> values. The page's own <c>ApplyAllowedOptions</c> (site.ts), driven by the
/// <c>incomeSubClassMap</c> hidden field, shows/hides/selects among these by value once a main
/// class is chosen -- both on <c>change</c> and when populating the edit form from AJAX data.
/// </para>
/// Shared by the create/edit forms in both <c>Views/AccountBook/Income.cshtml</c> and
/// <c>Views/Notice/FixedIncome.cshtml</c>, which otherwise repeated this identical list four times.
/// Takes the calling view's own <paramref name="localizer"/> (rather than resolving one itself) so
/// each page's subclass labels keep resolving from that page's own resource file.
/// </summary>
public static class IncomeSubClassOptionsExtensions
{
    /// <summary>
    /// Builds one <c>&lt;option&gt;</c> per distinct subclass across all main classes, in policy order.
    /// </summary>
    /// <param name="html">The view's HTML helper (extension receiver; not otherwise used).</param>
    /// <param name="localizer">The calling view's localizer, used for each option's label.</param>
    /// <returns>The rendered option list, ready to place inside the subclass <c>&lt;select&gt;</c>.</returns>
    public static IHtmlContent IncomeSubClassOptions(this IHtmlHelper html, IViewLocalizer localizer)
    {
        IReadOnlyList<string> distinctSubClasses =
        [
            .. IncomeClassPolicy.SubClassesByMainClass.Values.SelectMany(s => s).Distinct()
        ];

        var content = new HtmlContentBuilder();
        for (int i = 0; i < distinctSubClasses.Count; i++)
        {
            var option = new TagBuilder("option")
            {
                Attributes =
                {
                    ["value"] = distinctSubClasses[i]
                }
            };
            if (i == 0)
                option.Attributes["selected"] = "selected";
            option.InnerHtml.Append(localizer[distinctSubClasses[i]].Value);
            content.AppendHtml(option);
        }
        return content;
    }
}
