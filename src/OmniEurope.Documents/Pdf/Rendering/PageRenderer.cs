// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// Runs the operators of a content stream against a <see cref="Surface"/>: graphics state, paths and their
/// painting and clipping, colours, external graphics states (line style, opacity), forms, images and text.
/// Unsupported features (shadings, soft masks, blend modes) are recorded as gaps.
/// </summary>
internal sealed class PageRenderer
{
    private const int MaxDepth = 12;

    // Operators other than text ones; marked-content and compatibility operators are accepted and ignored.
    private static readonly Dictionary<string, Action<PageRenderer, IReadOnlyList<PdfObject>, ContentOperation, PdfDictionary?>> Operators = new(StringComparer.Ordinal)
    {
        ["q"] = (p, _, _, _) => p._saved.Push(p._state with { }),
        ["Q"] = (p, _, _, _) => p._state = p._saved.Count > 0 ? p._saved.Pop() : p._state,
        ["cm"] = (p, o, _, _) => p._state.Ctm = o.Count >= 6 ? Matrix.FromOperands(o).Multiply(p._state.Ctm) : p._state.Ctm,
        ["w"] = (p, o, _, _) => p._state.LineWidth = N(o, 0),
        ["J"] = (p, o, _, _) => p._state.LineCap = (int)N(o, 0),
        ["j"] = (p, o, _, _) => p._state.LineJoin = (int)N(o, 0),
        ["M"] = (p, o, _, _) => p._state.MiterLimit = N(o, 0),
        ["d"] = (p, o, _, _) => p.SetDash(o.Count > 0 ? o[0] as PdfArray : null, N(o, 1)),
        ["gs"] = (p, o, _, r) => p.ExternalState(o.Count > 0 ? o[0] as PdfName : null, r),
        ["m"] = (p, o, _, _) => p._path.MoveTo(N(o, 0), N(o, 1)),
        ["l"] = (p, o, _, _) => p._path.LineTo(N(o, 0), N(o, 1)),
        ["c"] = (p, o, _, _) => p._path.CurveTo(N(o, 0), N(o, 1), N(o, 2), N(o, 3), N(o, 4), N(o, 5)),
        ["v"] = (p, o, _, _) => p._path.CurveFromCurrent(N(o, 0), N(o, 1), N(o, 2), N(o, 3)),
        ["y"] = (p, o, _, _) => p._path.CurveTo(N(o, 0), N(o, 1), N(o, 2), N(o, 3), N(o, 2), N(o, 3)),
        ["h"] = (p, _, _, _) => p._path.Close(),
        ["re"] = (p, o, _, _) => p._path.Rectangle(N(o, 0), N(o, 1), N(o, 2), N(o, 3)),
        ["W"] = (p, _, _, _) => p._pendingClip = false,
        ["W*"] = (p, _, _, _) => p._pendingClip = true,
        ["S"] = (p, _, _, _) => p.Paint(fill: false, stroke: true, evenOdd: false),
        ["s"] = (p, _, _, _) => p.Paint(fill: false, stroke: true, evenOdd: false, close: true),
        ["f"] = (p, _, _, _) => p.Paint(fill: true, stroke: false, evenOdd: false),
        ["F"] = (p, _, _, _) => p.Paint(fill: true, stroke: false, evenOdd: false),
        ["f*"] = (p, _, _, _) => p.Paint(fill: true, stroke: false, evenOdd: true),
        ["B"] = (p, _, _, _) => p.Paint(fill: true, stroke: true, evenOdd: false),
        ["B*"] = (p, _, _, _) => p.Paint(fill: true, stroke: true, evenOdd: true),
        ["b"] = (p, _, _, _) => p.Paint(fill: true, stroke: true, evenOdd: false, close: true),
        ["b*"] = (p, _, _, _) => p.Paint(fill: true, stroke: true, evenOdd: true, close: true),
        ["n"] = (p, _, _, _) => p.Paint(fill: false, stroke: false, evenOdd: false),
        ["g"] = (p, o, _, _) => p.SetColor(true, PdfColorSpace.Gray, o),
        ["G"] = (p, o, _, _) => p.SetColor(false, PdfColorSpace.Gray, o),
        ["rg"] = (p, o, _, _) => p.SetColor(true, PdfColorSpace.Rgb, o),
        ["RG"] = (p, o, _, _) => p.SetColor(false, PdfColorSpace.Rgb, o),
        ["k"] = (p, o, _, _) => p.SetColor(true, PdfColorSpace.Cmyk, o),
        ["K"] = (p, o, _, _) => p.SetColor(false, PdfColorSpace.Cmyk, o),
        ["cs"] = (p, o, _, r) => p.SetSpace(true, o, r),
        ["CS"] = (p, o, _, r) => p.SetSpace(false, o, r),
        ["sc"] = (p, o, _, _) => p.SetColor(true, p._state.FillSpace, o),
        ["scn"] = (p, o, _, r) => p.SetColorOrPattern(true, o, r),
        ["SC"] = (p, o, _, _) => p.SetColor(false, p._state.StrokeSpace, o),
        ["SCN"] = (p, o, _, r) => p.SetColorOrPattern(false, o, r),
        ["Do"] = (p, o, _, r) => p.XObject(o.Count > 0 ? o[0] as PdfName : null, r),
        ["BI"] = (p, o, op, r) => p.InlineImage(o, op.InlineData, r),
        ["sh"] = (p, o, _, r) => p.ShadeArea(o.Count > 0 ? o[0] as PdfName : null, r),
    };

    // ExtGState entries that change drawing; others are ignored.
    private static readonly Dictionary<string, Action<PageRenderer, PdfObject?>> StateEntries = new(StringComparer.Ordinal)
    {
        ["LW"] = (p, v) => p._state.LineWidth = v is PdfNumber n ? n.Value : p._state.LineWidth,
        ["LC"] = (p, v) => p._state.LineCap = v is PdfNumber n ? n.IntValue : p._state.LineCap,
        ["LJ"] = (p, v) => p._state.LineJoin = v is PdfNumber n ? n.IntValue : p._state.LineJoin,
        ["ML"] = (p, v) => p._state.MiterLimit = v is PdfNumber n ? n.Value : p._state.MiterLimit,
        ["CA"] = (p, v) => p._state.StrokeAlpha = v is PdfNumber n ? Math.Clamp(n.Value, 0, 1) : p._state.StrokeAlpha,
        ["ca"] = (p, v) => p._state.FillAlpha = v is PdfNumber n ? Math.Clamp(n.Value, 0, 1) : p._state.FillAlpha,
        ["D"] = (p, v) => p.SetDash(v is PdfArray { Count: 2 } d ? p._store.Resolve(d[0]) as PdfArray : null, v is PdfArray { Count: 2 } e ? N([p._store.Resolve(e[1]) ?? PdfNull.Instance], 0) : 0),
        ["SMask"] = (p, v) => p.GapUnless(v is PdfName { Value: "None" }, "soft masks are not applied"),
        ["BM"] = (p, v) => p.GapUnless(v is PdfName { Value: "Normal" or "Compatible" }, "blend modes are drawn as normal"),
    };

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal) { "ri", "i", "BMC", "BDC", "EMC", "BX", "EX", "MP", "DP", "d0", "d1" };

    private readonly PdfObjectStore _store;
    private readonly Surface _surface;
    private readonly Stack<GraphicsState> _saved = new();
    private readonly PathBuilder _path = new();
    private readonly HashSet<PdfStream> _activeForms = new(ReferenceEqualityComparer.Instance);
    private readonly Matrix _base;
    private GraphicsState _state;
    private bool? _pendingClip;
    private int _depth;

    public PageRenderer(PdfObjectStore store, Surface surface, Matrix toDevice, RenderContext context)
    {
        _store = store;
        _surface = surface;
        _base = toDevice;
        _state = new GraphicsState { Ctm = toDevice };
        Context = context;
        Text = new TextPainter(this);
        Images = new ImagePainter(this);
    }

    public RenderContext Context { get; }

    public PdfObjectStore Store => _store;

    public Surface Surface => _surface;

    public GraphicsState State => _state;

    internal TextPainter Text { get; }

    internal ImagePainter Images { get; }

    public void Run(byte[] content, PdfDictionary? resources)
    {
        foreach (var operation in ContentStreamReader.Read(content))
        {
            try
            {
                Execute(operation, resources);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidCastException or ArgumentException or IndexOutOfRangeException)
            {
                Context.Gap("unreadable drawing operation skipped");
            }
        }
    }

    private void Execute(ContentOperation operation, PdfDictionary? resources)
    {
        if (Operators.TryGetValue(operation.Operator, out var handler))
        {
            handler(this, operation.Operands, operation, resources);
        }
        else if (!Ignored.Contains(operation.Operator))
        {
            Text.Execute(operation.Operator, operation.Operands, resources);
        }
    }

    private void SetDash(PdfArray? pattern, double phase)
    {
        _state.Dash = pattern?.Items.Select(i => Number(_store.Resolve(i) ?? PdfNull.Instance)).ToArray();
        _state.DashPhase = phase;
    }

    private void ExternalState(PdfName? name, PdfDictionary? resources)
    {
        if (name is null || _store.Get(_store.Get<PdfDictionary>(resources, "ExtGState"), name.Value) is not PdfDictionary gs)
        {
            return;
        }

        foreach (var (key, raw) in gs.Entries)
        {
            if (StateEntries.TryGetValue(key, out var apply))
            {
                apply(this, _store.Resolve(raw));
            }
        }
    }

    private void GapUnless(bool supported, string gap)
    {
        if (!supported)
        {
            Context.Gap(gap);
        }
    }

    private void Paint(bool fill, bool stroke, bool evenOdd, bool close = false)
    {
        if (close)
        {
            _path.Close();
        }

        var device = _path.Flatten(_state.Ctm);
        if (fill)
        {
            var polygons = device.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points);
            if (_state.FillPattern is { } pattern)
            {
                _surface.Fill(polygons, evenOdd, ShadingColors(pattern.Shading, pattern.ToDevice), _state.FillAlpha, _state.Clip);
            }
            else
            {
                _surface.Fill(polygons, evenOdd, _state.FillColor, _state.FillAlpha, _state.Clip);
            }
        }

        if (stroke)
        {
            StrokePath(device);
        }

        if (_pendingClip is { } clipEvenOdd)
        {
            _state.Clip = ClipMask.Intersect(_state.Clip, device.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points), clipEvenOdd, _surface.Width, _surface.Height);
            _pendingClip = null;
        }

        _path.Clear();
    }

    public void StrokePath(List<Polyline> device)
    {
        var scale = Math.Sqrt(Math.Abs((_state.Ctm.A * _state.Ctm.D) - (_state.Ctm.B * _state.Ctm.C)));
        var width = _state.LineWidth * scale;
        var dash = _state.Dash is { Length: > 0 } d ? d.Select(v => v * scale).ToArray() : null;
        var outline = Stroker.Outline(device, new StrokeStyle(width, _state.LineCap, _state.LineJoin, _state.MiterLimit, dash, _state.DashPhase * scale));
        _surface.Fill(outline, false, _state.StrokeColor, _state.StrokeAlpha, _state.Clip);
    }

    private void SetSpace(bool fill, IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (o.Count == 0)
        {
            return;
        }

        var space = PdfColorSpace.Resolve(_store, o[0], resources);
        SetColor(fill, space, Enumerable.Repeat<PdfObject>(new PdfNumber(0, true), space.Components).ToList());
    }

    private void SetColor(bool fill, PdfColorSpace space, IReadOnlyList<PdfObject> operands)
    {
        var values = operands.OfType<PdfNumber>().Select(n => n.Value).ToArray();
        var color = values.Length == 0 ? PdfColor.Black : GraphicsState.ToRgb(space, values);
        if (fill)
        {
            (_state.FillSpace, _state.FillColor, _state.FillPattern) = (space, color, null);
        }
        else
        {
            (_state.StrokeSpace, _state.StrokeColor) = (space, color);
        }
    }

    // In a Pattern colour space the operands end with the pattern's name; other spaces give components.
    private void SetColorOrPattern(bool fill, IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (o.Count == 0 || o[^1] is not PdfName name)
        {
            SetColor(fill, fill ? _state.FillSpace : _state.StrokeSpace, o);
            return;
        }

        var pattern = _store.Get(_store.Get<PdfDictionary>(resources, "Pattern"), name.Value) as PdfDictionary;
        var shading = fill && pattern is not null && _store.Number(pattern, "PatternType") == 2 ? Shading.Read(_store, _store.Get(pattern, "Shading"), resources) : null;
        if (shading is null)
        {
            Context.Gap(pattern is not null && _store.Number(pattern, "PatternType") == 2 && fill
                ? "shadings of this kind are not drawn"
                : "tiling patterns and stroked patterns are drawn as a flat colour");
            return;
        }

        // A pattern is placed by its own matrix in the default page space, not by the current matrix.
        _state.FillPattern = (shading, Matrix.FromArray(_store.Get<PdfArray>(pattern!, "Matrix")).Multiply(_base));
    }

    // The sh operator paints the shading over the whole clipping area.
    private void ShadeArea(PdfName? name, PdfDictionary? resources)
    {
        var shading = name is null ? null : Shading.Read(_store, _store.Get(_store.Get<PdfDictionary>(resources, "Shading"), name.Value), resources);
        if (shading is null)
        {
            Context.Gap("shadings of this kind are not drawn");
            return;
        }

        IReadOnlyList<(double X, double Y)> page = [(0, 0), (_surface.Width, 0), (_surface.Width, _surface.Height), (0, _surface.Height)];
        _surface.Fill([page], false, ShadingColors(shading, _state.Ctm), _state.FillAlpha, _state.Clip);
    }

    private static Func<int, int, PdfColor?> ShadingColors(Shading shading, Matrix toDevice)
    {
        var determinant = (toDevice.A * toDevice.D) - (toDevice.B * toDevice.C);
        if (Math.Abs(determinant) < 1e-12)
        {
            return (_, _) => null;
        }

        var inverse = new Matrix(toDevice.D / determinant, -toDevice.B / determinant, -toDevice.C / determinant, toDevice.A / determinant,
            ((toDevice.C * toDevice.F) - (toDevice.D * toDevice.E)) / determinant, ((toDevice.B * toDevice.E) - (toDevice.A * toDevice.F)) / determinant);
        return (x, y) =>
        {
            var (u, v) = inverse.Transform(x + 0.5, y + 0.5);
            return shading.ColorAt(u, v);
        };
    }

    private void InlineImage(IReadOnlyList<PdfObject> operands, byte[]? data, PdfDictionary? resources)
    {
        if (operands.Count == 1 && operands[0] is PdfDictionary inline)
        {
            Images.Draw(inline, data, resources);
        }
    }

    private void XObject(PdfName? name, PdfDictionary? resources)
    {
        if (name is null || _store.Get(_store.Get<PdfDictionary>(resources, "XObject"), name.Value) is not PdfStream stream)
        {
            return;
        }

        if (_store.Get(stream, "Subtype") is PdfName { Value: "Image" })
        {
            Images.Draw(stream, null, resources);
            return;
        }

        RunForm(stream, resources);
    }

    /// <summary>Runs a form XObject (or an annotation appearance) under the current state, clipped to its box.</summary>
    public void RunForm(PdfStream form, PdfDictionary? resources, Matrix? extra = null)
    {
        if (_depth >= MaxDepth || !_activeForms.Add(form))
        {
            return;
        }

        _depth++;
        _saved.Push(_state with { });
        try
        {
            _state.Ctm = Matrix.FromArray(_store.Get<PdfArray>(form, "Matrix")).Multiply(extra ?? Matrix.Identity).Multiply(_state.Ctm);
            if (_store.Get<PdfArray>(form, "BBox") is { Count: 4 } box)
            {
                var values = box.Items.Select(i => Number(_store.Resolve(i) ?? PdfNull.Instance)).ToArray();
                _path.Rectangle(values[0], values[1], values[2] - values[0], values[3] - values[1]);
                _pendingClip = false;
                Paint(fill: false, stroke: false, evenOdd: false);
            }

            Run(_store.DecodeBytes(form), _store.Get<PdfDictionary>(form, "Resources") ?? resources);
        }
        finally
        {
            _state = _saved.Pop();
            _activeForms.Remove(form);
            _depth--;
        }
    }

    public static double Number(PdfObject value) => value is PdfNumber n ? n.Value : 0;

    private static double N(IReadOnlyList<PdfObject> operands, int index) => index < operands.Count ? Number(operands[index]) : 0;
}
