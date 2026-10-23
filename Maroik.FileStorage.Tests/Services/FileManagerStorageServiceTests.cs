using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.FileStorage.Services;
using Maroik.FileStorage.Settings;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.FileStorage.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileManagerStorageService"/>, the File Manager's large-file flow (Implementation note [A]), against a real
/// throwaway storage root on disk and a mocked ClamAV client: chunks are appended to a staging file in offset order (P5); completing an
/// upload checks the size, scans the file streamed from disk and moves it to <c>upload/FileManager/{GUID}</c> (P1, S2); an abort drops
/// the staging file; and a delete removes only a file directly in <c>upload/FileManager/</c> (P4).
/// </summary>
public sealed class FileManagerStorageServiceTests : IDisposable
{
    /// <summary>The throwaway parent of the storage root.</summary>
    private readonly string _base = Path.Combine(Path.GetTempPath(), "fm-" + Guid.NewGuid().ToString("N"));

    /// <summary>The storage root (<c>{base}/upload</c>), as the service is configured with it.</summary>
    private string Root => Path.Combine(_base, "upload");

    /// <summary>The ClamAV client the service scans with.</summary>
    private readonly Mock<IClamavClient> _clamav = new();

    /// <summary>The upload id these tests use.</summary>
    private static readonly Guid UploadId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    /// <summary>Creates the storage root; the ClamAV mock answers Clean unless a test says otherwise.</summary>
    public FileManagerStorageServiceTests()
    {
        Directory.CreateDirectory(Root);
        Scans(ClamavScanResult.Clean);
    }

    /// <summary>Removes the throwaway storage.</summary>
    public void Dispose() => Directory.Delete(_base, recursive: true);

    /// <summary>Makes every scan answer <paramref name="result"/>, recording what was scanned.</summary>
    private void Scans(ClamavScanResult result, List<byte[]>? scanned = null) =>
        _clamav.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream s, string _, int _, TimeSpan _, CancellationToken c) =>
            {
                using var copy = new MemoryStream();
                s.Position = 0;
                await s.CopyToAsync(copy, c);
                scanned?.Add(copy.ToArray());
                return result;
            });

    /// <summary>The service under test, over the throwaway root.</summary>
    private FileManagerStorageService Sut => new(_clamav.Object, Options.Create(new FileStorageSetting { StorageRootPath = Root, ClamavHost = "clamav", ClamavPort = 3310 }));

    /// <summary>The staging file of <paramref name="id"/>.</summary>
    private string Staging(Guid id) => Path.Combine(Root, "FileManager", ".staging", id.ToString("D"));

    /// <summary>The stored file of <paramref name="id"/>.</summary>
    private string Stored(Guid id) => Path.Combine(Root, "FileManager", id.ToString("D"));

    /// <summary>Appends <paramref name="bytes"/> at <paramref name="offset"/>.</summary>
    private Task<(string? Error, long Length)> AppendAsync(long offset, params byte[] bytes) =>
        Sut.AppendChunkAsync(UploadId.ToString("D"), offset, new MemoryStream(bytes), TestContext.Current.CancellationToken);

    // -- AppendChunkAsync ---------------------------------------------------------

    /// <summary>The first chunk creates the staging file and each next chunk at the current length is appended to it.</summary>
    [Fact]
    public async Task AppendChunkAsync_AppendsChunks_InOffsetOrder()
    {
        Assert.Equal((null, 3L), await AppendAsync(0, 1, 2, 3));
        Assert.Equal((null, 5L), await AppendAsync(3, 4, 5));

        Assert.Equal([1, 2, 3, 4, 5], await File.ReadAllBytesAsync(Staging(UploadId), TestContext.Current.CancellationToken));
    }

    /// <summary>A chunk whose offset is not the current length (a resend, a gap) is refused with the current length, and nothing is written.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(5L)]
    public async Task AppendChunkAsync_RefusesAChunkAtAnotherOffset(long offset)
    {
        await AppendAsync(0, 1, 2, 3);

        Assert.Equal((FileStorageRefusals.OffsetMismatch, 3L), await AppendAsync(offset, 9, 9));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Staging(UploadId), TestContext.Current.CancellationToken));
    }

    /// <summary>An upload id that is not a GUID (a path, a traversal) is refused before anything touches the disk.</summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AppendChunkAsync_RefusesAnUploadIdThatIsNotAGuid(string? uploadId)
    {
        var result = await Sut.AppendChunkAsync(uploadId, 0, new MemoryStream([1]), TestContext.Current.CancellationToken);

        Assert.Equal((FileStorageRefusals.InvalidUpload, 0L), result);
        Assert.False(Directory.Exists(Path.Combine(Root, "FileManager")));
    }

    // -- CompleteAsync ------------------------------------------------------------

    /// <summary>
    /// A complete upload of the declared size is scanned — streamed from the staging file, with the File Manager's long time limit —
    /// then moved to upload/FileManager/{GUID}; the staging file is gone.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_ScansAndStoresTheFile()
    {
        List<byte[]> scanned = [];
        Scans(ClamavScanResult.Clean, scanned);
        await AppendAsync(0, 1, 2, 3);

        string? error = await Sut.CompleteAsync(UploadId.ToString("D"), 3, TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Stored(UploadId), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Staging(UploadId)));
        Assert.Equal([1, 2, 3], Assert.Single(scanned));
        _clamav.Verify(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), "clamav", 3310, FileManagerStorageService.ScanTimeout, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(FileManagerStorageService.ScanTimeout >= TimeSpan.FromMinutes(10));
    }

    /// <summary>An empty file (no chunk was sent) is stored too.</summary>
    [Fact]
    public async Task CompleteAsync_StoresAnEmptyFile()
    {
        Assert.Null(await Sut.CompleteAsync(UploadId.ToString("D"), 0, TestContext.Current.CancellationToken));

        Assert.Empty(await File.ReadAllBytesAsync(Stored(UploadId), TestContext.Current.CancellationToken));
    }

    /// <summary>An infected or unscannable file is not stored (S2): the refusal says which, and the staging file is deleted.</summary>
    [Theory]
    [InlineData(ClamavScanResult.Infected, FileStorageRefusals.Infected)]
    [InlineData(ClamavScanResult.Unavailable, FileStorageRefusals.ScanUnavailable)]
    public async Task CompleteAsync_RefusesAnInfectedOrUnscannableFile(ClamavScanResult scan, string refusal)
    {
        Scans(scan);
        await AppendAsync(0, 1, 2, 3);

        Assert.Equal(refusal, await Sut.CompleteAsync(UploadId.ToString("D"), 3, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Stored(UploadId)));
        Assert.False(File.Exists(Staging(UploadId)));
    }

    /// <summary>A staging file of another size than declared is refused without a scan and deleted.</summary>
    [Fact]
    public async Task CompleteAsync_RefusesASizeMismatch()
    {
        await AppendAsync(0, 1, 2, 3);

        Assert.Equal(FileStorageRefusals.SizeMismatch, await Sut.CompleteAsync(UploadId.ToString("D"), 4, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Staging(UploadId)));
        _clamav.Verify(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A file never overwrites one already stored under the same GUID.</summary>
    [Fact]
    public async Task CompleteAsync_NeverOverwritesAStoredFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Stored(UploadId))!);
        await File.WriteAllBytesAsync(Stored(UploadId), [7], TestContext.Current.CancellationToken);
        await AppendAsync(0, 1);

        Assert.Equal(FileStorageRefusals.InvalidUpload, await Sut.CompleteAsync(UploadId.ToString("D"), 1, TestContext.Current.CancellationToken));
        Assert.Equal([7], await File.ReadAllBytesAsync(Stored(UploadId), TestContext.Current.CancellationToken));
    }

    /// <summary>An invalid upload id is refused.</summary>
    [Fact]
    public async Task CompleteAsync_RefusesAnUploadIdThatIsNotAGuid()
    {
        Assert.Equal(FileStorageRefusals.InvalidUpload, await Sut.CompleteAsync("../x", 0, TestContext.Current.CancellationToken));
    }

    // -- Abort --------------------------------------------------------------------

    /// <summary>An abort deletes the staging file; aborting an upload that has none is fine; an invalid id is refused.</summary>
    [Fact]
    public async Task Abort_DeletesTheStagingFile()
    {
        await AppendAsync(0, 1, 2);

        Assert.True(Sut.Abort(UploadId.ToString("D")));
        Assert.False(File.Exists(Staging(UploadId)));
        Assert.True(Sut.Abort(Guid.NewGuid().ToString("D")));
        Assert.False(Sut.Abort("../x"));
    }

    // -- Delete -------------------------------------------------------------------

    /// <summary>A stored File Manager file is deleted.</summary>
    [Fact]
    public async Task Delete_RemovesAFileManagerFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Stored(UploadId))!);
        await File.WriteAllBytesAsync(Stored(UploadId), [1], TestContext.Current.CancellationToken);

        Assert.Null(Sut.Delete($"upload/FileManager/{UploadId:D}"));
        Assert.False(File.Exists(Stored(UploadId)));
    }

    /// <summary>A missing file is reported as not found.</summary>
    [Fact]
    public void Delete_ReportsAMissingFile()
    {
        Assert.Equal(FileStorageRefusals.FileNotFound, Sut.Delete($"upload/FileManager/{UploadId:D}"));
    }

    /// <summary>
    /// Only a GUID-named file directly in upload/FileManager/ can be deleted: an attachment, a staging file, a traversal or a name that is
    /// not a GUID is refused and the file stays (the attachments keep their "no delete API" rule).
    /// </summary>
    [Theory]
    [InlineData("upload/Forum/a.zip")]
    [InlineData("upload/FileManager/.staging/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("upload/FileManager/../Forum/a.zip")]
    [InlineData("upload/FileManager/notes.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Delete_RefusesAnythingButAFileManagerFile(string? path)
    {
        string forum = Path.Combine(Root, "Forum", "a.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(forum)!);
        await File.WriteAllBytesAsync(forum, [1], TestContext.Current.CancellationToken);

        Assert.Equal(FileStorageRefusals.NotAFileManagerFile, Sut.Delete(path));
        Assert.True(File.Exists(forum));
    }
}
