// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>What one redaction run removed and met, shared by the page and the forms it draws.</summary>
internal sealed class RedactionContext(PdfObjectStore store, IReadOnlyList<PdfRectangle> areas, PdfColor fill, Func<PdfStream, PdfObject> register)
{
    public PdfObjectStore Store { get; } = store;

    public IReadOnlyList<PdfRectangle> Areas { get; } = areas;

    public PdfColor Fill { get; } = fill;

    /// <summary>Adds a new stream to the output and returns the placeholder that stands for it in source objects.</summary>
    public Func<PdfStream, PdfObject> Register { get; } = register;

    public Dictionary<PdfDictionary, PdfFontDecoder> Fonts { get; } = new(ReferenceEqualityComparer.Instance);

    public HashSet<PdfStream> ActiveForms { get; } = new(ReferenceEqualityComparer.Instance);

    public System.Text.StringBuilder RemovedText { get; } = new();

    public int RemovedGlyphs { get; set; }

    public int RemovedImages { get; set; }

    public int BlankedImages { get; set; }

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    public bool Touches(PdfRectangle box) => Areas.Any(a => box.Left < a.Right && box.Right > a.Left && box.Bottom < a.Top && box.Top > a.Bottom);

    public bool Covers(PdfRectangle box) => Areas.Any(a => box.Left >= a.Left && box.Right <= a.Right && box.Bottom >= a.Bottom && box.Top <= a.Top);

    public static PdfRectangle Bounds(Matrix matrix, double width = 1, double height = 1, double x = 0, double y = 0)
    {
        var corners = new[] { matrix.Transform(x, y), matrix.Transform(x + width, y), matrix.Transform(x, y + height), matrix.Transform(x + width, y + height) };
        return new PdfRectangle(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
    }

    public static PdfStream Compressed(byte[] data) => (PdfStream)new PdfStream(PngCodec.Deflate(data, CompressionLevel.Optimal)).SetName("Filter", "FlateDecode");
}

/// <summary>
/// Rewrites a content stream without what lies under the redaction areas: each glyph whose box meets an area is
/// taken out of its string (the glyphs after it keep their place through a <c>TJ</c> adjustment), an image that an
/// area covers is dropped, one it meets is redrawn with the pixels under the area set to the fill colour, inline
/// images an area meets are dropped, forms an area meets are rewritten the same way, and marked content around
/// removed text loses its <c>ActualText</c>, <c>Alt</c> and <c>E</c> entries. Everything else is written back as read.
/// </summary>
internal sealed class RedactionRewriter
{
    private const int MaxFormDepth = 12;
    private static readonly string[] ReplacementKeys = ["ActualText", "Alt", "E"];

    private readonly RedactionContext _context;
    private readonly PdfDictionary? _resources;
    private readonly int _depth;
    private readonly List<ContentOperation> _output = [];
    private readonly Stack<int> _marked = new();
    private readonly HashSet<int> _dirtyMarks = [];
    private readonly Dictionary<string, PdfObject> _newXObjects = new(StringComparer.Ordinal);
    private readonly HashSet<string> _touchedXObjects = new(StringComparer.Ordinal);
    private RedactionState _state;
    private readonly Stack<RedactionState> _saved = new();
    private readonly RedactionPatterns _patterns;
    private readonly HashSet<string> _removedPatterns = new(StringComparer.Ordinal);
    private bool _changed;

    private RedactionRewriter(RedactionContext context, PdfDictionary? resources, Matrix ctm, int depth, RedactionState? inherited)
    {
        _context = context;
        _resources = resources;
        _depth = depth;
        _patterns = new RedactionPatterns(context);

        // The pattern colours are named in the resources of the stream that set them: a form's own are not those.
        _state = inherited is null ? new RedactionState { Ctm = ctm } : inherited with { Ctm = ctm, Tm = Matrix.Identity, Tlm = Matrix.Identity, FillPattern = null, StrokePattern = null };
    }

    // The rewritten content and resources; null when nothing under the areas was found. For a form, "inherited" is the
    // graphics state in force at its Do, whose text state (font, size, spacing, scale, leading, rise) the form's
    // content starts with (ISO 32000-1 8.10.1).
    public static (byte[] Content, PdfDictionary? Resources)? Rewrite(RedactionContext context, byte[] content, PdfDictionary? resources, Matrix ctm, int depth, RedactionState? inherited = null)
    {
        var rewriter = new RedactionRewriter(context, resources, ctm, depth, inherited);
        foreach (var operation in ContentStreamReader.Read(content))
        {
            rewriter.Step(operation);
        }

        return rewriter._changed ? (rewriter.Serialize(), rewriter.PatchedResources()) : null;
    }

    private void Step(ContentOperation operation)
    {
        if (!Replaced(operation))
        {
            _output.Add(operation);
        }
    }

    // True when the operation was replaced or dropped; otherwise its effect on the state is followed.
    private bool Replaced(ContentOperation operation)
    {
        var o = operation.Operands;
        if (operation.Operator is "Tj" or "'" or "\"" or "TJ")
        {
            return o.Count > 0 && Show(operation);
        }

        if (operation.Operator == "Do")
        {
            return o.Count > 0 && o[0] is PdfName name && Draw(name);
        }

        if (operation.Operator == "BI")
        {
            return DropInline();
        }

        Track(operation);
        return false;
    }

    private void Track(ContentOperation operation)
    {
        switch (operation.Operator)
        {
            case "q":
                _saved.Push(_state with { });
                break;
            case "Q":
                _state = _saved.Count > 0 ? _saved.Pop() : _state;
                break;
            case "BDC" or "BMC":
                _marked.Push(_output.Count);
                break;
            case "EMC":
                _marked.TryPop(out _);
                break;
            default:
                TrackPath(operation);
                break;
        }
    }

    // Path construction and painting, which tell where a pattern colour is used; the rest moves the state.
    private void TrackPath(ContentOperation operation)
    {
        if (RedactionPatterns.Constructs(operation.Operator))
        {
            _patterns.Construct(operation, _state.Ctm);
        }
        else if (RedactionPatterns.Ends(operation.Operator))
        {
            RemoveShowingPatterns(_patterns.Painted(operation.Operator, _state));
        }
        else
        {
            _state.Apply(operation, this);
        }
    }

    // A tiling pattern that shows text or images and is painted under an area leaves the page resources, for it is
    // redrawn by the viewer in every cell and cannot be rewritten with the page: the area is painted over its place.
    private void RemoveShowingPatterns(IEnumerable<string> names)
    {
        foreach (var name in names.Where(n => !_removedPatterns.Contains(n) && _patterns.ShowsContent(_resources, n)))
        {
            _removedPatterns.Add(name);
            _context.Gaps.Add("tiling patterns showing text or images under an area removed whole");
            _changed = true;
        }
    }

    private bool DropInline()
    {
        if (!_context.Touches(RedactionContext.Bounds(_state.Ctm)))
        {
            return false;
        }

        _context.RemovedImages++;
        _changed = true;
        return true;
    }
    internal PdfFontDecoder? Font(PdfName? name)
    {
        if (name is null || _context.Store.Get(_context.Store.Get<PdfDictionary>(_resources, "Font"), name.Value) is not PdfDictionary dictionary)
        {
            return null;
        }

        if (!_context.Fonts.TryGetValue(dictionary, out var decoder))
        {
            decoder = new PdfFontDecoder(_context.Store, dictionary);
            _context.Fonts[dictionary] = decoder;
        }

        return decoder;
    }

    // A text-showing operation: written back as it is when no glyph meets an area, else as a TJ without those glyphs.
    private bool Show(ContentOperation operation)
    {
        var o = operation.Operands;
        var spaced = operation.Operator == "\"" && o.Count >= 3;
        var nextLine = operation.Operator is "'" or "\"";
        if (spaced)
        {
            _state.WordSpacing = Number(o[0]);
            _state.CharSpacing = Number(o[1]);
        }

        if (nextLine)
        {
            _state.MoveLine(0, -_state.Leading);
        }

        var kept = new List<PdfObject>();
        if (!ShowItems(operation.Operator == "TJ" ? (o[^1] as PdfArray)?.Items ?? [] : [o[^1]], kept))
        {
            return false;
        }

        _changed = true;
        _dirtyMarks.UnionWith(_marked);
        if (spaced)
        {
            _output.Add(new ContentOperation("Tw", [o[0]]));
            _output.Add(new ContentOperation("Tc", [o[1]]));
        }

        if (nextLine)
        {
            _output.Add(new ContentOperation("T*", []));
        }

        _output.Add(new ContentOperation("TJ", [new PdfArray(kept)]));
        return true;
    }

    private bool ShowItems(IEnumerable<PdfObject> items, List<PdfObject> kept)
    {
        var removed = false;
        foreach (var item in items)
        {
            if (item is PdfNumber adjustment)
            {
                _state.Advance(-adjustment.Value / 1000 * _state.FontSize * _state.HorizontalScale);
                kept.Add(item);
            }
            else if (item is PdfString text)
            {
                removed |= ShowString(text, kept);
            }
        }

        return removed;
    }
    private bool ShowString(PdfString text, List<PdfObject> kept)
    {
        if (_state.Font is not { } font)
        {
            // Without the font the glyph boxes cannot be computed, so what lies under an area cannot be told from
            // what does not: keeping the text would give a document that looks redacted and is not.
            if (text.Bytes.Length == 0)
            {
                kept.Add(text);
                return false;
            }

            throw new PdfRedactionException("Text is shown with a font that cannot be resolved (no Tf before it, or a Tf naming a font missing from the resources), so it cannot be checked against the redaction areas; the redaction is refused.");
        }

        var removed = false;
        var segment = new List<byte>();
        double skipped = 0;
        foreach (var glyph in font.Decode(text.Bytes))
        {
            var trm = new Matrix(_state.FontSize * _state.HorizontalScale, 0, 0, _state.FontSize, 0, _state.Rise).Multiply(_state.Tm).Multiply(_state.Ctm);
            var advance = ((glyph.Advance * _state.FontSize) + _state.CharSpacing + (glyph.IsWordSpace ? _state.WordSpacing : 0)) * _state.HorizontalScale;
            var box = GlyphBox(font, glyph, trm);
            if (_context.Touches(box))
            {
                removed = true;
                skipped += advance;
                _context.RemovedGlyphs++;
                _context.RemovedText.Append(glyph.Text);
            }
            else
            {
                Flush(kept, segment, ref skipped);
                for (var i = glyph.Length - 1; i >= 0; i--)
                {
                    segment.Add((byte)(glyph.Code >> (8 * i)));
                }
            }

            _state.Advance(advance);
        }

        Flush(kept, segment, ref skipped);
        return removed;
    }

    // Where a glyph can draw. A Type 3 glyph is a drawing of any size: its box is the font's FontBBox through its
    // FontMatrix, and when that is unknown (absent, malformed or all zeros) the glyph is taken to reach a font size
    // away from its origin in every direction, so that an area near it removes it rather than leaving ink behind.
    private static PdfRectangle GlyphBox(PdfFontDecoder font, DecodedGlyph glyph, Matrix trm)
    {
        if (!font.IsType3)
        {
            return RedactionContext.Bounds(trm, Math.Max(glyph.Advance, 0), font.Ascent - font.Descent, 0, font.Descent);
        }

        return font.GlyphBounds is { } b
            ? RedactionContext.Bounds(trm, b.X1 - b.X0, b.Y1 - b.Y0, b.X0, b.Y0)
            : RedactionContext.Bounds(trm, Math.Max(glyph.Advance, 0) + 2, 2, -1, -1);
    }

    // The kept codes so far, then the room the removed glyphs took, as a TJ adjustment in thousandths of the font size.
    private void Flush(List<PdfObject> kept, List<byte> segment, ref double skipped)
    {
        if (segment.Count > 0)
        {
            kept.Add(new PdfString([.. segment], hex: true));
            segment.Clear();
        }

        if (skipped != 0)
        {
            var scale = _state.FontSize * _state.HorizontalScale;
            if (Math.Abs(scale) > 1e-9)
            {
                kept.Add(PdfNumber.Of(Math.Round(-skipped * 1000 / scale, 4)));
            }
            else
            {
                _context.Gaps.Add("glyphs after a redacted glyph drawn at size 0 may move");
            }

            skipped = 0;
        }
    }

    // An image or form XObject: true when the operation was replaced or dropped.
    private bool Draw(PdfName name)
    {
        var store = _context.Store;
        if (store.Get(store.Get<PdfDictionary>(_resources, "XObject"), name.Value) is not PdfStream stream)
        {
            return false;
        }

        var subtype = store.Get(stream, "Subtype") is PdfName { } kind ? kind.Value : string.Empty;
        var replaced = subtype switch
        {
            "Image" => DrawImage(name, stream),
            "Form" => DrawForm(name, stream),
            _ => false,
        };
        if (replaced)
        {
            _touchedXObjects.Add(name.Value);
        }

        return replaced;
    }

    private bool DrawImage(PdfName name, PdfStream image)
    {
        var bounds = RedactionContext.Bounds(_state.Ctm);
        if (!_context.Touches(bounds))
        {
            return false;
        }

        _changed = true;
        if (_context.Covers(bounds) || RedactionImages.Blank(_context, image, _resources, _state.Ctm) is not { } blanked)
        {
            _context.RemovedImages++;
            return true;
        }

        _context.BlankedImages++;
        Replace(name, _context.Register(blanked));
        return true;
    }

    private bool DrawForm(PdfName name, PdfStream form)
    {
        var store = _context.Store;
        var matrix = Matrix.FromArray(store.Get<PdfArray>(form, "Matrix")).Multiply(_state.Ctm);
        var box = store.Get<PdfArray>(form, "BBox") is { Count: 4 } bbox
            ? RedactionContext.Bounds(matrix, Number(bbox[2]) - Number(bbox[0]), Number(bbox[3]) - Number(bbox[1]), Number(bbox[0]), Number(bbox[1]))
            : RedactionContext.Bounds(Matrix.Identity, 1e9, 1e9, -5e8, -5e8);
        if (!_context.Touches(box))
        {
            return false;
        }

        // What the form paints with the pattern colour in force at the Do cannot be followed into its own resources.
        RemoveShowingPatterns(new[] { _state.FillPattern, _state.StrokePattern }.OfType<string>());

        if (_depth >= MaxFormDepth || !_context.ActiveForms.Add(form))
        {
            _context.Gaps.Add("forms nested too deep or drawing themselves under an area removed whole");
            _changed = true;
            return true;
        }

        try
        {
            // Resources (ISO 32000-1 8.10.1, ISO 32000-2 8.10.2): a form with its own /Resources is self-contained, so
            // a name its dictionaries lack is not looked up in the page's (the text is then refused, never guessed at);
            // only a form without /Resources (PDF 1.1 and earlier) takes those of the page that draws it. The text
            // state, font included, is part of the graphics state the form inherits at the Do.
            var own = store.Get<PdfDictionary>(form, "Resources");
            if (Rewrite(_context, store.DecodeBytes(form), own ?? _resources, matrix, _depth + 1, _state) is not { } rewritten)
            {
                return false;
            }

            var copy = RedactionContext.Compressed(rewritten.Content);
            foreach (var (key, value) in form.Entries.Where(e => e.Key is not ("Length" or "Filter" or "DecodeParms" or "Resources")))
            {
                copy.Set(key, value);
            }

            copy.Set("Resources", own is null ? DrawnOnly(rewritten.Resources, rewritten.Content) : rewritten.Resources);
            _changed = true;
            Replace(name, _context.Register(copy));
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            _context.Gaps.Add("undecodable forms under an area removed whole");
            _changed = true;
            return true;
        }
        finally
        {
            _context.ActiveForms.Remove(form);
        }
    }

    // The page resources a form without its own inherited, keeping only the XObjects the rewritten form draws: the
    // page's entries for the form itself and for drawings the page replaced would otherwise carry the originals,
    // with the text and pixels that were removed, into the output.
    private PdfDictionary? DrawnOnly(PdfDictionary? resources, byte[] content)
    {
        if (_context.Store.Get<PdfDictionary>(resources, "XObject") is not { } xobjects)
        {
            return resources;
        }

        var drawn = ContentStreamReader.Read(content).Where(o => o.Operator == "Do" && o.Operands.Count > 0 && o.Operands[0] is PdfName).Select(o => ((PdfName)o.Operands[0]).Value).ToHashSet(StringComparer.Ordinal);
        var kept = new PdfDictionary();
        foreach (var (key, value) in xobjects.Entries.Where(e => drawn.Contains(e.Key)))
        {
            kept.Set(key, value);
        }

        var copy = new PdfDictionary();
        foreach (var (key, value) in resources!.Entries)
        {
            copy.Set(key, value);
        }

        return copy.Set("XObject", kept);
    }

    private void Replace(PdfName name, PdfObject placeholder)
    {
        var fresh = name.Value + "_R" + (_newXObjects.Count + 1).ToString(CultureInfo.InvariantCulture);
        _newXObjects[fresh] = placeholder;
        _output.Add(new ContentOperation("Do", [PdfName.Of(fresh)]));
    }

    private PdfDictionary WithoutRemovedPatterns()
    {
        var patterns = new PdfDictionary();
        foreach (var (key, value) in (_context.Store.Get<PdfDictionary>(_resources, "Pattern")?.Entries ?? []).Where(e => !_removedPatterns.Contains(e.Key)))
        {
            patterns.Set(key, value);
        }

        return patterns;
    }

    private PdfDictionary? PatchedResources()
    {
        // A drawing replaced or removed everywhere it was used leaves the resources, so its original is not written.
        var stillUsed = _output.Where(o => o.Operator == "Do" && o.Operands.Count > 0 && o.Operands[0] is PdfName).Select(o => ((PdfName)o.Operands[0]).Value).ToHashSet(StringComparer.Ordinal);
        var unused = _touchedXObjects.Where(n => !stillUsed.Contains(n)).ToList();
        if (_newXObjects.Count == 0 && unused.Count == 0 && _removedPatterns.Count == 0)
        {
            return _resources;
        }

        var resources = new PdfDictionary();
        foreach (var (key, value) in _resources?.Entries ?? [])
        {
            resources.Set(key, value);
        }

        if (_removedPatterns.Count > 0)
        {
            resources.Set("Pattern", WithoutRemovedPatterns());
        }

        var xobjects = new PdfDictionary();
        foreach (var (key, value) in _context.Store.Get<PdfDictionary>(_resources, "XObject")?.Entries ?? [])
        {
            xobjects.Set(key, value);
        }

        foreach (var (key, value) in _newXObjects)
        {
            xobjects.Set(key, value);
        }

        foreach (var name in unused)
        {
            xobjects.Remove(name);
        }

        return resources.Set("XObject", xobjects);
    }

    private byte[] Serialize()
    {
        using var output = new MemoryStream();
        for (var i = 0; i < _output.Count; i++)
        {
            var operation = _output[i];
            var operands = _dirtyMarks.Contains(i) && operation.Operator == "BDC" ? Cleaned(operation.Operands) : operation.Operands;
            if (operation.Operator == "BI")
            {
                WriteInline(output, operation);
                continue;
            }

            foreach (var operand in operands)
            {
                PdfSerializer.Write(operand, output);
                output.WriteByte((byte)' ');
            }

            PdfSerializer.Ascii(output, operation.Operator + "\n");
        }

        return output.ToArray();
    }

    private static IReadOnlyList<PdfObject> Cleaned(IReadOnlyList<PdfObject> operands) =>
        operands.Select(o => o is PdfDictionary properties && ReplacementKeys.Any(properties.ContainsKey)
            ? properties.Entries.Where(e => !ReplacementKeys.Contains(e.Key)).Aggregate(new PdfDictionary(), (d, e) => d.Set(e.Key, e.Value))
            : o).ToList();

    private static void WriteInline(Stream output, ContentOperation operation)
    {
        PdfSerializer.Ascii(output, "BI\n");
        foreach (var (key, value) in ((PdfDictionary)operation.Operands[0]).Entries)
        {
            PdfSerializer.WriteName(key, output);
            output.WriteByte((byte)' ');
            PdfSerializer.Write(value, output);
            output.WriteByte((byte)'\n');
        }

        PdfSerializer.Ascii(output, "ID ");
        output.Write(operation.InlineData);
        PdfSerializer.Ascii(output, "\nEI\n");
    }

    internal static double Number(PdfObject value) => value is PdfNumber n ? n.Value : 0;
}

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
            case "Tm" when o.Count >= 6:
                Tm = Tlm = Matrix.FromOperands(o);
                break;
            case "Td" or "TD" when o.Count >= 2:
                Leading = operation.Operator == "TD" ? -N(1) : Leading;
                MoveLine(N(0), N(1));
                break;
            case "T*":
                MoveLine(0, -Leading);
                break;
            default:
                if (!ApplyPaint(operation.Operator, o))
                {
                    ApplySpacing(operation.Operator, o);
                }

                break;
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
