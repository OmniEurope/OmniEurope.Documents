// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// A font request: family and style. The document resolves it through its <see cref="Fonts.FontLibrary"/>
/// character by character, so text the family cannot show falls back to a bundled face instead of boxes.
/// </summary>
/// <param name="Family">Family name (Arial, Calibri, Liberation Serif...).</param>
/// <param name="Bold">Bold face.</param>
/// <param name="Italic">Italic face.</param>
public sealed record PdfFont(string Family, bool Bold = false, bool Italic = false)
{
    /// <summary>Liberation Sans (Arial metrics).</summary>
    public static PdfFont Sans { get; } = new("Liberation Sans");

    /// <summary>Liberation Serif (Times New Roman metrics).</summary>
    public static PdfFont Serif { get; } = new("Liberation Serif");

    /// <summary>Liberation Mono (Courier New metrics).</summary>
    public static PdfFont Mono { get; } = new("Liberation Mono");

    /// <summary>The same family in bold.</summary>
    public PdfFont AsBold() => this with { Bold = true };

    /// <summary>The same family in italic.</summary>
    public PdfFont AsItalic() => this with { Italic = true };
}

/// <summary>Vertical metrics of a font at a size, in points.</summary>
/// <param name="Ascent">Distance from the baseline to the top of the line box.</param>
/// <param name="Descent">Distance from the baseline to the bottom of the line box (positive).</param>
/// <param name="LineHeight">Single line spacing (ascent + descent, as word processors compute it).</param>
/// <param name="CapHeight">Height of capital letters.</param>
/// <param name="UnderlinePosition">Offset of the underline below the baseline (positive).</param>
/// <param name="UnderlineThickness">Underline thickness.</param>
public readonly record struct PdfFontMetrics(double Ascent, double Descent, double LineHeight, double CapHeight, double UnderlinePosition, double UnderlineThickness);

/// <summary>Standard page sizes in points (1/72 inch), portrait.</summary>
public static class PdfPageSize
{
    /// <summary>A4, 210 x 297 mm.</summary>
    public static (double Width, double Height) A4 { get; } = (595.28, 841.89);

    /// <summary>A3, 297 x 420 mm.</summary>
    public static (double Width, double Height) A3 { get; } = (841.89, 1190.55);

    /// <summary>A5, 148 x 210 mm.</summary>
    public static (double Width, double Height) A5 { get; } = (419.53, 595.28);

    /// <summary>US Letter, 8.5 x 11 in.</summary>
    public static (double Width, double Height) Letter { get; } = (612, 792);

    /// <summary>US Legal, 8.5 x 14 in.</summary>
    public static (double Width, double Height) Legal { get; } = (612, 1008);

    /// <summary>Millimetres to points.</summary>
    public static double FromMillimetres(double millimetres) => millimetres * 72 / 25.4;
}

/// <summary>Horizontal alignment of text in a box.</summary>
public enum PdfTextAlignment
{
    /// <summary>Left.</summary>
    Left,

    /// <summary>Centred.</summary>
    Center,

    /// <summary>Right.</summary>
    Right,

    /// <summary>Justified (last line left aligned).</summary>
    Justify,
}
