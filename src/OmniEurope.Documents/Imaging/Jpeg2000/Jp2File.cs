// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>The colour space a JPEG 2000 image declares for its colour channels.</summary>
internal enum Jpeg2000ColorSpace
{
    /// <summary>No declaration (a bare codestream): inferred from the channel count.</summary>
    Unknown,

    /// <summary>Greyscale.</summary>
    Gray,

    /// <summary>sRGB.</summary>
    Rgb,

    /// <summary>sYCC: luma and two chroma channels, converted to RGB.</summary>
    Ycc,

    /// <summary>CMYK.</summary>
    Cmyk,

    /// <summary>CIE L*a*b*.</summary>
    Lab,

    /// <summary>An ICC profile, its channel count given by the profile.</summary>
    Icc,
}

/// <summary>What a channel of the image is (JP2 channel definition box).</summary>
internal enum Jpeg2000ChannelType
{
    /// <summary>A colour channel.</summary>
    Color,

    /// <summary>Opacity.</summary>
    Opacity,

    /// <summary>Opacity the colour channels are already multiplied by.</summary>
    PremultipliedOpacity,

    /// <summary>Anything else: ignored.</summary>
    Other,
}

/// <summary>One output channel of a JP2 image: a codestream component, directly or through a palette column.</summary>
internal sealed record Jp2Channel(int Component, int PaletteColumn);

/// <summary>A palette (JP2 pclr box): entries by column, with each column's depth and signedness.</summary>
internal sealed record Jp2Palette(int[][] Columns, int[] Depths, bool[] Signed);

/// <summary>A channel definition (JP2 cdef box): its type and the colour it carries (1-based, 0 for the whole image).</summary>
internal sealed record Jp2ChannelDefinition(int Channel, Jpeg2000ChannelType Type, int Association);

/// <summary>The image header boxes of a JP2 (or JPX) file and where its codestream lies.</summary>
internal sealed class Jp2Header
{
    public Jpeg2000ColorSpace ColorSpace { get; set; }

    /// <summary>The CIE Lab ranges and offsets (RL, OL, RA, OA, RB, OB) when the colour specification gives them.</summary>
    public uint[]? LabParameters { get; set; }

    /// <summary>True when the CIE Lab illuminant is D65 rather than the default D50.</summary>
    public bool LabD65 { get; set; }

    public byte[]? IccProfile { get; set; }

    public Jp2Palette? Palette { get; set; }

    public List<Jp2Channel>? Channels { get; set; }

    public List<Jp2ChannelDefinition> Definitions { get; } = [];

    public int CodestreamStart { get; set; } = -1;

    public int CodestreamEnd { get; set; }
}

/// <summary>
/// Reads the box structure of a JP2 file (ISO/IEC 15444-1 annex I, and the JPX extension of 15444-2 as far as a
/// PDF image needs it): the colour specification, palette, component mapping and channel definition of the header
/// box, and the contiguous codestream box.
/// </summary>
internal static class Jp2File
{
    private const uint Signature = 0x6A502020;
    private const uint HeaderBox = 0x6A703268;
    private const uint CodestreamBox = 0x6A703263;
    private const uint ColorBox = 0x636F6C72;
    private const uint PaletteBox = 0x70636C72;
    private const uint MappingBox = 0x636D6170;
    private const uint DefinitionBox = 0x63646566;

    /// <summary>True when <paramref name="data"/> starts with the JP2 signature box.</summary>
    public static bool IsJp2(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && BinaryPrimitives.ReadUInt32BigEndian(data) == 12 && BinaryPrimitives.ReadUInt32BigEndian(data[4..]) == Signature;

    /// <summary>The header of a JP2 file and its first codestream.</summary>
    public static Jp2Header Read(byte[] data)
    {
        var header = new Jp2Header();
        ReadBoxes(data, 0, data.Length, header);
        if (header.CodestreamStart < 0)
        {
            throw new InvalidDataException("The JPEG 2000 file holds no codestream.");
        }

        return header;
    }

    private static void ReadBoxes(byte[] data, int start, int end, Jp2Header header)
    {
        var at = start;
        while (at + 8 <= end && header.CodestreamStart < 0)
        {
            var (type, contentStart, boxEnd) = Box(data, at, end);
            switch (type)
            {
                case HeaderBox:
                    ReadBoxes(data, contentStart, boxEnd, header);
                    break;
                case CodestreamBox:
                    header.CodestreamStart = contentStart;
                    header.CodestreamEnd = boxEnd;
                    break;
                default:
                    ReadHeaderBox(data, type, contentStart, boxEnd, header);
                    break;
            }

            at = boxEnd;
        }
    }

    private static void ReadHeaderBox(byte[] data, uint type, int start, int end, Jp2Header header)
    {
        switch (type)
        {
            // The first colour specification wins; later ones are alternatives.
            case ColorBox when header.ColorSpace == Jpeg2000ColorSpace.Unknown:
                ReadColor(data, start, end, header);
                break;
            case PaletteBox:
                header.Palette = ReadPalette(data, start, end);
                break;
            case MappingBox:
                header.Channels = [.. Enumerable.Range(0, (end - start) / 4)
                    .Select(i => start + (4 * i))
                    .Select(i => new Jp2Channel(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i)), data[i + 2] == 1 ? data[i + 3] : -1))];
                break;
            case DefinitionBox:
                ReadDefinitions(data, start, header);
                break;
        }
    }

    // A box: its type, where its content starts and where it ends (a length of 0 runs to the end of its parent; a
    // codestream box cut short ends with the data, which then decodes as far as it goes).
    private static (uint Type, int ContentStart, int End) Box(byte[] data, int at, int end)
    {
        long length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
        var type = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at + 4));
        var content = at + 8;
        if (length == 1)
        {
            length = (long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at + 8));
            content += 8;
        }

        var boxEnd = length == 0 || (type == CodestreamBox && at + length > end) ? end : at + length;
        if (boxEnd < content || boxEnd > end)
        {
            throw new InvalidDataException("A JPEG 2000 box runs past its container.");
        }

        return (type, content, (int)boxEnd);
    }

    private static void ReadColor(byte[] data, int start, int end, Jp2Header header)
    {
        var method = data[start];
        if (method == 1)
        {
            header.ColorSpace = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 3)) switch
            {
                16 or 20 or 21 or 22 or 23 => Jpeg2000ColorSpace.Rgb,
                17 => Jpeg2000ColorSpace.Gray,
                18 or 24 => Jpeg2000ColorSpace.Ycc,
                12 => Jpeg2000ColorSpace.Cmyk,
                14 => Lab(data, start, end, header),
                _ => Jpeg2000ColorSpace.Unknown,
            };
        }
        else if (method is 2 or 3)
        {
            header.ColorSpace = Jpeg2000ColorSpace.Icc;
            header.IccProfile = data[(start + 3)..end];
        }
    }

    // The CIE Lab parameters following the enumerated colour space (ISO/IEC 15444-2 M.11.7.4): ranges and offsets,
    // then the illuminant.
    private static Jpeg2000ColorSpace Lab(byte[] data, int start, int end, Jp2Header header)
    {
        if (end - start >= 7 + 28)
        {
            header.LabParameters = [.. Enumerable.Range(0, 6).Select(i => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 7 + (4 * i))))];
            header.LabD65 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(start + 31)) == 0x00443635;
        }

        return Jpeg2000ColorSpace.Lab;
    }

    private static Jp2Palette ReadPalette(byte[] data, int start, int end)
    {
        var entries = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start));
        var columns = data[start + 2];
        var depths = new int[columns];
        var signed = new bool[columns];
        for (var c = 0; c < columns; c++)
        {
            depths[c] = (data[start + 3 + c] & 0x7F) + 1;
            signed[c] = (data[start + 3 + c] & 0x80) != 0;
        }

        var values = new int[columns][];
        for (var c = 0; c < columns; c++)
        {
            values[c] = new int[entries];
        }

        var at = start + 3 + columns;
        for (var e = 0; e < entries; e++)
        {
            for (var c = 0; c < columns; c++)
            {
                var size = (depths[c] + 7) / 8;
                values[c][e] = Entry(data, at, size, end, depths[c], signed[c]);
                at += size;
            }
        }

        return new Jp2Palette(values, depths, signed);
    }

    private static int Entry(byte[] data, int at, int size, int end, int depth, bool signed)
    {
        if (at + size > end)
        {
            throw new InvalidDataException("A JPEG 2000 palette runs past its box.");
        }

        long value = 0;
        for (var i = 0; i < size; i++)
        {
            value = (value << 8) | data[at + i];
        }

        value &= (1L << depth) - 1;
        return (int)(signed && value >= 1L << (depth - 1) ? value - (1L << depth) : value);
    }

    private static void ReadDefinitions(byte[] data, int start, Jp2Header header)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start));
        for (var i = 0; i < count; i++)
        {
            var at = start + 2 + (6 * i);
            var type = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 2)) switch
            {
                0 => Jpeg2000ChannelType.Color,
                1 => Jpeg2000ChannelType.Opacity,
                2 => Jpeg2000ChannelType.PremultipliedOpacity,
                _ => Jpeg2000ChannelType.Other,
            };
            header.Definitions.Add(new Jp2ChannelDefinition(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at)), type,
                BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 4))));
        }
    }
}
