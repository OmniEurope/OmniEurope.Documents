// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>A JBIG2 segment (T.88 7.2): its number, type, the segments it refers to and its data range.</summary>
internal sealed record Jbig2Segment(int Number, int Type, int[] ReferredTo, int Page, int DataStart, int DataEnd);

/// <summary>Where and how a region segment's bitmap goes on the page (T.88 7.4.1).</summary>
internal readonly record struct Jbig2RegionInfo(int Width, int Height, int X, int Y, Jbig2Combination Combination)
{
    /// <summary>The size of the field.</summary>
    public const int Size = 17;

    public static Jbig2RegionInfo Parse(byte[] data, int at) => new(
        Jbig2Bytes.Int32(data, at),
        Jbig2Bytes.Int32(data, at + 4),
        Jbig2Bytes.Int32(data, at + 8),
        Jbig2Bytes.Int32(data, at + 12),
        (Jbig2Combination)Math.Min(Jbig2Bytes.Byte(data, at + 16) & 7, 4));
}

/// <summary>Reads the segments of embedded JBIG2 data (T.88 annex D.3, the organisation PDF uses).</summary>
internal static class Jbig2Segments
{
    /// <summary>The segment types (T.88 7.3).</summary>
    public const int SymbolDictionary = 0;
    public const int IntermediateText = 4;
    public const int ImmediateText = 6;
    public const int ImmediateLosslessText = 7;
    public const int PatternDictionary = 16;
    public const int IntermediateHalftone = 20;
    public const int ImmediateHalftone = 22;
    public const int ImmediateLosslessHalftone = 23;
    public const int IntermediateGeneric = 36;
    public const int ImmediateGeneric = 38;
    public const int ImmediateLosslessGeneric = 39;
    public const int IntermediateRefinement = 40;
    public const int ImmediateRefinement = 42;
    public const int ImmediateLosslessRefinement = 43;
    public const int PageInformation = 48;
    public const int EndOfPage = 49;
    public const int EndOfStripe = 50;
    public const int EndOfFile = 51;
    public const int Tables = 53;

    /// <summary>Reads every segment of <paramref name="data"/>, headers each followed by their data.</summary>
    public static List<Jbig2Segment> Read(byte[] data)
    {
        var segments = new List<Jbig2Segment>();
        var at = 0;
        while (at + 11 <= data.Length)
        {
            var segment = ReadHeader(data, ref at);
            segments.Add(segment);
            at = segment.DataEnd;
            if (segment.Type is EndOfFile)
            {
                break;
            }
        }

        return segments;
    }

    private static Jbig2Segment ReadHeader(byte[] data, ref int at)
    {
        var number = Jbig2Bytes.Int32(data, at);
        var flags = Jbig2Bytes.Byte(data, at + 4);
        at += 5;
        var referred = ReferredTo(data, ref at, number);
        var page = (flags & 0x40) != 0 ? Jbig2Bytes.Int32(data, at) : Jbig2Bytes.Byte(data, at);
        at += (flags & 0x40) != 0 ? 4 : 1;
        var length = Jbig2Bytes.Int32(data, at);
        at += 4;
        var type = flags & 0x3F;
        var end = length == -1 ? UnknownLengthEnd(data, at, type) : (long)at + (uint)length;
        if (end > data.Length)
        {
            end = data.Length;
        }

        return new Jbig2Segment(number, type, referred, page, at, (int)end);
    }

    // The referred-to segment count and retention flags, short or long form, then the referred-to numbers (7.2.4, 7.2.5).
    private static int[] ReferredTo(byte[] data, ref int at, int number)
    {
        var first = Jbig2Bytes.Byte(data, at);
        var count = first >> 5;
        if (count == 7)
        {
            count = Jbig2Bytes.Int32(data, at) & 0x1FFFFFFF;
            if (count > data.Length)
            {
                throw new InvalidDataException("A JBIG2 segment refers to more segments than the data holds.");
            }

            at += 4 + ((count + 8) / 8);
        }
        else
        {
            at++;
        }

        var size = (uint)number <= 256 ? 1 : (uint)number <= 65536 ? 2 : 4;
        var referred = new int[count];
        for (var i = 0; i < count; i++)
        {
            referred[i] = size == 1 ? Jbig2Bytes.Byte(data, at) : size == 2 ? Jbig2Bytes.UInt16(data, at) : Jbig2Bytes.Int32(data, at);
            at += size;
        }

        return referred;
    }

    // An immediate generic region of unknown length ends with a marker and its row count (7.2.7).
    private static long UnknownLengthEnd(byte[] data, int start, int type)
    {
        if (type is not (ImmediateGeneric or ImmediateLosslessGeneric) || start + Jbig2RegionInfo.Size >= data.Length)
        {
            throw new InvalidDataException("A JBIG2 segment of unknown length is not an immediate generic region.");
        }

        var mmr = (data[start + Jbig2RegionInfo.Size] & 1) != 0;
        var (first, second) = mmr ? ((byte)0x00, (byte)0x00) : ((byte)0xFF, (byte)0xAC);
        for (var i = start + Jbig2RegionInfo.Size + 1; i + 5 < data.Length; i++)
        {
            if (data[i] == first && data[i + 1] == second)
            {
                return i + 6;
            }
        }

        throw new InvalidDataException("A JBIG2 generic region of unknown length has no end marker.");
    }

    /// <summary>Reads <paramref name="count"/> adaptive pixels as signed byte pairs.</summary>
    public static (int X, int Y)[] AdaptivePixels(byte[] data, int at, int count)
    {
        var pixels = new (int X, int Y)[count];
        for (var i = 0; i < count; i++)
        {
            pixels[i] = ((sbyte)Jbig2Bytes.Byte(data, at + (2 * i)), (sbyte)Jbig2Bytes.Byte(data, at + (2 * i) + 1));
        }

        return pixels;
    }
}
