// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// What <see cref="WordEditableParagraph.InsertDrawing"/> and <see cref="WordEditableParagraph.ReplaceDrawing"/>
/// draw: freehand strokes (<see cref="Ink"/>) or a PNG image (<see cref="Png"/>).
/// </summary>
public sealed class WordDrawingContent
{
    private const double PointsPerPixel = 0.75;

    private WordDrawingContent(WordDrawingKind kind, IReadOnlyList<WordInkStroke> strokes, byte[]? png, double defaultWidth, double defaultHeight)
    {
        Kind = kind;
        Strokes = strokes;
        PngData = png;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
    }

    /// <summary>Whether the content is <see cref="WordDrawingKind.Ink"/> or a <see cref="WordDrawingKind.Picture"/>.</summary>
    public WordDrawingKind Kind { get; }

    /// <summary>The width a drawing of this content gets when its placement gives none, in points.</summary>
    public double DefaultWidth { get; }

    /// <summary>The height a drawing of this content gets when its placement gives none, in points.</summary>
    public double DefaultHeight { get; }

    internal IReadOnlyList<WordInkStroke> Strokes { get; }

    internal byte[]? PngData { get; }

    /// <summary>
    /// Freehand ink. Points are in points from the top-left corner of the drawing's box (x to the right, y
    /// downwards); the box defaults to the room the strokes need (their reach plus half the widest pen), and
    /// anything outside a larger or smaller box is clipped.
    /// </summary>
    /// <exception cref="ArgumentException">There is no stroke, or no stroke has a point, or a point is not a finite number.</exception>
    public static WordDrawingContent Ink(IEnumerable<WordInkStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var list = strokes.Where(s => s is not null && s.Points.Count > 0).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Ink needs at least one stroke with a point.", nameof(strokes));
        }

        var points = list.SelectMany(s => s.Points).ToList();
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
        {
            throw new ArgumentException("An ink point is not a finite number.", nameof(strokes));
        }

        var margin = list.Max(s => s.Width) / 2;
        return new WordDrawingContent(WordDrawingKind.Ink, list, null, Math.Max(1, points.Max(p => p.X) + margin), Math.Max(1, points.Max(p => p.Y) + margin));
    }

    /// <summary>
    /// A PNG image, kept as it is (transparency included). The box defaults to the pixel size at 96 dpi
    /// (0.75 point per pixel).
    /// </summary>
    /// <exception cref="ArgumentException">The data is not a PNG the library decodes.</exception>
    public static WordDrawingContent Png(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        try
        {
            var image = PngCodec.Decode(png);
            return new WordDrawingContent(WordDrawingKind.Picture, [], png, image.Width * PointsPerPixel, image.Height * PointsPerPixel);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or IndexOutOfRangeException or NotSupportedException or OverflowException)
        {
            throw new ArgumentException("The data is not a PNG image that can be decoded: " + exception.Message, nameof(png), exception);
        }
    }
}

/// <summary>
/// Where and how large a drawing is: a floating drawing without text wrapping (<c>wp:wrapNone</c>), placed from
/// the column (horizontally) and the paragraph (vertically).
/// </summary>
public sealed record WordDrawingPlacement
{
    /// <summary>Width of the drawing's box in points; null takes the content's default size when inserting, the
    /// replaced drawing's width when replacing.</summary>
    public double? Width { get; init; }

    /// <summary>Height of the drawing's box in points; null takes the content's default size when inserting, the
    /// replaced drawing's height when replacing.</summary>
    public double? Height { get; init; }

    /// <summary>Distance in points from the left edge of the column to the drawing's left edge.</summary>
    public double OffsetX { get; init; }

    /// <summary>Distance in points from the top of the paragraph to the drawing's top edge.</summary>
    public double OffsetY { get; init; }

    /// <summary>True to put the drawing behind the text, false (the default) to put it in front of it.</summary>
    public bool BehindText { get; init; }
}
