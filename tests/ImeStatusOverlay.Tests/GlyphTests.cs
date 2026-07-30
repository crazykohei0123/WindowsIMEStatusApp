using ImeStatusOverlay.Recognition;

namespace ImeStatusOverlay.Tests;

public class GlyphTests
{
    [Fact]
    public void Signature_AllBackground_ReturnsAllDotsAndZeroInk()
    {
        byte[] gray = { 100, 100, 100, 100 };
        string sig = Glyph.Signature(gray, 2, 2, out int ink);
        Assert.Equal("....", sig);
        Assert.Equal(0, ink);
    }

    [Fact]
    public void Signature_ForegroundPixel_MarkedAsHash()
    {
        // 3x3, all corners 50 (=> bg 50), center 200 differs by 150 (>40) => foreground.
        byte[] gray =
        {
            50, 50, 50,
            50, 200, 50,
            50, 50, 50,
        };
        string sig = Glyph.Signature(gray, 3, 3, out int ink);
        Assert.Equal(1, ink);
        Assert.Equal('#', sig[4]);
        Assert.Equal('.', sig[0]);
    }

    [Fact]
    public void Signature_StringLengthEqualsWidthTimesHeight()
    {
        byte[] gray = new byte[12]; // 3x4
        Array.Fill(gray, (byte)80);
        string sig = Glyph.Signature(gray, 3, 4, out _);
        Assert.Equal(12, sig.Length);
    }

    [Fact]
    public void Signature_DiffExactly40_IsBackground()
    {
        // corners 100 => bg 100; center 140 => |140-100|=40, NOT >40 => background.
        byte[] gray =
        {
            100, 100, 100,
            100, 140, 100,
            100, 100, 100,
        };
        string sig = Glyph.Signature(gray, 3, 3, out int ink);
        Assert.Equal(0, ink);
        Assert.Equal('.', sig[4]);
    }

    [Fact]
    public void Signature_Diff41_IsForeground()
    {
        byte[] gray =
        {
            100, 100, 100,
            100, 141, 100,
            100, 100, 100,
        };
        string sig = Glyph.Signature(gray, 3, 3, out int ink);
        Assert.Equal(1, ink);
        Assert.Equal('#', sig[4]);
    }
}
