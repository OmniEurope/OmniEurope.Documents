// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// Follows the path being built and, when it is painted with a tiling pattern as fill or stroke colour, says whether
/// the painted region meets a redaction area. The cell of such a pattern is drawn again and again by the viewer, so
/// text or images in it are not rewritten with the page content: a pattern that shows any and is painted under an
/// area is removed whole instead (the area is painted over the place anyway).
/// </summary>
internal sealed class RedactionPatterns(RedactionContext context)
{
    private static readonly HashSet<string> Fills = new(StringComparer.Ordinal) { "f", "F", "f*", "B", "B*", "b", "b*" };
    private static readonly HashSet<string> Strokes = new(StringComparer.Ordinal) { "S", "s", "B", "B*", "b", "b*" };
    private static readonly HashSet<string> Shows = new(StringComparer.Ordinal) { "Tj", "TJ", "'", "\"", "Do", "BI" };

    private static readonly HashSet<string> Constructions = new(StringComparer.Ordinal) { "m", "l", "c", "v", "y", "re" };

    private readonly List<(double X, double Y)> _points = [];

    /// <summary>True for the operators that add to the path.</summary>
    public static bool Constructs(string op) => Constructions.Contains(op);

    /// <summary>True for the operators that end the path, painting it or not.</summary>
    public static bool Ends(string op) => op == "n" || Fills.Contains(op) || Strokes.Contains(op);

    /// <summary>Follows a path-construction operator, in the coordinates the current matrix gives it.</summary>
    public void Construct(ContentOperation operation, Matrix ctm)
    {
        var o = operation.Operands;
        var count = operation.Operator switch { "m" or "l" => 1, "v" or "y" => 2, "c" => 3, _ => 0 };
        if (operation.Operator == "re" && o.Count >= 4)
        {
            var (x, y, w, h) = (RedactionRewriter.Number(o[0]), RedactionRewriter.Number(o[1]), RedactionRewriter.Number(o[2]), RedactionRewriter.Number(o[3]));
            _points.AddRange([ctm.Transform(x, y), ctm.Transform(x + w, y), ctm.Transform(x, y + h), ctm.Transform(x + w, y + h)]);
        }
        else if (count > 0 && o.Count >= count * 2)
        {
            for (var i = 0; i < count; i++)
            {
                _points.Add(ctm.Transform(RedactionRewriter.Number(o[2 * i]), RedactionRewriter.Number(o[(2 * i) + 1])));
            }
        }
    }

    /// <summary>The patterns a painting operator uses under an area; ends the path.</summary>
    public IEnumerable<string> Painted(string op, RedactionState state)
    {
        var points = _points.ToList();
        if (Ends(op))
        {
            _points.Clear();
        }

        var exposed = new List<string>();
        if (points.Count == 0)
        {
            return exposed;
        }

        if (Fills.Contains(op) && state.FillPattern is { } fill && Meets(points, 0))
        {
            exposed.Add(fill);
        }

        if (Strokes.Contains(op) && state.StrokePattern is { } stroke && Meets(points, StrokeMargin(state)))
        {
            exposed.Add(stroke);
        }

        return exposed;
    }

    /// <summary>Whether the tiling pattern named <paramref name="name"/> in the resources shows text or images (a
    /// cell that cannot be read, or that selects other patterns, counts as showing them).</summary>
    public bool ShowsContent(PdfDictionary? resources, string name)
    {
        var store = context.Store;
        if (store.Get(store.Get<PdfDictionary>(resources, "Pattern"), name) is not PdfStream { } pattern || store.Number(pattern, "PatternType") != 1)
        {
            return false;
        }

        try
        {
            // A stored cell that decodes to nothing may still be drawn, in part, by a more forgiving viewer.
            var cell = store.DecodeBytes(pattern);
            return (cell.Length == 0 && pattern.Data.Length > 0) || ContentStreamReader.Read(cell).Any(o => Shows.Contains(o.Operator) || (o.Operator is "scn" or "SCN" && o.Operands.Count > 0 && o.Operands[^1] is PdfName));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return true;
        }
    }

    /// <summary>Takes the line width (LW) and miter limit (ML) of the ExtGState <paramref name="name"/> into the state.</summary>
    public void ApplyExtGState(PdfDictionary? resources, PdfName? name, RedactionState state)
    {
        var store = context.Store;
        if (name is null || store.Get(store.Get<PdfDictionary>(resources, "ExtGState"), name.Value) is not PdfDictionary gs)
        {
            return;
        }

        state.LineWidth = store.Get(gs, "LW") is PdfNumber width ? width.Value : state.LineWidth;
        state.MiterLimit = store.Get(gs, "ML") is PdfNumber limit ? limit.Value : state.MiterLimit;
    }

    // Half the line width, widened for a miter join or a square cap, in the larger scale of the current matrix.
    private static double StrokeMargin(RedactionState state)
    {
        var scale = Math.Max(Math.Sqrt((state.Ctm.A * state.Ctm.A) + (state.Ctm.B * state.Ctm.B)), Math.Sqrt((state.Ctm.C * state.Ctm.C) + (state.Ctm.D * state.Ctm.D)));
        return 0.5 * state.LineWidth * scale * Math.Max(state.MiterLimit, 1.5);
    }

    private bool Meets(List<(double X, double Y)> points, double margin) =>
        context.Touches(new PdfRectangle(points.Min(p => p.X) - margin, points.Min(p => p.Y) - margin, points.Max(p => p.X) + margin, points.Max(p => p.Y) + margin));
}
