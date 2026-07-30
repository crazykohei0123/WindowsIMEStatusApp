using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace ImeStatusOverlay.Detection;

/// <summary>
/// Locates the IME mode indicator (the あ / A glyph) in the taskbar via UI
/// Automation and captures its pixels as a grayscale buffer.
/// </summary>
public static class Indicator
{
    /// <summary>
    /// Finds the screen rectangle of the mode indicator glyph.
    /// Returns null when it cannot be located.
    /// </summary>
    public static Rectangle? TryFindIndicatorRect()
    {
        foreach (var className in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            var rect = FindInTaskbar(className);
            if (rect.HasValue)
                return rect;
        }
        return null;
    }

    private static Rectangle? FindInTaskbar(string className)
    {
        AutomationElement? taskbar = FindByClass(AutomationElement.RootElement, className);
        if (taskbar == null)
            return null;

        // Mode indicator buttons are named "トレイ入力インジケーター ...".
        // The mode (あ/A) one mentions "IME のオプション"; fall back to any.
        var buttons = taskbar.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

        AutomationElement? fallback = null;
        foreach (AutomationElement b in buttons)
        {
            string name = b.Current.Name ?? string.Empty;
            if (!name.Contains("トレイ入力インジケーター"))
                continue;
            if (name.Contains("IME のオプション"))
                return GlyphRect(b);
            fallback ??= b;
        }
        return fallback != null ? GlyphRect(fallback) : null;
    }

    // The glyph is the child <Image>; use its bounds, else the button's center.
    private static Rectangle? GlyphRect(AutomationElement button)
    {
        var image = button.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image));
        var r = image != null ? image.Current.BoundingRectangle : button.Current.BoundingRectangle;

        if (image == null)
        {
            // Crop to the centered square region where the glyph sits.
            int side = (int)Math.Min(r.Width, r.Height) / 2;
            if (side <= 0) return null;
            int x = (int)(r.X + (r.Width - side) / 2);
            int y = (int)(r.Y + (r.Height - side) / 2);
            return new Rectangle(x, y, side, side);
        }

        if (r.Width <= 0 || r.Height <= 0) return null;
        return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
    }

    private static AutomationElement? FindByClass(AutomationElement parent, string className)
    {
        return parent.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ClassNameProperty, className));
    }

    /// <summary>
    /// Captures the given screen rectangle and returns a grayscale buffer
    /// (row-major, 0..255 per pixel) together with its dimensions.
    /// </summary>
    public static byte[] CaptureGray(Rectangle rect, out int width, out int height)
    {
        width = Math.Max(1, rect.Width);
        height = Math.Max(1, rect.Height);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var gfx = Graphics.FromImage(bmp))
            gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));

        var data = new byte[width * height];
        var bd = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int stride = bd.Stride;
            var raw = new byte[height * stride];
            Marshal.Copy(bd.Scan0, raw, 0, raw.Length);

            // Format32bppArgb is laid out in memory as BGRA per pixel.
            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                int dst = y * width;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x * 4;
                    byte b = raw[i];
                    byte g = raw[i + 1];
                    byte r = raw[i + 2];
                    data[dst + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return data;
    }
}
