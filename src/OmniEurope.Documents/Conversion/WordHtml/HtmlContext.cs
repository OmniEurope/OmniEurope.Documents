// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>Shared state of one HTML conversion: the document, the options, list counters, note numbering,
/// picture sources, the note links already written and the gaps met.</summary>
internal sealed class HtmlContext
{
    private readonly HashSet<(WordNoteKind, int)> _referenced = [];

    public HtmlContext(WordDocument document, WordHtmlOptions options)
    {
        Document = document;
        Options = options;
        Lists = new WordListCounter(document.Numbering);
        Notes = new NoteNumbering(document);
        Images = new HtmlImages(Gaps);
    }

    public WordDocument Document { get; }

    public WordHtmlOptions Options { get; }

    public WordStyleSheet Styles => Document.Styles;

    public WordListCounter Lists { get; }

    public NoteNumbering Notes { get; }

    public HtmlImages Images { get; }

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    public bool Marked => Options.Revisions == WordHtmlRevisions.Marked;

    /// <summary>True when the content is left out of the page: a deletion in the accepted view.</summary>
    public bool Skips(WordRevision? revision) => !Marked && revision?.Kind == WordRevisionKind.Deleted;

    public bool IsHighlighted(string? address) =>
        address is not null && string.Equals(address, Options.HighlightAddress, StringComparison.OrdinalIgnoreCase);

    /// <summary>The id of a note element (<c>omni-fn-3</c>) or of its first reference (<c>omni-fnref-3</c>).</summary>
    public static string NoteId(WordNoteKind kind, int id, bool reference) =>
        (kind == WordNoteKind.Footnote ? "omni-fn" : "omni-en") + (reference ? "ref-" : "-") + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True the first time a reference to the note is written, when it takes the reference id.</summary>
    public bool FirstReference(WordNoteKind kind, int id) => _referenced.Add((kind, id));
}
