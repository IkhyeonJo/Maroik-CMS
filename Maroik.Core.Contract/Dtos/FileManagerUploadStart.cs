namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// The answer to a started File Manager upload: either a name conflict the user must decide (<see cref="Conflict"/>), or the upload id
/// the chunks are sent under, the name the file is stored as (a "Keep both" name differs) and the chunk size to send.
/// </summary>
/// <param name="Conflict">True when a finished file of the same name is in the folder and the request asked first.</param>
/// <param name="UploadId">The upload id (the file's storage GUID); empty on a conflict.</param>
/// <param name="Name">The name the file will be stored under.</param>
/// <param name="ChunkBytes">The size of each chunk to send.</param>
public sealed record FileManagerUploadStart(bool Conflict, Guid UploadId, string Name, int ChunkBytes);
