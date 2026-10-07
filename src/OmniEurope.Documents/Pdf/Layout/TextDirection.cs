// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Layout;

/// <summary>
/// A direction of text on a page, in degrees counter-clockwise from the page's x axis (rounded to 5 degrees, from 0
/// to 355), and the frame it is read in: x along its baselines, y across them (growing towards the top of its lines).
/// </summary>
internal readonly record struct TextDirection(double Angle)
{
    private double Cos => Math.Cos(-Angle * Math.PI / 180);

    private double Sin => Math.Sin(-Angle * Math.PI / 180);

    /// <summary>The letters grouped by direction, each group projected into its own frame, groups in the order
    /// their first letter is drawn.</summary>
    public static IEnumerable<(TextDirection Direction, List<PlacedLetter> Letters)> Group(IEnumerable<PdfLetter> letters) =>
        letters.GroupBy(l => Of(l.Rotation)).Select(g => (g.Key, g.Select(g.Key.Place).ToList()));

    /// <summary>The direction of a rotation: rounded to 5 degrees and brought between 0 and 360.</summary>
    public static TextDirection Of(double rotation) => new((((Math.Round(rotation / 5) * 5) % 360) + 360) % 360);

    /// <summary>A letter with its position in this frame.</summary>
    public PlacedLetter Place(PdfLetter letter)
    {
        var (x, y) = Project(letter.X, letter.Y);
        return new PlacedLetter(letter, x, y);
    }

    /// <summary>A point of the page in this frame.</summary>
    public (double X, double Y) Project(double x, double y) => ((x * Cos) - (y * Sin), (x * Sin) + (y * Cos));

    /// <summary>The smallest rectangle of this frame holding a rectangle of the page.</summary>
    public PdfRectangle Project(PdfRectangle box)
    {
        var corners = new[] { Project(box.Left, box.Bottom), Project(box.Right, box.Bottom), Project(box.Left, box.Top), Project(box.Right, box.Top) };
        return new PdfRectangle(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
    }
}
