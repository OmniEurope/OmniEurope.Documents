// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Rendering.Fonts;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// Glyph lookup shared by name-keyed programs (CFF and Type 1): an encoding difference names the glyph,
/// else the program's own encoding, else the Unicode text of the code matched against the glyph names.
/// </summary>
internal static class GlyphNameLookup
{
    public static Dictionary<string, string> ByUnicode(IEnumerable<string> names)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (GlyphNames.ToUnicode(name) is { } text)
            {
                result.TryAdd(text, name);
            }
        }

        return result;
    }

    public static string? Find(PdfFontDecoder decoder, DecodedGlyph glyph, Dictionary<int, string>? builtIn, Dictionary<string, string> byUnicode, Func<string, bool> exists)
    {
        if (decoder.GlyphName(glyph.Code) is { } difference && exists(difference))
        {
            return difference;
        }

        if (byUnicode.TryGetValue(glyph.Text, out var named))
        {
            return named;
        }

        return builtIn is not null && builtIn.TryGetValue((int)glyph.Code, out var own) && exists(own) ? own : null;
    }
}

/// <summary>An embedded CFF program (bare, or inside an OpenType wrapper); glyphs by name or, for CID-keyed fonts, by CID.</summary>
internal sealed class CompactRenderFont : RenderFont
{
    private readonly CffFont _font;
    private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _byUnicode;
    private readonly ushort[]? _cidToGid;

    private CompactRenderFont(PdfFontDecoder decoder, CffFont font, ushort[]? cidToGid)
        : base(decoder)
    {
        _font = font;
        _cidToGid = cidToGid;
        for (var g = 0; g < font.GlyphNames.Length; g++)
        {
            _byName.TryAdd(font.GlyphNames[g] ?? string.Empty, g);
        }

        _byUnicode = GlyphNameLookup.ByUnicode(font.IsCid ? [] : font.GlyphNames.Where(n => n is not null));
    }

    /// <summary>A CFF program; an OpenType wrapper is opened at its CFF table (a TrueType-flavoured one is drawn as TrueType).</summary>
    public static RenderFont Create(PdfFontDecoder decoder, byte[] data, string subtype, ushort[]? cidToGid)
    {
        if (subtype == "OpenType" || (data.Length > 4 && BinaryPrimitives.ReadUInt32BigEndian(data) is 0x4F54544F or 0x00010000))
        {
            if (Table(data, "CFF ") is { } table)
            {
                data = table;
            }
            else
            {
                return new TrueTypeRenderFont(decoder, TrueTypeFont.Load(data), cidToGid);
            }
        }

        return new CompactRenderFont(decoder, new CffFont(data), cidToGid);
    }

    // The bytes of one table of an sfnt font (OpenType).
    private static byte[]? Table(byte[] data, string tag)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        for (var i = 0; i < count && 12 + (i * 16) + 16 <= data.Length; i++)
        {
            var record = data.AsSpan(12 + (i * 16));
            if (System.Text.Encoding.ASCII.GetString(record[..4]) == tag)
            {
                var (offset, length) = ((int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]), (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]));
                return offset + length <= data.Length ? data[offset..(offset + length)] : null;
            }
        }

        return null;
    }

    protected override GlyphShape? Outline(DecodedGlyph glyph)
    {
        if (Decoder.IsComposite || _font.IsCid)
        {
            var cid = Decoder.Cid(glyph.Code);
            var gid = _cidToGid is not null && cid < _cidToGid.Length ? _cidToGid[cid] : _font.IsCid && _byName.TryGetValue(cid.ToString(System.Globalization.CultureInfo.InvariantCulture), out var byCid) ? byCid : cid;
            return _font.Outline(gid);
        }

        var name = GlyphNameLookup.Find(Decoder, glyph, _font.BuiltInEncoding, _byUnicode, _byName.ContainsKey);
        return name is not null && _byName.TryGetValue(name, out var index) ? _font.Outline(index) : null;
    }
}

/// <summary>An embedded Type 1 program; glyphs by name.</summary>
internal sealed class Type1RenderFont : RenderFont
{
    private readonly Type1Font _font;
    private readonly Dictionary<string, string> _byUnicode;

    private Type1RenderFont(PdfFontDecoder decoder, Type1Font font)
        : base(decoder)
    {
        _font = font;
        _byUnicode = GlyphNameLookup.ByUnicode(font.CharStrings.Keys);
    }

    public static RenderFont Create(PdfFontDecoder decoder, byte[] program, int length1, int length2) =>
        new Type1RenderFont(decoder, Type1Font.Parse(program, length1, length2));

    protected override GlyphShape? Outline(DecodedGlyph glyph)
    {
        var name = GlyphNameLookup.Find(Decoder, glyph, _font.Encoding.Count > 0 ? _font.Encoding : null, _byUnicode, _font.CharStrings.ContainsKey);
        return name is null ? null : _font.Outline(name);
    }
}
