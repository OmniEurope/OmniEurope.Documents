// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging;

/// <summary>
/// JPEG decoding: baseline, extended and progressive Huffman-coded frames, 8-bit precision, any sampling
/// factors, restart intervals; greyscale, YCbCr, RGB, CMYK and YCCK (Adobe) colour. Lossless, hierarchical
/// and arithmetic-coded JPEG are not supported and throw <see cref="NotSupportedException"/>.
/// </summary>
public static class JpegDecoder
{
    /// <summary>True when <paramref name="data"/> starts with a JPEG start-of-image marker.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> data) => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    /// <summary>Decodes a JPEG.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid JPEG.</exception>
    public static RasterImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsJpeg(data))
        {
            throw new InvalidDataException("Not a JPEG file.");
        }

        try
        {
            return Read(data);
        }
        catch (Exception exception) when (DamagedData.IsOverrun(exception))
        {
            throw DamagedData.Error("JPEG", exception);
        }
    }

    private static RasterImage Read(byte[] data)
    {
        var state = new JpegState();
        var position = 2;
        while (position + 4 <= data.Length)
        {
            var marker = data[position + 1];
            if (data[position] != 0xFF || IsStandalone(marker))
            {
                position++;
                continue;
            }

            if (marker == 0xD9)
            {
                break;
            }

            var segment = Segment(data, position);
            position += 4 + segment.Length;
            if (marker == 0xDA)
            {
                position = state.ReadScan(segment, data, position);
            }
            else
            {
                state.ReadSegment(marker, segment);
            }
        }

        return state.Output();
    }

    // Fill bytes, stuffed zeros, TEM, SOI and restart markers carry no length.
    private static bool IsStandalone(byte marker) => marker is 0xFF or 0x00 or 0x01 or (>= 0xD0 and <= 0xD8);

    // SOF0 to SOF15, without DHT (C4), JPG (C8) and DAC (CC), which share the range.
    private static bool IsFrameMarker(byte marker) => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    // The body of the segment whose marker is at position.
    private static ReadOnlySpan<byte> Segment(byte[] data, int position)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2));
        if (length < 2 || position + 2 + length > data.Length)
        {
            throw new InvalidDataException("Truncated JPEG segment.");
        }

        return data.AsSpan(position + 4, length - 2);
    }
    /// <summary>Reads the size, component count and resolution without decoding the pixels.</summary>
    public static (int Width, int Height, int Components) ReadHeader(ReadOnlySpan<byte> data)
    {
        var position = 2;
        while (position + 9 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }

            var marker = data[position + 1];
            if (IsFrameMarker(marker))
            {
                return (BinaryPrimitives.ReadUInt16BigEndian(data[(position + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]), data[position + 9]);
            }

            position += IsStandalone(marker) ? 1 : 2 + BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
        }

        throw new InvalidDataException("No JPEG frame header found.");
    }
}

/// <summary>Tables and frame collected while walking the markers.</summary>
internal sealed class JpegState
{
    private readonly ushort[][] _quant = new ushort[4][];
    private readonly JpegHuffmanTable?[] _dc = new JpegHuffmanTable?[4];
    private readonly JpegHuffmanTable?[] _ac = new JpegHuffmanTable?[4];
    private JpegFrame? _frame;
    private int _restartInterval;
    private int _adobeTransform = -1;
    private double _dpiX;
    private double _dpiY;

    public void ReadSegment(byte marker, ReadOnlySpan<byte> segment)
    {
        switch (marker)
        {
            case 0xDB:
                ReadQuantTables(segment);
                break;
            case 0xC4:
                ReadHuffmanTables(segment);
                break;
            case 0xDD:
                _restartInterval = BinaryPrimitives.ReadUInt16BigEndian(segment);
                break;
            case 0xE0 when segment.Length >= 12 && segment.StartsWith("JFIF\0"u8):
                ReadDensity(segment);
                break;
            case 0xEE when segment.Length >= 12 && segment.StartsWith("Adobe"u8):
                _adobeTransform = segment[11];
                break;
            case 0xC0 or 0xC1 or 0xC2:
                ReadFrame(segment, progressive: marker == 0xC2);
                break;
            case >= 0xC3 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC):
                throw new NotSupportedException("Lossless, hierarchical and arithmetic-coded JPEG are not supported.");
        }
    }

    public int ReadScan(ReadOnlySpan<byte> header, byte[] data, int position)
    {
        var frame = _frame ?? throw new InvalidDataException("JPEG scan before the frame header.");
        var count = header[0];
        var components = new List<JpegComponent>(count);
        for (var i = 0; i < count; i++)
        {
            var id = header[1 + (i * 2)];
            var component = frame.Components.Find(c => c.Id == id) ?? throw new InvalidDataException("JPEG scan names an unknown component.");
            var tables = header[2 + (i * 2)];
            component.DcTable = tables >> 4;
            component.AcTable = tables & 15;
            components.Add(component);
        }

        var rest = header[(1 + (count * 2))..];
        var decoder = new JpegScanDecoder(frame, components, _dc, _ac, rest[0], rest[1], rest[2] >> 4, rest[2] & 15, _restartInterval);
        return decoder.Decode(data, position);
    }

    public RasterImage Output()
    {
        var frame = _frame ?? throw new InvalidDataException("JPEG without a frame header.");
        var planes = frame.Components.Select(c => JpegIdct.Plane(c, _quant[c.QuantTable] ?? throw new InvalidDataException("JPEG uses an undefined quantisation table."))).ToList();
        var image = JpegColor.Convert(frame, planes, _adobeTransform);
        image.DpiX = _dpiX;
        image.DpiY = _dpiY;
        return image;
    }

    private void ReadQuantTables(ReadOnlySpan<byte> segment)
    {
        while (segment.Length > 0)
        {
            var precision = segment[0] >> 4;
            var id = segment[0] & 3;
            var table = new ushort[64];
            for (var k = 0; k < 64; k++)
            {
                table[JpegFrame.ZigZag[k]] = precision == 0 ? segment[1 + k] : BinaryPrimitives.ReadUInt16BigEndian(segment[(1 + (2 * k))..]);
            }

            _quant[id] = table;
            segment = segment[(1 + (precision == 0 ? 64 : 128))..];
        }
    }

    private void ReadHuffmanTables(ReadOnlySpan<byte> segment)
    {
        while (segment.Length >= 17)
        {
            var tableClass = segment[0] >> 4;
            var id = segment[0] & 3;
            var counts = segment.Slice(1, 16);
            var total = 0;
            foreach (var c in counts)
            {
                total += c;
            }

            var table = new JpegHuffmanTable(counts, segment.Slice(17, total).ToArray());
            (tableClass == 0 ? _dc : _ac)[id] = table;
            segment = segment[(17 + total)..];
        }
    }

    private void ReadFrame(ReadOnlySpan<byte> segment, bool progressive)
    {
        if (segment[0] != 8)
        {
            throw new NotSupportedException("Only 8-bit JPEG precision is supported.");
        }

        var height = BinaryPrimitives.ReadUInt16BigEndian(segment[1..]);
        var width = BinaryPrimitives.ReadUInt16BigEndian(segment[3..]);
        var count = segment[5];
        if (width == 0 || height == 0 || count is 0 or 2 or > 4 || (long)width * height > RasterImage.MaxPixels)
        {
            throw new InvalidDataException("Unsupported JPEG frame.");
        }

        var components = new List<JpegComponent>(count);
        for (var i = 0; i < count; i++)
        {
            components.Add(ReadComponent(segment.Slice(6 + (i * 3), 3)));
        }

        _frame = new JpegFrame(width, height, progressive, components);
    }

    // A frame component: identifier, horizontal and vertical sampling factors (1 to 4), quantisation table.
    private static JpegComponent ReadComponent(ReadOnlySpan<byte> c)
    {
        var h = c[1] >> 4;
        var v = c[1] & 15;
        return h is >= 1 and <= 4 && v is >= 1 and <= 4
            ? new JpegComponent(c[0], h, v, c[2] & 3)
            : throw new InvalidDataException("Invalid JPEG sampling factors.");
    }
    private void ReadDensity(ReadOnlySpan<byte> segment)
    {
        var unit = segment[7];
        var x = BinaryPrimitives.ReadUInt16BigEndian(segment[8..]);
        var y = BinaryPrimitives.ReadUInt16BigEndian(segment[10..]);
        var factor = unit switch { 1 => 1d, 2 => 2.54, _ => 0d };
        _dpiX = x * factor;
        _dpiY = y * factor;
    }
}
