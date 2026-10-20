// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>The graphics and text state the rewriter follows to place glyphs and images.</summary>
internal sealed record RedactionState
{
    public Matrix Ctm { get; set; } = Matrix.Identity;

    public Matrix Tm { get; set; } = Matrix.Identity;

    public Matrix Tlm { get; set; } = Matrix.Identity;

    public PdfFontDecoder? Font { get; set; }

    public double FontSize { get; set; } = 1;

    public double CharSpacing { get; set; }

    public double WordSpacing { get; set; }

    public double HorizontalScale { get; set; } = 1;

    public double Leading { get; set; }

    public double Rise { get; set; }

    /// <summary>The tiling pattern named in the resources as the fill (or stroke) colour, when that is a pattern.</summary>
    public string? FillPattern { get; set; }

    public string? StrokePattern { get; set; }

    public double LineWidth { get; set; } = 1;

    public double MiterLimit { get; set; } = 10;

    /// <summary>The text rendering mode (Tr): 0 fill, 1 stroke, 2 both, 3 invisible, 4 to 7 as 0 to 3 and clip.</summary>
    public int RenderMode { get; set; }

    /// <summary>The pattern colours the glyphs shown now are painted with, by the rendering mode.</summary>
    public IEnumerable<string> TextPatterns()
    {
        var fills = RenderMode is 0 or 2 or 4 or 6;
        var strokes = RenderMode is 1 or 2 or 5 or 6;
        return new[] { fills ? FillPattern : null, strokes ? StrokePattern : null }.OfType<string>();
    }

    public void MoveLine(double tx, double ty)
    {
        Tlm = new Matrix(1, 0, 0, 1, tx, ty).Multiply(Tlm);
        Tm = Tlm;
    }

    public void Advance(double tx) => Tm = new Matrix(1, 0, 0, 1, tx, 0).Multiply(Tm);

    /// <summary>Follows the operators that move the text or change the transformation (ISO 32000-1 §8.4.4, §9.3, §9.4).</summary>
    public void Apply(ContentOperation operation, RedactionRewriter rewriter)
    {
        var o = operation.Operands;
        double N(int i) => RedactionRewriter.Number(o[i]);
        switch (operation.Operator)
        {
            case "cm":
                Ctm = Matrix.FromOperands(o).Multiply(Ctm);
                break;
            case "BT":
                Tm = Tlm = Matrix.Identity;
                break;
            case "Tf" when o.Count >= 2:
                (Font, FontSize) = (rewriter.Font(o[0] as PdfName), N(1));
                break;
            case "gs" when o.Count >= 1:
                rewriter.ApplyExtGState(o[0] as PdfName, this);
                break;
            default:
                if (!ApplyTextMove(operation.Operator, o) && !ApplyPaint(operation.Operator, o))
                {
                    ApplySpacing(operation.Operator, o);
                }

                break;
        }
    }

    // The operators that move the text line: Tm, Td, TD and T*.
    private bool ApplyTextMove(string op, IReadOnlyList<PdfObject> o)
    {
        switch (op)
        {
            case "Tm" when o.Count >= 6:
                Tm = Tlm = Matrix.FromOperands(o);
                return true;
            case "Td" or "TD" when o.Count >= 2:
                Leading = op == "TD" ? -RedactionRewriter.Number(o[1]) : Leading;
                MoveLine(RedactionRewriter.Number(o[0]), RedactionRewriter.Number(o[1]));
                return true;
            case "T*":
                MoveLine(0, -Leading);
                return true;
            default:
                return false;
        }
    }

    // Colours (a pattern is named by the last operand of scn or SCN, any other colour operator ends it) and the
    // line settings that widen a stroke.
    private bool ApplyPaint(string op, IReadOnlyList<PdfObject> o)
    {
        var pattern = o.Count > 0 && o[^1] is PdfName name ? name.Value : null;
        if (op == "scn" || FillColours.Contains(op))
        {
            FillPattern = op == "scn" ? pattern : null;
        }
        else if (op == "SCN" || StrokeColours.Contains(op))
        {
            StrokePattern = op == "SCN" ? pattern : null;
        }
        else if (o.Count > 0 && op == "w")
        {
            LineWidth = RedactionRewriter.Number(o[0]);
        }
        else if (o.Count > 0 && op == "M")
        {
            MiterLimit = RedactionRewriter.Number(o[0]);
        }
        else if (o.Count > 0 && op == "Tr")
        {
            RenderMode = (int)RedactionRewriter.Number(o[0]);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static readonly HashSet<string> FillColours = new(StringComparer.Ordinal) { "cs", "sc", "g", "rg", "k" };
    private static readonly HashSet<string> StrokeColours = new(StringComparer.Ordinal) { "CS", "SC", "G", "RG", "K" };

    private void ApplySpacing(string op, IReadOnlyList<PdfObject> o)
    {
        if (o.Count < 1)
        {
            return;
        }

        var value = RedactionRewriter.Number(o[0]);
        switch (op)
        {
            case "Tc":
                CharSpacing = value;
                break;
            case "Tw":
                WordSpacing = value;
                break;
            case "Tz":
                HorizontalScale = value / 100;
                break;
            case "TL":
                Leading = value;
                break;
            case "Ts":
                Rise = value;
                break;
        }
    }
}
