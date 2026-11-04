// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordLayout;

/// <summary>Draws freehand ink as vector strokes: each stroke is an open path with the pen's width, colour and round ends.</summary>
internal static class InkPainter
{
    /// <summary>Draws the strokes of <paramref name="ink"/> with the top-left corner of its box at the given point.</summary>
    public static void Paint(PdfCanvas canvas, WordInk ink, double x, double y)
    {
        foreach (var stroke in ink.Strokes)
        {
            var points = stroke.Points.Select(p => (x + p.X, y + p.Y)).ToList();
            canvas.StrokePen(points, PdfColor.FromHex(stroke.Color), stroke.Width, stroke.Opacity);
        }
    }
}
