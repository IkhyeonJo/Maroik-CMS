using Maroik.Core.Contract.Misc.Extensions;

namespace Maroik.Core.Contract.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="Int64Extensions"/> — byte-count formatting helpers
/// used for attachment size and upload-limit display.
/// </summary>
public class Int64ExtensionsTests
{
    // -- ToMegabytesLabel ---------------------------------------------------------

    /// <summary>To megabytes label exact megabyte returns whole number.</summary>
    [Fact]
    public void ToMegabytesLabel_ExactMegabyte_ReturnsWholeNumber()
    {
        Assert.Equal("10", (10L * 1048576).ToMegabytesLabel());
    }

    /// <summary>To megabytes label partial megabyte rounds to one decimal.</summary>
    [Fact]
    public void ToMegabytesLabel_PartialMegabyte_RoundsToOneDecimal()
    {
        // 9.5MB = 9,961,472.8 bytes; use an exact value to avoid rounding ambiguity.
        const long bytes = (long)(9.5 * 1048576);
        Assert.Equal("9.5", bytes.ToMegabytesLabel());
    }

    /// <summary>To megabytes label zero returns zero.</summary>
    [Fact]
    public void ToMegabytesLabel_Zero_ReturnsZero()
    {
        Assert.Equal("0", 0L.ToMegabytesLabel());
    }

    // -- ToKilobytesLabel -----------------------------------------------------------

    /// <summary>To kilobytes label exact kilobyte returns whole number.</summary>
    [Fact]
    public void ToKilobytesLabel_ExactKilobyte_ReturnsWholeNumber()
    {
        Assert.Equal("1", 1024L.ToKilobytesLabel());
    }

    /// <summary>To kilobytes label truncates partial kilobyte.</summary>
    [Fact]
    public void ToKilobytesLabel_TruncatesPartialKilobyte()
    {
        // 1536 bytes = 1.5KB, but the conversion truncates (int division) before formatting.
        Assert.Equal("1", 1536L.ToKilobytesLabel());
    }

    /// <summary>To kilobytes label large value groups thousands.</summary>
    [Fact]
    public void ToKilobytesLabel_LargeValue_GroupsThousands()
    {
        Assert.Equal("1,024", (1024L * 1024).ToKilobytesLabel());
    }

    /// <summary>To kilobytes label zero returns zero.</summary>
    [Fact]
    public void ToKilobytesLabel_Zero_ReturnsZero()
    {
        Assert.Equal("0", 0L.ToKilobytesLabel());
    }
}
