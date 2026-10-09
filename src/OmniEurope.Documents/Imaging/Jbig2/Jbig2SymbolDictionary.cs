// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>A decoded symbol dictionary: the symbols it exports and the coding contexts it may hand on.</summary>
internal sealed record Jbig2SymbolDictionaryResult(IReadOnlyList<Jbig2Bitmap> Exported, Jbig2SymbolContexts? Retained);

/// <summary>The arithmetic contexts of a symbol dictionary that a later dictionary may reuse (T.88 7.4.2.1.1).</summary>
internal sealed record Jbig2SymbolContexts(Jbig2GenericDecoder Generic, Jbig2RefinementDecoder Refinement)
{
    /// <summary>A copy, so that every dictionary reusing them starts from the state they were retained in.</summary>
    public Jbig2SymbolContexts Copy() => new(Generic.Copy(), Refinement.Copy());
}

/// <summary>The fields of a symbol dictionary segment (T.88 7.4.2.1).</summary>
internal sealed record Jbig2SymbolDictionaryHeader
{
    public bool Huffman { get; init; }

    public bool RefineAggregate { get; init; }

    public int Template { get; init; }

    public int RefinementTemplate { get; init; }

    public (int X, int Y)[] Adaptive { get; init; } = [];

    public (int X, int Y)[] RefinementAdaptive { get; init; } = [];

    public int ExportedCount { get; init; }

    public int NewCount { get; init; }

    public bool ContextUsed { get; init; }

    public bool ContextRetained { get; init; }

    public int HeightTable { get; init; }

    public int WidthTable { get; init; }

    public int SizeTable { get; init; }

    public int AggregateTable { get; init; }

    /// <summary>The byte the coded data starts at.</summary>
    public int DataStart { get; init; }

    public static Jbig2SymbolDictionaryHeader Parse(byte[] data, int start)
    {
        var flags = Jbig2Bytes.UInt16(data, start);
        var huffman = (flags & 1) != 0;
        var aggregate = (flags & 2) != 0;
        var template = (flags >> 10) & 3;
        var refinementTemplate = (flags >> 12) & 1;
        var at = start + 2;
        var adaptive = Array.Empty<(int X, int Y)>();
        if (!huffman)
        {
            adaptive = Jbig2Segments.AdaptivePixels(data, at, Jbig2GenericDecoder.AdaptivePixelCount(template));
            at += adaptive.Length * 2;
        }

        var refinementAdaptive = Jbig2RefinementDecoder.DefaultAdaptivePixels;
        if (aggregate && refinementTemplate == 0)
        {
            refinementAdaptive = Jbig2Segments.AdaptivePixels(data, at, 2);
            at += 4;
        }

        return new Jbig2SymbolDictionaryHeader
        {
            Huffman = huffman,
            RefineAggregate = aggregate,
            HeightTable = (flags >> 2) & 3,
            WidthTable = (flags >> 4) & 3,
            SizeTable = (flags >> 6) & 1,
            AggregateTable = (flags >> 7) & 1,
            ContextUsed = (flags & 0x100) != 0,
            ContextRetained = (flags & 0x200) != 0,
            Template = template,
            RefinementTemplate = refinementTemplate,
            Adaptive = adaptive,
            RefinementAdaptive = refinementAdaptive,
            ExportedCount = Jbig2Bytes.Int32(data, at),
            NewCount = Jbig2Bytes.Int32(data, at + 4),
            DataStart = at + 8,
        };
    }
}

/// <summary>
/// Symbol dictionary decoding (T.88 6.5): height classes of new symbols, each a generic region, a refinement or
/// aggregate of known symbols, or (Huffman coded) one collective bitmap cut by the symbol widths; then the export
/// flags pick the symbols the dictionary exports.
/// </summary>
internal sealed class Jbig2SymbolDictionary
{
    private readonly Jbig2SymbolDictionaryHeader _header;
    private readonly IReadOnlyList<Jbig2Bitmap> _input;
    private readonly List<Jbig2Bitmap> _new = [];
    private readonly int _codeLength;
    private long _pixels;

    private Jbig2SymbolDictionary(Jbig2SymbolDictionaryHeader header, IReadOnlyList<Jbig2Bitmap> input)
    {
        _header = header;
        _input = input;
        Known.AddRange(input);
        if (header.NewCount < 0 || header.ExportedCount < 0 || header.NewCount > 1 << 24)
        {
            throw new InvalidDataException("A JBIG2 symbol dictionary has an impossible symbol count.");
        }

        _codeLength = CodeLength(input.Count + header.NewCount);
    }

    /// <summary>The number of bits that number <paramref name="count"/> symbols (ceil(log2 count)).</summary>
    public static int CodeLength(int count)
    {
        var length = 0;
        while ((1L << length) < count)
        {
            length++;
        }

        return length;
    }

    /// <summary>Decodes a symbol dictionary segment's data.</summary>
    /// <param name="data">The segment data.</param>
    /// <param name="start">The start of the segment data.</param>
    /// <param name="end">The end of the segment data.</param>
    /// <param name="input">The symbols the referred-to dictionaries export.</param>
    /// <param name="tables">The referred-to table segments, in order.</param>
    /// <param name="reused">The contexts of the last referred-to dictionary, when it retained them.</param>
    public static Jbig2SymbolDictionaryResult Decode(byte[] data, int start, int end, IReadOnlyList<Jbig2Bitmap> input,
        IReadOnlyList<Jbig2HuffmanTable> tables, Jbig2SymbolContexts? reused)
    {
        var header = Jbig2SymbolDictionaryHeader.Parse(data, start);
        var dictionary = new Jbig2SymbolDictionary(header, input);
        var contexts = header.ContextUsed && reused is not null
            ? reused.Copy()
            : new Jbig2SymbolContexts(new Jbig2GenericDecoder(header.Template), new Jbig2RefinementDecoder(header.RefinementTemplate));
        var exportFlags = header.Huffman
            ? dictionary.DecodeHuffman(new Jbig2BitReader(data, header.DataStart, end), new Jbig2SymbolTables(header, tables), contexts)
            : dictionary.DecodeArithmetic(new MqDecoder(data, header.DataStart, end), contexts);
        var all = input.Concat(dictionary._new).ToList();
        var exported = new List<Jbig2Bitmap>();
        for (var i = 0; i < all.Count; i++)
        {
            if (exportFlags[i])
            {
                exported.Add(all[i]);
            }
        }

        return new Jbig2SymbolDictionaryResult(exported, header.ContextRetained ? contexts : null);
    }

    private List<Jbig2Bitmap> Known { get; } = [];

    private bool[] DecodeArithmetic(MqDecoder decoder, Jbig2SymbolContexts contexts)
    {
        var height = new Jbig2IntegerDecoder();
        var width = new Jbig2IntegerDecoder();
        var aggregate = new Jbig2IntegerDecoder();
        var text = new Jbig2TextContexts(_codeLength, _header.RefinementTemplate) { Refinement = contexts.Refinement };
        var classHeight = 0;
        while (_new.Count < _header.NewCount)
        {
            classHeight += Required(height.Decode(decoder));
            var symbolWidth = 0;
            while (width.Decode(decoder) is { } delta)
            {
                symbolWidth += delta;
                CheckRoom();
                if (!_header.RefineAggregate)
                {
                    AddNew(contexts.Generic.Decode(decoder, symbolWidth, classHeight, _header.Adaptive, typicalPrediction: false));
                    continue;
                }

                var count = Required(aggregate.Decode(decoder));
                var values = new Jbig2ArithmeticTextValues(decoder, text, refine: true, _header.RefinementAdaptive);
                AddNew(count == 1
                    ? RefineOne(text.Id.Decode(decoder), Required(text.Rdx.Decode(decoder)), Required(text.Rdy.Decode(decoder)),
                        symbolWidth, classHeight, (bitmap, w, h, dx, dy) => contexts.Refinement.Decode(decoder, w, h, bitmap, dx, dy, _header.RefinementAdaptive, false))
                    : Aggregate(count, symbolWidth, classHeight, values));
            }
        }

        var exportRun = new Jbig2IntegerDecoder();
        return ExportFlags(() => Required(exportRun.Decode(decoder)));
    }

    private bool[] DecodeHuffman(Jbig2BitReader reader, Jbig2SymbolTables tables, Jbig2SymbolContexts contexts)
    {
        var textTables = new Jbig2TextTables(Jbig2HuffmanTable.Standard(6), Jbig2HuffmanTable.Standard(8), Jbig2HuffmanTable.Standard(11),
            Jbig2HuffmanTable.Standard(15), Jbig2HuffmanTable.Standard(15), Jbig2HuffmanTable.Standard(15), Jbig2HuffmanTable.Standard(15),
            Jbig2HuffmanTable.Standard(1));
        var values = new Jbig2HuffmanTextValues(reader, textTables, null, _codeLength, true, contexts.Refinement, _header.RefinementAdaptive);
        var classHeight = 0;
        while (_new.Count < _header.NewCount)
        {
            classHeight += tables.Height.DecodeValue(reader);
            var symbolWidth = 0;
            var widths = new List<int>();
            while (tables.Width.Decode(reader) is { } delta)
            {
                symbolWidth += (int)delta;
                CheckRoom();
                if (!_header.RefineAggregate)
                {
                    widths.Add(symbolWidth);
                    continue;
                }

                var count = tables.Aggregate.DecodeValue(reader);
                AddNew(count == 1
                    ? RefineOne((int)reader.ReadBits(_codeLength), Jbig2HuffmanTable.Standard(15).DecodeValue(reader),
                        Jbig2HuffmanTable.Standard(15).DecodeValue(reader), symbolWidth, classHeight,
                        (bitmap, w, h, dx, dy) => RefineCoded(reader, contexts.Refinement, bitmap, w, h, dx, dy))
                    : Aggregate(count, symbolWidth, classHeight, values));
            }

            if (widths.Count > 0)
            {
                CollectiveBitmap(reader, tables.Size.DecodeValue(reader), widths, classHeight);
            }
        }

        return ExportFlags(() => Jbig2HuffmanTable.Standard(1).DecodeValue(reader));
    }

    private void AddNew(Jbig2Bitmap symbol)
    {
        _pixels += symbol.Pixels.Length;
        if (_pixels > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("A JBIG2 symbol dictionary holds more pixels than an image may.");
        }

        _new.Add(symbol);
        Known.Add(symbol);
    }

    private void CheckRoom()
    {
        if (_new.Count >= _header.NewCount)
        {
            throw new InvalidDataException("A JBIG2 symbol dictionary holds more symbols than it announces.");
        }
    }

    // A refinement of one known symbol (T.88 6.5.8.2.2).
    private Jbig2Bitmap RefineOne(int id, int dx, int dy, int width, int height, Func<Jbig2Bitmap, int, int, int, int, Jbig2Bitmap> refine)
    {
        var known = Known;
        if ((uint)id >= (uint)known.Count)
        {
            throw new InvalidDataException("A JBIG2 symbol refinement names a symbol that does not exist.");
        }

        return refine(known[id], width, height, dx, dy);
    }

    private Jbig2Bitmap RefineCoded(Jbig2BitReader reader, Jbig2RefinementDecoder refinement, Jbig2Bitmap reference, int width, int height, int dx, int dy)
    {
        var size = Jbig2HuffmanTable.Standard(1).DecodeValue(reader);
        reader.AlignToByte();
        var start = reader.BytePosition;
        var decoder = new MqDecoder(reader.Data, start, Math.Min(reader.End, start + size));
        var bitmap = refinement.Decode(decoder, width, height, reference, dx, dy, _header.RefinementAdaptive, typicalPrediction: false);
        reader.Seek(start + size);
        return bitmap;
    }

    // An aggregate of known symbols laid out as a one-strip text region (T.88 6.5.8.2.1).
    private Jbig2Bitmap Aggregate(int count, int width, int height, IJbig2TextValues values) =>
        Jbig2TextRegion.Decode(new Jbig2TextParameters { Width = width, Height = height, Instances = count, Symbols = Known }, values);

    // A height class coded as one bitmap, uncompressed or MMR, cut into its symbols (T.88 6.5.9).
    private void CollectiveBitmap(Jbig2BitReader reader, int size, List<int> widths, int height)
    {
        reader.AlignToByte();
        var start = reader.BytePosition;
        var total = SumWidths(widths);
        Jbig2Bitmap collective;
        if (size == 0)
        {
            collective = new Jbig2Bitmap(total, height);
            var stride = (total + 7) / 8;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < total; x++)
                {
                    collective[x, y] = (Jbig2Bytes.Byte(reader.Data, start + (y * stride) + (x >> 3)) >> (7 - (x & 7))) & 1;
                }
            }

            reader.Seek(start + (stride * height));
        }
        else
        {
            collective = Jbig2GenericDecoder.DecodeMmr(reader.Data, start, Math.Min(reader.End, start + size), total, height, out _);
            reader.Seek(start + size);
        }

        var x0 = 0;
        foreach (var width in widths)
        {
            AddNew(collective.Extract(x0, 0, width, height));
            x0 += width;
        }
    }

    private static int SumWidths(List<int> widths) => (int)Math.Min(int.MaxValue, widths.Sum(w => (long)w));

    // The export flags, coded as alternating runs starting with symbols not exported (T.88 6.5.10).
    private bool[] ExportFlags(Func<int> run)
    {
        var count = _input.Count + _new.Count;
        var flags = new bool[count];
        var index = 0;
        var exported = false;
        while (index < count)
        {
            var length = run();
            if (length < 0 || index + length > count)
            {
                throw new InvalidDataException("A JBIG2 symbol dictionary's export runs overrun its symbols.");
            }

            if (exported)
            {
                Array.Fill(flags, true, index, length);
            }

            index += length;
            exported = !exported;
        }

        return flags;
    }

    private static int Required(int? value) => value ?? throw new InvalidDataException("A JBIG2 symbol dictionary value is out of band.");
}

/// <summary>The Huffman tables a symbol dictionary reads its heights, widths and sizes with (T.88 7.4.2.1.6).</summary>
internal sealed class Jbig2SymbolTables
{
    public Jbig2SymbolTables(Jbig2SymbolDictionaryHeader header, IReadOnlyList<Jbig2HuffmanTable> custom)
    {
        var next = 0;
        Jbig2HuffmanTable Custom() => next < custom.Count ? custom[next++] : throw new InvalidDataException("A JBIG2 symbol dictionary lacks a custom Huffman table.");
        Height = header.HeightTable switch { 0 => Jbig2HuffmanTable.Standard(4), 1 => Jbig2HuffmanTable.Standard(5), _ => Custom() };
        Width = header.WidthTable switch { 0 => Jbig2HuffmanTable.Standard(2), 1 => Jbig2HuffmanTable.Standard(3), _ => Custom() };
        Size = header.SizeTable == 0 ? Jbig2HuffmanTable.Standard(1) : Custom();
        Aggregate = header.AggregateTable == 0 ? Jbig2HuffmanTable.Standard(1) : Custom();
    }

    public Jbig2HuffmanTable Height { get; }

    public Jbig2HuffmanTable Width { get; }

    public Jbig2HuffmanTable Size { get; }

    public Jbig2HuffmanTable Aggregate { get; }
}
