// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>
/// Runs the operators of a content stream against a <see cref="Surface"/>: graphics state, paths and their
/// painting and clipping, colours and patterns, external graphics states (line style, opacity, blend mode, soft
/// mask), forms and transparency groups, images and text. What cannot be drawn faithfully is recorded as a gap.
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
    private static readonly Dictionary<string, Action<PageRenderer, PdfObject?, PdfDictionary?>> StateEntries = new(StringComparer.Ordinal)
    {
        ["LW"] = (p, v, _) => p._state.LineWidth = v is PdfNumber n ? n.Value : p._state.LineWidth,
        ["LC"] = (p, v, _) => p._state.LineCap = v is PdfNumber n ? n.IntValue : p._state.LineCap,
        ["LJ"] = (p, v, _) => p._state.LineJoin = v is PdfNumber n ? n.IntValue : p._state.LineJoin,
        ["ML"] = (p, v, _) => p._state.MiterLimit = v is PdfNumber n ? n.Value : p._state.MiterLimit,
        ["CA"] = (p, v, _) => p._state.StrokeAlpha = v is PdfNumber n ? Math.Clamp(n.Value, 0, 1) : p._state.StrokeAlpha,
        ["ca"] = (p, v, _) => p._state.FillAlpha = v is PdfNumber n ? Math.Clamp(n.Value, 0, 1) : p._state.FillAlpha,
        ["D"] = (p, v, _) => p.SetDash(v is PdfArray { Count: 2 } d ? p._store.Resolve(d[0]) as PdfArray : null, v is PdfArray { Count: 2 } e ? N([p._store.Resolve(e[1]) ?? PdfNull.Instance], 0) : 0),
        ["SMask"] = (p, v, r) => p._state.Mask = v is PdfDictionary mask ? p.Transparency.Mask(mask, r) : null,
        ["BM"] = (p, v, _) => p._state.Mode = Blending.Parse(p._store, v),
        ["AIS"] = (p, v, _) => p._state.AlphaIsShape = v is PdfBoolean { Value: true },
    };

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal) { "ri", "i", "BMC", "BDC", "EMC", "BX", "EX", "MP", "DP", "d0", "d1" };

    private readonly PdfObjectStore _store;
    private readonly Dictionary<(PdfStream Pattern, Matrix ToDevice, PdfColor? Colour), TilingPaint?> _tiles = [];
    private Canvas _target;
    private Stack<GraphicsState> _saved = new();
    private readonly PathBuilder _path = new();
    private readonly HashSet<PdfStream> _activeForms = new(ReferenceEqualityComparer.Instance);
    private Matrix _patternBase;
    private bool _colourLocked;
    private GraphicsState _state;
    private bool? _pendingClip;
    private int _depth;

    public PageRenderer(PdfObjectStore store, Surface surface, Matrix toDevice, RenderContext context)
    {
        _store = store;
        _target = surface;
        _patternBase = toDevice;
        _state = new GraphicsState { Ctm = toDevice };
        Context = context;
        Text = new TextPainter(this);
        Images = new ImagePainter(this);
        Transparency = new TransparencyPainter(this);
    }

    public RenderContext Context { get; }

    public PdfObjectStore Store => _store;

    /// <summary>What is painted on: the page, or the transparency group, soft mask or pattern cell being drawn.</summary>
    public Canvas Surface => _target;

    public GraphicsState State => _state;

    internal TextPainter Text { get; }

    internal ImagePainter Images { get; }

    internal TransparencyPainter Transparency { get; }

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
                apply(this, _store.Resolve(raw), resources);
            }
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
            FillShape(device.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points), evenOdd);
        }

        if (stroke)
        {
            StrokePath(device);
        }

        if (_pendingClip is { } clipEvenOdd)
        {
            _state.Clip = ClipMask.Intersect(_state.Clip, device.Select(p => (IReadOnlyList<(double X, double Y)>)p.Points), clipEvenOdd, _target.Width, _target.Height);
            _pendingClip = null;
        }

        _path.Clear();
    }

    /// <summary>Fills device polygons with the fill colour or pattern of the graphics state.</summary>
    public void FillShape(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool evenOdd)
    {
        if (_state.FillPaint is { } paint)
        {
            _target.Fill(polygons, evenOdd, paint, _state.FillAlpha, _state.Compositing);
        }
        else
        {
            _target.Fill(polygons, evenOdd, _state.FillColor, _state.FillAlpha, _state.Compositing);
        }
    }

    public void StrokePath(List<Polyline> device)
    {
        var scale = Math.Sqrt(Math.Abs((_state.Ctm.A * _state.Ctm.D) - (_state.Ctm.B * _state.Ctm.C)));
        var width = _state.LineWidth * scale;
        var dash = _state.Dash is { Length: > 0 } d ? d.Select(v => v * scale).ToArray() : null;
        var outline = Stroker.Outline(device, new StrokeStyle(width, _state.LineCap, _state.LineJoin, _state.MiterLimit, dash, _state.DashPhase * scale));
        if (_state.StrokePaint is { } paint)
        {
            _target.Fill(outline, false, paint, _state.StrokeAlpha, _state.Compositing);
        }
        else
        {
            _target.Fill(outline, false, _state.StrokeColor, _state.StrokeAlpha, _state.Compositing);
        }
    }

    private void SetSpace(bool fill, IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (o.Count == 0 || _colourLocked)
        {
            return;
        }

        var space = PdfColorSpace.Resolve(_store, o[0], resources);
        SetColor(fill, space, Enumerable.Repeat<PdfObject>(new PdfNumber(0, true), space.Components).ToList());
        var underlying = TilingPattern.Underlying(_store, o[0], resources);
        if (fill)
        {
            _state.FillUnderlying = underlying;
        }
        else
        {
            _state.StrokeUnderlying = underlying;
        }
    }

    private void SetColor(bool fill, PdfColorSpace space, IReadOnlyList<PdfObject> operands)
    {
        if (_colourLocked)
        {
            return;
        }

        var values = operands.OfType<PdfNumber>().Select(n => n.Value).ToArray();
        var color = values.Length == 0 ? PdfColor.Black : GraphicsState.ToRgb(space, values);
        if (fill)
        {
            (_state.FillSpace, _state.FillColor, _state.FillPaint) = (space, color, null);
        }
        else
        {
            (_state.StrokeSpace, _state.StrokeColor, _state.StrokePaint) = (space, color, null);
        }
    }

    // In a Pattern colour space the operands end with the name of the pattern (after the colour of an uncoloured
    // tiling pattern); other spaces give components.
    private void SetColorOrPattern(bool fill, IReadOnlyList<PdfObject> o, PdfDictionary? resources)
    {
        if (o.Count == 0 || o[^1] is not PdfName name)
        {
            SetColor(fill, fill ? _state.FillSpace : _state.StrokeSpace, o);
            return;
        }

        if (_colourLocked)
        {
            return;
        }

        var pattern = _store.Get(_store.Get<PdfDictionary>(resources, "Pattern"), name.Value);
        var colour = o.Take(o.Count - 1).OfType<PdfNumber>().Select(n => n.Value).ToArray();
        if (Pattern(pattern, colour, fill ? _state.FillUnderlying : _state.StrokeUnderlying, resources) is not { } paint)
        {
            return;
        }

        if (fill)
        {
            _state.FillPaint = paint;
        }
        else
        {
            _state.StrokePaint = paint;
        }
    }

    // A pattern is placed by its own matrix in the default space of the content stream that uses it (§8.7.3.1).
    private PatternPaint? Pattern(PdfObject? value, double[] colour, PdfColorSpace? underlying, PdfDictionary? resources)
    {
        if (_store.Resolve(value) is PdfDictionary pattern && _store.Number(pattern, "PatternType") == 2)
        {
            if (Shading.Read(_store, _store.Get(pattern, "Shading"), resources) is { } shading)
            {
                return new ShadingPaint(shading, Matrix.FromArray(_store.Get<PdfArray>(pattern, "Matrix")).Multiply(_patternBase), background: true);
            }

            Context.Gap("shadings of this kind are not drawn");
            return null;
        }

        if (TilingPattern.Read(_store, value) is { } tiling)
        {
            var uncoloured = tiling.Uncoloured ? GraphicsState.ToRgb(underlying ?? PdfColorSpace.Gray, colour) : (PdfColor?)null;
            if (Tiling(tiling, uncoloured, resources) is { } paint)
            {
                return paint;
            }
        }

        Context.Gap("patterns that cannot be read are drawn as a flat colour");
        return null;
    }

    // The cell is drawn once per device placement and colour on a transparent raster, clipped to its box; an
    // uncoloured cell is painted with the given colour, its own colour operators ignored (§8.7.3.3).
    private TilingPaint? Tiling(TilingPattern pattern, PdfColor? colour, PdfDictionary? resources)
    {
        var toDevice = pattern.Matrix.Multiply(_patternBase);
        if (_tiles.TryGetValue((pattern.Stream, toDevice, colour), out var cached))
        {
            return cached;
        }

        if (_depth >= MaxDepth || !_activeForms.Add(pattern.Stream))
        {
            return null;
        }

        var (scale, width, height, toCell) = TilingPaint.Layout(pattern, toDevice);
        var cell = new GroupCanvas(width, height, new PixelBounds(0, 0, width, height), null, false);
        var state = new GraphicsState { Ctm = toCell, FillColor = colour ?? PdfColor.Black, StrokeColor = colour ?? PdfColor.Black };
        _depth++;
        try
        {
            RunOn(cell, state, toCell, () =>
            {
                _colourLocked = colour is not null;
                var (x0, y0, x1, y1) = pattern.Box;
                _path.Rectangle(x0, y0, x1 - x0, y1 - y0);
                _pendingClip = false;
                Paint(fill: false, stroke: false, evenOdd: false);
                Run(_store.DecodeBytes(pattern.Stream), _store.Get<PdfDictionary>(pattern.Stream, "Resources") ?? resources);
            });
        }
        finally
        {
            _depth--;
            _activeForms.Remove(pattern.Stream);
        }

        var paint = new TilingPaint(pattern, toDevice, cell, scale);
        _tiles[(pattern.Stream, toDevice, colour)] = paint;
        return paint;
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

        IReadOnlyList<(double X, double Y)> page = [(0, 0), (_target.Width, 0), (_target.Width, _target.Height), (0, _target.Height)];
        _target.Fill([page], false, new ShadingPaint(shading, _state.Ctm, background: false), _state.FillAlpha, _state.Compositing);
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

    /// <summary>
    /// Runs a form XObject (or an annotation appearance) under the current state, clipped to its box; a transparency
    /// group form is drawn as a group.
    /// </summary>
    public void RunForm(PdfStream form, PdfDictionary? resources, Matrix? extra = null)
    {
        if (_depth >= MaxDepth || !_activeForms.Add(form))
        {
            return;
        }

        _depth++;
        _saved.Push(_state with { });
        var patternBase = _patternBase;
        try
        {
            _state.Ctm = Matrix.FromArray(_store.Get<PdfArray>(form, "Matrix")).Multiply(extra ?? Matrix.Identity).Multiply(_state.Ctm);
            _patternBase = _state.Ctm;
            var box = FormBox(form);
            if (box is { } b)
            {
                _path.Rectangle(b.X0, b.Y0, b.X1 - b.X0, b.Y1 - b.Y0);
                _pendingClip = false;
                Paint(fill: false, stroke: false, evenOdd: false);
            }

            var formResources = _store.Get<PdfDictionary>(form, "Resources") ?? resources;
            if (TransparencyPainter.Group(_store, form) is { } group)
            {
                Transparency.PaintGroup(form, group, formResources, box);
            }
            else
            {
                Run(_store.DecodeBytes(form), formResources);
            }
        }
        finally
        {
            _state = _saved.Pop();
            _patternBase = patternBase;
            _activeForms.Remove(form);
            _depth--;
        }
    }

    /// <summary>The BBox of a form, null when it has none.</summary>
    public (double X0, double Y0, double X1, double Y1)? FormBox(PdfStream form)
    {
        if (_store.Get<PdfArray>(form, "BBox") is not { Count: 4 } box)
        {
            return null;
        }

        var values = box.Items.Select(i => Number(_store.Resolve(i) ?? PdfNull.Instance)).ToArray();
        return (values[0], values[1], values[2], values[3]);
    }

    /// <summary>
    /// Runs drawing on another canvas from a given graphics state (with a fresh stack of saved states), then restores
    /// the canvas, the state, the pattern base (kept when null) and the colour lock.
    /// </summary>
    public void RunOn(Canvas target, GraphicsState state, Matrix? patternBase, Action draw)
    {
        var saved = (_target, _state, _saved, _patternBase, _colourLocked, _pendingClip);
        (_target, _state, _saved, _patternBase, _pendingClip) = (target, state, new Stack<GraphicsState>(), patternBase ?? _patternBase, null);
        try
        {
            draw();
        }
        finally
        {
            (_target, _state, _saved, _patternBase, _colourLocked, _pendingClip) = saved;
        }
    }

    public static double Number(PdfObject value) => value is PdfNumber n ? n.Value : 0;

    private static double N(IReadOnlyList<PdfObject> operands, int index) => index < operands.Count ? Number(operands[index]) : 0;
}
