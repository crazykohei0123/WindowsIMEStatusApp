using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using IconGen;

// Generates a multi-resolution ICO file from a vector drawing that mirrors
// assets/app.svg. We draw with GDI+ instead of rasterizing the SVG because the
// environment lacks ImageMagick/Inkscape/cairosvg.
//
// Usage:  dotnet run --project tools/IconGen -- <output.ico> [size...]
// Default sizes: 256, 128, 64, 48, 32, 16

var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
var outPath = cmdArgs.Length > 0 ? cmdArgs[0] : "app.ico";
var sizes = cmdArgs.Length > 1
    ? cmdArgs.Skip(1).Select(int.Parse).ToArray()
    : new[] { 256, 128, 64, 48, 32, 16 };

var bitmaps = new List<Bitmap>();
foreach (var s in sizes)
    bitmaps.Add(DrawIcon(s));

IconWriter.SaveAsIcon(bitmaps, outPath);
foreach (var b in bitmaps) b.Dispose();
Console.WriteLine($"Wrote {outPath} ({sizes.Length} sizes)");

static Bitmap DrawIcon(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    bmp.SetResolution(96, 96);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    float S = size;          // scale factor (SVG is 256-based)
    float k = S / 256f;

    // Background rounded rect with gradient
    var bgRect = new RectangleF(8 * k, 8 * k, 240 * k, 240 * k);
    float radius = 48 * k;
    using (var bgPath = RoundedRect(bgRect, radius))
    using (var brush = new LinearGradientBrush(
        new PointF(bgRect.Left, bgRect.Top),
        new PointF(bgRect.Left, bgRect.Bottom),
        ColorTranslator.FromHtml("#1E3A8A"),
        ColorTranslator.FromHtml("#0E7490")))
    {
        g.FillPath(brush, bgPath);
    }

    // Eye almond shape
    var eyePath = new GraphicsPath();
    eyePath.AddBezier(
        new PointF(40 * k, 128 * k),
        new PointF(80 * k, 80 * k),
        new PointF(176 * k, 80 * k),
        new PointF(216 * k, 128 * k));
    eyePath.AddBezier(
        new PointF(216 * k, 128 * k),
        new PointF(176 * k, 176 * k),
        new PointF(80 * k, 176 * k),
        new PointF(40 * k, 128 * k));
    eyePath.CloseFigure();

    g.FillPath(Brushes.White, eyePath);
    using var eyePen = new Pen(ColorTranslator.FromHtml("#0F172A"), 6 * k) { LineJoin = LineJoin.Round };
    g.DrawPath(eyePen, eyePath);

    // Iris
    var irisRect = new RectangleF(
        (128 - 44) * k, (128 - 44) * k,
        88 * k, 88 * k);
    using (var irisBrush = new LinearGradientBrush(
        irisRect,
        ColorTranslator.FromHtml("#34D399"),
        ColorTranslator.FromHtml("#0EA5E9"),
        LinearGradientMode.Vertical))
    {
        g.FillEllipse(irisBrush, irisRect);
    }
    using var irisPen = new Pen(ColorTranslator.FromHtml("#0F172A"), 4 * k);
    g.DrawEllipse(irisPen, irisRect);

    // Pupil
    var pupilRect = new RectangleF(
        (128 - 20) * k, (128 - 20) * k,
        40 * k, 40 * k);
    g.FillEllipse(Brushes.Black, pupilRect);

    // Glint (soft white highlight)
    using (var glintPath = new GraphicsPath())
    {
        glintPath.AddEllipse(irisRect);
        using var glintBrush = new PathGradientBrush(glintPath)
        {
            CenterColor = Color.FromArgb(230, 255, 255, 255),
            SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) }
        };
        // Shift highlight toward upper-left
        var state = g.Save();
        using (glintBrush)
        {
            g.SetClip(glintPath);
            var glintRect = new RectangleF(
                irisRect.X - irisRect.Width * 0.15f,
                irisRect.Y - irisRect.Height * 0.15f,
                irisRect.Width * 1.3f,
                irisRect.Height * 1.3f);
            g.FillEllipse(glintBrush, glintRect);
            g.ResetClip();
        }
        g.Restore(state);
    }

    // Small hard glint dot
    var dotRect = new RectangleF(
        (116 - 8) * k, (116 - 8) * k,
        16 * k, 16 * k);
    g.FillEllipse(Brushes.White, dotRect);

    // "あ" glyph in pupil to represent IME
    if (size >= 32)
    {
        float fontSize = 22 * k;
        using var font = new Font("Yu Gothic", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        using var glyphBrush = new SolidBrush(ColorTranslator.FromHtml("#34D399"));
        g.DrawString("あ", font, glyphBrush, 128 * k, 132 * k, sf);
    }

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF rect, float radius)
{
    var path = new GraphicsPath();
    float d = radius * 2;
    path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}