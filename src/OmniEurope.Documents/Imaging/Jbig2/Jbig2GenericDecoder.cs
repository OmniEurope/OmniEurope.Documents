// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// Generic region decoding (T.88 6.2): each pixel decoded in the context of the pixels already decoded around it,
/// by one of four templates whose adaptive pixels the segment places, with typical prediction (a row flagged as
/// equal to the one above) and an optional skip bitmap; or MMR (CCITT Group 4) coded. The contexts live with the
/// decoder, so the symbols of one dictionary share them as the standard asks.
/// </summary>
internal sealed class Jbig2GenericDecoder
{
    // The template pixels, most significant context bit first; null marks an adaptive pixel, in its standard slot.
    private static readonly (int X, int Y)?[][] Templates =
    [
        [null, (-1, -2), (0, -2), (1, -2), null, null, (-2, -1), (-1, -1), (0, -1), (1, -1), (2, -1), null, (-4, 0), (-3, 0), (-2, 0), (-1, 0)],
        [(-1, -2), (0, -2), (1, -2), (2, -2), (-2, -1), (-1, -1), (0, -1), (1, -1), (2, -1), null, (-3, 0), (-2, 0), (-1, 0)],
        [(-1, -2), (0, -2), (1, -2), (-2, -1), (-1, -1), (0, -1), (1, -1), null, (-2, 0), (-1, 0)],
        [(-3, -1), (-2, -1), (-1, -1), (0, -1), (1, -1), null, (-4, 0), (-3, 0), (-2, 0), (-1, 0)],
    ];

    // Which adaptive pixel fills each null slot of template 0 (A4, A3, A2, A1); the other templates have only A1.
    private static readonly int[] TemplateZeroSlots = [3, 2, 1, 0];

    // The context typical prediction toggles its flag with (T.88 6.2.5.7, SLTP).
    private static readonly int[] PredictionContexts = [0x9B25, 0x0795, 0x00E5, 0x0195];

    private readonly int _template;
    private readonly byte[] _contexts;

    public Jbig2GenericDecoder(int template)
    {
        _template = template;
        _contexts = new byte[1 << Templates[template].Length];
    }

    private Jbig2GenericDecoder(int template, byte[] contexts)
    {
        _template = template;
        _contexts = contexts;
    }

    /// <summary>A decoder starting from this one's current contexts.</summary>
    public Jbig2GenericDecoder Copy() => new(_template, (byte[])_contexts.Clone());

    /// <summary>The number of adaptive pixels a template's segment lists.</summary>
    public static int AdaptivePixelCount(int template) => template == 0 ? 4 : 1;

    public Jbig2Bitmap Decode(MqDecoder decoder, int width, int height, (int X, int Y)[] adaptive, bool typicalPrediction, Jbig2Bitmap? skip = null)
    {
        var pixels = Pixels(adaptive);
        if (pixels.Any(p => p.Y > 0 || (p.Y == 0 && p.X >= 0)))
        {
            throw new InvalidDataException("A JBIG2 adaptive pixel lies on a pixel not decoded yet.");
        }

        // The bitmap is decoded in a buffer with white margins wide enough for every template pixel, so the context
        // of a pixel is read without bounds checks.
        var side = pixels.Max(p => Math.Abs(p.X));
        var top = pixels.Max(p => -p.Y);
        var stride = width + (2 * side);
        var buffer = new byte[(long)(height + top) * stride];
        var offsets = pixels.Select(p => (p.Y * stride) + p.X).ToArray();
        var prediction = false;
        for (var y = 0; y < height; y++)
        {
            var row = ((y + top) * stride) + side;
            if (typicalPrediction)
            {
                prediction ^= decoder.Decode(_contexts, PredictionContexts[_template]) == 1;
                if (prediction)
                {
                    Array.Copy(buffer, row - stride, buffer, row, width);
                    continue;
                }
            }

            DecodeRow(decoder, buffer, row, width, offsets, skip, y);
        }

        var bitmap = new Jbig2Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            Array.Copy(buffer, ((y + top) * stride) + side, bitmap.Pixels, y * width, width);
        }

        return bitmap;
    }

    private void DecodeRow(MqDecoder decoder, byte[] buffer, int row, int width, int[] offsets, Jbig2Bitmap? skip, int y)
    {
        for (var x = 0; x < width; x++)
        {
            if (skip is not null && skip.Pixels[(y * width) + x] == 1)
            {
                continue;
            }

            var at = row + x;
            var context = 0;
            foreach (var offset in offsets)
            {
                context = (context << 1) | buffer[at + offset];
            }

            buffer[at] = (byte)decoder.Decode(_contexts, context);
        }
    }

    /// <summary>
    /// An MMR (CCITT Group 4) coded bitmap, 1 bits black, read from <paramref name="start"/>; an end-of-block code
    /// ends it early (the rows left are white). <paramref name="next"/> is the byte after its data.
    /// </summary>
    public static Jbig2Bitmap DecodeMmr(byte[] data, int start, int end, int width, int height, out int next)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        var reader = new CcittBitReader(data[start..Math.Max(start, end)]);
        var reference = new[] { width, width };
        for (var y = 0; y < height && width > 0 && !reader.AtEnd && !reader.PeekEndOfBlock(); y++)
        {
            var changes = CcittRows.Read2D(reader, width, reference);
            for (var i = 0; i + 1 < changes.Length; i += 2)
            {
                Array.Fill(bitmap.Pixels, (byte)1, (y * width) + changes[i], Math.Min(changes[i + 1], width) - changes[i]);
            }

            reference = changes;
        }

        if (reader.PeekEndOfBlock())
        {
            reader.Skip(24);
        }

        next = start + (int)((reader.BitPosition + 7) / 8);
        return bitmap;
    }

    // The template's pixels with its adaptive pixels in their slots.
    private (int X, int Y)[] Pixels((int X, int Y)[] adaptive)
    {
        var template = Templates[_template];
        var result = new (int X, int Y)[template.Length];
        var next = 0;
        for (var i = 0; i < template.Length; i++)
        {
            result[i] = template[i] ?? adaptive[_template == 0 ? TemplateZeroSlots[next++] : next++];
        }

        return result;
    }
}
