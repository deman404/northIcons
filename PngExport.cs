using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NorthIcons;

public static class PngExport
{
    public static void Save(DrawingImage image, string target, int size)
    {
        if (size < 16 || size > 4096) throw new ArgumentOutOfRangeException(nameof(size));
        var bounds = image.Drawing.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidDataException("SVG has no drawable bounds.");
        double scale = Math.Min(size / bounds.Width, size / bounds.Height);
        var rect = new Rect((size - bounds.Width * scale) / 2, (size - bounds.Height * scale) / 2, bounds.Width * scale, bounds.Height * scale);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawImage(image, rect);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(target); encoder.Save(output);
    }
}
