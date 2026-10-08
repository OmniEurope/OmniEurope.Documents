// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>The kind of text part a paragraph of <see cref="WordEditor"/> sits in.</summary>
public enum WordPartKind
{
    /// <summary>The main document body.</summary>
    Body,

    /// <summary>A header part.</summary>
    Header,

    /// <summary>A footer part.</summary>
    Footer,

    /// <summary>A footnote of the footnotes part.</summary>
    Footnote,

    /// <summary>An endnote of the endnotes part.</summary>
    Endnote,

    /// <summary>A comment of the comments part.</summary>
    Comment,
}

/// <summary>The kind of a <see cref="WordPathStep"/>.</summary>
public enum WordPathStepKind
{
    /// <summary>The n-th block of its container, counting only paragraphs, tables, block content controls
    /// (<c>w:sdt</c>) and block custom XML (<c>w:customXml</c>); section properties, bookmarks and other
    /// markers take no index.</summary>
    Block,

    /// <summary>The n-th row of a table, counting only its rows (a row inside a row-level content control or
    /// custom XML element counts as a row of the table).</summary>
    Row,

    /// <summary>The n-th cell of a row, counting only its cells (cell-level wrappers are transparent).</summary>
    Cell,

    /// <summary>Entering the content (<c>w:sdtContent</c>) of the block content control of the previous step.</summary>
    ContentControl,

    /// <summary>Entering the block custom XML element of the previous step.</summary>
    CustomXml,

    /// <summary>The n-th text box (<c>w:txbxContent</c>) anchored in the paragraph of the previous step,
    /// counting only the boxes whose nearest paragraph is that one (a box inside a box belongs to the
    /// paragraph that holds it).</summary>
    TextBox,
}

/// <summary>One step of a paragraph's path from its part (or note, or comment) down to the paragraph.</summary>
/// <param name="Kind">What the step enters.</param>
/// <param name="Index">The position counted as <see cref="WordPathStepKind"/> describes, from 0; always 0 for
/// <see cref="WordPathStepKind.ContentControl"/> and <see cref="WordPathStepKind.CustomXml"/>.</param>
public readonly record struct WordPathStep(WordPathStepKind Kind, int Index = 0)
{
    /// <summary>The n-th block of the container.</summary>
    public static WordPathStep Block(int index) => new(WordPathStepKind.Block, index);

    /// <summary>The n-th row of the table.</summary>
    public static WordPathStep Row(int index) => new(WordPathStepKind.Row, index);

    /// <summary>The n-th cell of the row.</summary>
    public static WordPathStep Cell(int index) => new(WordPathStepKind.Cell, index);

    /// <summary>The n-th text box of the paragraph.</summary>
    public static WordPathStep TextBox(int index) => new(WordPathStepKind.TextBox, index);

    /// <summary>Entering a block content control.</summary>
    public static WordPathStep ContentControl { get; } = new(WordPathStepKind.ContentControl);

    /// <summary>Entering a block custom XML element.</summary>
    public static WordPathStep CustomXml { get; } = new(WordPathStepKind.CustomXml);

    /// <summary><c>Block(2)</c>, <c>Row(0)</c>, <c>ContentControl</c>...</summary>
    public override string ToString() => Kind is WordPathStepKind.ContentControl or WordPathStepKind.CustomXml
        ? Kind.ToString()
        : Kind.ToString() + "(" + Index.ToString(CultureInfo.InvariantCulture) + ")";
}

/// <summary>
/// Where a paragraph of <see cref="WordEditor.Paragraphs"/> sits: its part, the note or comment holding it and
/// the block path down to it. Two locations are equal when all their members are (the path step by step);
/// the location of a paragraph is the same after the document is saved again without change.
/// </summary>
public sealed record WordParagraphLocation
{
    private readonly WordPathStep[] _path;

    /// <summary>A location; <paramref name="path"/> is copied.</summary>
    public WordParagraphLocation(string partName, WordPartKind partKind, IEnumerable<WordPathStep> path)
    {
        ArgumentNullException.ThrowIfNull(partName);
        ArgumentNullException.ThrowIfNull(path);
        PartName = partName;
        PartKind = partKind;
        _path = path.ToArray();
    }

    /// <summary>The part holding the paragraph (<c>word/document.xml</c>, <c>word/header1.xml</c>...).</summary>
    public string PartName { get; }

    /// <summary>The kind of the part.</summary>
    public WordPartKind PartKind { get; }

    /// <summary>For a header or footer, the relationship id by which the main document part refers to it
    /// (<c>rId7</c>; the first one when several refer to the same part); null otherwise.</summary>
    public string? RelationshipId { get; init; }

    /// <summary>For a footnote or endnote paragraph, the note id (<c>w:id</c>); null otherwise.</summary>
    public int? NoteId { get; init; }

    /// <summary>True for a separator or continuation note (a <c>w:type</c> other than <c>normal</c>, or the ids
    /// -1 and 0): it holds no text of the document and a caller translating notes skips it.</summary>
    public bool IsSeparatorNote { get; init; }

    /// <summary>For a comment paragraph, the comment id (<c>w:id</c>); null otherwise.</summary>
    public int? CommentId { get; init; }

    /// <summary>The steps from the part's root (the body, the header, the note or the comment) down to the
    /// paragraph, whose own <see cref="WordPathStepKind.Block"/> step comes last.</summary>
    public IReadOnlyList<WordPathStep> Path => _path;

    /// <inheritdoc/>
    public bool Equals(WordParagraphLocation? other) =>
        other is not null
        && string.Equals(PartName, other.PartName, StringComparison.OrdinalIgnoreCase)
        && PartKind == other.PartKind
        && RelationshipId == other.RelationshipId
        && NoteId == other.NoteId
        && IsSeparatorNote == other.IsSeparatorNote
        && CommentId == other.CommentId
        && _path.AsSpan().SequenceEqual(other._path);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PartName, StringComparer.OrdinalIgnoreCase);
        hash.Add(NoteId);
        hash.Add(CommentId);
        foreach (var step in _path)
        {
            hash.Add(step);
        }

        return hash.ToHashCode();
    }

    /// <summary><c>word/footnotes.xml/Note(2)/Block(0)</c>, <c>word/document.xml/Block(3)/Row(1)/Cell(0)/Block(0)</c>...</summary>
    public override string ToString()
    {
        var parts = new List<string> { PartName };
        if (NoteId is { } note)
        {
            parts.Add("Note(" + note.ToString(CultureInfo.InvariantCulture) + ")");
        }

        if (CommentId is { } comment)
        {
            parts.Add("Comment(" + comment.ToString(CultureInfo.InvariantCulture) + ")");
        }

        parts.AddRange(_path.Select(s => s.ToString()));
        return string.Join('/', parts);
    }
}
