// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>Box-filter downscaling.</summary>
public static class ImageScaler
{
    /// <summary>Scales <paramref name="image"/> down so its longer side is <paramref name="maxSide"/> pixels
    /// (area averaging); smaller images are returned unchanged.</summary>
    public static RasterImage Fit(RasterImage image, int maxSide)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSide, 1);
        var scale = (double)maxSide / Math.Max(image.Width, image.Height);
        if (scale >= 1)
        {
            return image;
        }

        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        var components = image.Components;
        var result = new RasterImage(width, height, image.ColorType) { DpiX = image.DpiX * scale, DpiY = image.DpiY * scale };
        var sums = new double[components];
        for (var y = 0; y < height; y++)
        {
            var y0 = y * image.Height / height;
            var y1 = Math.Max(y0 + 1, (y + 1) * image.Height / height);
            for (var x = 0; x < width; x++)
            {
                var x0 = x * image.Width / width;
                var x1 = Math.Max(x0 + 1, (x + 1) * image.Width / width);
                Array.Clear(sums);
                for (var sy = y0; sy < y1; sy++)
                {
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var i = ((sy * image.Width) + sx) * components;
                        for (var c = 0; c < components; c++)
                        {
                            sums[c] += image.Pixels[i + c];
                        }
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var o = ((y * width) + x) * components;
                for (var c = 0; c < components; c++)
                {
                    result.Pixels[o + c] = (byte)Math.Round(sums[c] / count);
                }
            }
        }

        return result;
    }
}
