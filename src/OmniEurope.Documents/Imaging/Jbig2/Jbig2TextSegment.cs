// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// A text region segment (T.88 7.4.3): its flags, Huffman table choices, refinement adaptive pixels, instance count
/// and, Huffman coded, the code table of its symbol IDs (7.4.3.1.7); then the region decoded.
/// </summary>
internal static class Jbig2TextSegment
{
    public static Jbig2Bitmap Decode(byte[] data, int start, int end, Jbig2RegionInfo info, IReadOnlyList<Jbig2Bitmap> symbols,
        IReadOnlyList<Jbig2HuffmanTable> tables)
    {
        var flags = Jbig2Bytes.UInt16(data, start);
        var at = start + 2;
        var huffman = (flags & 1) != 0;
        var refine = (flags & 2) != 0;
        var refinementTemplate = (flags >> 15) & 1;
        var huffmanFlags = 0;
        if (huffman)
        {
            huffmanFlags = Jbig2Bytes.UInt16(data, at);
            at += 2;
        }

        var adaptive = Jbig2RefinementDecoder.DefaultAdaptivePixels;
        if (refine && refinementTemplate == 0)
        {
            adaptive = Jbig2Segments.AdaptivePixels(data, at, 2);
            at += 4;
        }

        var instances = Jbig2Bytes.Int32(data, at);
        at += 4;
        var dsOffset = (flags >> 10) & 0x1F;
        var parameters = new Jbig2TextParameters
        {
            Width = info.Width,
            Height = info.Height,
            Instances = instances,
            Symbols = symbols,
            LogStrips = (flags >> 2) & 3,
            Corner = (Jbig2Corner)((flags >> 4) & 3),
            Transposed = (flags & 0x40) != 0,
            Combination = (Jbig2Combination)((flags >> 7) & 3),
            DefaultPixel = (flags >> 9) & 1,
            DsOffset = dsOffset >= 16 ? dsOffset - 32 : dsOffset,
        };
        var codeLength = Jbig2SymbolDictionary.CodeLength(symbols.Count);
        IJbig2TextValues values;
        if (huffman)
        {
            var reader = new Jbig2BitReader(data, at, end);
            var textTables = Tables(huffmanFlags, tables);
            var symbolCodes = SymbolCodes(reader, symbols.Count);
            values = new Jbig2HuffmanTextValues(reader, textTables, symbolCodes, codeLength, refine, new Jbig2RefinementDecoder(refinementTemplate), adaptive);
        }
        else
        {
            values = new Jbig2ArithmeticTextValues(new MqDecoder(data, at, end), new Jbig2TextContexts(codeLength, refinementTemplate), refine, adaptive);
        }

        return Jbig2TextRegion.Decode(parameters, values);
    }

    // The tables the Huffman flags pick, custom ones taken in order from the referred-to table segments (7.4.3.1.6).
    private static Jbig2TextTables Tables(int flags, IReadOnlyList<Jbig2HuffmanTable> custom)
    {
        var next = 0;
        Jbig2HuffmanTable Pick(int selector, params int[] standard) =>
            selector < standard.Length ? Jbig2HuffmanTable.Standard(standard[selector])
            : next < custom.Count ? custom[next++] : throw new InvalidDataException("A JBIG2 text region lacks a custom Huffman table.");

        var fs = Pick(flags & 3, 6, 7);
        var ds = Pick((flags >> 2) & 3, 8, 9, 10);
        var dt = Pick((flags >> 4) & 3, 11, 12, 13);
        var rdw = Pick((flags >> 6) & 3, 14, 15);
        var rdh = Pick((flags >> 8) & 3, 14, 15);
        var rdx = Pick((flags >> 10) & 3, 14, 15);
        var rdy = Pick((flags >> 12) & 3, 14, 15);
        var rsize = Pick((flags >> 14) & 1, 1);
        return new Jbig2TextTables(fs, ds, dt, rdw, rdh, rdx, rdy, rsize);
    }

    // The symbol ID code table: run code lengths, then each symbol's code length run-length coded (7.4.3.1.7).
    private static Jbig2HuffmanTable SymbolCodes(Jbig2BitReader reader, int count)
    {
        var runLengths = new int[35];
        for (var i = 0; i < runLengths.Length; i++)
        {
            runLengths[i] = (int)reader.ReadBits(4);
        }

        var runCodes = new Jbig2HuffmanTable([.. runLengths.Select((length, i) => new Jbig2HuffmanLine(i, length, 0))]);
        var lengths = new List<int>(count);
        while (lengths.Count < count)
        {
            var code = runCodes.DecodeValue(reader);
            var (value, repeat) = code switch
            {
                < 32 => (code, 1),
                32 => (lengths.Count > 0 ? lengths[^1] : throw new InvalidDataException("A JBIG2 symbol code length repeats nothing."), 3 + (int)reader.ReadBits(2)),
                33 => (0, 3 + (int)reader.ReadBits(3)),
                _ => (0, 11 + (int)reader.ReadBits(7)),
            };
            for (var i = 0; i < repeat && lengths.Count < count; i++)
            {
                lengths.Add(value);
            }
        }

        reader.AlignToByte();
        return new Jbig2HuffmanTable([.. lengths.Select((length, i) => new Jbig2HuffmanLine(i, length, 0))]);
    }
}
