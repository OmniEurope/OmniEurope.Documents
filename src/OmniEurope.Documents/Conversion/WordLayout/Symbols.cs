// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Characters of the Symbol and Wingdings fonts (which are not bundled) drawn with Unicode look-alikes
/// that the bundled fonts carry. Symbol-font text is addressed either directly or in the private use area
/// (U+F000 + code). Symbol text is drawn with Liberation Sans but advances by the Symbol font's own widths
/// (Adobe's Core 14 metrics). Wingdings and Webdings text is drawn with Noto Sans Symbols 2 (Liberation Sans
/// for a character it lacks) and keeps the line height of Liberation Sans; a Wingdings code without a
/// look-alike in the table below is drawn as a bullet (•).
/// </summary>
internal static class Symbols
{
    /// <summary>The bundled face Symbol text is drawn with.</summary>
    public const string SymbolFace = "Liberation Sans";

    /// <summary>The bundled face Wingdings and Webdings text is drawn with.</summary>
    public const string DingbatFace = "Noto Sans Symbols 2";

    private const string SymbolUpper = "ΑΒΧΔΕΦΓΗΙϑΚΛΜΝΟΠΘΡΣΤΥςΩΞΨΖ";
    private const string SymbolLower = "αβχδεφγηιϕκλμνοπθρστυϖωξψζ";

    private static readonly Dictionary<int, char> SymbolSpecials = new()
    {
        [0xB7] = '•', [0xB4] = '×', [0xB8] = '÷', [0xB3] = '≥', [0xA3] = '≤', [0xB9] = '≠', [0xA5] = '∞', [0xB1] = '±',
        [0xD6] = '√', [0xAE] = '→', [0xAC] = '←', [0xAD] = '↑', [0xAF] = '↓', [0xAB] = '↔', [0xA8] = '♦', [0xA7] = '♣',
        [0xA9] = '♥', [0xAA] = '♠', [0xB0] = '°', [0xE0] = '◊', [0xBB] = '≈', [0xBA] = '≡', [0xD7] = '·',
    };

    private static readonly Dictionary<int, char> Wingdings = new()
    {
        [0x6C] = '●', [0x6E] = '■', [0x6F] = '□', [0x71] = '□', [0x72] = '□', [0x75] = '◆', [0x76] = '◆', [0x77] = '◆',
        [0x9F] = '•', [0xA1] = '○', [0xA7] = '▪', [0xA8] = '□', [0xD8] = '►', [0xE0] = '→', [0xE8] = '→', [0xF0] = '→',
        [0xFC] = '√', [0xFB] = '×', [0xFE] = '√', [0xA0] = '▪', [0x4A] = '☺',
    };

    // The Symbol code each drawn character stands for: the code of its look-alike entry (which wins: Symbol 0xB8
    // draws ÷, whose own code 0xF7 is another Symbol character), else the code passed through unchanged.
    private static readonly Lazy<Dictionary<char, int>> SymbolCodes = new(() =>
    {
        var codes = new Dictionary<char, int>();
        var unmapped = Enumerable.Range(0x20, 0xE0).Where(code => MapSymbol(code) is null);
        foreach (var code in unmapped)
        {
            codes[(char)code] = code;
        }

        foreach (var code in Enumerable.Range(0x20, 0xE0).Except(unmapped))
        {
            codes[MapSymbol(code)!.Value] = code;
        }

        return codes;
    });

    public static bool IsSymbolFont(string? font) =>
        font is not null && (IsSymbol(font) || font.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase) || font.StartsWith("Webdings", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The style to draw a symbol font's text with: Symbol text in <see cref="SymbolFace"/> advancing by the
    /// Symbol widths, Wingdings and Webdings text in <see cref="DingbatFace"/> on a <see cref="SymbolFace"/> line.
    /// Any other font leaves the style unchanged.
    /// </summary>
    public static TextStyle Style(string? symbolFont, TextStyle style)
    {
        if (!IsSymbolFont(symbolFont))
        {
            return style;
        }

        var text = style.Font with { Family = SymbolFace };
        return IsSymbol(symbolFont)
            ? style with { Font = text, SymbolAdvances = true }
            : style with { Font = style.Font with { Family = DingbatFace }, LineFont = text };
    }

    /// <summary>The Symbol font's advance, in thousandths of an em, of a character <see cref="Map"/> produced
    /// from Symbol text; null when the Symbol font has no such character.</summary>
    public static int? SymbolAdvance(char drawn) =>
        SymbolCodes.Value.TryGetValue(drawn, out var code) ? SymbolMetrics.Advance(code) : null;

    /// <summary>The text with symbol-font characters replaced by look-alikes.</summary>
    public static string Map(string text, string? font, ISet<string> gaps)
    {
        var symbol = IsSymbol(font);
        var dingbats = IsSymbolFont(font) && !symbol;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var code = c is >= '' and <= '' ? c - 0xF000 : c;
            var mapped = symbol ? MapSymbol(code) : dingbats ? MapDingbat(code) : (char?)null;
            if (mapped is null && c is >= '' and <= '')
            {
                gaps.Add("symbol font characters approximated");
                mapped = (char)code;
            }

            builder.Append(mapped ?? c);
        }

        return builder.ToString();
    }

    private static bool IsSymbol(string? font) => font?.Equals("Symbol", StringComparison.OrdinalIgnoreCase) == true;

    private static char? MapSymbol(int code) => code switch
    {
        >= 0x41 and <= 0x5A => SymbolUpper[code - 0x41],
        >= 0x61 and <= 0x7A => SymbolLower[code - 0x61],
        _ => SymbolSpecials.TryGetValue(code, out var special) ? special : null,
    };

    private static char? MapDingbat(int code) => Wingdings.TryGetValue(code, out var mapped) ? mapped : '•';
}
