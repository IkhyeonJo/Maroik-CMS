namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Instructions for a transformed <c>&lt;img&gt;</c> tag. Null attribute fields are left unchanged;
/// when <paramref name="Remove"/> is <see langword="true"/> the tag is deleted from the document
/// and the other fields are ignored.
/// </summary>
public record HtmlImgPatch(
    string? NewAlt = null,
    string? DataFile = null,
    string? DataContentType = null,
    bool Remove = false);
