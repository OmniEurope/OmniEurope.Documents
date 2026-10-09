// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>The corner of a symbol instance its coordinates give (T.88 7.4.3.1.1, REFCORNER).</summary>
internal enum Jbig2Corner
{
    BottomLeft,
    TopLeft,
    BottomRight,
    TopRight,
}

/// <summary>The parameters of text region decoding (T.88 6.4.2).</summary>
internal sealed record Jbig2TextParameters
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int Instances { get; init; }

    public required IReadOnlyList<Jbig2Bitmap> Symbols { get; init; }

    public int LogStrips { get; init; }

    public int DefaultPixel { get; init; }

    public Jbig2Combination Combination { get; init; }

    public bool Transposed { get; init; }

    public Jbig2Corner Corner { get; init; } = Jbig2Corner.TopLeft;

    public int DsOffset { get; init; }
}

/// <summary>
/// The values a text region reads, from arithmetic or Huffman coded data: strip and position deltas, symbol IDs
/// and the refinement of an instance.
/// </summary>
internal interface IJbig2TextValues
{
    int StripDelta();

    int FirstS();

    /// <summary>The next S delta in the strip, or null when the strip ends.</summary>
    int? NextS();

    /// <summary>Reads the out-of-band value that ends the last strip.</summary>
    void EndStrip();

    int StripT(int logStrips);

    int SymbolId();

    /// <summary>The bitmap of an instance of <paramref name="symbol"/>: the symbol, or its refinement.</summary>
    Jbig2Bitmap Instance(Jbig2Bitmap symbol);
}

/// <summary>Text region decoding (T.88 6.4): symbol instances placed strip by strip and combined onto a region.</summary>
internal static class Jbig2TextRegion
{
    public static Jbig2Bitmap Decode(Jbig2TextParameters parameters, IJbig2TextValues values)
    {
        // More instances than pixels can only come from damaged data, which would otherwise keep the loop going.
        if (parameters.Instances < 0 || parameters.Instances > ((long)parameters.Width * parameters.Height) + 65536)
        {
            throw new InvalidDataException("A JBIG2 text region announces more symbol instances than it has pixels.");
        }

        var region = new Jbig2Bitmap(parameters.Width, parameters.Height, (byte)parameters.DefaultPixel);
        var strips = 1 << parameters.LogStrips;
        long stripT = -(long)values.StripDelta() * strips;
        long firstS = 0;
        var instances = 0;
        while (instances < parameters.Instances)
        {
            stripT += (long)values.StripDelta() * strips;
            firstS += values.FirstS();
            var s = firstS;
            while (true)
            {
                var t = stripT + (strips == 1 ? 0 : values.StripT(parameters.LogStrips));
                var id = values.SymbolId();
                if ((uint)id >= (uint)parameters.Symbols.Count)
                {
                    throw new InvalidDataException("A JBIG2 text region names a symbol that does not exist.");
                }

                var bitmap = values.Instance(parameters.Symbols[id]);
                s = Place(region, parameters, bitmap, s, t);
                instances++;

                // Every strip ends with an out-of-band S delta, the last one too (6.4.5 3 c i).
                if (instances >= parameters.Instances)
                {
                    values.EndStrip();
                    break;
                }

                if (values.NextS() is not { } delta)
                {
                    break;
                }

                s += delta + parameters.DsOffset;
            }
        }

        return region;
    }

    // Draws one instance with its reference corner at (s, t) and returns S moved past it (T.88 6.4.5 3 c x and xi).
    private static long Place(Jbig2Bitmap region, Jbig2TextParameters parameters, Jbig2Bitmap bitmap, long s, long t)
    {
        var corner = parameters.Corner;
        var right = corner is Jbig2Corner.TopRight or Jbig2Corner.BottomRight;
        var bottom = corner is Jbig2Corner.BottomLeft or Jbig2Corner.BottomRight;
        var (along, across) = parameters.Transposed ? (bitmap.Height, bitmap.Width) : (bitmap.Width, bitmap.Height);

        // The S coordinate runs along x (along y when transposed); the trailing corners move it first.
        var trailing = parameters.Transposed ? bottom : right;
        if (trailing)
        {
            s += along - 1;
        }

        var (sx, tx) = (s, t);
        long x;
        long y;
        if (parameters.Transposed)
        {
            x = right ? tx - across + 1 : tx;
            y = bottom ? sx - along + 1 : sx;
        }
        else
        {
            x = right ? sx - along + 1 : sx;
            y = bottom ? tx - across + 1 : tx;
        }

        region.Combine(bitmap, (int)Math.Clamp(x, int.MinValue / 2, int.MaxValue / 2), (int)Math.Clamp(y, int.MinValue / 2, int.MaxValue / 2), parameters.Combination);
        return trailing ? s : s + along - 1;
    }
}

/// <summary>The arithmetic decoding contexts a text region (or a symbol dictionary's refinements) reads with.</summary>
internal sealed class Jbig2TextContexts(int symbolCodeLength, int refinementTemplate)
{
    public Jbig2IntegerDecoder Dt { get; } = new();

    public Jbig2IntegerDecoder Fs { get; } = new();

    public Jbig2IntegerDecoder Ds { get; } = new();

    public Jbig2IntegerDecoder It { get; } = new();

    public Jbig2IntegerDecoder Ri { get; } = new();

    public Jbig2IntegerDecoder Rdw { get; } = new();

    public Jbig2IntegerDecoder Rdh { get; } = new();

    public Jbig2IntegerDecoder Rdx { get; } = new();

    public Jbig2IntegerDecoder Rdy { get; } = new();

    public Jbig2SymbolIdDecoder Id { get; } = new(symbolCodeLength);

    public Jbig2RefinementDecoder Refinement { get; set; } = new(refinementTemplate);
}

/// <summary>Text region values from arithmetic coded data (T.88 6.4 with SBHUFF 0).</summary>
internal sealed class Jbig2ArithmeticTextValues(MqDecoder decoder, Jbig2TextContexts contexts, bool refine, (int X, int Y)[] refinementAdaptive)
    : IJbig2TextValues
{
    public int StripDelta() => Required(contexts.Dt);

    public int FirstS() => Required(contexts.Fs);

    public int? NextS() => contexts.Ds.Decode(decoder);

    public void EndStrip() => contexts.Ds.Decode(decoder);

    public int StripT(int logStrips) => Required(contexts.It);

    public int SymbolId() => contexts.Id.Decode(decoder);

    public Jbig2Bitmap Instance(Jbig2Bitmap symbol)
    {
        if (!refine || Required(contexts.Ri) == 0)
        {
            return symbol;
        }

        var dw = Required(contexts.Rdw);
        var dh = Required(contexts.Rdh);
        var dx = Required(contexts.Rdx);
        var dy = Required(contexts.Rdy);
        return contexts.Refinement.Decode(decoder, symbol.Width + dw, symbol.Height + dh, symbol, (dw >> 1) + dx, (dh >> 1) + dy,
            refinementAdaptive, typicalPrediction: false);
    }

    private int Required(Jbig2IntegerDecoder integer) =>
        integer.Decode(decoder) ?? throw new InvalidDataException("A JBIG2 text region value is out of band.");
}

/// <summary>The Huffman tables a text region reads with (T.88 7.4.3.1.6).</summary>
internal sealed record Jbig2TextTables(
    Jbig2HuffmanTable Fs,
    Jbig2HuffmanTable Ds,
    Jbig2HuffmanTable Dt,
    Jbig2HuffmanTable Rdw,
    Jbig2HuffmanTable Rdh,
    Jbig2HuffmanTable Rdx,
    Jbig2HuffmanTable Rdy,
    Jbig2HuffmanTable Rsize);

/// <summary>
/// Text region values from Huffman coded data (T.88 6.4 with SBHUFF 1); symbol IDs come from the region's own
/// code table, or as fixed-length numbers when <paramref name="symbolCodes"/> is null. A refinement is arithmetic
/// coded in its own byte range.
/// </summary>
internal sealed class Jbig2HuffmanTextValues(
    Jbig2BitReader reader,
    Jbig2TextTables tables,
    Jbig2HuffmanTable? symbolCodes,
    int symbolCodeLength,
    bool refine,
    Jbig2RefinementDecoder refinement,
    (int X, int Y)[] refinementAdaptive) : IJbig2TextValues
{
    public int StripDelta() => tables.Dt.DecodeValue(reader);

    public int FirstS() => tables.Fs.DecodeValue(reader);

    public int? NextS() => (int?)tables.Ds.Decode(reader);

    // At the end of a region the data may stop before it, which loses nothing.
    public void EndStrip()
    {
        try
        {
            tables.Ds.Decode(reader);
        }
        catch (InvalidDataException)
        {
            // Nothing follows the last instance.
        }
    }

    public int StripT(int logStrips) => (int)reader.ReadBits(logStrips);

    public int SymbolId() => symbolCodes is null ? (int)reader.ReadBits(symbolCodeLength) : symbolCodes.DecodeValue(reader);

    public Jbig2Bitmap Instance(Jbig2Bitmap symbol)
    {
        if (!refine || reader.ReadBit() == 0)
        {
            return symbol;
        }

        var dw = tables.Rdw.DecodeValue(reader);
        var dh = tables.Rdh.DecodeValue(reader);
        var dx = tables.Rdx.DecodeValue(reader);
        var dy = tables.Rdy.DecodeValue(reader);
        var size = tables.Rsize.DecodeValue(reader);
        reader.AlignToByte();
        var start = reader.BytePosition;
        var decoder = new MqDecoder(reader.Data, start, Math.Min(reader.End, start + size));
        var bitmap = refinement.Decode(decoder, symbol.Width + dw, symbol.Height + dh, symbol, (dw >> 1) + dx, (dh >> 1) + dy,
            refinementAdaptive, typicalPrediction: false);
        reader.Seek(start + size);
        return bitmap;
    }
}
