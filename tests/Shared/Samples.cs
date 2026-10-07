// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;

namespace OmniEurope.Documents.Tests;

/// <summary>Inputs and checks shared by the end-to-end scenarios.</summary>
internal static class Samples
{
    /// <summary>A PNG filled with one opaque colour.</summary>
    public static byte[] Png(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            (pixels[i], pixels[i + 1], pixels[i + 2]) = (r, g, b);
        }

        return PngCodec.Encode(new RasterImage(width, height, ImageColorType.Rgb, pixels));
    }

    /// <summary>The text of every page, in page order.</summary>
    public static string AllText(PdfDocument pdf) => string.Join("\n", pdf.Pages.Select(p => p.Text));

    /// <summary>Asserts that the fragments appear in this order in the text.</summary>
    public static void InOrder(string text, params string[] fragments)
    {
        var at = 0;
        foreach (var fragment in fragments)
        {
            var found = text.IndexOf(fragment, at, StringComparison.Ordinal);
            Assert.True(found >= 0, $"'{fragment}' not found after position {at}.");
            at = found + fragment.Length;
        }
    }

    /// <summary>The number of rendered pixels close to the colour.</summary>
    public static int Pixels(PdfPage page, byte r, byte g, byte b, double dpi = 36)
    {
        var image = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = dpi }).Image;
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetRgba(x, y);
                if (Math.Abs(pixel.R - r) < 40 && Math.Abs(pixel.G - g) < 40 && Math.Abs(pixel.B - b) < 40)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
