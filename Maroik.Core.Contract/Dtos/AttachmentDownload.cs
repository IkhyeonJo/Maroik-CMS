namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// A stored attachment opened for download: a stream read from file storage as it is copied to the
/// response (never buffered whole), and the name to offer it under. The receiver owns the stream.
/// </summary>
/// <param name="Content">The file's content, streamed from file storage.</param>
/// <param name="FileName">The file name (with extension) the browser saves it as.</param>
public sealed record AttachmentDownload(Stream Content, string FileName);
