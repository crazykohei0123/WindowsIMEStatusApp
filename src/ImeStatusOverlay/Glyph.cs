namespace ImeStatusOverlay;

/// <summary>Helpers for turning a grayscale glyph capture into a binary signature.</summary>
public static class Glyph
{
    /// <summary>
    /// Binarizes a grayscale frame against its corner background.
    /// Returns a string of '#' (foreground) and '.' (background), and sets
    /// <paramref name="ink"/> to the number of foreground pixels. あ has more
    /// ink than A, which is how the two states are auto-labeled.
    /// </summary>
    public static string Signature(byte[] gray, int w, int h, out int ink)
    {
        int bg = (gray[0] + gray[w - 1] + gray[(h - 1) * w] + gray[(h - 1) * w + w - 1]) / 4;
        var sb = new System.Text.StringBuilder(gray.Length);
        ink = 0;
        foreach (var v in gray)
        {
            if (System.Math.Abs(v - bg) > 40) { sb.Append('#'); ink++; }
            else sb.Append('.');
        }
        return sb.ToString();
    }
}