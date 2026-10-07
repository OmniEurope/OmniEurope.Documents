// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// The text operators: text state, positioning and showing. Each glyph outline is placed by the text
/// rendering matrix and filled and/or stroked according to the rendering mode; clipping modes add the
/// glyphs to the clip at the end of the text object. Type 3 glyphs run their procedures.
/// </summary>
internal sealed class TextPainter(PageRenderer page)
{
    // Text state, positioning and showing operators.
    private static readonly Dictionary<string, Action<TextPainter, IReadOnlyList<PdfObject>, PdfDictionary?>> Operators = new(StringComparer.Ordinal)
    {
        ["Tf"] = (t, o, r) => (t.State.Font, t.State.FontSize) = (t.Font(r, o.Count > 0 ? o[0] as PdfName : null), Last(o)),
        ["Tc"] = (t, o, _) => t.State.CharSpacing = Last(o),
        ["Tw"] = (t, o, _) => t.State.WordSpacing = Last(o),
        ["Tz"] = (t, o, _) => t.State.HorizontalScale = Last(o) / 100,
        ["TL"] = (t, o, _) => t.State.Leading = Last(o),
        ["Ts"] = (t, o, _) => t.State.Rise = Last(o),
        ["Tr"] = (t, o, _) => t.State.RenderMode = (int)Last(o),
        ["BT"] = (t, _, _) => t.BeginText(),
        ["ET"] = (t, _, _) => t.EndText(),
        ["Td"] = (t, o, _) => t.MoveLine(At(o, 0), At(o, 1)),
        ["TD"] = (t, o, _) => t.MoveLineSettingLeading(At(o, 0), At(o, 1)),
        ["Tm"] = (t, o, _) => t.SetMatrix(o),
        ["T*"] = (t, _, _) => t.MoveLine(0, -t.State.Leading),
        ["Tj"] = (t, o, r) => t.Show(o.Count > 0 ? o[^1] : PdfNull.Instance, r),
        ["'"] = (t, o, r) => t.NextLineShow(o.Count > 0 ? o[^1] : PdfNull.Instance, r),
        ["\""] = (t, o, r) => t.SpacedShow(o, r),
        ["TJ"] = (t, o, r) => t.ShowArray(o.Count > 0 ? o[^1] as PdfArray : null, r),
    };

    private readonly List<Polyline> _clip = [];
    private Matrix _tm = Matrix.Identity;
    private Matrix _tlm = Matrix.Identity;

    private GraphicsState State => page.State;

    public void Execute(string op, IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (Operators.TryGetValue(op, out var handler))
        {
            handler(this, o, resources);
        }
    }

    private static double Last(IReadOnlyList<PdfObject> o) => o.Count > 0 ? PageRenderer.Number(o[^1]) : 0;

    private static double At(IReadOnlyList<PdfObject> o, int index) => index < o.Count ? PageRenderer.Number(o[index]) : 0;

    private void BeginText()
    {
        _tm = _tlm = Matrix.Identity;
        _clip.Clear();
    }

    private void SetMatrix(IReadOnlyList<PdfObject> o)
    {
        if (o.Count >= 6)
        {
            _tm = _tlm = Matrix.FromOperands(o);
        }
    }

    private void MoveLineSettingLeading(double tx, double ty)
    {
        State.Leading = -ty;
        MoveLine(tx, ty);
    }

    private void NextLineShow(PdfObject text, PdfDictionary? resources)
    {
        MoveLine(0, -State.Leading);
        Show(text, resources);
    }

    private void SpacedShow(IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (o.Count < 3)
        {
            return;
        }

        (State.WordSpacing, State.CharSpacing) = (At(o, 0), At(o, 1));
        NextLineShow(o[2], resources);
    }

    private RenderFont? Font(PdfDictionary? resources, PdfName? name)
    {
        var store = page.Store;
        return name is not null && store.Get(store.Get<PdfDictionary>(resources, "Font"), name.Value) is PdfDictionary dictionary
            ? page.Context.Font(store, dictionary)
            : null;
    }

    private void MoveLine(double tx, double ty)
    {
        _tlm = new Matrix(1, 0, 0, 1, tx, ty).Multiply(_tlm);
        _tm = _tlm;
    }

    private void ShowArray(PdfArray? array, PdfDictionary? resources)
    {
        foreach (var item in array?.Items ?? [])
        {
            if (item is PdfNumber adjustment)
            {
                Advance(-adjustment.Value / 1000 * State.FontSize * State.HorizontalScale);
            }
            else
            {
                Show(item, resources);
            }
        }
    }

    private void Show(PdfObject text, PdfDictionary? resources)
    {
        if (text is not PdfString s || State.Font is not { } font)
        {
            return;
        }

        foreach (var glyph in font.Decoder.Decode(s.Bytes))
        {
            var trm = new Matrix(State.FontSize * State.HorizontalScale, 0, 0, State.FontSize, 0, State.Rise).Multiply(_tm).Multiply(State.Ctm);
            if (State.RenderMode != 3)
            {
                Draw(font, glyph, trm, resources);
            }

            Advance(((glyph.Advance * State.FontSize) + State.CharSpacing + (glyph.IsWordSpace ? State.WordSpacing : 0)) * State.HorizontalScale);
        }
    }

    private void Draw(RenderFont font, DecodedGlyph glyph, Matrix trm, PdfDictionary? resources)
    {
        if (font.Procedure(glyph) is { } procedure)
        {
            page.RunForm(procedure.Procedure, procedure.Resources ?? resources, procedure.FontMatrix.Multiply(new Matrix(State.FontSize * State.HorizontalScale, 0, 0, State.FontSize, 0, State.Rise)).Multiply(_tm));
            return;
        }

        if (font.Shape(glyph) is not { } shape)
        {
            return;
        }

        var path = new PathBuilder();
        shape.AppendTo(path);
        var device = path.Flatten(trm);
        var mode = State.RenderMode;
        if (mode is 0 or 2 or 4 or 6)
        {
            page.Surface.Fill(device.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points), false, State.FillColor, State.FillAlpha, State.Clip);
        }

        if (mode is 1 or 2 or 5 or 6)
        {
            page.StrokePath(device);
        }

        if (mode >= 4)
        {
            _clip.AddRange(device);
        }
    }

    private void Advance(double tx) => _tm = new Matrix(1, 0, 0, 1, tx, 0).Multiply(_tm);

    // Clipping text modes intersect the clip with every glyph shown in the text object.
    private void EndText()
    {
        if (_clip.Count > 0)
        {
            State.Clip = ClipMask.Intersect(State.Clip, _clip.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points), false, page.Surface.Width, page.Surface.Height);
            _clip.Clear();
        }
    }
}
