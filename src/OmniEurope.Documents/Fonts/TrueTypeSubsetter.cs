// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Fonts;

/// <summary>The result of subsetting: the new font file and where each kept glyph went.</summary>
/// <param name="Data">The subset font file.</param>
/// <param name="GlyphMap">Original glyph index to new glyph index.</param>
public sealed record FontSubset(byte[] Data, IReadOnlyDictionary<int, int> GlyphMap);

/// <summary>
/// Builds a TrueType font holding only the requested glyphs (plus .notdef and the components of composite
/// glyphs), renumbered densely. Kept tables: head, hhea, maxp, hmtx, loca, glyf, cvt, fpgm, prep, OS/2,
/// name, and post (rewritten without glyph names). The character map is dropped: PDF embedding maps
/// characters itself.
/// </summary>
public static class TrueTypeSubsetter
{
    private static readonly string[] CopiedTables = ["cvt ", "fpgm", "prep", "OS/2", "name"];

    /// <summary>Subsets <paramref name="font"/> to <paramref name="glyphs"/>.</summary>
    /// <exception cref="NotSupportedException">The font has PostScript outlines or forbids embedding.</exception>
    public static FontSubset Subset(TrueTypeFont font, IEnumerable<int> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        var outlines = font.Glyphs ?? throw new NotSupportedException("Only TrueType-outline fonts can be subset.");
        if (!font.EmbeddingAllowed)
        {
            throw new NotSupportedException($"The licence of '{font.Names.FullName}' forbids embedding.");
        }

        var kept = Close(outlines, glyphs.Where(g => g >= 0 && g < font.GlyphCount).Append(0));
        var order = kept.Order().ToList();
        var map = new Dictionary<int, int>();
        for (var i = 0; i < order.Count; i++)
        {
            map[order[i]] = i;
        }

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var (glyf, loca) = BuildGlyf(outlines, order, map);
        var longLoca = glyf.Length > 0x1FFFE;
        tables["glyf"] = glyf;
        tables["loca"] = longLoca ? loca.SelectMany(BigEndian32).ToArray() : loca.SelectMany(o => BigEndian16((ushort)(o / 2))).ToArray();
        tables["hmtx"] = order.SelectMany(g => BigEndian16((ushort)font.GetAdvanceWidth(g)).Concat(BigEndian16((ushort)LeftSideBearing(outlines, g)))).ToArray();
        tables["head"] = Patch(font, "head", t => BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(50), (short)(longLoca ? 1 : 0)));
        tables["hhea"] = Patch(font, "hhea", t => BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(34), (ushort)order.Count));
        tables["maxp"] = Patch(font, "maxp", t => BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(4), (ushort)order.Count));
        tables["post"] = Post(font);
        foreach (var tag in CopiedTables)
        {
            if (font.Table(tag) is { } table)
            {
                tables[tag] = font.Reader.Slice(table.Offset, table.Length).ToArray();
            }
        }

        return new FontSubset(FontFileWriter.Write(tables), map);
    }

    private static HashSet<int> Close(TrueTypeGlyphs outlines, IEnumerable<int> glyphs)
    {
        var kept = new HashSet<int>();
        var pending = new Stack<int>(glyphs);
        while (pending.Count > 0)
        {
            var glyph = pending.Pop();
            if (!kept.Add(glyph))
            {
                continue;
            }

            foreach (var component in outlines.Components(glyph))
            {
                pending.Push(component);
            }
        }

        return kept;
    }

    private static (byte[] Glyf, List<int> Loca) BuildGlyf(TrueTypeGlyphs outlines, List<int> order, Dictionary<int, int> map)
    {
        using var glyf = new MemoryStream();
        var loca = new List<int>(order.Count + 1);
        foreach (var glyph in order)
        {
            loca.Add((int)glyf.Length);
            var data = outlines.RawGlyph(glyph).ToArray();
            if (data.Length >= 10 && BinaryPrimitives.ReadInt16BigEndian(data) < 0)
            {
                RenumberComponents(data, map);
            }

            glyf.Write(data);
            while (glyf.Length % 4 != 0)
            {
                glyf.WriteByte(0);
            }
        }

        loca.Add((int)glyf.Length);
        return (glyf.ToArray(), loca);
    }

    private static void RenumberComponents(byte[] data, Dictionary<int, int> map)
    {
        var position = 10;
        while (position + 4 <= data.Length)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position));
            var component = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2));
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(position + 2), (ushort)map.GetValueOrDefault(component));
            position += TrueTypeGlyphs.ComponentSize(flags);
            if ((flags & 0x0020) == 0)
            {
                break;
            }
        }
    }

    private static short LeftSideBearing(TrueTypeGlyphs outlines, int glyph)
    {
        var data = outlines.RawGlyph(glyph);
        return data.Length >= 4 ? BinaryPrimitives.ReadInt16BigEndian(data[2..]) : (short)0;
    }

    private static byte[] Patch(TrueTypeFont font, string tag, Action<byte[]> change)
    {
        var table = font.Table(tag) ?? throw new InvalidDataException($"The font has no '{tag}' table.");
        var copy = font.Reader.Slice(table.Offset, table.Length).ToArray();
        change(copy);
        return copy;
    }

    // post version 3: the metrics of the original without glyph names.
    private static byte[] Post(TrueTypeFont font)
    {
        var post = new byte[32];
        BinaryPrimitives.WriteUInt32BigEndian(post, 0x00030000);
        if (font.Table("post") is { Length: >= 32 } table)
        {
            font.Reader.Slice(table.Offset + 4, 28).CopyTo(post.AsSpan(4));
        }

        return post;
    }

    private static IEnumerable<byte> BigEndian16(ushort value) => [(byte)(value >> 8), (byte)value];

    private static IEnumerable<byte> BigEndian32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}

/// <summary>Writes an sfnt font file: table directory, 4-byte aligned tables, checksums.</summary>
internal static class FontFileWriter
{
    public static byte[] Write(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var power = 1;
        var log = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            log++;
        }

        var headerSize = 12 + (16 * count);
        var output = new byte[headerSize + tables.Values.Sum(t => (t.Length + 3) & ~3)];
        BinaryPrimitives.WriteUInt32BigEndian(output, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)((count * 16) - (power * 16)));
        var offset = headerSize;
        var record = 12;
        var headOffset = -1;
        foreach (var (tag, data) in tables)
        {
            if (tag == "head")
            {
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 0);
                headOffset = offset;
            }

            System.Text.Encoding.ASCII.GetBytes(tag, output.AsSpan(record, 4));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 4), Checksum(data));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 12), (uint)data.Length);
            data.CopyTo(output.AsSpan(offset));
            offset += (data.Length + 3) & ~3;
            record += 16;
        }

        if (headOffset >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8), unchecked(0xB1B0AFBA - Checksum(output)));
        }

        return output;
    }

    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var k = 0; k < 4; k++)
            {
                word = (word << 8) | (i + k < data.Length ? data[i + k] : 0u);
            }

            sum = unchecked(sum + word);
        }

        return sum;
    }
}
