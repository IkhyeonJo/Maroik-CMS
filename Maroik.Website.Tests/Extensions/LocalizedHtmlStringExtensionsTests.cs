using Maroik.Website.Extensions;
using Microsoft.AspNetCore.Mvc.Localization;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="LocalizedHtmlStringExtensions"/>.
/// <para>
/// Regression coverage for a real bug this extension exists to work around:
/// <see cref="LocalizedHtmlString.Value"/> is documented as "the original resource string, prior to
/// formatting" (it never substitutes <c>{0}</c>-style placeholders), and its inherited
/// <see cref="object.ToString"/> is not overridden (it returns the CLR type name). Every controller
/// in this codebase that renders a dynamic-value <c>ServiceResult</c> error currently calls
/// <c>_localizer[key, args].Value</c>, so those messages render with an un-substituted <c>{0}</c> in
/// production — <see cref="LocalizedHtmlStringExtensions.ToPlainString"/> should replace that call at each such call site, but that
/// wider rollout is a separate follow-up; only <c>ManagementController.CreateAccount</c>'s nickname-
/// conflict path (which this extension unblocks) has been switched over so far.
/// </para>
/// </summary>
public class LocalizedHtmlStringExtensionsTests
{
    // -- ToPlainString ------------------------------------------------------

    /// <summary>Substitutes a composite-format argument into the resource template.</summary>
    [Fact]
    public void ToPlainString_SubstitutesFormatArgument()
    {
        var localized = new LocalizedHtmlString(
            "key", "'{0}' is a Nickname that already exists.", isResourceNotFound: false, arguments: ["TakenNick"]);

        string result = localized.ToPlainString();

        Assert.Equal("'TakenNick' is a Nickname that already exists.", result);
    }

    /// <summary>Returns the resource text unchanged when there are no format arguments.</summary>
    [Fact]
    public void ToPlainString_ReturnsResourceUnchanged_WhenThereAreNoArguments()
    {
        var localized = new LocalizedHtmlString("key", "This account has already been created.");

        string result = localized.ToPlainString();

        Assert.Equal("This account has already been created.", result);
    }

    /// <summary>HTML-encodes a format argument, so it cannot inject markup into an HTML rendering of the message.</summary>
    [Fact]
    public void ToPlainString_HtmlEncodesFormatArgument()
    {
        var localized = new LocalizedHtmlString(
            "key", "'{0}' is a Nickname that already exists.", isResourceNotFound: false, arguments: ["<script>'"]);

        string result = localized.ToPlainString();

        Assert.Equal("'&lt;script&gt;&#x27;' is a Nickname that already exists.", result);
    }
}
