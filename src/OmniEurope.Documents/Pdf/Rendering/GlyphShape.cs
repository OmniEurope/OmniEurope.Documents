// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>A drawing command of a glyph outline.</summary>
internal readonly record struct GlyphCommand(char Op, double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0, double X3 = 0, double Y3 = 0);

/// <summary>A glyph outline in text space units (1 is the font size, y up), ready to be placed by the text matrix.</summary>
internal sealed class GlyphShape(List<GlyphCommand> commands)
{
    public List<GlyphCommand> Commands { get; } = commands;

    public void AppendTo(PathBuilder path)
    {
        foreach (var c in Commands)
        {
            switch (c.Op)
            {
                case 'M':
                    path.MoveTo(c.X1, c.Y1);
                    break;
                case 'L':
                    path.LineTo(c.X1, c.Y1);
                    break;
                case 'C':
                    path.CurveTo(c.X1, c.Y1, c.X2, c.Y2, c.X3, c.Y3);
                    break;
                case 'Z':
                    path.Close();
                    break;
            }
        }
    }

    /// <summary>A TrueType outline (quadratic contours in font units) scaled by <paramref name="scale"/>.</summary>
    public static GlyphShape FromQuadratic(GlyphOutline outline, double scale)
    {
        var commands = new List<GlyphCommand>();
        foreach (var contour in outline.Contours)
        {
            Contour(commands, contour, scale);
        }

        return new GlyphShape(commands);
    }

    // A contour may start off the curve and hold consecutive off-curve points with implied on-curve midpoints.
    private static void Contour(List<GlyphCommand> commands, IReadOnlyList<GlyphPoint> contour, double scale)
    {
        if (contour.Count < 2)
        {
            return;
        }

        var startIndex = -1;
        for (var i = 0; i < contour.Count && startIndex < 0; i++)
        {
            startIndex = contour[i].OnCurve ? i : -1;
        }

        var start = startIndex >= 0
            ? (contour[startIndex].X, contour[startIndex].Y)
            : ((contour[0].X + contour[1].X) / 2, (contour[0].Y + contour[1].Y) / 2);
        var first = startIndex >= 0 ? startIndex : 0;
        commands.Add(new GlyphCommand('M', start.Item1 * scale, start.Item2 * scale));
        var current = start;
        (double X, double Y)? control = null;
        for (var step = 1; step <= contour.Count; step++)
        {
            var point = contour[(first + step) % contour.Count];
            var p = (point.X, point.Y);
            if (point.OnCurve)
            {
                current = Emit(commands, current, control, p, scale);
                control = null;
            }
            else if (control is { } previous)
            {
                var middle = ((previous.X + p.X) / 2, (previous.Y + p.Y) / 2);
                current = Emit(commands, current, previous, middle, scale);
                control = p;
            }
            else
            {
                control = p;
            }
        }

        if (control is { } last)
        {
            Emit(commands, current, last, start, scale);
        }

        commands.Add(new GlyphCommand('Z'));
    }

    // A quadratic segment becomes the equivalent cubic (control points two thirds of the way to the quadratic one).
    private static (double X, double Y) Emit(List<GlyphCommand> commands, (double X, double Y) from, (double X, double Y)? control, (double X, double Y) to, double scale)
    {
        if (control is not { } q)
        {
            commands.Add(new GlyphCommand('L', to.X * scale, to.Y * scale));
            return to;
        }

        var c1 = (from.X + (2.0 / 3 * (q.X - from.X)), from.Y + (2.0 / 3 * (q.Y - from.Y)));
        var c2 = (to.X + (2.0 / 3 * (q.X - to.X)), to.Y + (2.0 / 3 * (q.Y - to.Y)));
        commands.Add(new GlyphCommand('C', c1.Item1 * scale, c1.Item2 * scale, c2.Item1 * scale, c2.Item2 * scale, to.X * scale, to.Y * scale));
        return to;
    }

    /// <summary>The outline transformed by a matrix (font matrices of CFF and Type 1 fonts).</summary>
    public GlyphShape Transform(Matrix matrix) => new(Commands.Select(c =>
    {
        var (x1, y1) = matrix.Transform(c.X1, c.Y1);
        var (x2, y2) = matrix.Transform(c.X2, c.Y2);
        var (x3, y3) = matrix.Transform(c.X3, c.Y3);
        return new GlyphCommand(c.Op, x1, y1, x2, y2, x3, y3);
    }).ToList());
}
