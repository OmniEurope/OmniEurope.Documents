// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging;

/// <summary>
/// Baseline JPEG encoding: greyscale or YCbCr with optional 2x2 chroma subsampling, quality-scaled
/// quantisation, and Huffman tables computed for each image (two passes), so no fixed table is needed.
/// Alpha is dropped; CMYK is converted to RGB first.
/// </summary>
public static class JpegEncoder
{
    // The example quantisation tables of the JPEG standard (natural order), scaled by quality.
    private static readonly byte[] LumaBase =
    [
        16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62, 18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99,
    ];

    private static readonly byte[] ChromaBase = BuildChromaBase();

    private static byte[] BuildChromaBase()
    {
        var table = new byte[64];
        Array.Fill(table, (byte)99);
        byte[] top = [17, 18, 24, 47, 18, 21, 26, 66, 24, 26, 56, 99, 47, 66, 99, 99];
        for (var i = 0; i < 16; i++)
        {
            table[((i / 4) * 8) + (i % 4)] = top[i];
        }

        return table;
    }

    /// <summary>Encodes <paramref name="image"/>.</summary>
    /// <param name="image">The image.</param>
    /// <param name="quality">1 (smallest) to 100 (best).</param>
    /// <param name="subsampleChroma">True to halve the chroma resolution both ways (4:2:0).</param>
    public static byte[] Encode(RasterImage image, int quality = 85, bool subsampleChroma = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        if (image.Width > 65535 || image.Height > 65535)
        {
            throw new ArgumentException("JPEG images are limited to 65535 pixels per side.", nameof(image));
        }

        var gray = image.ColorType is ImageColorType.Gray or ImageColorType.GrayAlpha;
        var quant = new[] { Scale(LumaBase, quality), Scale(ChromaBase, quality) };
        EncoderComponent[] components = gray
            ? [new EncoderComponent(1, 1, 1, 0, JpegPlanes.Luma(image))]
            : JpegPlanes.YCbCr(image, subsampleChroma);
        var blocks = components.Select(c => JpegForward.Quantize(c, quant[c.Table], components[0].H, components[0].V)).ToList();
        var tables = JpegHuffmanBuilder.Build(components, blocks);
        using var output = new MemoryStream();
        JpegMarkers.WriteHeaders(output, image, components, quant, tables);
        JpegEntropy.Write(output, components, blocks, tables);
        output.Write([0xFF, 0xD9]);
        return output.ToArray();
    }

    private static byte[] Scale(byte[] table, int quality)
    {
        var factor = quality < 50 ? 5000 / quality : 200 - (quality * 2);
        var scaled = new byte[64];
        for (var i = 0; i < 64; i++)
        {
            scaled[i] = (byte)Math.Clamp(((table[i] * factor) + 50) / 100, 1, 255);
        }

        return scaled;
    }
}

/// <summary>A component being encoded: its sampling, quantisation table index and padded sample plane.</summary>
internal sealed record EncoderComponent(int Id, int H, int V, int Table, JpegSamplePlane Plane);

/// <summary>Samples of one component (unpadded).</summary>
internal sealed record JpegSamplePlane(int Width, int Height, float[] Samples)
{
    public float At(int x, int y) => Samples[(Math.Min(y, Height - 1) * Width) + Math.Min(x, Width - 1)];
}

/// <summary>Colour conversion and chroma subsampling for encoding.</summary>
internal static class JpegPlanes
{
    public static JpegSamplePlane Luma(RasterImage image)
    {
        var samples = new float[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var (r, g, b, _) = image.GetRgba(x, y);
                samples[(y * image.Width) + x] = (0.299f * r) + (0.587f * g) + (0.114f * b);
            }
        }

        return new JpegSamplePlane(image.Width, image.Height, samples);
    }

    public static EncoderComponent[] YCbCr(RasterImage image, bool subsample)
    {
        var size = image.Width * image.Height;
        var y = new float[size];
        var cb = new float[size];
        var cr = new float[size];
        for (var row = 0; row < image.Height; row++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var (r, g, b, _) = image.GetRgba(x, row);
                var i = (row * image.Width) + x;
                y[i] = (0.299f * r) + (0.587f * g) + (0.114f * b);
                cb[i] = 128 - (0.168736f * r) - (0.331264f * g) + (0.5f * b);
                cr[i] = 128 + (0.5f * r) - (0.418688f * g) - (0.081312f * b);
            }
        }

        var full = new JpegSamplePlane(image.Width, image.Height, y);
        var blue = new JpegSamplePlane(image.Width, image.Height, cb);
        var red = new JpegSamplePlane(image.Width, image.Height, cr);
        return subsample
            ? [new EncoderComponent(1, 2, 2, 0, full), new EncoderComponent(2, 1, 1, 1, Half(blue)), new EncoderComponent(3, 1, 1, 1, Half(red))]
            : [new EncoderComponent(1, 1, 1, 0, full), new EncoderComponent(2, 1, 1, 1, blue), new EncoderComponent(3, 1, 1, 1, red)];
    }

    private static JpegSamplePlane Half(JpegSamplePlane plane)
    {
        var width = (plane.Width + 1) / 2;
        var height = (plane.Height + 1) / 2;
        var samples = new float[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                samples[(y * width) + x] = (plane.At(2 * x, 2 * y) + plane.At((2 * x) + 1, 2 * y) + plane.At(2 * x, (2 * y) + 1) + plane.At((2 * x) + 1, (2 * y) + 1)) / 4;
            }
        }

        return new JpegSamplePlane(width, height, samples);
    }
}

/// <summary>Forward DCT and quantisation of every block of a component, in MCU order.</summary>
internal static class JpegForward
{
    private static readonly float[] Basis = BuildBasis();

    private static float[] BuildBasis()
    {
        var basis = new float[64];
        for (var u = 0; u < 8; u++)
        {
            var scale = u == 0 ? 1 / Math.Sqrt(2) : 1;
            for (var x = 0; x < 8; x++)
            {
                basis[(u * 8) + x] = (float)(scale / 2 * Math.Cos(((2 * x) + 1) * u * Math.PI / 16));
            }
        }

        return basis;
    }

    /// <summary>Quantised coefficients (natural order) of the component's blocks: block rows of
    /// <c>blocksPerLine</c>, padded to whole MCUs of the frame.</summary>
    public static short[][] Quantize(EncoderComponent component, byte[] quant, int maxH, int maxV)
    {
        var plane = component.Plane;
        var mcuWidth = 8 * maxH;
        var mcuHeight = 8 * maxV;
        var frameWidth = plane.Width * maxH / component.H;
        var frameHeight = plane.Height * maxV / component.V;
        var mcusX = (frameWidth + mcuWidth - 1) / mcuWidth;
        var mcusY = (frameHeight + mcuHeight - 1) / mcuHeight;
        var blocksX = mcusX * component.H;
        var blocksY = mcusY * component.V;
        var result = new short[blocksX * blocksY][];
        Span<float> block = stackalloc float[64];
        Span<float> temp = stackalloc float[64];
        for (var by = 0; by < blocksY; by++)
        {
            for (var bx = 0; bx < blocksX; bx++)
            {
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        block[(y * 8) + x] = plane.At((bx * 8) + x, (by * 8) + y) - 128;
                    }
                }

                Forward(block, temp);
                var coefficients = new short[64];
                for (var i = 0; i < 64; i++)
                {
                    coefficients[i] = (short)MathF.Round(block[i] / quant[i]);
                }

                result[(by * blocksX) + bx] = coefficients;
            }
        }

        return result;
    }

    private static void Forward(Span<float> block, Span<float> temp)
    {
        for (var y = 0; y < 8; y++)
        {
            for (var u = 0; u < 8; u++)
            {
                float sum = 0;
                for (var x = 0; x < 8; x++)
                {
                    sum += Basis[(u * 8) + x] * block[(y * 8) + x];
                }

                temp[(y * 8) + u] = sum;
            }
        }

        for (var u = 0; u < 8; u++)
        {
            for (var v = 0; v < 8; v++)
            {
                float sum = 0;
                for (var y = 0; y < 8; y++)
                {
                    sum += Basis[(v * 8) + y] * temp[(y * 8) + u];
                }

                block[(v * 8) + u] = sum;
            }
        }
    }
}

/// <summary>Marker segments of the encoded file.</summary>
internal static class JpegMarkers
{
    public static void WriteHeaders(Stream output, RasterImage image, EncoderComponent[] components, byte[][] quant, JpegHuffmanTables tables)
    {
        output.Write([0xFF, 0xD8]);
        var dpiX = (ushort)Math.Clamp(Math.Round(image.DpiX), 0, 65535);
        var dpiY = (ushort)Math.Clamp(Math.Round(image.DpiY), 0, 65535);
        Segment(output, 0xE0, w =>
        {
            w.Write("JFIF\0"u8);
            w.Write([1, 1, (byte)(dpiX > 0 ? 1 : 0)]);
            Word(w, dpiX > 0 ? dpiX : (ushort)1);
            Word(w, dpiY > 0 ? dpiY : (ushort)1);
            w.Write([0, 0]);
        });
        var used = components.Length == 1 ? 1 : 2;
        Segment(output, 0xDB, w =>
        {
            for (var t = 0; t < used; t++)
            {
                w.WriteByte((byte)t);
                for (var k = 0; k < 64; k++)
                {
                    w.WriteByte(quant[t][JpegFrame.ZigZag[k]]);
                }
            }
        });
        Segment(output, 0xC0, w =>
        {
            w.WriteByte(8);
            Word(w, (ushort)image.Height);
            Word(w, (ushort)image.Width);
            w.WriteByte((byte)components.Length);
            foreach (var c in components)
            {
                w.Write([(byte)c.Id, (byte)((c.H << 4) | c.V), (byte)c.Table]);
            }
        });
        Segment(output, 0xC4, w => tables.WriteDefinitions(w, used));
        Segment(output, 0xDA, w =>
        {
            w.WriteByte((byte)components.Length);
            foreach (var c in components)
            {
                w.Write([(byte)c.Id, (byte)((c.Table << 4) | c.Table)]);
            }

            w.Write([0, 63, 0]);
        });
    }

    private static void Segment(Stream output, byte marker, Action<Stream> write)
    {
        using var body = new MemoryStream();
        write(body);
        output.Write([0xFF, marker]);
        Word(output, (ushort)(body.Length + 2));
        body.WriteTo(output);
    }

    private static void Word(Stream output, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        output.Write(bytes);
    }
}
