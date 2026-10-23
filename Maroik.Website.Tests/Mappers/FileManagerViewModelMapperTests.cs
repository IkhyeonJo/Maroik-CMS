using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.FileManagers;

namespace Maroik.Website.Tests.Mappers;

/// <summary>
/// Unit tests for <see cref="FileManagerViewModelMapper"/>: the service's drive, folder and items become what the page draws — sizes as
/// labels, times in the user's zone, an icon per file type (U7), a search result's location from "My Drive" down, and the usage label and
/// bar fill (F7), all computed here rather than in the browser.
/// </summary>
public class FileManagerViewModelMapperTests
{
    /// <summary>A file item response.</summary>
    private static FileManagerItemResponse File(string name, long size = 1536) => new()
    {
        Id = 5, ParentId = 2, Name = name, Size = size, Updated = new DateTime(2026, 5, 24, 15, 30, 0), Previewable = true,
    };

    /// <summary>A file keeps its identity, gets a size label, its local modified time and its type's icon.</summary>
    [Fact]
    public void ToViewModel_MapsAFile()
    {
        FileManagerItemViewModel vm = File("Report.PDF").ToViewModel("Asia/Seoul", "My Drive", withLocation: false);

        Assert.Equal((5L, (long?)2, "Report.PDF", false, "1.5 KB", "2026-05-25 00:30", "fas fa-file-pdf text-danger", true, ""),
            (vm.Id, vm.ParentId, vm.Name, vm.IsFolder, vm.Size, vm.Modified, vm.Icon, vm.Previewable, vm.Location));
    }

    /// <summary>A folder has no size label, a folder icon and its item count.</summary>
    [Fact]
    public void ToViewModel_MapsAFolder()
    {
        var folder = new FileManagerItemResponse { Id = 2, Name = "Docs", IsFolder = true, ItemCount = 4, Updated = new DateTime(2026, 5, 24) };

        FileManagerItemViewModel vm = folder.ToViewModel("UTC", "My Drive", withLocation: false);

        Assert.Equal(("", "fas fa-folder text-warning", 4), (vm.Size, vm.Icon, vm.ItemCount));
    }

    /// <summary>Each file type has its icon; an unknown or missing extension gets the plain file icon.</summary>
    [Theory]
    [InlineData("a.png", "fas fa-file-image text-info")]
    [InlineData("a.JPEG", "fas fa-file-image text-info")]
    [InlineData("a.mp4", "fas fa-file-video text-purple")]
    [InlineData("a.mp3", "fas fa-file-audio text-success")]
    [InlineData("a.zip", "fas fa-file-archive text-warning")]
    [InlineData("a.txt", "fas fa-file-alt text-secondary")]
    [InlineData("a.docx", "fas fa-file-word text-primary")]
    [InlineData("a.xlsx", "fas fa-file-excel text-success")]
    [InlineData("a.pptx", "fas fa-file-powerpoint text-orange")]
    [InlineData("a.cs", "fas fa-file-code text-teal")]
    [InlineData("a.unknown", "fas fa-file text-muted")]
    [InlineData("README", "fas fa-file text-muted")]
    public void ToViewModel_PicksTheIcon_ByFileType(string name, string icon)
    {
        Assert.Equal(icon, File(name).ToViewModel("UTC", "My Drive", false).Icon);
    }

    /// <summary>A search result's location starts at the root's (localized) name.</summary>
    [Fact]
    public void ToViewModel_WithLocation_JoinsThePathFromTheRoot()
    {
        FileManagerItemResponse item = File("a.txt");
        item.Location = ["Documents", "Work"];

        Assert.Equal("My Drive / Documents / Work", item.ToViewModel("UTC", "My Drive", withLocation: true).Location);
        Assert.Equal("내 드라이브", File("b.txt").ToViewModel("UTC", "내 드라이브", withLocation: true).Location);
    }

    /// <summary>A folder keeps its breadcrumb and items in order.</summary>
    [Fact]
    public void ToViewModel_MapsAFolderResponse()
    {
        var folder = new FileManagerFolderResponse
        {
            FolderId = 2,
            Breadcrumb = [new() { Id = 1, Name = "A" }, new() { Id = 2, Name = "B" }],
            Items = [File("x.txt"), File("y.txt")],
        };

        FileManagerFolderViewModel vm = folder.ToViewModel("UTC", "My Drive");

        Assert.Equal((long?)2, vm.FolderId);
        Assert.Equal([(1L, "A"), (2L, "B")], vm.Breadcrumb.Select(c => (c.Id, c.Name)));
        Assert.Equal(["x.txt", "y.txt"], vm.Items.Select(i => i.Name));
    }

    /// <summary>The usage label is "used / quota", and the bar fill the percentage used (capped at 100; 100 for any use of a 0 quota).</summary>
    [Theory]
    [InlineData(0L, 0L, "0 B / 0 B", 0)]
    [InlineData(10L, 0L, "10 B / 0 B", 100)]
    [InlineData(512L, 1024L, "512 B / 1 KB", 50)]
    [InlineData(4096L, 1024L, "4 KB / 1 KB", 100)]
    public void ToViewModel_MapsTheDrive_WithTheUsage(long used, long quota, string usage, int percent)
    {
        var drive = new FileManagerDriveResponse
        {
            UsedBytes = used, QuotaBytes = quota, RootItemCount = 3,
            Folders = [new() { Id = 1, Name = "Docs", IsFolder = true, ItemCount = 2 }],
        };

        FileManagerDriveViewModel vm = drive.ToViewModel("UTC", "My Drive");

        Assert.Equal((usage, percent, 3), (vm.Usage, vm.UsedPercent, vm.RootItemCount));
        FileManagerItemViewModel folder = Assert.Single(vm.Folders);
        Assert.Equal(("Docs", "My Drive"), (folder.Name, folder.Location));
    }
}
