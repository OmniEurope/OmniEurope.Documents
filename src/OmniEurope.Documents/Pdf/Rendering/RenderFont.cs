// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>Shared state of one rendering: fonts, loaded font programs and the gaps met.</summary>
internal sealed class RenderContext(FontLibrary fonts)
{
    private readonly Dictionary<PdfDictionary, RenderFont> _fonts = new(ReferenceEqualityComparer.Instance);

    public FontLibrary Fonts { get; } = fonts;

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    public void Gap(string gap) => Gaps.Add(gap);

    public RenderFont Font(PdfObjectStore store, PdfDictionary dictionary)
    {
        if (!_fonts.TryGetValue(dictionary, out var font))
        {
            font = RenderFont.Load(store, dictionary, this);
            _fonts[dictionary] = font;
        }

        return font;
    }
}

/// <summary>A Type 3 glyph: the content stream that draws it, the font matrix and the resources.</summary>
internal sealed record Type3Glyph(PdfStream Procedure, Matrix FontMatrix, PdfDictionary? Resources);

/// <summary>
/// A font as drawn: decodes the shown bytes (through <see cref="PdfFontDecoder"/>) and gives the outline of
/// each glyph in text space, from the embedded program (TrueType, CFF, Type 1) or, when the font is not
/// embedded, from a bundled look-alike stretched to the PDF's widths. Type 3 fonts give a glyph procedure.
/// </summary>
internal abstract class RenderFont(PdfFontDecoder decoder)
{
    private readonly Dictionary<uint, GlyphShape?> _cache = [];

    public PdfFontDecoder Decoder { get; } = decoder;

    public GlyphShape? Shape(DecodedGlyph glyph)
    {
        if (!_cache.TryGetValue(glyph.Code, out var shape))
        {
            shape = Outline(glyph);
            _cache[glyph.Code] = shape;
        }

        return shape;
    }

    public virtual Type3Glyph? Procedure(DecodedGlyph glyph) => null;

    protected abstract GlyphShape? Outline(DecodedGlyph glyph);

    public static RenderFont Load(PdfObjectStore store, PdfDictionary font, RenderContext context)
    {
        var decoder = new PdfFontDecoder(store, font);
        var subtype = store.Get(font, "Subtype") is PdfName s ? s.Value : "Type1";
        if (subtype == "Type3")
        {
            return new Type3RenderFont(store, font, decoder);
        }

        var descendant = decoder.IsComposite && store.Get<PdfArray>(font, "DescendantFonts") is { Count: > 0 } array ? store.Resolve(array[0]) as PdfDictionary : null;
        var embedded = Embedded(store, decoder, descendant ?? font, context);
        if (embedded is not null)
        {
            return embedded;
        }

        if (!IsStandard(decoder.BaseFont))
        {
            context.Gap("fonts that are not embedded are drawn with bundled look-alikes");
        }

        return new BundledRenderFont(decoder, context.Fonts);
    }

    private static RenderFont? Embedded(PdfObjectStore store, PdfFontDecoder decoder, PdfDictionary font, RenderContext context)
    {
        var descriptor = decoder.Descriptor;
        try
        {
            if (store.Get(descriptor, "FontFile2") is PdfStream trueType)
            {
                return new TrueTypeRenderFont(decoder, TrueTypeFont.Load(store.DecodeBytes(trueType)), CidToGid(store, font));
            }

            if (store.Get(descriptor, "FontFile3") is PdfStream compact)
            {
                return CompactRenderFont.Create(decoder, store.DecodeBytes(compact), store.Get(compact, "Subtype") is PdfName sub ? sub.Value : string.Empty, CidToGid(store, font));
            }

            if (store.Get(descriptor, "FontFile") is PdfStream type1)
            {
                return Type1RenderFont.Create(decoder, store.DecodeBytes(type1), (int)store.Number(type1, "Length1"), (int)store.Number(type1, "Length2"));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            context.Gap("unreadable embedded fonts are drawn with bundled look-alikes");
        }

        return null;
    }

    // CIDToGIDMap of a CIDFontType2: a stream of big-endian glyph ids indexed by CID, or Identity (null).
    private static ushort[]? CidToGid(PdfObjectStore store, PdfDictionary font)
    {
        if (store.Get(font, "CIDToGIDMap") is not PdfStream map)
        {
            return null;
        }

        var bytes = store.DecodeBytes(map);
        var result = new ushort[bytes.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (ushort)((bytes[2 * i] << 8) | bytes[(2 * i) + 1]);
        }

        return result;
    }

    private static bool IsStandard(string baseFont)
    {
        var name = baseFont.Contains('+') ? baseFont[(baseFont.IndexOf('+') + 1)..] : baseFont;
        return name.StartsWith("Helvetica", StringComparison.Ordinal) || name.StartsWith("Times", StringComparison.Ordinal)
            || name.StartsWith("Courier", StringComparison.Ordinal) || name.StartsWith("Arial", StringComparison.Ordinal)
            || name is "Symbol" or "ZapfDingbats";
    }

    /// <summary>The first code point of a glyph's text, or 0.</summary>
    protected static int CodePoint(DecodedGlyph glyph) =>
        glyph.Text.Length > 0 && glyph.Text != "�" ? char.ConvertToUtf32(glyph.Text, 0) : 0;
}

/// <summary>An embedded TrueType program: glyphs by CID (composite) or by the cmap the font carries (simple).</summary>
internal sealed class TrueTypeRenderFont(PdfFontDecoder decoder, TrueTypeFont font, ushort[]? cidToGid) : RenderFont(decoder)
{
    protected override GlyphShape? Outline(DecodedGlyph glyph)
    {
        var gid = Glyph(glyph);
        if (gid <= 0 || font.Glyphs is not { } glyphs || gid >= font.GlyphCount)
        {
            return null;
        }

        return GlyphShape.FromQuadratic(glyphs.GetOutline(gid), 1.0 / font.UnitsPerEm);
    }

    private int Glyph(DecodedGlyph glyph)
    {
        if (Decoder.IsComposite)
        {
            var cid = Decoder.Cid(glyph.Code);
            return cidToGid is null ? cid : cid < cidToGid.Length ? cidToGid[cid] : 0;
        }

        if (font.IsSymbolFont)
        {
            var symbol = font.GetGlyphIndex(0xF000 + (int)glyph.Code);
            return symbol != 0 ? symbol : font.GetGlyphIndex((int)glyph.Code);
        }

        var byText = CodePoint(glyph) is > 0 and var codePoint ? font.GetGlyphIndex(codePoint) : 0;
        return byText != 0 ? byText : font.GetGlyphIndex((int)glyph.Code);
    }
}

/// <summary>A font that is not embedded: the bundled face of the same family, each glyph stretched to the PDF's width.</summary>
internal sealed class BundledRenderFont(PdfFontDecoder decoder, FontLibrary fonts) : RenderFont(decoder)
{
    protected override GlyphShape? Outline(DecodedGlyph glyph)
    {
        var codePoint = CodePoint(glyph);
        if (codePoint <= 32)
        {
            return null;
        }

        var name = Decoder.BaseFont.Contains('+') ? Decoder.BaseFont[(Decoder.BaseFont.IndexOf('+') + 1)..] : Decoder.BaseFont;
        var bold = name.Contains("Bold", StringComparison.OrdinalIgnoreCase) || name.Contains("Black", StringComparison.OrdinalIgnoreCase);
        var italic = name.Contains("Italic", StringComparison.OrdinalIgnoreCase) || name.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
        var family = name.Split(',', '-')[0];
        var face = fonts.ResolveForCharacter(family, bold, italic, codePoint);
        var gid = face.GetGlyphIndex(codePoint);
        if (gid == 0 || face.Glyphs is not { } glyphs)
        {
            return null;
        }

        var natural = face.GetAdvanceWidth(gid) / (double)face.UnitsPerEm;
        var stretch = natural > 0 && glyph.Advance > 0 ? glyph.Advance / natural : 1;
        return GlyphShape.FromQuadratic(glyphs.GetOutline(gid), 1.0 / face.UnitsPerEm).Transform(new Matrix(stretch, 0, 0, 1, 0, 0));
    }
}

/// <summary>A Type 3 font: glyphs are content streams found by glyph name in CharProcs.</summary>
internal sealed class Type3RenderFont(PdfObjectStore store, PdfDictionary font, PdfFontDecoder decoder) : RenderFont(decoder)
{
    private readonly Matrix _matrix = Matrix.FromArray(store.Get<PdfArray>(font, "FontMatrix") is { Count: 6 } m ? m : null);

    protected override GlyphShape? Outline(DecodedGlyph glyph) => null;

    public override Type3Glyph? Procedure(DecodedGlyph glyph)
    {
        var name = Decoder.GlyphName(glyph.Code);
        var procedures = store.Get<PdfDictionary>(font, "CharProcs");
        return name is not null && store.Get(procedures, name) is PdfStream procedure
            ? new Type3Glyph(procedure, _matrix == Matrix.Identity ? new Matrix(0.001, 0, 0, 0.001, 0, 0) : _matrix, store.Get<PdfDictionary>(font, "Resources"))
            : null;
    }
}
