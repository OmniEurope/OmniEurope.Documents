// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// The drawing surface of one page. Coordinates are in points with the origin at the top-left corner and
/// y growing downwards; text is placed by its baseline.
/// </summary>
public sealed class PdfCanvas
{
    private readonly PdfDocumentBuilder _document;
    private readonly ContentStreamBuilder _content = new();
    private readonly HashSet<EmbeddedFont> _fonts = [];
    private readonly HashSet<PdfImage> _images = [];
    private readonly SortedDictionary<int, string> _opacities = [];
    private readonly List<PdfDictionary> _annotations = [];
    private int _stateDepth;

    internal PdfCanvas(PdfDocumentBuilder document, double width, double height)
    {
        _document = document;
        Width = width;
        Height = height;
    }

    /// <summary>Page width in points.</summary>
    public double Width { get; }

    /// <summary>Page height in points.</summary>
    public double Height { get; }

    /// <summary>The 1-based number of this page.</summary>
    public int Number => _document.Pages.ToList().IndexOf(this) + 1;

    /// <summary>The width of <paramref name="text"/> in points.</summary>
    public double MeasureText(string text, PdfFont font, double size, double characterSpacing = 0) =>
        _document.MeasureText(text, font, size, characterSpacing);

    /// <summary>The vertical metrics of a font at a size.</summary>
    public PdfFontMetrics Metrics(PdfFont font, double size) => _document.Metrics(font, size);

    /// <summary>Draws one line of text with its baseline at <paramref name="baseline"/>; returns its width.
    /// Line breaks and control characters are not drawn. An <paramref name="opacity"/> below 1 makes it translucent.</summary>
    public double DrawText(string text, double x, double baseline, PdfFont font, double size, PdfColor? color = null,
        double characterSpacing = 0, bool underline = false, bool strikethrough = false, double opacity = 1)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var fill = color ?? PdfColor.Black;
        var start = x;
        _content.SaveState();
        Opacity(opacity);
        foreach (var (face, run) in Runs(text, font))
        {
            x += DrawRun(face, run, (1, 0, x, Y(baseline)), size, fill, characterSpacing);
        }

        _content.RestoreState();

        var width = x - start;
        if (underline || strikethrough)
        {
            var metrics = Metrics(font, size);
            if (underline)
            {
                FillRectangle(start, baseline + metrics.UnderlinePosition - (metrics.UnderlineThickness / 2), width, metrics.UnderlineThickness, fill);
            }

            if (strikethrough)
            {
                FillRectangle(start, baseline - (size * 0.28) - (metrics.UnderlineThickness / 2), width, metrics.UnderlineThickness, fill);
            }
        }

        return width;
    }

    /// <summary>Draws one line of text turned by <paramref name="angle"/> degrees counter-clockwise around the start
    /// of its baseline (<paramref name="x"/>, <paramref name="baseline"/>): 90 reads from bottom to top, 270 from top
    /// to bottom. Returns its width along the baseline. Line breaks and control characters are not drawn.</summary>
    public double DrawRotatedText(string text, double x, double baseline, double angle, PdfFont font, double size, PdfColor? color = null,
        double characterSpacing = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var radians = angle * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        double width = 0;
        _content.SaveState();
        foreach (var (face, run) in Runs(text, font))
        {
            width += DrawRun(face, run, (cos, sin, x + (width * cos), Y(baseline) + (width * sin)), size, color ?? PdfColor.Black, characterSpacing);
        }

        _content.RestoreState();
        return width;
    }

    /// <summary>Draws one line of text inside a box: aligned horizontally, vertically centred on the box (or
    /// top-aligned when <paramref name="height"/> is 0).</summary>
    public double DrawTextInBox(string text, double x, double y, double width, double height, PdfFont font, double size,
        PdfColor? color = null, PdfTextAlignment alignment = PdfTextAlignment.Left)
    {
        var metrics = Metrics(font, size);
        var textWidth = MeasureText(text, font, size);
        var left = alignment switch
        {
            PdfTextAlignment.Center => x + ((width - textWidth) / 2),
            PdfTextAlignment.Right => x + width - textWidth,
            _ => x,
        };
        var top = height > 0 ? y + ((height - metrics.LineHeight) / 2) : y;
        return DrawText(text, left, top + metrics.Ascent, font, size, color);
    }

    /// <summary>Draws a straight line.</summary>
    public void DrawLine(double x1, double y1, double x2, double y2, PdfColor color, double lineWidth = 1, double[]? dash = null)
    {
        _content.SaveState().StrokeColor(color).Op("w", lineWidth);
        Dash(dash);
        _content.Op("m", x1, Y(y1)).Op("l", x2, Y(y2)).Op("S").RestoreState();
    }

    /// <summary>Fills a rectangle; <paramref name="opacity"/> below 1 makes it translucent.</summary>
    public void FillRectangle(double x, double y, double width, double height, PdfColor color, double opacity = 1)
    {
        _content.SaveState();
        Opacity(opacity);
        _content.FillColor(color).Op("re", x, Y(y + height), width, height).Op("f").RestoreState();
    }

    /// <summary>Strokes the outline of a rectangle.</summary>
    public void StrokeRectangle(double x, double y, double width, double height, PdfColor color, double lineWidth = 1, double[]? dash = null)
    {
        _content.SaveState().StrokeColor(color).Op("w", lineWidth);
        Dash(dash);
        _content.Op("re", x, Y(y + height), width, height).Op("S").RestoreState();
    }

    /// <summary>Fills a polygon (non-zero winding, or even-odd).</summary>
    public void FillPolygon(IReadOnlyList<(double X, double Y)> points, PdfColor color, bool evenOdd = false, double opacity = 1)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return;
        }

        _content.SaveState();
        Opacity(opacity);
        _content.FillColor(color);
        Path(points, close: true);
        _content.Op(evenOdd ? "f*" : "f").RestoreState();
    }

    /// <summary>Strokes a polyline, closed or open.</summary>
    public void StrokePolyline(IReadOnlyList<(double X, double Y)> points, PdfColor color, double lineWidth = 1, bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            return;
        }

        _content.SaveState().StrokeColor(color).Op("w", lineWidth).Op("j", 1);
        Path(points, closed);
        _content.Op("S").RestoreState();
    }

    /// <summary>
    /// Fills and/or strokes a path made of several polygons (holes are cut by the even-odd rule or by
    /// opposite winding). Subpaths are closed when <paramref name="closed"/> is set.
    /// </summary>
    public void DrawPath(IReadOnlyList<IReadOnlyList<(double X, double Y)>> subpaths, PdfColor? fill, PdfColor? stroke, double lineWidth = 1, bool evenOdd = false, bool closed = true)
    {
        ArgumentNullException.ThrowIfNull(subpaths);
        var usable = subpaths.Where(s => s.Count >= 2).ToList();
        if (usable.Count == 0 || (fill is null && stroke is null))
        {
            return;
        }

        _content.SaveState();
        if (fill is { } fillColor)
        {
            _content.FillColor(fillColor);
        }

        if (stroke is { } strokeColor)
        {
            _content.StrokeColor(strokeColor).Op("w", lineWidth).Op("j", 1);
        }

        foreach (var subpath in usable)
        {
            Path(subpath, closed);
        }

        var paint = (fill is not null, stroke is not null) switch
        {
            (true, true) => evenOdd ? "B*" : "B",
            (true, false) => evenOdd ? "f*" : "f",
            _ => "S",
        };
        _content.Op(paint).RestoreState();
    }

    /// <summary>Draws an image scaled into the rectangle.</summary>
    public void DrawImage(PdfImage image, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(image);
        _images.Add(image);
        _content.SaveState().Op("cm", width, 0, 0, height, x, Y(y + height)).Name(image.ResourceName).Raw("Do\n").RestoreState();
    }

    /// <summary>Makes a rectangle a link to <paramref name="uri"/>.</summary>
    public void AddLink(double x, double y, double width, double height, string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        _annotations.Add(new PdfDictionary()
            .SetName("Type", "Annot").SetName("Subtype", "Link")
            .Set("Rect", PdfArray.OfNumbers(x, Y(y + height), x + width, Y(y)))
            .Set("Border", PdfArray.OfNumbers(0, 0, 0))
            .Set("A", new PdfDictionary().SetName("S", "URI").Set("URI", new PdfString(Encoding.ASCII.GetBytes(uri)))));
    }

    /// <summary>Saves the graphics state (clip, opacity); pair with <see cref="RestoreState"/>.</summary>
    public void SaveState()
    {
        _stateDepth++;
        _content.SaveState();
    }

    /// <summary>Restores the state saved by <see cref="SaveState"/>.</summary>
    public void RestoreState()
    {
        if (_stateDepth == 0)
        {
            throw new InvalidOperationException("RestoreState without a matching SaveState.");
        }

        _stateDepth--;
        _content.RestoreState();
    }

    /// <summary>Restricts drawing to a rectangle until the next <see cref="RestoreState"/>.</summary>
    public void ClipRectangle(double x, double y, double width, double height) =>
        _content.Op("re", x, Y(y + height), width, height).Op("W").Op("n");

    internal PdfReference WriteObjects(PdfObjectTable table, PdfReference parent)
    {
        var page = new PdfDictionary()
            .SetName("Type", "Page")
            .Set("Parent", parent)
            .Set("MediaBox", PdfArray.OfNumbers(0, 0, Width, Height))
            .Set("Resources", BuildResources())
            .Set("Contents", table.Add(EmbeddedFont.Stream(FinishContent())));
        if (_annotations.Count > 0)
        {
            page.Set("Annots", new PdfArray(_annotations.Select(a => (PdfObject)table.Add(a))));
        }

        return table.Add(page);
    }

    /// <summary>The content stream, with any unbalanced saved state closed.</summary>
    internal byte[] FinishContent()
    {
        while (_stateDepth > 0)
        {
            RestoreState();
        }

        return _content.ToBytes();
    }

    internal IReadOnlyList<PdfDictionary> Annotations => _annotations;

    /// <summary>The fonts, images and graphics states this canvas uses.</summary>
    internal PdfDictionary BuildResources()
    {
        var resources = new PdfDictionary();
        if (_fonts.Count > 0)
        {
            var fonts = new PdfDictionary();
            foreach (var font in _fonts.OrderBy(f => f.ResourceName, StringComparer.Ordinal))
            {
                fonts.Set(font.ResourceName, font.Reference);
            }

            resources.Set("Font", fonts);
        }

        if (_images.Count > 0)
        {
            var images = new PdfDictionary();
            foreach (var image in _images.OrderBy(i => i.ResourceName, StringComparer.Ordinal))
            {
                images.Set(image.ResourceName, image.Reference);
            }

            resources.Set("XObject", images);
        }

        if (_opacities.Count > 0)
        {
            var states = new PdfDictionary();
            foreach (var (percent, name) in _opacities)
            {
                states.Set(name, new PdfDictionary().SetNumber("ca", percent / 1000.0).SetNumber("CA", percent / 1000.0));
            }

            resources.Set("ExtGState", states);
        }

        return resources;
    }

    private double Y(double y) => Height - y;

    private IEnumerable<(TrueTypeFont Face, List<(int Glyph, string Text)> Run)> Runs(string text, PdfFont font)
    {
        TrueTypeFont? current = null;
        var run = new List<(int, string)>();
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value == '\t' ? ' ' : rune.Value;
            if (value < 0x20 || value == 0x7F)
            {
                continue;
            }

            var face = _document.Face(font, value);
            if (current is not null && !ReferenceEquals(face, current))
            {
                yield return (current, run);
                run = [];
            }

            current = face;
            run.Add((face.GetGlyphIndex(value), rune.ToString()));
        }

        if (current is not null && run.Count > 0)
        {
            yield return (current, run);
        }
    }

    // Draws a run of one face from an origin in PDF space, its baseline along the direction (cos, sin).
    private double DrawRun(TrueTypeFont face, List<(int Glyph, string Text)> run, (double Cos, double Sin, double X, double Y) origin, double size, PdfColor color, double characterSpacing)
    {
        var font = _document.Embed(face);
        _fonts.Add(font);
        var codes = new byte[run.Count * 2];
        double width = 0;
        for (var i = 0; i < run.Count; i++)
        {
            var (glyph, text) = run[i];
            font.Use(glyph, text);
            codes[i * 2] = (byte)(glyph >> 8);
            codes[(i * 2) + 1] = (byte)glyph;
            width += (face.GetAdvanceWidth(glyph) * size / face.UnitsPerEm) + characterSpacing;
        }

        _content.Op("BT").FillColor(color).Name(font.ResourceName).Op("Tf", size);
        if (characterSpacing != 0)
        {
            _content.Op("Tc", characterSpacing);
        }

        _content.Op("Tm", origin.Cos, origin.Sin, -origin.Sin, origin.Cos, origin.X, origin.Y).Hex(codes).Raw("Tj\nET\n");
        return width;
    }

    private void Opacity(double opacity)
    {
        if (opacity >= 1)
        {
            return;
        }

        var key = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 1000);
        if (!_opacities.TryGetValue(key, out var name))
        {
            name = _document.ResourcePrefix + "GS" + key;
            _opacities.Add(key, name);
        }

        _content.Name(name).Raw("gs\n");
    }

    private void Dash(double[]? dash)
    {
        if (dash is { Length: > 0 })
        {
            _content.Raw("[" + string.Join(' ', dash.Select(PdfFormat.Real)) + "] 0 d\n");
        }
    }

    private void Path(IReadOnlyList<(double X, double Y)> points, bool close)
    {
        _content.Op("m", points[0].X, Y(points[0].Y));
        for (var i = 1; i < points.Count; i++)
        {
            _content.Op("l", points[i].X, Y(points[i].Y));
        }

        if (close)
        {
            _content.Op("h");
        }
    }
}
