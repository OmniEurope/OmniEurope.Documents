// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// One glyph drawn on a page: its Unicode text, the start of its baseline (<see cref="X"/>, <see cref="Y"/>), its
/// advance along the baseline, its box, font name and effective size, and its direction. Coordinates are
/// PDF user space (points, origin bottom-left, page rotation not applied).
/// </summary>
public sealed record PdfLetter(string Value, double X, double Y, double Width, PdfRectangle BoundingBox, string FontName, double FontSize, double Rotation, int RenderingMode);

/// <summary>Runs the text operators of a page (and of the form XObjects it draws) and collects the letters.</summary>
internal sealed class TextExtractor
{
    private const int MaxFormDepth = 12;
    private readonly PdfObjectStore _store;
    private readonly List<PdfLetter> _letters = [];
    private readonly Dictionary<PdfDictionary, PdfFontDecoder> _fonts = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<PdfStream> _activeForms = new(ReferenceEqualityComparer.Instance);

    // The operators that change what text is drawn and where, with the operand count each needs.
    private static readonly Dictionary<string, (int Operands, Action<TextExtractor, RunContext, IReadOnlyList<PdfObject>> Run)> Operators = new(StringComparer.Ordinal)
    {
        ["q"] = (0, (_, c, _) => c.Saved.Push(c.State with { })),
        ["Q"] = (0, (_, c, _) => c.State = c.Saved.Count > 0 ? c.Saved.Pop() : c.State),
        ["cm"] = (0, (_, c, o) => c.State.Ctm = Matrix.FromOperands(o).Multiply(c.State.Ctm)),
        ["BT"] = (0, (_, c, _) => c.State.Tm = c.State.Tlm = Matrix.Identity),
        ["Tf"] = (2, (x, c, o) => (c.State.Font, c.State.FontSize) = (x.Font(c.Resources, o[0] as PdfName), Number(o[1]))),
        ["Tc"] = (1, (_, c, o) => c.State.CharSpacing = Number(o[0])),
        ["Tw"] = (1, (_, c, o) => c.State.WordSpacing = Number(o[0])),
        ["Tz"] = (1, (_, c, o) => c.State.HorizontalScale = Number(o[0]) / 100),
        ["TL"] = (1, (_, c, o) => c.State.Leading = Number(o[0])),
        ["Ts"] = (1, (_, c, o) => c.State.Rise = Number(o[0])),
        ["Tr"] = (1, (_, c, o) => c.State.RenderMode = (int)Number(o[0])),
        ["Td"] = (2, (_, c, o) => c.State.MoveLine(Number(o[0]), Number(o[1]))),
        ["TD"] = (2, (_, c, o) =>
        {
            c.State.Leading = -Number(o[1]);
            c.State.MoveLine(Number(o[0]), Number(o[1]));
        }),
        ["Tm"] = (6, (_, c, o) => c.State.Tm = c.State.Tlm = Matrix.FromOperands(o)),
        ["T*"] = (0, (_, c, _) => c.State.MoveLine(0, -c.State.Leading)),
        ["Tj"] = (1, (x, c, o) => x.Show(c.State, o[^1])),
        ["'"] = (1, (x, c, o) => ShowNextLine(x, c, o[^1])),
        ["\""] = (3, ShowSpaced),
        ["TJ"] = (1, (x, c, o) =>
        {
            if (o[^1] is PdfArray array)
            {
                x.ShowArray(c.State, array);
            }
        }),
        ["Do"] = (1, (x, c, o) =>
        {
            if (c.Depth < MaxFormDepth)
            {
                x.RunForm(c.Resources, o[0] as PdfName, c.State.Ctm, c.Depth);
            }
        }),
    };

    private TextExtractor(PdfObjectStore store) => _store = store;

    public static IReadOnlyList<PdfLetter> Extract(PdfPage page)
    {
        var extractor = new TextExtractor(page.Store);
        extractor.Run(page.ContentBytes(), page.Resources, Matrix.Identity, 0);
        return extractor._letters;
    }

    private void Run(byte[] content, PdfDictionary? resources, Matrix initial, int depth)
    {
        var context = new RunContext(new TextState { Ctm = initial }, resources, depth);
        foreach (var operation in ContentStreamReader.Read(content))
        {
            if (Operators.TryGetValue(operation.Operator, out var op) && operation.Operands.Count >= op.Operands)
            {
                op.Run(this, context, operation.Operands);
            }
        }
    }

    private static void ShowNextLine(TextExtractor extractor, RunContext context, PdfObject text)
    {
        context.State.MoveLine(0, -context.State.Leading);
        extractor.Show(context.State, text);
    }

    private static void ShowSpaced(TextExtractor extractor, RunContext context, IReadOnlyList<PdfObject> o)
    {
        context.State.WordSpacing = Number(o[0]);
        context.State.CharSpacing = Number(o[1]);
        ShowNextLine(extractor, context, o[2]);
    }

    private PdfStream? FormNamed(PdfDictionary? resources, PdfName? name) =>
        name is not null && _store.Get(_store.Get<PdfDictionary>(resources, "XObject"), name.Value) is PdfStream stream
        && _store.Get(stream, "Subtype") is PdfName { Value: "Form" }
            ? stream
            : null;
    private void RunForm(PdfDictionary? resources, PdfName? name, Matrix ctm, int depth)
    {
        // A form that is already being run (a form drawing itself) is skipped.
        if (FormNamed(resources, name) is not { } form || !_activeForms.Add(form))
        {
            return;
        }

        try
        {
            var matrix = Matrix.FromArray(_store.Get<PdfArray>(form, "Matrix")).Multiply(ctm);
            Run(_store.DecodeBytes(form), _store.Get<PdfDictionary>(form, "Resources") ?? resources, matrix, depth + 1);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            // An undecodable form is skipped.
        }
        finally
        {
            _activeForms.Remove(form);
        }
    }

    private PdfFontDecoder? Font(PdfDictionary? resources, PdfName? name)
    {
        var fonts = _store.Get<PdfDictionary>(resources, "Font");
        if (name is null || _store.Get(fonts, name.Value) is not PdfDictionary dictionary)
        {
            return null;
        }

        if (!_fonts.TryGetValue(dictionary, out var decoder))
        {
            decoder = new PdfFontDecoder(_store, dictionary);
            _fonts[dictionary] = decoder;
        }

        return decoder;
    }

    private void ShowArray(TextState state, PdfArray array)
    {
        foreach (var item in array.Items)
        {
            if (item is PdfNumber adjustment)
            {
                state.Advance(-adjustment.Value / 1000 * state.FontSize * state.HorizontalScale);
            }
            else
            {
                Show(state, item);
            }
        }
    }

    private void Show(TextState state, PdfObject text)
    {
        if (text is not PdfString s || state.Font is not { } font)
        {
            return;
        }

        foreach (var glyph in font.Decode(s.Bytes))
        {
            var trm = new Matrix(state.FontSize * state.HorizontalScale, 0, 0, state.FontSize, 0, state.Rise).Multiply(state.Tm).Multiply(state.Ctm);
            var advance = ((glyph.Advance * state.FontSize) + state.CharSpacing + (glyph.IsWordSpace ? state.WordSpacing : 0)) * state.HorizontalScale;
            AddLetter(glyph, font, state, trm, advance);
            state.Advance(advance);
        }
    }

    private void AddLetter(DecodedGlyph glyph, PdfFontDecoder font, TextState state, Matrix trm, double advance)
    {
        var (x, y) = trm.Transform(0, 0);
        var width = Math.Max(glyph.Advance, 0);
        var corners = new[] { trm.Transform(0, font.Descent), trm.Transform(width, font.Descent), trm.Transform(0, font.Ascent), trm.Transform(width, font.Ascent) };
        var box = new PdfRectangle(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
        var (endX, endY) = new Matrix(state.HorizontalScale, 0, 0, 1, 0, 0).Multiply(state.Tm).Multiply(state.Ctm).Transform(advance, 0);
        var (originX, originY) = state.Tm.Multiply(state.Ctm).Transform(0, 0);
        var length = Math.Sqrt(((endX - originX) * (endX - originX)) + ((endY - originY) * (endY - originY)));
        var size = state.FontSize * Math.Sqrt((trm.C * trm.C) + (trm.D * trm.D)) / Math.Max(Math.Abs(state.FontSize), 1e-9);
        var rotation = Math.Round(Math.Atan2(trm.B, trm.A) * 180 / Math.PI, 2);
        var name = font.BaseFont.Contains('+') ? font.BaseFont[(font.BaseFont.IndexOf('+') + 1)..] : font.BaseFont;
        _letters.Add(new PdfLetter(glyph.Text, x, y, length, box, name, Math.Round(size, 3), rotation, state.RenderMode));
    }

    private static double Number(PdfObject value) => value is PdfNumber n ? n.Value : 0;

    /// <summary>The state of one content stream run: the text state, its saved copies and where it runs.</summary>
    private sealed class RunContext(TextState state, PdfDictionary? resources, int depth)
    {
        public TextState State { get; set; } = state;

        public Stack<TextState> Saved { get; } = new();

        public PdfDictionary? Resources { get; } = resources;

        public int Depth { get; } = depth;
    }

    private sealed record TextState
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

        public int RenderMode { get; set; }

        public void MoveLine(double tx, double ty)
        {
            Tlm = new Matrix(1, 0, 0, 1, tx, ty).Multiply(Tlm);
            Tm = Tlm;
        }

        public void Advance(double tx) => Tm = new Matrix(1, 0, 0, 1, tx, 0).Multiply(Tm);
    }
}
