// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// CSS declarations for resolved Word formatting. Only values the converter builds go into a declaration:
/// numbers, colours checked to be six hexadecimal digits, keywords, and font names stripped of everything
/// but letters, digits, spaces, hyphens, dots and underscores.
/// </summary>
internal static class HtmlCss
{
    /// <summary>The line height of single spacing, as a multiple of the font size.</summary>
    public const double SingleLine = 1.15;

    /// <summary>Declarations joined into a <c>style</c> attribute value; null when there are none.</summary>
    public static string? Join(IEnumerable<string> declarations)
    {
        var text = string.Join(';', declarations);
        return text.Length == 0 ? null : text;
    }

    public static string Points(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    public static string Number(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary><c>#RRGGBB</c> for a colour written as six hexadecimal digits; null otherwise (<c>auto</c>...).</summary>
    public static string? Color(string? hex) => TextStyle.ParseColor(hex) is { } color ? Color(color) : null;

    public static string Color(PdfColor color) => string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");

    /// <summary>A quoted font family with a generic fallback; null when nothing of the name is kept.</summary>
    public static string? FontFamily(string? font)
    {
        if (font is null)
        {
            return null;
        }

        var builder = new StringBuilder(font.Length);
        foreach (var c in font)
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '-' or '.' or '_')
            {
                builder.Append(c);
            }
        }

        var name = builder.ToString().Trim();
        return name.Length == 0 ? null : "'" + name + "',sans-serif";
    }

    public static IEnumerable<string> Paragraph(WordParagraphProperties p)
    {
        if (Alignment(p.Alignment) is { } align)
        {
            yield return "text-align:" + align;
        }

        foreach (var (name, value) in new[] { ("margin-left", p.IndentLeft), ("margin-right", p.IndentRight), ("text-indent", p.FirstLineIndent) })
        {
            if (value is { } amount && amount != 0)
            {
                yield return name + ":" + Points(amount);
            }
        }

        if (LineHeight(p.LineSpacing, p.LineSpacingRule) is { } height)
        {
            yield return "line-height:" + height;
        }

        if (Color(p.Shading) is { } shading)
        {
            yield return "background-color:" + shading;
        }

        foreach (var declaration in Decorations(p))
        {
            yield return declaration;
        }
    }

    private static IEnumerable<string> Decorations(WordParagraphProperties p)
    {
        var borders = p.Borders;
        foreach (var (side, border) in new[] { ("top", borders?.Top), ("left", borders?.Left), ("bottom", borders?.Bottom), ("right", borders?.Right) })
        {
            if (Border(border) is { } line)
            {
                yield return "border-" + side + ":" + line;
            }
        }

        if (p.PageBreakBefore == true)
        {
            yield return "break-before:page";
        }

        if (p.RightToLeft == true)
        {
            yield return "direction:rtl";
        }
    }

    public static string? Alignment(WordAlignment? alignment) => alignment switch
    {
        WordAlignment.Center => "center",
        WordAlignment.Right => "right",
        WordAlignment.Justify or WordAlignment.Distribute => "justify",
        _ => null,
    };

    private static string? LineHeight(double? spacing, WordLineSpacingRule? rule) => (spacing, rule) switch
    {
        (null, _) => null,
        ({ } value, WordLineSpacingRule.Exact) => Points(value),
        ({ } value, WordLineSpacingRule.AtLeast) => "max(" + Points(value) + "," + Number(SingleLine) + "em)",
        ({ } value, _) => Number(value * SingleLine),
    };

    /// <summary>A border shorthand (<c>0.5pt solid #000000</c>); null for no line.</summary>
    public static string? Border(WordBorder? border)
    {
        if (border is null || border.IsNone)
        {
            return null;
        }

        var style = border.Style switch
        {
            "double" => "double",
            "dotted" => "dotted",
            "dashed" or "dashSmallGap" or "dotDash" or "dotDotDash" => "dashed",
            _ => "solid",
        };
        return Points(Math.Max(border.Width, 0.25)) + " " + style + " " + (Color(border.Color) ?? "#000000");
    }

    /// <summary>The run declarations that differ from the paragraph's own run formatting.</summary>
    public static IEnumerable<string> Run(WordRunProperties run, WordRunProperties paragraph)
    {
        if (run.Font != paragraph.Font && !Symbols.IsSymbolFont(run.Font) && FontFamily(run.Font) is { } family)
        {
            yield return "font-family:" + family;
        }

        if (run.FontSize != paragraph.FontSize && run.FontSize is { } size)
        {
            yield return "font-size:" + Points(size);
        }

        if (run.Color != paragraph.Color && Color(run.Color) is { } color)
        {
            yield return "color:" + color;
        }

        foreach (var declaration in Effects(run))
        {
            yield return declaration;
        }
    }

    private static IEnumerable<string> Effects(WordRunProperties run)
    {
        if (TextStyle.BackgroundOf(run) is { } background)
        {
            yield return "background-color:" + Color(background);
        }

        if (run.CharacterSpacing is { } spacing && spacing != 0)
        {
            yield return "letter-spacing:" + Points(spacing);
        }

        if (run.Caps == true)
        {
            yield return "text-transform:uppercase";
        }
        else if (run.SmallCaps == true)
        {
            yield return "font-variant:small-caps";
        }

        if (run.Position is { } position && position != 0)
        {
            yield return "vertical-align:" + Points(position);
        }
    }

    /// <summary>The declarations a paragraph takes from its own run formatting (font, size, colour).</summary>
    public static IEnumerable<string> ParagraphFont(WordRunProperties run)
    {
        if (!Symbols.IsSymbolFont(run.Font) && FontFamily(run.Font) is { } family)
        {
            yield return "font-family:" + family;
        }

        if (run.FontSize is { } size)
        {
            yield return "font-size:" + Points(size);
        }

        if (Color(run.Color) is { } color)
        {
            yield return "color:" + color;
        }
    }

    /// <summary>The <c>text-decoration-style</c> of an underline that is not a plain single line.</summary>
    public static string? UnderlineStyle(WordUnderline underline) => underline switch
    {
        WordUnderline.Double => "text-decoration-style:double",
        WordUnderline.Dotted => "text-decoration-style:dotted",
        WordUnderline.Dash => "text-decoration-style:dashed",
        WordUnderline.Wave => "text-decoration-style:wavy",
        WordUnderline.Thick => "text-decoration-thickness:2px",
        _ => null,
    };
}
