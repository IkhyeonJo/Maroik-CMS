// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Website.Models.ViewModels.FileManagers;

/// <summary>The total size of a batch about to be uploaded (the quota check before it starts, F2).</summary>
public class FileManagerQuotaInputViewModel
{
    /// <summary>The batch's total size in bytes.</summary>
    public long TotalBytes { get; set; }
}
