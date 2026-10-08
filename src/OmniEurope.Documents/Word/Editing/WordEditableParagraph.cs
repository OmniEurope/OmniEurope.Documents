// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word.Reading;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// A paragraph of a document opened with <see cref="WordEditor"/>, addressed as <c>part#index</c> (the
/// index counts the paragraphs of the part in document order) and located by <see cref="Location"/>.
/// Addresses and locations stay valid while no paragraph is added or removed.
/// </summary>
public sealed class WordEditableParagraph
{
    private readonly WordEditContext _context;
    private readonly XElement _element;

    internal WordEditableParagraph(WordEditContext context, string partName, int index, XElement element, WordParagraphLocation location)
    {
        _context = context;
        _element = element;
        PartName = partName;
        Index = index;
        Location = location;
    }

    /// <summary>The part holding the paragraph (<c>word/document.xml</c>, <c>word/header1.xml</c>...).</summary>
    public string PartName { get; }

    /// <summary>Position among the paragraphs of the part, from 0.</summary>
    public int Index { get; }

    /// <summary>The stable address <c>part#index</c>.</summary>
    public string Address => PartName + "#" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Where the paragraph sits: part, note or comment, and block path.</summary>
    public WordParagraphLocation Location { get; }

    /// <summary>The paragraph style id.</summary>
    public string? StyleId => Val(_element.Element(W + "pPr"), "pStyle");

    /// <summary>The visible text, field results included, deleted revisions and text boxes excluded.</summary>
    public string Text => Read().Text;

    /// <summary>The text that <see cref="SetText(string, string?)"/> replaces: text runs only, fields excluded.</summary>
    public string EditableText => WordRunScanner.Text(WordRunScanner.Runs(_element));

    /// <summary>A model snapshot of the paragraph as it is now.</summary>
    public WordParagraph Read() => new WordContentReader(_context.Read, PartName).Paragraph(_element);

    /// <summary>
    /// The paragraph's own runs in document order: never those of a text box it anchors, never deleted runs
    /// (<c>w:del</c>, <c>w:moveFrom</c>), never those of an alternate-content fallback. A snapshot: read it again
    /// after an edit.
    /// </summary>
    public IReadOnlyList<WordEditableRun> Runs()
    {
        var paragraph = ResolveProperties();
        var table = TableStyleId();
        return WordRunScanner.Runs(_element).Select((run, index) =>
        {
            var direct = WordPropertyReader.Run(run.Element.Element(W + "rPr")) ?? WordRunProperties.Empty;
            var text = run.IsText ? WordRunScanner.Text(run.Element) : string.Empty;
            return new WordEditableRun(index, run.Kind, text, direct, _context.Styles.ResolveRun(paragraph, direct, table)) { ElementName = run.Name };
        }).ToList();
    }

    /// <summary>The paragraph formatting it shows: document defaults, table style, paragraph style chain, list
    /// level, then its direct formatting.</summary>
    public WordParagraphProperties ResolveProperties() =>
        _context.Styles.ResolveParagraph(WordPropertyReader.Paragraph(_element.Element(W + "pPr")) ?? WordParagraphProperties.Empty, _context.Numbering, TableStyleId());

    /// <summary>The character formatting the paragraph gives its text before any run formatting: document
    /// defaults, table style and paragraph style chain, theme fonts resolved (each run's own result is
    /// <see cref="WordEditableRun.ResolvedProperties"/>).</summary>
    public WordRunProperties ResolveRunProperties() => _context.Styles.ResolveRun(ResolveProperties(), WordRunProperties.Empty, TableStyleId());

    /// <summary>
    /// Replaces the text runs with <paramref name="text"/> in the formatting of the first text run; fields,
    /// notes, pictures and breaks stay where they are. <paramref name="language"/> sets <c>w:lang</c>. Nothing
    /// changes when the text and language are already those.
    /// </summary>
    public void SetText(string text, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        SetText([new WordTextPiece(text)], language);
    }

    /// <summary>Replaces the text runs with pieces, each formatted as the first text run with its own
    /// <see cref="WordTextPiece.Format"/> applied on top.</summary>
    public void SetText(IEnumerable<WordTextPiece> pieces, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        var list = pieces.Where(p => p.Text.Length > 0).ToList();
        var runs = WordRunScanner.Runs(_element);
        var textRuns = runs.Where(r => r.IsText).Select(r => r.Element).ToList();
        if (language is null && list.All(p => p.Format is null) && string.Concat(list.Select(p => p.Text)) == WordRunScanner.Text(runs))
        {
            return;
        }

        var template = WordRunBuilder.Template(_element, textRuns.FirstOrDefault());
        var created = list.Select(p => WordRunBuilder.NewRun(p, template, language)).ToList();
        if (textRuns.Count > 0)
        {
            textRuns[0].AddBeforeSelf(created);
        }
        else
        {
            _element.Add(created);
        }

        foreach (var run in textRuns)
        {
            var parent = run.Parent;
            run.Remove();
            WordRunBuilder.RemoveIfEmpty(_element, parent);
        }
    }

    /// <summary>
    /// Rewrites the paragraph's own runs from <paramref name="pieces"/>, in order: each <see cref="WordTextPiece"/>
    /// becomes a new run in the format of the paragraph's first text run (the base format) with
    /// <see cref="WordTextPiece.Format"/> applied on top (tab and line break as <c>w:tab</c> and <c>w:br</c>), and
    /// each <see cref="WordKeptRun"/> moves that run of <see cref="Runs"/> to this place (a run of a field moves
    /// the whole field, begin to end; a run named twice stays at its first place; a kept text run is kept as
    /// it is). The other text runs are removed. A run that is not text and that no piece names is never
    /// dropped: it follows the new content, in its original order. A run moves with the wrappers that hold
    /// nothing else (hyperlink, simple field, content control, tracked insertion). The new content goes where
    /// the first replaced text run was, outside the hyperlinks, smart tags, custom XML and tracked insertions
    /// or moves that held only replaced text (but inside a content control); with no text run replaced, where
    /// the first run was.
    /// <paramref name="language"/>, when given, sets <c>w:lang</c> on the new runs. Paragraph properties,
    /// bookmarks, comment anchors, deleted runs and everything else outside the own runs stay. Nothing changes
    /// when the pieces give the text and run order the paragraph already has and no format or language is
    /// given.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A kept run index is not one of <see cref="Runs"/>.</exception>
    /// <exception cref="ArgumentException">A piece is null.</exception>
    public void SetContent(IEnumerable<WordContentPiece> pieces, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        WordContentRewriter.Apply(_element, pieces, language);
    }

    // The style of the table holding the paragraph (not crossing a text box).
    private string? TableStyleId() =>
        _element.Ancestors().TakeWhile(a => a.Name != W + "txbxContent").FirstOrDefault(a => a.Name == W + "tbl") is { } table
            ? Val(table.Element(W + "tblPr"), "tblStyle")
            : null;
}
