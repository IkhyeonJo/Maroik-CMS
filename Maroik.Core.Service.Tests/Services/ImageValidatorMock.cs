using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// An <see cref="IImageValidatorService"/> mock whose <c>StripMetadata</c> hands the bytes back unchanged, labelled
/// the way the real validator labels them — PNG when they start with the PNG signature, JPEG otherwise — so a test
/// that is not about re-encoding still gets a usable result. A test may override it with its own setup.
/// </summary>
internal static class ImageValidatorMock
{
    /// <summary>The 8-byte signature every PNG file starts with.</summary>
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A loose mock with the echoing <c>StripMetadata</c> described on the class.</summary>
    public static Mock<IImageValidatorService> Create()
    {
        var mock = new Mock<IImageValidatorService>();
        mock.Setup(v => v.StripMetadata(It.IsAny<byte[]>())).Returns<byte[]>(bytes =>
            bytes.AsSpan().StartsWith(_pngSignature)
                ? new StrippedImage(bytes, ".png", "image/png")
                : new StrippedImage(bytes, ".jpg", "image/jpeg"));
        return mock;
    }
}
