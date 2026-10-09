// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// Generic refinement region decoding (T.88 6.3): a bitmap decoded against a reference bitmap, each pixel in the
/// context of the pixels already decoded and of the reference pixels around it, by template 0 (two adaptive
/// pixels) or 1, with typical prediction (a pixel whose 3 by 3 reference neighbourhood is uniform takes its value).
/// </summary>
internal sealed class Jbig2RefinementDecoder
{
    // The pixels of the bitmap being decoded, then those of the reference; null marks the adaptive pixel.
    private static readonly (int X, int Y)?[][] Coding = [[(0, -1), (1, -1), (-1, 0), null], [(-1, -1), (0, -1), (1, -1), (-1, 0)]];

    private static readonly (int X, int Y)?[][] Reference =
    [
        [(0, -1), (1, -1), (-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1), (1, 1), null],
        [(0, -1), (-1, 0), (0, 0), (1, 0), (0, 1), (1, 1)],
    ];

    // The context typical prediction toggles its flag with (T.88 6.3.5.6): with the pixel order above, the one where
    // only the reference pixel under the pixel being decoded is black.
    private static readonly int[] PredictionContexts = [0x0020, 0x0008];

    private readonly int _template;
    private readonly byte[] _contexts;

    public Jbig2RefinementDecoder(int template)
    {
        _template = template;
        _contexts = new byte[1 << (Coding[template].Length + Reference[template].Length)];
    }

    private Jbig2RefinementDecoder(int template, byte[] contexts)
    {
        _template = template;
        _contexts = contexts;
    }

    /// <summary>A decoder starting from this one's current contexts.</summary>
    public Jbig2RefinementDecoder Copy() => new(_template, (byte[])_contexts.Clone());

    /// <summary>The nominal adaptive pixels of template 0 (one in the bitmap, one in the reference); template 1 has none.</summary>
    public static (int X, int Y)[] DefaultAdaptivePixels { get; } = [(-1, -1), (-1, -1)];

    /// <summary>
    /// Decodes a <paramref name="width"/> by <paramref name="height"/> bitmap whose pixel (x, y) corresponds to the
    /// reference pixel (x - dx, y - dy).
    /// </summary>
    public Jbig2Bitmap Decode(MqDecoder decoder, int width, int height, Jbig2Bitmap reference, int dx, int dy,
        (int X, int Y)[] adaptive, bool typicalPrediction)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        var coding = Coding[_template].Select(p => p ?? adaptive[0]).ToArray();
        var referencePixels = Reference[_template].Select(p => p ?? adaptive[1]).ToArray();
        var prediction = false;
        for (var y = 0; y < height; y++)
        {
            if (typicalPrediction)
            {
                prediction ^= decoder.Decode(_contexts, PredictionContexts[_template]) == 1;
            }

            for (var x = 0; x < width; x++)
            {
                var rx = x - dx;
                var ry = y - dy;
                if (prediction && Uniform(reference, rx, ry) is { } value)
                {
                    bitmap[x, y] = value;
                    continue;
                }

                var context = 0;
                foreach (var (px, py) in coding)
                {
                    context = (context << 1) | bitmap[x + px, y + py];
                }

                foreach (var (px, py) in referencePixels)
                {
                    context = (context << 1) | reference[rx + px, ry + py];
                }

                if (decoder.Decode(_contexts, context) == 1)
                {
                    bitmap[x, y] = 1;
                }
            }
        }

        return bitmap;
    }

    // The value of a 3 by 3 reference neighbourhood all of one value, or null.
    private static int? Uniform(Jbig2Bitmap reference, int x, int y)
    {
        var first = reference[x - 1, y - 1];
        for (var j = -1; j <= 1; j++)
        {
            for (var i = -1; i <= 1; i++)
            {
                if (reference[x + i, y + j] != first)
                {
                    return null;
                }
            }
        }

        return first;
    }
}
