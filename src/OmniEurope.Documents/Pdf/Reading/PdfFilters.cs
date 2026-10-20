// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>
/// The general-purpose stream filters (ISO 32000-1 §7.4): FlateDecode (tolerating a missing or damaged
/// zlib wrapper), LZWDecode, ASCIIHexDecode, ASCII85Decode and RunLengthDecode, with PNG and TIFF
/// predictors. Image filters (DCT, CCITT, JBIG2, JPX) are left to image decoding and stop the chain.
/// Output size is capped to defeat decompression bombs.
/// </summary>
internal static class PdfFilters
{
    public const long MaxDecodedSize = 512L << 20;

    public const string TooLargeMessage = "A PDF stream decodes to more data than allowed.";

    public static readonly HashSet<string> ImageFilters = new(StringComparer.Ordinal)
    {
        "DCTDecode", "DCT", "CCITTFaxDecode", "CCF", "JBIG2Decode", "JPXDecode",
    };

    // The filters decoded here, by full and abbreviated (inline image) name. Crypt is applied when the object
    // is read, so it passes the data through.
    private static readonly Dictionary<string, Func<byte[], PdfDictionary?, long, byte[]>> StreamFilters = new(StringComparer.Ordinal)
    {
        ["FlateDecode"] = (data, parms, max) => Predict(Inflate(data, max), parms),
        ["Fl"] = (data, parms, max) => Predict(Inflate(data, max), parms),
        ["LZWDecode"] = (data, parms, max) => Predict(Lzw(data, EarlyChange(parms), max), parms),
        ["LZW"] = (data, parms, max) => Predict(Lzw(data, EarlyChange(parms), max), parms),
        ["ASCIIHexDecode"] = (data, _, max) => AsciiHex(data, max),
        ["AHx"] = (data, _, max) => AsciiHex(data, max),
        ["ASCII85Decode"] = (data, _, max) => Ascii85(data, max),
        ["A85"] = (data, _, max) => Ascii85(data, max),
        ["RunLengthDecode"] = (data, _, max) => RunLength(data, max),
        ["RL"] = (data, _, max) => RunLength(data, max),
        ["Crypt"] = (data, _, _) => data,
    };

    /// <summary>Applies the non-image filters in order; returns the bytes and the first image filter met (null when none).</summary>
    public static (byte[] Data, string? ImageFilter, PdfDictionary? ImageParameters) Decode(byte[] data, IReadOnlyList<string> filters, IReadOnlyList<PdfDictionary?> parameters, long maxDecodedSize = MaxDecodedSize)
    {
        for (var i = 0; i < filters.Count; i++)
        {
            var name = filters[i];
            var parms = i < parameters.Count ? parameters[i] : null;
            if (ImageFilters.Contains(name))
            {
                return (data, name, parms);
            }

            data = StreamFilters.TryGetValue(name, out var filter)
                ? filter(data, parms, maxDecodedSize)
                : throw new NotSupportedException($"PDF filter '{name}' is not supported.");
            if (data.Length > maxDecodedSize)
            {
                throw TooLarge();
            }
        }

        return (data, null, null);
    }

    // Reported as InvalidDataException like every other unreadable stream, so callers that skip unreadable
    // content keep working; the Flate recovery below never swallows it (it is thrown only past the ceiling).
    private static InvalidDataException TooLarge() => new(TooLargeMessage);

    private static void Guard(long length, long add, long max)
    {
        if (length + add > max)
        {
            throw TooLarge();
        }
    }

    public static byte[] Inflate(byte[] data, long max = MaxDecodedSize)
    {
        var skip = 0;
        if (data.Length >= 2 && (data[0] & 0x0F) == 8 && ((data[0] << 8) | data[1]) % 31 == 0)
        {
            skip = 2;
        }

        // Raw deflate after the header: a bad or missing Adler-32 checksum does not lose the data.
        var output = Inflate(data, skip, 65536, max, out var damaged);
        if (!damaged)
        {
            return output;
        }

        // Corrupt data: keep what was decoded, as viewers do. A failing read loses what it had decoded, so the
        // data is read again one byte at a time (only the byte decoded by the failing read is lost); nothing
        // decoded at all is an error.
        var kept = Inflate(data, skip, 1, max, out _);
        return kept.Length > 0 ? kept : throw new InvalidDataException("Damaged FlateDecode data.");
    }

    private static byte[] Inflate(byte[] data, int skip, int chunk, long max, out bool damaged)
    {
        using var input = new DeflateStream(new MemoryStream(data, skip, data.Length - skip), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[chunk];
        damaged = false;
        try
        {
            int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                Guard(output.Length, n, max);
                output.Write(buffer, 0, n);
            }
        }
        catch (InvalidDataException exception) when (exception.Message != TooLargeMessage)
        {
            damaged = true;
        }

        return output.ToArray();
    }

    private static int EarlyChange(PdfDictionary? parms) => parms?["EarlyChange"] is PdfNumber n ? n.IntValue : 1;

    public static byte[] Predict(byte[] data, PdfDictionary? parms)
    {
        var predictor = parms?["Predictor"] is PdfNumber p ? p.IntValue : 1;
        if (predictor < 2)
        {
            return data;
        }

        var colors = parms!["Colors"] is PdfNumber c ? Math.Max(1, c.IntValue) : 1;
        var bits = parms["BitsPerComponent"] is PdfNumber b ? b.IntValue : 8;
        var columns = parms["Columns"] is PdfNumber w ? Math.Max(1, w.IntValue) : 1;
        // Computed in 64 bits, so that forged dimensions cannot wrap to an empty row that never advances; a row
        // longer than the data holds no complete row and leaves nothing to predict.
        var rowBytes = (((long)colors * bits * columns) + 7) / 8;
        if (bits is not (1 or 2 or 4 or 8 or 16) || colors > 32 || rowBytes >= Array.MaxLength)
        {
            throw new InvalidDataException($"Unsupported predictor parameters: {colors} colours of {bits} bits, {columns} columns.");
        }

        var bytesPerPixel = Math.Max(1, (colors * bits) + 7 >> 3);
        var rowLength = (int)rowBytes;
        return predictor == 2 ? TiffPredictor(data, rowLength, bytesPerPixel, bits) : PngPredictor(data, rowLength, bytesPerPixel);
    }

    private static byte[] PngPredictor(byte[] data, int rowLength, int bpp)
    {
        var rows = data.Length / (rowLength + 1);
        if (rows == 0)
        {
            return [];
        }

        var output = new byte[rows * rowLength];
        var previous = new byte[rowLength];
        for (var r = 0; r < rows; r++)
        {
            var filter = data[r * (rowLength + 1)];
            var row = output.AsSpan(r * rowLength, rowLength);
            data.AsSpan((r * (rowLength + 1)) + 1, rowLength).CopyTo(row);
            for (var i = 0; i < rowLength; i++)
            {
                var left = i >= bpp ? row[i - bpp] : 0;
                var up = previous[i];
                var upLeft = i >= bpp ? previous[i - bpp] : 0;
                row[i] += filter switch
                {
                    1 => (byte)left,
                    2 => up,
                    3 => (byte)((left + up) >> 1),
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
            }

            row.CopyTo(previous);
        }

        return output;
    }

    private static byte Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    private static byte[] TiffPredictor(byte[] data, int rowLength, int bpp, int bits)
    {
        if (bits != 8)
        {
            return data;
        }

        var output = (byte[])data.Clone();
        for (var start = 0; start + rowLength <= output.Length; start += rowLength)
        {
            for (var i = bpp; i < rowLength; i++)
            {
                output[start + i] += output[start + i - bpp];
            }
        }

        return output;
    }

    private static byte[] Lzw(byte[] data, int earlyChange, long max)
    {
        using var output = new MemoryStream();
        var table = new List<byte[]>(4096);
        void Reset()
        {
            table.Clear();
            for (var i = 0; i < 256; i++)
            {
                table.Add([(byte)i]);
            }

            table.Add([]);
            table.Add([]);
        }

        Reset();
        var width = 9;
        byte[]? previous = null;
        long bit = 0;
        var total = (long)data.Length * 8;
        while (bit + width <= total)
        {
            var code = 0;
            for (var i = 0; i < width; i++, bit++)
            {
                code = (code << 1) | ((data[bit >> 3] >> (7 - (int)(bit & 7))) & 1);
            }

            if (code == 256)
            {
                Reset();
                width = 9;
                previous = null;
                continue;
            }

            if (code == 257)
            {
                break;
            }

            byte[] entry;
            if (code < table.Count)
            {
                entry = table[code];
            }
            else if (previous is not null && code == table.Count)
            {
                entry = [.. previous, previous[0]];
            }
            else
            {
                break;
            }

            Guard(output.Length, entry.Length, max);
            output.Write(entry);

            if (previous is not null && table.Count < 4096)
            {
                table.Add([.. previous, entry[0]]);
            }

            previous = entry;
            var limit = (1 << width) - earlyChange;
            if (table.Count >= limit && width < 12)
            {
                width++;
            }
        }

        return output.ToArray();
    }

    private static byte[] AsciiHex(byte[] data, long max)
    {
        var output = new List<byte>(data.Length / 2);
        var high = -1;
        foreach (var b in data)
        {
            if (b == '>')
            {
                break;
            }

            if (!PdfLexer.IsHex(b))
            {
                continue;
            }

            if (high < 0)
            {
                high = PdfLexer.HexValue(b);
            }
            else
            {
                Guard(output.Count, 1, max);
                output.Add((byte)((high << 4) | PdfLexer.HexValue(b)));
                high = -1;
            }
        }

        if (high >= 0)
        {
            Guard(output.Count, 1, max);
            output.Add((byte)(high << 4));
        }

        return [.. output];
    }

    private static byte[] Ascii85(byte[] data, long max)
    {
        var output = new List<byte>(data.Length);
        Span<int> group = stackalloc int[5];
        var count = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (b == '~')
            {
                break;
            }

            if (b == 'z' && count == 0)
            {
                Guard(output.Count, 4, max);
                output.AddRange([0, 0, 0, 0]);
                continue;
            }

            if (b is < (byte)'!' or > (byte)'u')
            {
                continue;
            }

            group[count++] = b - '!';
            if (count == 5)
            {
                Emit(group, 4, output, max);
                count = 0;
            }
        }

        if (count > 1)
        {
            for (var i = count; i < 5; i++)
            {
                group[i] = 84;
            }

            Emit(group, count - 1, output, max);
        }

        return [.. output];
    }

    private static void Emit(Span<int> group, int bytes, List<byte> output, long max)
    {
        Guard(output.Count, bytes, max);
        long value = 0;
        foreach (var digit in group)
        {
            value = (value * 85) + digit;
        }

        for (var k = 0; k < bytes; k++)
        {
            output.Add((byte)(value >> (24 - (8 * k))));
        }
    }

    private static byte[] RunLength(byte[] data, long max)
    {
        var output = new List<byte>(data.Length * 2);
        var i = 0;
        while (i < data.Length)
        {
            var length = data[i++];
            if (length == 128)
            {
                break;
            }

            if (length < 128)
            {
                var count = Math.Min(length + 1, data.Length - i);
                Guard(output.Count, count, max);
                output.AddRange(data.AsSpan(i, count));
                i += count;
            }
            else if (i < data.Length)
            {
                Guard(output.Count, 257 - length, max);
                output.AddRange(Enumerable.Repeat(data[i++], 257 - length));
            }
        }

        return [.. output];
    }
}
