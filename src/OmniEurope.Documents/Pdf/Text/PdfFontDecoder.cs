// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>One code of a shown string: its text, its advance in text space (per unit of font size) and
/// whether it is the single-byte space that word spacing applies to.</summary>
internal readonly record struct DecodedGlyph(string Text, double Advance, bool IsWordSpace, uint Code, int Length = 1);

/// <summary>
/// Turns the bytes of a text-showing operator into glyphs for one font resource: code splitting (one byte
/// for simple fonts, the encoding CMap for Type0), Unicode (ToUnicode first, else the encoding and its
/// Differences through glyph names), and advances (Widths, W/DW, Type 3 FontMatrix, else the metrics of the
/// metric-compatible bundled face for the 14 standard fonts).
/// </summary>
internal sealed class PdfFontDecoder
{
    private readonly PdfCMap? _encoding;
    private readonly PdfCMap? _toUnicode;
    private readonly string?[]? _simpleTable;
    private readonly Dictionary<int, double> _widths = [];
    private readonly double _defaultWidth;
    private readonly double _scale = 0.001;
    private readonly TrueTypeFont? _standardMetrics;
    private readonly bool _ucs2Encoding;
    private readonly Dictionary<int, string> _names = [];

    public PdfFontDecoder(PdfObjectStore store, PdfDictionary font)
    {
        var subtype = store.Get(font, "Subtype") is PdfName s ? s.Value : "Type1";
        BaseFont = store.Get(font, "BaseFont") is PdfName b ? b.Value : subtype;
        IsComposite = subtype == "Type0";
        _toUnicode = store.Get(font, "ToUnicode") is PdfStream toUnicode ? Safe(() => PdfCMap.Parse(store.DecodeBytes(toUnicode))) : null;
        if (IsComposite)
        {
            var encoding = store.Get(font, "Encoding");
            _ucs2Encoding = encoding is PdfName name && (name.Value.Contains("UCS2", StringComparison.Ordinal) || name.Value.Contains("UTF16", StringComparison.Ordinal));
            _encoding = encoding is PdfStream stream ? Safe(() => PdfCMap.Parse(store.DecodeBytes(stream))) ?? PdfCMap.Identity : PdfCMap.Identity;
            var descendant = store.Get<PdfArray>(font, "DescendantFonts") is { Count: > 0 } array ? store.Resolve(array[0]) as PdfDictionary : null;
            _defaultWidth = store.Number(descendant, "DW", 1000);
            ReadCidWidths(store, store.Get<PdfArray>(descendant, "W"));
            Descriptor = store.Get<PdfDictionary>(descendant, "FontDescriptor");
        }
        else
        {
            Descriptor = store.Get<PdfDictionary>(font, "FontDescriptor");
            _simpleTable = SimpleEncoding(store, font, subtype, _names);
            ReadSimpleWidths(store, font);
            _defaultWidth = store.Number(Descriptor, "MissingWidth", 0);
            if (subtype == "Type3" && store.Get<PdfArray>(font, "FontMatrix") is { Count: >= 1 } matrix && store.Resolve(matrix[0]) is PdfNumber a)
            {
                _scale = a.Value;
            }

            if (store.Get(font, "Widths") is null)
            {
                _standardMetrics = StandardFace(BaseFont);
            }
        }

        Ascent = Math.Clamp(store.Number(Descriptor, "Ascent", 750) / 1000, 0.3, 1.5);
        Descent = Math.Clamp(store.Number(Descriptor, "Descent", -250) / 1000, -0.8, 0);
    }

    public string BaseFont { get; }

    public bool IsComposite { get; }

    public PdfDictionary? Descriptor { get; }

    /// <summary>Ascent and descent per unit of font size (from the descriptor, defaults 0.75 and -0.25).</summary>
    public double Ascent { get; }

    public double Descent { get; }

    /// <summary>The glyph name an encoding difference gives a code of a simple font, or null.</summary>
    public string? GlyphName(uint code) => _names.TryGetValue((int)code, out var name) ? name : null;

    /// <summary>The character identifier of a code of a composite font (the code itself for a simple font).</summary>
    public int Cid(uint code) => IsComposite && _encoding is not null ? _encoding.ToCid(code) : (int)code;

    public IEnumerable<DecodedGlyph> Decode(byte[] bytes)
    {
        if (!IsComposite)
        {
            foreach (var b in bytes)
            {
                yield return new DecodedGlyph(Text(b, 1), Width(b, b), b == 32, b);
            }

            yield break;
        }

        foreach (var (code, length) in _encoding!.Codes(bytes))
        {
            var cid = _encoding.ToCid(code);
            yield return new DecodedGlyph(Text(code, length), Width(cid, code), length == 1 && code == 32, code, length);
        }
    }

    private string Text(uint code, int length)
    {
        if (_toUnicode?.ToUnicode(code) is { } mapped)
        {
            return mapped;
        }

        if (_ucs2Encoding)
        {
            return ((char)code).ToString();
        }

        if (_simpleTable is not null && code < 256 && _simpleTable[code] is { } text)
        {
            return text;
        }

        // Unknown: U+FFFD for a composite font, the byte itself (Latin-1) for a simple one.
        return IsComposite || length > 1 ? "�" : ((char)code).ToString();
    }

    private double Width(int index, uint code)
    {
        if (_widths.TryGetValue(index, out var width))
        {
            return width * _scale;
        }

        if (_standardMetrics is not null)
        {
            var text = Text(code, 1);
            var codePoint = text.Length > 0 ? char.ConvertToUtf32(text, 0) : ' ';
            var glyph = _standardMetrics.GetGlyphIndex(codePoint);
            return _standardMetrics.GetAdvanceWidth(glyph) / (double)_standardMetrics.UnitsPerEm;
        }

        return _defaultWidth * _scale;
    }

    private void ReadSimpleWidths(PdfObjectStore store, PdfDictionary font)
    {
        var first = (int)store.Number(font, "FirstChar");
        if (store.Get<PdfArray>(font, "Widths") is not { } widths)
        {
            return;
        }

        for (var i = 0; i < widths.Count; i++)
        {
            if (store.Resolve(widths[i]) is PdfNumber n)
            {
                _widths[first + i] = n.Value;
            }
        }
    }

    // c [w1 w2 ...]: consecutive CIDs from c (65536 at most).
    private void ReadWidthList(PdfObjectStore store, int first, PdfArray list)
    {
        for (var k = 0; k < list.Count && k < 65536; k++)
        {
            if (store.Resolve(list[k]) is PdfNumber n)
            {
                _widths[first + k] = n.Value;
            }
        }
    }

    // cfirst clast w: one width for a range of CIDs (65536 at most).
    private void ReadWidthRange(int first, int last, double width)
    {
        for (var cid = first; cid <= last && cid - first < 65536; cid++)
        {
            _widths[cid] = width;
        }
    }

    private void ReadCidWidths(PdfObjectStore store, PdfArray? w)
    {
        if (w is null)
        {
            return;
        }

        var items = w.Items.Select(store.Resolve).ToList();
        for (var i = 0; i + 1 < items.Count;)
        {
            if (items[i] is not PdfNumber first)
            {
                i++;
                continue;
            }

            if (items[i + 1] is PdfArray list)
            {
                ReadWidthList(store, first.IntValue, list);
                i += 2;
            }
            else if (i + 2 < items.Count && items[i + 1] is PdfNumber last && items[i + 2] is PdfNumber value)
            {
                ReadWidthRange(first.IntValue, last.IntValue, value.Value);
                i += 3;
            }
            else
            {
                i++;
            }
        }
    }

    private static string?[] SimpleEncoding(PdfObjectStore store, PdfDictionary font, string subtype, Dictionary<int, string> names)
    {
        var encoding = store.Get(font, "Encoding");
        var table = BaseTable(store, font, subtype, encoding);
        if (encoding is not PdfName && EmbeddedType1Encoding(store, font) is { } builtIn)
        {
            foreach (var (code, text) in builtIn)
            {
                table[code] = text;
            }
        }

        if (encoding is PdfDictionary withDifferences && store.Get<PdfArray>(withDifferences, "Differences") is { } differences)
        {
            ApplyDifferences(store, differences, table, names);
        }

        return table;
    }

    // The named base encoding (or the font's default), the Symbol table for a symbol font without one.
    private static string?[] BaseTable(PdfObjectStore store, PdfDictionary font, string subtype, PdfObject? encoding)
    {
        var baseFont = store.Get(font, "BaseFont") is PdfName b ? b.Value : string.Empty;
        if (baseFont.Contains("Symbol", StringComparison.Ordinal) && encoding is not PdfName)
        {
            return (string?[])StandardEncodings.Symbol.Clone();
        }

        var baseName = encoding switch
        {
            PdfName name => name.Value,
            PdfDictionary dictionary when store.Get(dictionary, "BaseEncoding") is PdfName name => name.Value,
            _ => subtype == "TrueType" ? "WinAnsiEncoding" : "StandardEncoding",
        };
        return (string?[])StandardEncodings.Named(baseName).Clone();
    }

    // Differences: a code, then the glyph names of that code and the following ones.
    private static void ApplyDifferences(PdfObjectStore store, PdfArray differences, string?[] table, Dictionary<int, string> names)
    {
        var code = 0;
        foreach (var item in differences.Items.Select(store.Resolve))
        {
            if (item is PdfNumber n)
            {
                code = n.IntValue;
            }
            else if (item is PdfName glyph && code is >= 0 and < 256)
            {
                names[code] = glyph.Value;
                table[code] = GlyphNames.ToUnicode(glyph.Value) ?? table[code];
                code++;
            }
        }
    }

    // The built-in encoding of an embedded Type 1 font: "dup 65 /A put" lines of its clear-text part.
    private static Dictionary<int, string>? EmbeddedType1Encoding(PdfObjectStore store, PdfDictionary font)
    {
        var descriptor = store.Get<PdfDictionary>(font, "FontDescriptor");
        if (store.Get(descriptor, "FontFile") is not PdfStream program)
        {
            return null;
        }

        var clear = Encoding.Latin1.GetString(store.DecodeBytes(program).AsSpan(0, Math.Min(64 * 1024, (int)store.Number(program, "Length1", 64 * 1024))));
        var result = new Dictionary<int, string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(clear, @"dup\s+(\d+)\s*/([^\s/\[\]{}()<>%]+)\s+put"))
        {
            var code = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (code < 256 && GlyphNames.ToUnicode(match.Groups[2].Value) is { } text)
            {
                result[code] = text;
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static TrueTypeFont? StandardFace(string baseFont)
    {
        var name = baseFont.Contains('+') ? baseFont[(baseFont.IndexOf('+') + 1)..] : baseFont;
        var bold = name.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        var italic = name.Contains("Italic", StringComparison.OrdinalIgnoreCase) || name.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
        var stem = name.StartsWith("Courier", StringComparison.OrdinalIgnoreCase) ? "LiberationMono"
            : name.StartsWith("Times", StringComparison.OrdinalIgnoreCase) ? "LiberationSerif"
            : name.StartsWith("Helvetica", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Arial", StringComparison.OrdinalIgnoreCase) ? "LiberationSans"
            : null;
        return stem is null ? null : FontLibrary.Bundled(stem, bold, italic);
    }

    private static T? Safe<T>(Func<T> parse)
        where T : class
    {
        try
        {
            return parse();
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or FormatException)
        {
            return null;
        }
    }
}
