// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Tests.Imaging;

/// <summary>The fixtures written by Fixtures/Images/generate-images.ps1 and the pixel patterns they encode.</summary>
internal static class ImageFixtures
{
    public const int Width = 37;
    public const int Height = 23;

    public static byte[] Read(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", name));

    public static (byte R, byte G, byte B) Rgb(int x, int y) => ((byte)((x * 7) & 255), (byte)((y * 11) & 255), (byte)(((x + y) * 5) & 255));

    public static byte Alpha(int x, int y) => (byte)(((x * 13) + (y * 3)) & 255);

    public static byte Gray(int x) => (byte)((x * 7) & 255);

    public static (byte R, byte G, byte B) Palette(int x, int y) => ((byte)(x / 5 * 36), (byte)(y / 5 * 50), 128);

    public static bool IsBlack(int x, int y) => y switch
    {
        20 => false,
        21 => true,
        _ => x / (1 + (y * 7)) % 2 == 1,
    };

    /// <summary>Mean and largest absolute difference between an image and a BGR24 reference.</summary>
    public static (double Mean, int Max) Compare(RasterImage image, byte[] bgr)
    {
        long total = 0;
        var max = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var (r, g, b, _) = image.GetRgba(x, y);
                var i = ((y * image.Width) + x) * 3;
                foreach (var d in (int[])[Math.Abs(r - bgr[i + 2]), Math.Abs(g - bgr[i + 1]), Math.Abs(b - bgr[i])])
                {
                    total += d;
                    max = Math.Max(max, d);
                }
            }
        }

        return ((double)total / (image.Width * image.Height * 3), max);
    }
}
