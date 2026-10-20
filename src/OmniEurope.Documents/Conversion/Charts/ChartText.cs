// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Excel;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Conversion.Charts;

/// <summary>A rectangle of the page, in points from its top-left corner.</summary>
internal readonly record struct ChartBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public ChartBox Inset(double left, double top, double right, double bottom) =>
        new(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));
}

/// <summary>The text of a chart: the theme's body font (Calibri) in the dark grey of the chart styles, measured and
/// drawn through the layout so that substituted fonts are reported like the text's.</summary>
internal sealed class ChartText(LayoutContext layout, PdfCanvas canvas, CultureInfo culture)
{
    public const double LabelSize = 9;
    public const double TitleSize = 14;
    public const double AxisTitleSize = 10;

    public static readonly PdfColor Gray = new(0x59, 0x59, 0x59);

    public PdfCanvas Canvas { get; } = canvas;

    public CultureInfo Culture { get; } = culture;

    public double Width(string text, double size) => layout.Measure(text, Style(size));

    public double LineHeight(double size) => layout.Metrics(Style(size)).LineHeight;

    /// <summary>Draws one line with its top at <paramref name="top"/>, its start at <paramref name="x"/>.</summary>
    public void Draw(string text, double x, double top, double size, PdfColor? color = null)
    {
        var style = Style(size);
        Canvas.DrawText(text, x, top + layout.Metrics(style).Ascent, style.Font, size, color ?? Gray);
    }

    /// <summary>Draws one line centred on <paramref name="centerX"/>.</summary>
    public void Centered(string text, double centerX, double top, double size, PdfColor? color = null) =>
        Draw(text, centerX - (Width(text, size) / 2), top, size, color);

    /// <summary>Draws one line centred on a point, turned to read from bottom to top.</summary>
    public void Rotated(string text, double centerX, double centerY, double size)
    {
        var style = Style(size);
        var metrics = layout.Metrics(style);
        // Turned a quarter left, the glyphs rise towards smaller x: the baseline stands right of the centre.
        var baseline = centerX + ((metrics.Ascent - metrics.Descent) / 2);
        Canvas.DrawRotatedText(text, baseline, centerY + (Width(text, size) / 2), 90, style.Font, size, Gray);
    }

    /// <summary>A value in a number format, in the culture of the conversion.</summary>
    public string Format(double value, string format) => NumberFormatter.Format(value, format, Culture);

    public static PdfColor Color(string hex) => TextStyle.ParseColor(hex) ?? PdfColor.Black;

    private static TextStyle Style(double size) => new(new PdfFont("Calibri"), size, Gray);
}
