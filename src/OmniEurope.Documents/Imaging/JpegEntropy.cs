// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>A Huffman table built for encoding: the DHT counts and values, and each symbol's code.</summary>
internal sealed class JpegEncodingTable
{
    public JpegEncodingTable(byte[] counts, byte[] values)
    {
        Counts = counts;
        Values = values;
        var code = 0;
        var k = 0;
        for (var length = 1; length <= 16; length++)
        {
            for (var i = 0; i < counts[length - 1]; i++, k++)
            {
                Codes[values[k]] = code++;
                Sizes[values[k]] = length;
            }

            code <<= 1;
        }
    }

    public byte[] Counts { get; }

    public byte[] Values { get; }

    public int[] Codes { get; } = new int[256];

    public int[] Sizes { get; } = new int[256];
}

/// <summary>The four tables of an image (DC and AC, luma and chroma).</summary>
internal sealed record JpegHuffmanTables(JpegEncodingTable[] Dc, JpegEncodingTable[] Ac)
{
    public void WriteDefinitions(Stream output, int used)
    {
        for (var t = 0; t < used; t++)
        {
            Write(output, 0x00 | t, Dc[t]);
            Write(output, 0x10 | t, Ac[t]);
        }
    }

    private static void Write(Stream output, int id, JpegEncodingTable table)
    {
        output.WriteByte((byte)id);
        output.Write(table.Counts);
        output.Write(table.Values);
    }
}

/// <summary>Builds length-limited (16 bits) optimal Huffman tables from symbol frequencies.</summary>
internal static class JpegHuffmanBuilder
{
    public static JpegHuffmanTables Build(EncoderComponent[] components, List<short[][]> blocks)
    {
        var dc = new long[2][];
        var ac = new long[2][];
        for (var t = 0; t < 2; t++)
        {
            dc[t] = new long[257];
            ac[t] = new long[257];
        }

        JpegEntropy.Traverse(components, blocks, (table, symbol, _, _, dcSymbol) =>
        {
            (dcSymbol ? dc : ac)[table][symbol]++;
        });
        return new JpegHuffmanTables([Table(dc[0]), Table(dc[1])], [Table(ac[0]), Table(ac[1])]);
    }

    private static JpegEncodingTable Table(long[] frequencies)
    {
        var freq = (long[])frequencies.Clone();
        if (Array.TrueForAll(freq, f => f == 0))
        {
            // An unused table still needs one code to be well formed.
            freq[0] = 1;
        }

        freq[256] = 1;
        var sizes = new int[257];
        var others = Enumerable.Repeat(-1, 257).ToArray();
        while (true)
        {
            var v1 = Least(freq, -1);
            var v2 = Least(freq, v1);
            if (v2 < 0)
            {
                break;
            }

            freq[v1] += freq[v2];
            freq[v2] = 0;
            for (sizes[v1]++; others[v1] >= 0; sizes[v1]++)
            {
                v1 = others[v1];
            }

            others[v1] = v2;
            for (sizes[v2]++; others[v2] >= 0; sizes[v2]++)
            {
                v2 = others[v2];
            }
        }

        var counts = new int[33];
        for (var i = 0; i < 257; i++)
        {
            if (sizes[i] > 0)
            {
                counts[sizes[i]]++;
            }
        }

        Limit(counts);
        var values = new List<byte>();
        for (var length = 1; length <= 32; length++)
        {
            for (var symbol = 0; symbol < 256; symbol++)
            {
                if (sizes[symbol] == length)
                {
                    values.Add((byte)symbol);
                }
            }
        }

        return new JpegEncodingTable(counts[1..17].Select(c => (byte)c).ToArray(), [.. values]);
    }

    // Moves codes longer than 16 bits up, then drops the reserved all-ones code.
    private static void Limit(int[] counts)
    {
        for (var i = 32; i > 16; i--)
        {
            while (counts[i] > 0)
            {
                var j = i - 2;
                while (counts[j] == 0)
                {
                    j--;
                }

                counts[i] -= 2;
                counts[i - 1]++;
                counts[j + 1] += 2;
                counts[j]--;
            }
        }

        var longest = 16;
        while (counts[longest] == 0)
        {
            longest--;
        }

        counts[longest]--;
    }

    // The symbol with the smallest non-zero frequency (the larger symbol on ties), excluding one.
    private static int Least(long[] freq, int excluded)
    {
        var best = -1;
        for (var i = 0; i < freq.Length; i++)
        {
            if (freq[i] > 0 && i != excluded && (best < 0 || freq[i] <= freq[best]))
            {
                best = i;
            }
        }

        return best;
    }
}

/// <summary>Writes the entropy-coded data of a baseline single-scan image.</summary>
internal static class JpegEntropy
{
    /// <summary>Called for each symbol: table index, symbol, extra bits value, extra bits count, DC or AC.</summary>
    public delegate void SymbolSink(int table, int symbol, int bits, int bitCount, bool dc);

    public static void Write(Stream output, EncoderComponent[] components, List<short[][]> blocks, JpegHuffmanTables tables)
    {
        var writer = new JpegBitWriter(output);
        Traverse(components, blocks, (table, symbol, bits, count, dc) =>
        {
            var t = dc ? tables.Dc[table] : tables.Ac[table];
            writer.Write(t.Codes[symbol], t.Sizes[symbol]);
            writer.Write(bits, count);
        });
        writer.Flush();
    }

    public static void Traverse(EncoderComponent[] components, List<short[][]> blocks, SymbolSink sink)
    {
        var maxH = components.Max(c => c.H);
        var maxV = components.Max(c => c.V);
        var luma = components[0];
        var lumaWidth = luma.Plane.Width * maxH / luma.H;
        var lumaHeight = luma.Plane.Height * maxV / luma.V;
        var mcusX = (lumaWidth + (8 * maxH) - 1) / (8 * maxH);
        var mcusY = (lumaHeight + (8 * maxV) - 1) / (8 * maxV);
        var predictors = new int[components.Length];
        for (var my = 0; my < mcusY; my++)
        {
            for (var mx = 0; mx < mcusX; mx++)
            {
                for (var c = 0; c < components.Length; c++)
                {
                    var component = components[c];
                    var blocksX = mcusX * component.H;
                    for (var v = 0; v < component.V; v++)
                    {
                        for (var h = 0; h < component.H; h++)
                        {
                            var block = blocks[c][((((my * component.V) + v) * blocksX) + (mx * component.H)) + h];
                            EncodeBlock(block, component.Table, ref predictors[c], sink);
                        }
                    }
                }
            }
        }
    }

    private static void EncodeBlock(short[] block, int table, ref int predictor, SymbolSink sink)
    {
        var diff = block[0] - predictor;
        predictor = block[0];
        var size = BitLength(diff);
        sink(table, size, Magnitude(diff, size), size, true);
        var run = 0;
        for (var k = 1; k < 64; k++)
        {
            var value = block[JpegFrame.ZigZag[k]];
            if (value == 0)
            {
                run++;
                continue;
            }

            while (run > 15)
            {
                sink(table, 0xF0, 0, 0, false);
                run -= 16;
            }

            var bits = BitLength(value);
            sink(table, (run << 4) | bits, Magnitude(value, bits), bits, false);
            run = 0;
        }

        if (run > 0)
        {
            sink(table, 0x00, 0, 0, false);
        }
    }

    private static int BitLength(int value)
    {
        var magnitude = Math.Abs(value);
        var length = 0;
        while (magnitude > 0)
        {
            length++;
            magnitude >>= 1;
        }

        return length;
    }

    private static int Magnitude(int value, int size) => value >= 0 ? value : (value - 1) & ((1 << size) - 1);
}

/// <summary>Packs bits most significant first, stuffing a zero byte after each 0xFF.</summary>
internal sealed class JpegBitWriter(Stream output)
{
    private int _buffer;
    private int _count;

    public void Write(int bits, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            _buffer = (_buffer << 1) | ((bits >> i) & 1);
            if (++_count == 8)
            {
                Emit();
            }
        }
    }

    public void Flush()
    {
        while (_count != 0)
        {
            Write(1, 1);
        }
    }

    private void Emit()
    {
        output.WriteByte((byte)_buffer);
        if (_buffer == 0xFF)
        {
            output.WriteByte(0);
        }

        _buffer = 0;
        _count = 0;
    }
}
