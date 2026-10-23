using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Extensions;
using Maroik.Website.Extensions;
using Maroik.Website.Models.ViewModels.FileManagers;

namespace Maroik.Website.Mappers;

/// <summary>
/// Maps the File Manager service's responses to what the page draws: sizes as labels, times in the user's zone, an icon per file type
/// (U7), a search result's location from the root down, and the usage label and bar fill (F7). Everything the browser shows is computed
/// here; the page script only places it.
/// </summary>
public static class FileManagerViewModelMapper
{
    /// <summary>The icon of a folder.</summary>
    private const string FolderIcon = "fas fa-folder text-warning";

    /// <summary>The icon of a file whose type is not listed.</summary>
    private const string DefaultFileIcon = "fas fa-file text-muted";

    /// <summary>The icon of each file type, by extension (lower-case, with the dot).</summary>
    private static readonly Dictionary<string, string> _icons = BuildIcons();

    /// <summary>Builds <see cref="_icons"/>.</summary>
    private static Dictionary<string, string> BuildIcons()
    {
        Dictionary<string, string> icons = new(StringComparer.Ordinal);
        void Add(string icon, params string[] extensions)
        {
            foreach (string extension in extensions) icons[extension] = icon;
        }
        Add("fas fa-file-pdf text-danger", ".pdf");
        Add("fas fa-file-image text-info", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico", ".tif", ".tiff", ".heic");
        Add("fas fa-file-video text-purple", ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".m4v");
        Add("fas fa-file-audio text-success", ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma");
        Add("fas fa-file-archive text-warning", ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz");
        Add("fas fa-file-alt text-secondary", ".txt", ".md", ".csv", ".log", ".rtf");
        Add("fas fa-file-word text-primary", ".doc", ".docx", ".odt", ".hwp");
        Add("fas fa-file-excel text-success", ".xls", ".xlsx", ".ods");
        Add("fas fa-file-powerpoint text-orange", ".ppt", ".pptx", ".odp");
        Add("fas fa-file-code text-teal", ".cs", ".ts", ".js", ".json", ".xml", ".html", ".css", ".sql", ".py", ".java", ".sh", ".yml", ".yaml");
        return icons;
    }

    /// <summary>The icon classes of an item named <paramref name="name"/>.</summary>
    private static string IconOf(string name, bool isFolder) =>
        isFolder ? FolderIcon : _icons.GetValueOrDefault(Path.GetExtension(name).ToLowerInvariant(), DefaultFileIcon);

    extension(FileManagerItemResponse item)
    {
        /// <summary>
        /// The item as the page draws it; <paramref name="withLocation"/> fills its location ("My Drive / Documents") for a search
        /// result, starting at <paramref name="rootName"/>.
        /// </summary>
        public FileManagerItemViewModel ToViewModel(string timeZoneIanaId, string rootName, bool withLocation) => new()
        {
            Id = item.Id,
            ParentId = item.ParentId,
            Name = item.Name,
            IsFolder = item.IsFolder,
            Size = item.IsFolder ? "" : item.Size.ToSizeLabel(),
            Modified = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Icon = IconOf(item.Name, item.IsFolder),
            Previewable = item.Previewable,
            Location = withLocation ? string.Join(" / ", [rootName, .. item.Location]) : "",
            ItemCount = item.ItemCount
        };
    }

    extension(FileManagerFolderResponse folder)
    {
        /// <summary>The open folder as the page draws it.</summary>
        public FileManagerFolderViewModel ToViewModel(string timeZoneIanaId, string rootName) => new()
        {
            FolderId = folder.FolderId,
            Breadcrumb = [.. folder.Breadcrumb.Select(c => new FileManagerCrumbViewModel { Id = c.Id, Name = c.Name })],
            Items = [.. folder.Items.Select(i => i.ToViewModel(timeZoneIanaId, rootName, withLocation: false))]
        };
    }

    extension(FileManagerDriveResponse drive)
    {
        /// <summary>The left panel's data: the folder tree (each folder with its location, for the Move list), the usage label and the usage bar's fill.</summary>
        public FileManagerDriveViewModel ToViewModel(string timeZoneIanaId, string rootName) => new()
        {
            Usage = $"{drive.UsedBytes.ToSizeLabel()} / {drive.QuotaBytes.ToSizeLabel()}",
            UsedPercent = drive.QuotaBytes <= 0
                ? (drive.UsedBytes > 0 ? 100 : 0)
                : (int)Math.Min(100, drive.UsedBytes * 100 / drive.QuotaBytes),
            RootItemCount = drive.RootItemCount,
            Folders = [.. drive.Folders.Select(f => f.ToViewModel(timeZoneIanaId, rootName, withLocation: true))]
        };
    }
}
