// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Resolved character formatting ready to draw: font, size, colours and decorations.</summary>
internal sealed record TextStyle(PdfFont Font, double Size, PdfColor Color)
{
    /// <summary>Underline style (None when not underlined).</summary>
    public WordUnderline Underline { get; init; }

    public bool Strike { get; init; }

    public bool DoubleStrike { get; init; }

    /// <summary>Baseline shift upwards, in points (superscript, raised position).</summary>
    public double Shift { get; init; }

    /// <summary>Highlight or shading behind the text.</summary>
    public PdfColor? Background { get; init; }

    public double CharacterSpacing { get; init; }

    public bool Caps { get; init; }

    public bool SmallCaps { get; init; }

    public bool Hidden { get; init; }

    /// <summary>External link target of the text, if any.</summary>
    public string? Link { get; init; }

    /// <summary>The font whose vertical metrics lay the text out, when it is not <see cref="Font"/>: a symbol font
    /// drawn with a stand-in face whose own line is taller keeps the line of a text face.</summary>
    public PdfFont? LineFont { get; init; }

    /// <summary>The font the line height and decorations are measured with.</summary>
    public PdfFont MetricsFont => LineFont ?? Font;

    /// <summary>The text is Symbol-font text drawn with look-alikes: its advances are the Symbol font's.</summary>
    public bool SymbolAdvances { get; init; }

    /// <summary>The run is right to left (<c>w:rtl</c>): it is drawn with the complex script font and size, and its
    /// neutral characters count as right to left in the bidirectional algorithm.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>The style of complex script text (Hebrew, Arabic...) in this run when its complex script font or size
    /// differs; null when it is this style.</summary>
    public TextStyle? ComplexScript { get; init; }

    /// <summary>The style to draw complex script characters with.</summary>
    public TextStyle ForComplexScript() => ComplexScript ?? this;

    private static readonly Dictionary<string, PdfColor> Highlights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = new(255, 255, 0),
        ["green"] = new(0, 255, 0),
        ["cyan"] = new(0, 255, 255),
        ["magenta"] = new(255, 0, 255),
        ["blue"] = new(0, 0, 255),
        ["red"] = new(255, 0, 0),
        ["darkBlue"] = new(0, 0, 128),
        ["darkCyan"] = new(0, 128, 128),
        ["darkGreen"] = new(0, 128, 0),
        ["darkMagenta"] = new(128, 0, 128),
        ["darkRed"] = new(128, 0, 0),
        ["darkYellow"] = new(128, 128, 0),
        ["darkGray"] = new(128, 128, 128),
        ["lightGray"] = new(192, 192, 192),
        ["black"] = new(0, 0, 0),
        ["white"] = new(255, 255, 255),
    };

    /// <summary>
    /// The style of fully resolved run properties. Without a size the run is 10 pt and without a font
    /// Times New Roman, as in Word. Superscript and subscript text is drawn at two thirds of the size,
    /// raised by a third of it or lowered by a tenth. A right-to-left run takes the complex script font and size.
    /// </summary>
    public static TextStyle From(WordRunProperties p, string? link = null)
    {
        if (p.RightToLeft == true)
        {
            return Build(p, p.FontComplex ?? p.Font, p.FontSizeComplex ?? p.FontSize, link) with { RightToLeft = true };
        }

        var style = Build(p, p.Font, p.FontSize, link);
        var complexFont = p.FontComplex ?? p.Font;
        var complexSize = p.FontSizeComplex ?? p.FontSize;
        return complexFont == p.Font && complexSize == p.FontSize ? style : style with { ComplexScript = Build(p, complexFont, complexSize, link) };
    }

    private static TextStyle Build(WordRunProperties p, string? family, double? fontSize, string? link)
    {
        var size = fontSize ?? 10;
        var shift = p.Position ?? 0;
        if (p.VerticalPosition is WordVerticalPosition.Superscript or WordVerticalPosition.Subscript)
        {
            shift += p.VerticalPosition == WordVerticalPosition.Superscript ? size / 3 : -size / 10;
            size *= 2.0 / 3;
        }

        var font = new PdfFont(family ?? "Times New Roman", p.Bold == true, p.Italic == true);
        return new TextStyle(font, Math.Max(size, 1), ParseColor(p.Color) ?? PdfColor.Black)
        {
            Underline = p.Underline ?? WordUnderline.None,
            Strike = p.Strike == true,
            DoubleStrike = p.DoubleStrike == true,
            Shift = shift,
            Background = BackgroundOf(p),
            CharacterSpacing = p.CharacterSpacing ?? 0,
            Caps = p.Caps == true,
            SmallCaps = p.SmallCaps == true && p.Caps != true,
            Hidden = p.Hidden == true,
            Link = link,
        };
    }

    /// <summary>The highlight colour of the run, else its shading; null when neither is set.</summary>
    public static PdfColor? BackgroundOf(WordRunProperties p) =>
        p.Highlight is { } name && name != "none" && Highlights.TryGetValue(name, out var highlight) ? highlight : ParseColor(p.Shading);

    /// <summary>A colour written as <c>RRGGBB</c>; null for <c>auto</c> or anything else.</summary>
    public static PdfColor? ParseColor(string? hex)
    {
        if (hex is null || hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return new PdfColor((byte)(value >> 16), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
    }
}
