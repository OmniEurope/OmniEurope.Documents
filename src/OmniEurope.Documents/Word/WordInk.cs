// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word;

/// <summary>One pen stroke of a <see cref="WordInk"/>: the sampled positions of the pen and the pen itself.</summary>
public sealed class WordInkStroke
{
    /// <summary>A stroke through the given points, in points from the top-left corner of the ink's box.</summary>
    /// <param name="points">The sampled positions, x to the right and y downwards.</param>
    /// <param name="width">The pen width in points.</param>
    /// <param name="color">The pen colour as six hexadecimal digits (<c>RRGGBB</c>).</param>
    /// <param name="opacity">1 for an opaque pen, less (down to 0) for a translucent one such as a highlighter.</param>
    /// <exception cref="FormatException"><paramref name="color"/> is not a hexadecimal colour.</exception>
    public WordInkStroke(IReadOnlyList<(double X, double Y)> points, double width = 1.5, string color = "000000", double opacity = 1)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points;
        Width = width;
        Color = Pdf.PdfColor.FromHex(color).ToHex()[1..];
        Opacity = Math.Clamp(opacity, 0, 1);
    }

    /// <summary>The sampled positions in points from the top-left corner of the ink's box.</summary>
    public IReadOnlyList<(double X, double Y)> Points { get; }

    /// <summary>The pen width in points.</summary>
    public double Width { get; }

    /// <summary>The pen colour as six hexadecimal digits.</summary>
    public string Color { get; }

    /// <summary>The pen opacity, 1 when opaque.</summary>
    public double Opacity { get; }
}

/// <summary>
/// Freehand ink (Word 2010 <c>wordprocessingInk</c>, with its strokes read from the InkML part the drawing names, or
/// the picture of its VML fallback when that part cannot be read: then <see cref="Strokes"/> is empty and the shape
/// is a <see cref="WordPicture"/> instead). The strokes sit in the shape's box: <see cref="WordShape.Width"/> by
/// <see cref="WordShape.Height"/> points. Saving writes the strokes back as an InkML part, without the VML fallback.
/// </summary>
public sealed class WordInk(double width, double height) : WordShape(width, height)
{
    /// <summary>The strokes, in drawing order.</summary>
    public List<WordInkStroke> Strokes { get; } = [];
}
