// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>
/// Characters of the Symbol and Wingdings fonts (which are not bundled) drawn with Unicode look-alikes
/// that the bundled fonts carry. Symbol-font text is addressed either directly or in the private use area
/// (U+F000 + code).
/// </summary>
internal static class Symbols
{
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

    public static bool IsSymbolFont(string? font) =>
        font is not null && (font.Equals("Symbol", StringComparison.OrdinalIgnoreCase) || font.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase) || font.StartsWith("Webdings", StringComparison.OrdinalIgnoreCase));

    /// <summary>The font to draw a symbol with: a bundled sans face instead of a symbol font.</summary>
    public static PdfFont Font(string? symbolFont, PdfFont current) => IsSymbolFont(symbolFont) ? current with { Family = "Liberation Sans" } : current;

    /// <summary>The text with symbol-font characters replaced by look-alikes.</summary>
    public static string Map(string text, string? font, LayoutContext context)
    {
        var symbol = font?.Equals("Symbol", StringComparison.OrdinalIgnoreCase) == true;
        var dingbats = IsSymbolFont(font) && !symbol;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var code = c is >= '' and <= '' ? c - 0xF000 : c;
            var mapped = symbol ? MapSymbol(code) : dingbats ? MapDingbat(code) : (char?)null;
            if (mapped is null && c is >= '' and <= '')
            {
                context.Gaps.Add("symbol font characters approximated");
                mapped = (char)code;
            }

            builder.Append(mapped ?? c);
        }

        return builder.ToString();
    }

    private static char? MapSymbol(int code) => code switch
    {
        >= 0x41 and <= 0x5A => SymbolUpper[code - 0x41],
        >= 0x61 and <= 0x7A => SymbolLower[code - 0x61],
        _ => SymbolSpecials.TryGetValue(code, out var special) ? special : null,
    };

    private static char? MapDingbat(int code) => Wingdings.TryGetValue(code, out var mapped) ? mapped : '•';
}
