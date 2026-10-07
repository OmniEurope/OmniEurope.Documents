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
    /// raised by a third of it or lowered by a tenth.
    /// </summary>
    public static TextStyle From(WordRunProperties p, string? link = null)
    {
        var size = p.FontSize ?? 10;
        var shift = p.Position ?? 0;
        if (p.VerticalPosition is WordVerticalPosition.Superscript or WordVerticalPosition.Subscript)
        {
            shift += p.VerticalPosition == WordVerticalPosition.Superscript ? size / 3 : -size / 10;
            size *= 2.0 / 3;
        }

        var font = new PdfFont(p.Font ?? "Times New Roman", p.Bold == true, p.Italic == true);
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
