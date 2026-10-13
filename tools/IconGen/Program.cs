// Generates assets/app.ico. Run: dotnet run --project tools/IconGen -- <output.ico>
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

int[] sizes = [256, 48, 32, 16];
string outPath = args.Length > 0 ? args[0] : "app.ico";

var images = sizes.Select(size => EncodeDib(DrawIcon(size))).ToList();
using (var fs = File.Create(outPath))
{
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0);
    bw.Write((ushort)1); // icon
    bw.Write((ushort)images.Count);

    int offset = 6 + 16 * images.Count;
    foreach (var (image, size) in images.Zip(sizes))
    {
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write((uint)image.Length);
        bw.Write((uint)offset);
        offset += image.Length;
    }

    foreach (var image in images)
    {
        bw.Write(image);
    }
}

Console.WriteLine($"Wrote {outPath}");
Validate(outPath);

static Bitmap DrawIcon(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    int pad = Math.Max(1, (int)(size * 0.04));
    var rect = new Rectangle(pad, pad, size - 2 * pad, size - 2 * pad);
    int radius = (int)(size * 0.22);
    using var path = new GraphicsPath();
    int d = radius * 2;
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    using var blue = new SolidBrush(Color.FromArgb(255, 37, 99, 235));
    g.FillPath(blue, path);

    using var font = new Font("Segoe UI", Math.Max(8, size * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
    using var white = new SolidBrush(Color.White);
    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    g.DrawString("R", font, white, new RectangleF(0, -size * 0.02f, size, size), sf);
    return bmp;
}

static byte[] EncodeDib(Bitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] pixels = new byte[data.Stride * h];
    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
    bmp.UnlockBits(data);

    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write(40);
    bw.Write(w);
    bw.Write(h * 2);
    bw.Write((ushort)1);
    bw.Write((ushort)32);
    bw.Write(0); // BI_RGB
    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

    int stride = data.Stride;
    for (int y = h - 1; y >= 0; y--)
    {
        bw.Write(pixels.AsSpan(y * stride, w * 4));
    }

    int maskRow = ((w + 31) / 32) * 4;
    bw.Write(new byte[maskRow * h]);
    bw.Flush();
    return ms.ToArray();
}

static void Validate(string path)
{
    using var icon = new Icon(path, 16, 16);
    if (icon.Width != 16 || icon.Height != 16)
    {
        throw new InvalidOperationException($"validation failed: {icon.Width}x{icon.Height}");
    }

    Console.WriteLine($"Validated: loads as {icon.Width}x{icon.Height}");
}
