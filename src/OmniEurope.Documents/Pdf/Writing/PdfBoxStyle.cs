// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>How <see cref="PdfFlowLayout.AddBox"/> draws a box: padding, an optional fill and an optional border on
/// all four sides (never a single side), and where the box and its text sit.</summary>
public sealed class PdfBoxStyle
{
    /// <summary>Space between the text and the edge of the box on every side, in points. Default 6.</summary>
    public double Padding { get; init; } = 6;

    /// <summary>Background of the box; none when null.</summary>
    public PdfColor? Fill { get; init; }

    /// <summary>Colour of the frame drawn on all four sides; no frame when null.</summary>
    public PdfColor? Border { get; init; }

    /// <summary>Line width of the frame. Default 0.5.</summary>
    public double BorderWidth { get; init; } = 0.5;

    /// <summary>Colour of the text; the default text colour when null.</summary>
    public PdfColor? TextColor { get; init; }

    /// <summary>Width of the box in points (reduced to the content width when larger). When null the box is as wide as
    /// its longest line of text plus the padding, wrapping at the content width: a badge.</summary>
    public double? Width { get; init; }

    /// <summary>Where the box sits in the content width. Default left.</summary>
    public PdfTextAlignment BoxAlignment { get; init; }

    /// <summary>Alignment of the text inside the box; only visible when the box is wider than its text or the text has
    /// several lines. Default left.</summary>
    public PdfTextAlignment TextAlignment { get; init; }
}
