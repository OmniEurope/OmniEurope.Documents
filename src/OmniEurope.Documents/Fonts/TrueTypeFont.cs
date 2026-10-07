// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Fonts;

/// <summary>
/// A TrueType font (also the TrueType-outline flavour of OpenType, and one face of a collection): names,
/// vertical metrics, advance widths, character map and glyph outlines. Fonts with PostScript (CFF)
/// outlines load for metrics and character map but have no <see cref="TrueTypeGlyphs"/>.
/// </summary>
public sealed class TrueTypeFont
{
    private readonly FontReader _font;
    private readonly Dictionary<string, FontTable> _tables;
    private readonly ushort[] _advances;
    private readonly TrueTypeCmap _cmap;

    private TrueTypeFont(byte[] data, int offset)
    {
        _font = new FontReader(data);
        _tables = ReadDirectory(_font, offset);
        var head = Require("head");
        UnitsPerEm = _font.U16(head.Offset + 18);
        if (UnitsPerEm is < 16 or > 16384)
        {
            throw new InvalidDataException("Invalid unitsPerEm in the font.");
        }

        BoundingBox = (_font.S16(head.Offset + 36), _font.S16(head.Offset + 38), _font.S16(head.Offset + 40), _font.S16(head.Offset + 42));
        var macStyle = _font.U16(head.Offset + 44);
        IndexToLocLong = _font.S16(head.Offset + 50) == 1;
        GlyphCount = _font.U16(Require("maxp").Offset + 4);
        var hhea = Require("hhea");
        Ascender = _font.S16(hhea.Offset + 4);
        Descender = _font.S16(hhea.Offset + 6);
        LineGap = _font.S16(hhea.Offset + 8);
        _advances = ReadAdvances(_font.U16(hhea.Offset + 34));
        _cmap = _tables.TryGetValue("cmap", out var cmap) ? new TrueTypeCmap(_font, cmap) : new TrueTypeCmap(new FontReader([0, 0, 0, 0]), new FontTable(0, 4));
        ReadOs2(macStyle);
        ReadPost();
        Names = new TrueTypeNames(_font, _tables.GetValueOrDefault("name"));
        Glyphs = _tables.ContainsKey("glyf") && _tables.ContainsKey("loca") ? new TrueTypeGlyphs(this, _font, Require("glyf"), Require("loca")) : null;
    }

    /// <summary>The whole font file this face was read from.</summary>
    public byte[] Data => _font.Data;

    /// <summary>The family, subfamily, full and PostScript names.</summary>
    public TrueTypeNames Names { get; }

    /// <summary>Font design units per em.</summary>
    public int UnitsPerEm { get; }

    /// <summary>Number of glyphs.</summary>
    public int GlyphCount { get; }

    /// <summary>The glyph bounding box of all glyphs (xMin, yMin, xMax, yMax) in font units.</summary>
    public (short XMin, short YMin, short XMax, short YMax) BoundingBox { get; }

    /// <summary>Typographic ascender (hhea), font units.</summary>
    public short Ascender { get; }

    /// <summary>Typographic descender (hhea, negative), font units.</summary>
    public short Descender { get; }

    /// <summary>Line gap (hhea), font units.</summary>
    public short LineGap { get; }

    /// <summary>Windows ascent (OS/2 usWinAscent), font units; the hhea ascender when absent.</summary>
    public int WinAscent { get; private set; }

    /// <summary>Windows descent (OS/2 usWinDescent, positive), font units.</summary>
    public int WinDescent { get; private set; }

    /// <summary>Height of capitals (OS/2), font units; 70% of the em when absent.</summary>
    public int CapHeight { get; private set; }

    /// <summary>Height of lower-case x (OS/2), font units; half the em when absent.</summary>
    public int XHeight { get; private set; }

    /// <summary>Underline position (post, negative below the baseline), font units.</summary>
    public short UnderlinePosition { get; private set; }

    /// <summary>Underline thickness (post), font units.</summary>
    public short UnderlineThickness { get; private set; }

    /// <summary>Strike-out position above the baseline (OS/2), font units.</summary>
    public short StrikeoutPosition { get; private set; }

    /// <summary>Strike-out thickness (OS/2), font units.</summary>
    public short StrikeoutSize { get; private set; }

    /// <summary>Italic angle in degrees (post), negative leaning right.</summary>
    public double ItalicAngle { get; private set; }

    /// <summary>True for a bold face.</summary>
    public bool IsBold { get; private set; }

    /// <summary>True for an italic or oblique face.</summary>
    public bool IsItalic { get; private set; }

    /// <summary>True when every glyph has the same advance.</summary>
    public bool IsFixedPitch { get; private set; }

    /// <summary>OS/2 weight class (400 regular, 700 bold).</summary>
    public int WeightClass { get; private set; } = 400;

    /// <summary>False when the font's licence forbids embedding (OS/2 fsType restricted).</summary>
    public bool EmbeddingAllowed { get; private set; } = true;

    /// <summary>True when the font maps codes through a Windows symbol table (Symbol, Wingdings).</summary>
    public bool IsSymbolFont => _cmap.IsSymbol;

    /// <summary>The glyph outlines, or null for a font with PostScript outlines.</summary>
    public TrueTypeGlyphs? Glyphs { get; }

    /// <summary>Every mapped code point and its glyph.</summary>
    public IReadOnlyDictionary<int, int> CharacterMap => _cmap.Map;

    internal bool IndexToLocLong { get; }

    /// <summary>Loads a font file (.ttf, .otf, or face <paramref name="faceIndex"/> of a .ttc collection).</summary>
    /// <exception cref="InvalidDataException">The data is not a supported font.</exception>
    public static TrueTypeFont Load(byte[] data, int faceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        var font = new FontReader(data);
        if (font.Length < 12)
        {
            throw new InvalidDataException("Not a font file.");
        }

        var offset = 0;
        if (font.Tag(0) == "ttcf")
        {
            var faces = (int)font.U32(8);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(faceIndex, faces);
            offset = (int)font.U32(12 + (faceIndex * 4));
        }

        return new TrueTypeFont(data, offset);
    }

    /// <summary>The glyph of a code point, 0 (.notdef) when the font has none.</summary>
    public int GetGlyphIndex(int codePoint) => _cmap.Lookup(codePoint);

    /// <summary>True when the font has a glyph for <paramref name="codePoint"/>.</summary>
    public bool HasGlyph(int codePoint) => _cmap.Lookup(codePoint) != 0;

    /// <summary>The advance width of a glyph, font units.</summary>
    public int GetAdvanceWidth(int glyph) =>
        _advances.Length == 0 ? 0 : glyph < _advances.Length ? _advances[glyph] : _advances[^1];

    /// <summary>The width of <paramref name="text"/> in points at <paramref name="size"/> points (no kerning).</summary>
    public double MeasureText(string text, double size)
    {
        ArgumentNullException.ThrowIfNull(text);
        long units = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            units += GetAdvanceWidth(GetGlyphIndex(rune.Value));
        }

        return units * size / UnitsPerEm;
    }

    /// <summary>True when the font has a table with this tag.</summary>
    public bool HasTable(string tag) => _tables.ContainsKey(tag);

    internal FontTable? Table(string tag) => _tables.TryGetValue(tag, out var table) ? table : null;

    internal FontReader Reader => _font;

    private FontTable Require(string tag) =>
        _tables.TryGetValue(tag, out var table) ? table : throw new InvalidDataException($"The font has no '{tag}' table.");

    private static Dictionary<string, FontTable> ReadDirectory(FontReader font, int offset)
    {
        var version = font.U32(offset);
        if (version is not (0x00010000 or 0x74727565 or 0x4F54544F))
        {
            throw new InvalidDataException("Not a TrueType or OpenType font.");
        }

        var count = font.U16(offset + 4);
        var tables = new Dictionary<string, FontTable>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var record = offset + 12 + (i * 16);
            var tag = font.Tag(record);
            var tableOffset = (int)font.U32(record + 8);
            var length = (int)font.U32(record + 12);
            if (font.Has(tableOffset, length))
            {
                tables[tag] = new FontTable(tableOffset, length);
            }
        }

        return tables;
    }

    private ushort[] ReadAdvances(int metrics)
    {
        if (!_tables.TryGetValue("hmtx", out var hmtx))
        {
            return [];
        }

        metrics = Math.Min(metrics, hmtx.Length / 4);
        var advances = new ushort[Math.Max(metrics, 1)];
        for (var i = 0; i < metrics; i++)
        {
            advances[i] = _font.U16(hmtx.Offset + (i * 4));
        }

        return advances;
    }

    private void ReadOs2(ushort macStyle)
    {
        IsBold = (macStyle & 1) != 0;
        IsItalic = (macStyle & 2) != 0;
        WinAscent = Ascender;
        WinDescent = -Descender;
        CapHeight = UnitsPerEm * 7 / 10;
        XHeight = UnitsPerEm / 2;
        StrikeoutPosition = (short)(UnitsPerEm * 0.28);
        StrikeoutSize = (short)(UnitsPerEm / 20);
        if (!_tables.TryGetValue("OS/2", out var os2) || os2.Length < 78)
        {
            return;
        }

        var o = os2.Offset;
        WeightClass = _font.U16(o + 4);
        var fsType = _font.U16(o + 8);
        EmbeddingAllowed = (fsType & 0x000F) != 0x0002;
        StrikeoutSize = _font.S16(o + 26);
        StrikeoutPosition = _font.S16(o + 28);
        var selection = _font.U16(o + 62);
        IsItalic = (selection & 0x01) != 0 || IsItalic;
        IsBold = (selection & 0x20) != 0 || IsBold;
        WinAscent = _font.U16(o + 74);
        WinDescent = _font.U16(o + 76);
        if (os2.Length >= 90 && _font.U16(o) >= 2)
        {
            XHeight = _font.S16(o + 86);
            CapHeight = _font.S16(o + 88);
        }
    }

    private void ReadPost()
    {
        if (!_tables.TryGetValue("post", out var post) || post.Length < 32)
        {
            UnderlinePosition = (short)(-UnitsPerEm / 10);
            UnderlineThickness = (short)(UnitsPerEm / 20);
            return;
        }

        ItalicAngle = _font.S32(post.Offset + 4) / 65536.0;
        UnderlinePosition = _font.S16(post.Offset + 8);
        UnderlineThickness = _font.S16(post.Offset + 10);
        IsFixedPitch = _font.U32(post.Offset + 12) != 0;
    }
}

/// <summary>The names of a font face (name table), preferring Windows English entries.</summary>
public sealed class TrueTypeNames
{
    internal TrueTypeNames(FontReader font, FontTable? table)
    {
        if (table is not { } name)
        {
            return;
        }

        var best = ReadBest(font, name);
        Family = Get(best, 16) ?? Get(best, 1) ?? string.Empty;
        Subfamily = Get(best, 17) ?? Get(best, 2) ?? string.Empty;
        FullName = Get(best, 4) ?? string.Empty;
        PostScriptName = Get(best, 6) ?? string.Empty;
    }

    /// <summary>Typographic family name (Liberation Sans).</summary>
    public string Family { get; } = string.Empty;

    /// <summary>Style name (Bold Italic).</summary>
    public string Subfamily { get; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; } = string.Empty;

    /// <summary>PostScript name (LiberationSans-Bold).</summary>
    public string PostScriptName { get; } = string.Empty;

    private static string? Get(Dictionary<int, (int Score, string Value)> names, int id) =>
        names.TryGetValue(id, out var entry) && entry.Value.Length > 0 ? entry.Value : null;

    // Family, subfamily, full name, PostScript name, typographic family and typographic subfamily.
    private static readonly HashSet<int> WantedIds = [1, 2, 4, 6, 16, 17];

    // The preferred record of each wanted name: Windows over Unicode over Macintosh, US English or default language first.
    private static Dictionary<int, (int Score, string Value)> ReadBest(FontReader font, FontTable name)
    {
        var count = font.U16(name.Offset + 2);
        var storage = name.Offset + font.U16(name.Offset + 4);
        var best = new Dictionary<int, (int Score, string Value)>();
        for (var i = 0; i < count; i++)
        {
            var record = name.Offset + 6 + (i * 12);
            var platform = font.U16(record);
            var id = font.U16(record + 6);
            var length = font.U16(record + 8);
            var offset = storage + font.U16(record + 10);
            if (!WantedIds.Contains(id) || !font.Has(offset, length))
            {
                continue;
            }

            var bytes = font.Slice(offset, length);
            var value = platform is 0 or 3 ? Encoding.BigEndianUnicode.GetString(bytes) : Encoding.Latin1.GetString(bytes);
            var score = Score(platform, font.U16(record + 4));
            if (!best.TryGetValue(id, out var current) || score > current.Score)
            {
                best[id] = (score, value);
            }
        }

        return best;
    }

    private static int Score(int platform, int language) => (platform == 3 ? 2 : platform == 0 ? 1 : 0) + (language is 0x409 or 0 ? 4 : 0);
}
