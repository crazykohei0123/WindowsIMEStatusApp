using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IconGen;

// Writes a multi-image ICO file (ICONDIR + ICONDIRENTRY[] + image data).
// Supports 32-bit BGRA PNG-encoded entries (Vista+), which Windows uses for
// large icons and the taskbar/tray.
internal static class IconWriter
{
    public static void SaveAsIcon(IEnumerable<Bitmap> bitmaps, string path)
    {
        var list = bitmaps.ToList();
        if (list.Count == 0)
            throw new ArgumentException("No bitmaps to write.", nameof(bitmaps));

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR header
        bw.Write((short)0);     // reserved
        bw.Write((short)1);     // type: 1 = icon
        bw.Write((short)list.Count);

        // Directory entries (16 bytes each)
        int offset = 6 + list.Count * 16;
        var pngs = new List<byte[]>();
        foreach (var bmp in list)
        {
            byte[] data = EncodePng(bmp);
            pngs.Add(data);

            byte w = bmp.Width >= 256 ? (byte)0 : (byte)bmp.Width;
            byte h = bmp.Height >= 256 ? (byte)0 : (byte)bmp.Height;

            bw.Write(w);                        // width
            bw.Write(h);                        // height
            bw.Write((byte)0);                   // color count (0 = >8bpp)
            bw.Write((byte)0);                   // reserved
            bw.Write((short)1);                  // planes
            bw.Write((short)32);                 // bits per pixel
            bw.Write(data.Length);               // image size
            bw.Write(offset);                    // image offset
            offset += data.Length;
        }

        // Image data
        foreach (var data in pngs)
            bw.Write(data);
    }

    private static byte[] EncodePng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}