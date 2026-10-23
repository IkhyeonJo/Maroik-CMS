// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>A user's whole File Manager drive as the left panel shows it: the folder tree and the storage usage (U1, F7).</summary>
public class FileManagerDriveResponse
{
    /// <summary>Bytes in use: every file, unfinished uploads included (Q4).</summary>
    public long UsedBytes { get; set; }

    /// <summary>The account's quota in bytes.</summary>
    public long QuotaBytes { get; set; }

    /// <summary>The number of finished items directly at the root ("My Drive").</summary>
    public int RootItemCount { get; set; }

    /// <summary>Every folder (the tree shows folders only, U2), each with its parent and item count.</summary>
    public List<FileManagerItemResponse> Folders { get; set; } = [];
}
