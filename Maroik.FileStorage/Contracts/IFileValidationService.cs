namespace Maroik.FileStorage.Contracts;

/// <summary>
/// Validates and saves uploaded files: checks extension, size, magic bytes,
/// ZIP encryption, and ClamAV virus scan before writing to disk.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates and saves <paramref name="file"/> to <paramref name="filePath"/>.
    /// Returns <c>(true, null)</c> on success; <c>(false, errorMessage)</c> on any failure.
    /// </summary>
    Task<(bool Success, string? Error)> ValidateAndSaveAsync(IFormFile file, string filePath);
}
