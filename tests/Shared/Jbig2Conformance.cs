// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Tests;

/// <summary>One page of a JBIG2 conformance sample and the reference bitmap it must decode to (1 is black).</summary>
internal sealed record Jbig2Sample(string File, int Page, string Expected)
{
    public override string ToString() => $"{File} page {Page}";
}

/// <summary>
/// The JBIG2 conformance samples of Fixtures/Jbig2 (standalone JBIG2 files, T.88 annex D.1 and D.2) with the
/// reference bitmaps listed in its manifest, turned into the embedded organisation a PDF holds.
/// </summary>
internal static class Jbig2Conformance
{
    public static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Jbig2");

    /// <summary>The samples of the manifest whose file lies in <paramref name="subfolder"/>.</summary>
    public static IEnumerable<Jbig2Sample> Samples(string subfolder) =>
        File.ReadAllLines(Path.Combine(Folder, "manifest.txt"))
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split(' '))
            .Where(parts => parts[0].StartsWith(subfolder + "/", StringComparison.Ordinal))
            .Select(parts => new Jbig2Sample(parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture), parts[2]));

    /// <summary>The reference bitmap of a sample, from its PBM file: width, height and one byte per pixel.</summary>
    public static (int Width, int Height, byte[] Pixels) Expected(Jbig2Sample sample)
    {
        var data = File.ReadAllBytes(Path.Combine(Folder, "Expected", sample.Expected));
        var at = 0;
        Assert.Equal("P4", Token(data, ref at));
        var width = int.Parse(Token(data, ref at), CultureInfo.InvariantCulture);
        var height = int.Parse(Token(data, ref at), CultureInfo.InvariantCulture);
        at++;
        var stride = (width + 7) / 8;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = (byte)((data[at + (y * stride) + (x / 8)] >> (7 - (x % 8))) & 1);
            }
        }

        return (width, height, pixels);
    }

    /// <summary>The sample's page as embedded data: the page-0 segments as globals (null if none), the page's segments.</summary>
    public static (byte[]? Globals, byte[] Data) Embedded(Jbig2Sample sample)
    {
        var segments = Segments(File.ReadAllBytes(Path.Combine(Folder, sample.File)));
        var globals = segments.Where(s => s.Page == 0 && s.Type != 51).SelectMany(s => s.Bytes).ToArray();
        var data = segments.Where(s => s.Page == sample.Page).SelectMany(s => s.Bytes).ToArray();
        Assert.NotEmpty(data);
        return (globals.Length > 0 ? globals : null, data);
    }

    /// <summary>Every segment of a standalone file, header then data, with its type and page.</summary>
    public static List<(int Type, int Page, byte[] Bytes)> Segments(byte[] file)
    {
        Assert.Equal([0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A], file[..8]);
        var flags = file[8];
        var at = 9 + ((flags & 2) == 0 ? 4 : 0);
        var segments = new List<(int, int, byte[])>();
        if ((flags & 1) != 0)
        {
            // Sequential: each header followed by its data.
            while (at < file.Length)
            {
                var (headerLength, length, type, page) = Header(file, at);
                var dataLength = length == uint.MaxValue ? UnknownLength(file, at + headerLength) : (int)length;
                var end = Math.Min(file.Length, at + headerLength + dataLength);
                segments.Add((type, page, file[at..end]));
                at = end;
                if (type == 51)
                {
                    break;
                }
            }

            return segments;
        }

        // Random access: every header, then every segment's data in the same order.
        var headers = new List<(int At, int HeaderLength, int Length, int Type, int Page)>();
        while (true)
        {
            var (headerLength, length, type, page) = Header(file, at);
            headers.Add((at, headerLength, (int)length, type, page));
            at += headerLength;
            if (type == 51)
            {
                break;
            }
        }

        foreach (var header in headers)
        {
            segments.Add((header.Type, header.Page, [.. file[header.At..(header.At + header.HeaderLength)], .. file[at..(at + header.Length)]]));
            at += header.Length;
        }

        return segments;
    }

    /// <summary>A one-page PDF drawing the embedded JBIG2 data as a DeviceGray image filling the page.</summary>
    public static byte[] Pdf(byte[] data, byte[]? globals, int width, int height)
    {
        var output = new MemoryStream();
        var offsets = new List<long>();
        void Write(string text) => output.Write(Encoding.Latin1.GetBytes(text));
        void Object(string text)
        {
            offsets.Add(output.Position);
            Write(text);
        }

        var content = $"q {width} 0 0 {height} 0 0 cm /Im0 Do Q";
        var parameters = globals is null ? string.Empty : "/DecodeParms << /JBIG2Globals 6 0 R >>";
        Write("%PDF-1.7\n");
        Object("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Object("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Object($"3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >> endobj\n");
        Object($"4 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");
        Object($"5 0 obj << /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /JBIG2Decode {parameters} /Length {data.Length} >> stream\n");
        output.Write(data);
        Write("\nendstream endobj\n");
        if (globals is not null)
        {
            Object($"6 0 obj << /Length {globals.Length} >> stream\n");
            output.Write(globals);
            Write("\nendstream endobj\n");
        }

        var xref = output.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:D10} 00000 n \n");
        }

        Write($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Token(byte[] data, ref int at)
    {
        while (data[at] is (byte)' ' or (byte)'\n')
        {
            at++;
        }

        var start = at;
        while (data[at] is not ((byte)' ' or (byte)'\n'))
        {
            at++;
        }

        return Encoding.ASCII.GetString(data, start, at - start);
    }

    // The segment header (T.88 7.2): its length, the data length, the type and the page it belongs to.
    private static (int HeaderLength, uint Length, int Type, int Page) Header(byte[] data, int at)
    {
        var start = at;
        var number = BigEndian(data, at);
        var flags = data[at + 4];
        at += 5;
        var count = data[at] >> 5;
        if (count == 7)
        {
            count = BigEndian(data, at) & 0x1FFFFFFF;
            at += 4 + ((count + 8) / 8);
        }
        else
        {
            at++;
        }

        at += count * ((uint)number <= 256 ? 1 : (uint)number <= 65536 ? 2 : 4);
        var longPage = (flags & 0x40) != 0;
        var page = longPage ? BigEndian(data, at) : data[at];
        at += longPage ? 4 : 1;
        var length = (uint)BigEndian(data, at);
        return (at + 4 - start, length, flags & 0x3F, page);
    }

    // An immediate generic region of unknown length ends with its marker then a row count (T.88 7.2.7).
    private static int UnknownLength(byte[] data, int start)
    {
        var mmr = (data[start + 17] & 1) != 0;
        for (var i = start + 18; i + 5 < data.Length; i++)
        {
            if (mmr ? data[i] == 0 && data[i + 1] == 0 : data[i] == 0xFF && data[i + 1] == 0xAC)
            {
                return i + 6 - start;
            }
        }

        throw new InvalidDataException("A generic region of unknown length has no end marker.");
    }

    private static int BigEndian(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
}
