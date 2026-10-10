// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Tests;

/// <summary>A JPEG 2000 conformance sample, the reference planes it must decode to and the tolerance allowed.</summary>
internal sealed record Jpeg2000Sample(string Group, string File, string Expected, int Tolerance, string Reference)
{
    public override string ToString() => File;
}

/// <summary>One plane of samples: its size, its largest sample value (2^depth - 1) and its samples row by row.</summary>
internal sealed record Jpeg2000TestPlane(int Width, int Height, int Max, int[] Samples);

/// <summary>
/// The JPEG 2000 samples of Fixtures/Jpeg2000 with the reference planes listed in its manifest, the comparison of
/// decoded planes with them, a marker scan telling which features a sample uses, and PDF wrappers.
/// </summary>
internal static class Jpeg2000Conformance
{
    public static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Jpeg2000");

    public static IEnumerable<Jpeg2000Sample> Samples() =>
        File.ReadAllLines(Path.Combine(Folder, "manifest.txt"))
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split(' '))
            .Select(p => new Jpeg2000Sample(p[0], p[1], p[2], int.Parse(p[3], CultureInfo.InvariantCulture), p[4]));

    public static Jpeg2000Sample Sample(string name) => Samples().Single(s => s.File == "Samples/" + name);

    public static byte[] Data(Jpeg2000Sample sample) => File.ReadAllBytes(Path.Combine(Folder, sample.File));

    public static List<Jpeg2000TestPlane> Expected(Jpeg2000Sample sample) => Planes(File.ReadAllBytes(Path.Combine(Folder, "Expected", sample.Expected)));

    public static List<Jpeg2000TestPlane> Expected(string file) => Planes(File.ReadAllBytes(Path.Combine(Folder, "Expected", file)));

    /// <summary>The images of a concatenated binary PGM file (two bytes per sample, most significant first, above 255).</summary>
    public static List<Jpeg2000TestPlane> Planes(byte[] data)
    {
        var planes = new List<Jpeg2000TestPlane>();
        var at = 0;
        while (at < data.Length)
        {
            Assert.Equal("P5", Token(data, ref at));
            var width = int.Parse(Token(data, ref at), CultureInfo.InvariantCulture);
            var height = int.Parse(Token(data, ref at), CultureInfo.InvariantCulture);
            var max = int.Parse(Token(data, ref at), CultureInfo.InvariantCulture);
            at++;
            var wide = max > 255;
            var samples = new int[width * height];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = wide ? (data[at] << 8) | data[at + 1] : data[at];
                at += wide ? 2 : 1;
            }

            planes.Add(new Jpeg2000TestPlane(width, height, max, samples));
        }

        return planes;
    }

    /// <summary>Null when every decoded plane matches its reference within the tolerance; otherwise what differs.</summary>
    public static string? Mismatch(IReadOnlyList<Jpeg2000TestPlane> decoded, IReadOnlyList<Jpeg2000TestPlane> expected, int tolerance)
    {
        if (decoded.Count != expected.Count)
        {
            return $"{decoded.Count} planes decoded, {expected.Count} expected";
        }

        var problems = new List<string>();
        for (var p = 0; p < expected.Count; p++)
        {
            var (a, e) = (decoded[p], expected[p]);
            if ((a.Width, a.Height, a.Max) != (e.Width, e.Height, e.Max))
            {
                problems.Add($"plane {p} is {a.Width}x{a.Height} of depth {a.Max}, expected {e.Width}x{e.Height} of depth {e.Max}");
                continue;
            }

            var wrong = Enumerable.Range(0, e.Samples.Length).Where(i => Math.Abs(a.Samples[i] - e.Samples[i]) > tolerance).ToList();
            if (wrong.Count > 0)
            {
                var first = wrong[0];
                var worst = wrong.Max(i => Math.Abs(a.Samples[i] - e.Samples[i]));
                problems.Add($"plane {p}: {wrong.Count} samples differ by more than {tolerance} (up to {worst}; first at {first % e.Width},{first / e.Width}: {a.Samples[first]} for {e.Samples[first]})");
            }
        }

        return problems.Count == 0 ? null : string.Join("; ", problems);
    }

    /// <summary>A one-page PDF drawing the JPEG 2000 data as an image filling the page, with extra dictionary entries.</summary>
    public static byte[] Pdf(byte[] data, int width, int height, string entries = "")
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
        Write("%PDF-1.7\n");
        Object("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Object("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Object($"3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >> endobj\n");
        Object($"4 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");
        Object($"5 0 obj << /Type /XObject /Subtype /Image /Width {width} /Height {height} {entries} /Filter /JPXDecode /Length {data.Length} >> stream\n");
        output.Write(data);
        Write("\nendstream endobj\n");
        var xref = output.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:D10} 00000 n \n");
        }

        Write($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    /// <summary>The stress image Large/large-4096x3072.jp2 encodes losslessly: gradients, a coarse checker and rings.</summary>
    public static byte[] Pattern(int width, int height)
    {
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = ((y * width) + x) * 3;
                rgb[i] = (byte)((x * 255 / (width - 1)) ^ ((((x >> 6) + (y >> 6)) & 1) * 16));
                rgb[i + 1] = (byte)((x + (2 * y)) >> 4);
                rgb[i + 2] = (byte)((((long)x * x) + ((long)y * y)) >> 13);
            }
        }

        return rgb;
    }

    /// <summary>
    /// The features a sample uses, read from its bytes: JP2 boxes and colour specifications, the main and tile-part
    /// header markers with the fields that select a feature, and SOP and EPH markers in the packet data.
    /// </summary>
    public static HashSet<string> Features(byte[] data)
    {
        var features = new HashSet<string>();
        var start = 0;
        if (data.Length > 12 && data[4] == 'j' && data[5] == 'P')
        {
            start = Boxes(data, 0, data.Length, features);
        }
        else
        {
            features.Add("codestream");
        }

        Codestream(data, start, features);
        return features;
    }

    // The boxes of a JP2 file (the header box entered); returns where the codestream starts.
    private static int Boxes(byte[] data, int at, int end, HashSet<string> features)
    {
        var codestream = -1;
        while (at + 8 <= end)
        {
            var length = BigEndian(data, at);
            var type = Encoding.ASCII.GetString(data, at + 4, 4);
            var boxEnd = length == 0 ? end : at + length;
            features.Add("box:" + type);
            if (type == "jp2h")
            {
                Boxes(data, at + 8, boxEnd, features);
            }
            else if (type == "colr")
            {
                features.Add(data[at + 8] == 1 ? "colr:" + BigEndian(data, at + 11) : "colr:icc");
            }
            else if (type == "jp2c" && codestream < 0)
            {
                codestream = at + 8;
            }

            at = boxEnd;
        }

        return codestream;
    }

    private static void Codestream(byte[] data, int at, HashSet<string> features)
    {
        at += 2;
        while (Marker(data, at) != 0xFF90)
        {
            Segment(data, at, features, "main");
            at += 2 + Short(data, at + 2);
        }

        var tiles = new Dictionary<int, int>();
        while (at + 12 <= data.Length && Marker(data, at) == 0xFF90)
        {
            var tile = Short(data, at + 4);
            tiles[tile] = tiles.GetValueOrDefault(tile) + 1;
            var length = BigEndian(data, at + 6);
            var end = length == 0 ? data.Length : at + length;
            var p = at + 12;
            while (Marker(data, p) != 0xFF93)
            {
                Segment(data, p, features, "tile");
                p += 2 + Short(data, p + 2);
            }

            for (var i = p + 2; i + 1 < end; i++)
            {
                if (data[i] == 0xFF && data[i + 1] is 0x91 or 0x92)
                {
                    features.Add(data[i + 1] == 0x91 ? "SOP" : "EPH");
                }
            }

            at = end;
        }

        features.Add(tiles.Count > 1 ? "tiles" : "one-tile");
        if (tiles.Values.Any(parts => parts > 1))
        {
            features.Add("tile-parts");
        }
    }

    private static void Segment(byte[] data, int at, HashSet<string> features, string where)
    {
        var name = Marker(data, at) switch
        {
            0xFF51 => Size(data, at, features),
            0xFF52 => CodingStyle(data, at, features),
            0xFF53 => "COC",
            0xFF5C or 0xFF5D => Quantization(data, at, features),
            0xFF5E => "RGN",
            0xFF5F => "POC",
            0xFF60 => "PPM",
            0xFF61 => "PPT",
            0xFF55 => "TLM",
            0xFF58 => "PLT",
            0xFF64 => "COM",
            var other => $"{other:X4}",
        };
        features.Add(name);
        features.Add(name + ":" + where);
    }

    private static string Size(byte[] data, int at, HashSet<string> features)
    {
        if (BigEndian(data, at + 14) > 0 || BigEndian(data, at + 18) > 0)
        {
            features.Add("origin");
        }

        var count = Short(data, at + 38);
        features.Add("components:" + count);
        for (var c = 0; c < count; c++)
        {
            var bits = data[at + 40 + (3 * c)];
            features.Add("depth:" + ((bits & 0x7F) + 1));
            features.Add($"subsampling:{data[at + 41 + (3 * c)]}x{data[at + 42 + (3 * c)]}");
            if ((bits & 0x80) != 0)
            {
                features.Add("signed");
            }
        }

        return "SIZ";
    }

    private static string CodingStyle(byte[] data, int at, HashSet<string> features)
    {
        if ((data[at + 4] & 1) != 0)
        {
            features.Add("precincts");
        }

        features.Add("order:" + data[at + 5]);
        features.Add(Short(data, at + 6) > 1 ? "layers" : "one-layer");
        features.Add(data[at + 13] == 1 ? "wavelet:5-3" : "wavelet:9-7");
        if (data[at + 8] != 0)
        {
            features.Add(data[at + 13] == 1 ? "RCT" : "ICT");
        }

        for (var bit = 0; bit < 6; bit++)
        {
            if ((data[at + 12] & (1 << bit)) != 0)
            {
                features.Add("block-style:" + (1 << bit));
            }
        }

        return "COD";
    }

    private static string Quantization(byte[] data, int at, HashSet<string> features)
    {
        var offset = Marker(data, at) == 0xFF5D ? 5 : 4;
        features.Add("quantization:" + (data[at + offset] & 0x1F));
        return Marker(data, at) == 0xFF5D ? "QCC" : "QCD";
    }

    private static int Marker(byte[] data, int at) => Short(data, at);

    private static int Short(byte[] data, int at) => (data[at] << 8) | data[at + 1];

    private static int BigEndian(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

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
}
