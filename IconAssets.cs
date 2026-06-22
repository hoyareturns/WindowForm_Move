using System.Drawing.Imaging;
using System.Reflection;

namespace WindowForm_Move;

public static class IconAssets
{
    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void Draw(Graphics graphics, Rectangle bounds, string name, Color color, bool sharp)
    {
        var bitmap = Get(name);
        if (bitmap is null)
        {
            return;
        }

        var size = Math.Min(22, Math.Min(bounds.Width - 2, bounds.Height - 1));
        var destination = new Rectangle(
            bounds.Left + (bounds.Width - size) / 2,
            bounds.Top + (bounds.Height - size) / 2,
            size,
            size);
        using var attributes = new ImageAttributes();
        var red = color.R / 255F;
        var green = color.G / 255F;
        var blue = color.B / 255F;
        attributes.SetColorMatrix(new ColorMatrix(new[]
        {
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { 0F, 0F, 0F, 0F, 0F },
            new[] { red, green, blue, 1F, 0F },
            new[] { 0F, 0F, 0F, 0F, 1F }
        }));
        graphics.InterpolationMode = sharp
            ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
            : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = sharp
            ? System.Drawing.Drawing2D.PixelOffsetMode.Half
            : System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            bitmap,
            destination,
            0,
            0,
            bitmap.Width,
            bitmap.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static Bitmap? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var resourceName = $"WindowForm_Move.Assets.Icons.{name}.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var image = Image.FromStream(stream);
        var bitmap = new Bitmap(image);
        Cache[name] = bitmap;
        return bitmap;
    }
}
