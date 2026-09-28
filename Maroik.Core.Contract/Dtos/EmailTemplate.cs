namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Localized subject/title/body content for a transactional email (registration confirmation,
/// password reset) that <see cref="Interfaces.IAccountService"/> sends on the caller's behalf.
/// </summary>
public class EmailTemplate
{
    /// <summary>Email subject line.</summary>
    public string Subject { get; init; } = "";

    /// <summary>Heading shown at the top of the email body.</summary>
    public string Title { get; init; } = "";

    /// <summary>First line of body text (e.g. instructions above the action link).</summary>
    public string Content0 { get; init; } = "";

    /// <summary>Second line of body text (e.g. a fallback note below the action link).</summary>
    public string Content1 { get; init; } = "";
}
