using Maroik.FileStorage.Helpers;

namespace Maroik.FileStorage.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="StoragePath.ResolveWithinRoot"/> -- the backstop that keeps a
/// malformed or hostile caller-supplied path from reading or writing outside the configured
/// storage root. Every real caller (BoardService / CalendarService / AttachmentContentService)
/// sends a relative path that already starts with the <c>upload/</c> segment; the tests below use
/// that same convention alongside path-traversal and absolute-path attempts that must be rejected.
/// </summary>
public class StoragePathTests
{
    /// <summary>The storage root the paths are resolved against (never created on disk).</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MaroikStoragePathTests", "upload");

    // -- Null/empty inputs -------------------------------------------------------

    /// <summary>A null or blank upload root is rejected -- there is nothing to confine to.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveWithinRoot_ReturnsNull_WhenUploadRootIsBlank(string? uploadRoot)
    {
        Assert.Null(StoragePath.ResolveWithinRoot(uploadRoot, "upload/file.txt"));
    }

    /// <summary>A null or blank requested path is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveWithinRoot_ReturnsNull_WhenRequestedPathIsBlank(string? requestedPath)
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, requestedPath));
    }

    // -- Legitimate relative paths ------------------------------------------------

    /// <summary>A well-formed relative path (as every real caller sends) resolves to the expected absolute path inside root.</summary>
    [Fact]
    public void ResolveWithinRoot_ResolvesRelativePath_WhenItStaysInsideRoot()
    {
        string? resolved = StoragePath.ResolveWithinRoot(_root, "upload/Management/Profile/Avatar/photo.png");

        Assert.Equal(Path.Combine(_root, "Management", "Profile", "Avatar", "photo.png"), resolved);
    }

    /// <summary>A request for the root directory itself (no sub-path) is allowed -- the exact-match boundary.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsRootItself_WhenRequestedPathIsExactlyTheRoot()
    {
        string? resolved = StoragePath.ResolveWithinRoot(_root, "upload");

        Assert.Equal(_root, resolved);
    }

    /// <summary>A relative path containing a "../" that still lands back inside root is allowed.</summary>
    [Fact]
    public void ResolveWithinRoot_ResolvesRelativePath_WhenDotDotSegmentStaysInsideRoot()
    {
        string? resolved = StoragePath.ResolveWithinRoot(_root, "upload/sub/../file.txt");

        Assert.Equal(Path.Combine(_root, "file.txt"), resolved);
    }

    /// <summary>A trailing directory separator on the configured upload root does not change the resolved result.</summary>
    [Fact]
    public void ResolveWithinRoot_IgnoresTrailingSeparator_OnUploadRoot()
    {
        string? resolved = StoragePath.ResolveWithinRoot(_root + Path.DirectorySeparatorChar, "upload/file.txt");

        Assert.Equal(Path.Combine(_root, "file.txt"), resolved);
    }

    // -- Path-traversal / escape attempts -----------------------------------------

    /// <summary>A "../" deep enough to climb out of the upload root entirely is rejected.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_WhenDotDotSegmentsEscapeRoot()
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, "upload/../secret.txt"));
    }

    /// <summary>A deeply nested "../../../.." attempt is rejected just as well as a single escape.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_ForDeeplyNestedEscapeAttempt()
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, "upload/../../../../../../etc/passwd"));
    }

    /// <summary>A path that does not even start with the "upload/" segment lands outside root and is rejected.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_WhenRelativePathHasNoUploadSegment()
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, "some/other/file.txt"));
    }

    /// <summary>An absolute path elsewhere on the filesystem is rejected -- an absolute requestedPath ignores the root entirely.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_ForAbsolutePathOutsideRoot()
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, "/etc/passwd"));
    }

    /// <summary>
    /// A sibling directory whose name merely starts with the same characters as the root (e.g.
    /// "upload-evil" vs "upload") must not be treated as "inside root" by a naive string-prefix check.
    /// </summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_ForSiblingDirectoryWithSharedPrefix()
    {
        string sibling = _root + "-evil";

        Assert.Null(StoragePath.ResolveWithinRoot(_root, sibling));
    }

    /// <summary>An embedded NUL byte (not achievable through a normal HTTP request, but worth guarding) does not throw -- it is treated as an invalid path and rejected.</summary>
    [Fact]
    public void ResolveWithinRoot_ReturnsNull_ForPathWithEmbeddedNulCharacter()
    {
        Assert.Null(StoragePath.ResolveWithinRoot(_root, "upload/file\0.txt"));
    }
}
