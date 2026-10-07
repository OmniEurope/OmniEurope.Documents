// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word.Reading;
using OmniEurope.Documents.Word.Writing;

namespace OmniEurope.Documents.Word;

/// <summary>
/// A Word (.docx) document as an object model: sections of blocks, styles, numbering, notes, comments and
/// properties. Use it to read a document, or to build one and save it. Loading keeps what the model holds;
/// content it has no model for is skipped and listed in <see cref="Gaps"/>. To change an existing file
/// without losing anything, use <see cref="Editing.WordEditor"/> instead.
/// </summary>
public sealed class WordDocument
{
    /// <summary>A new document: one A4 section, the default styles (Calibri 11 pt) in
    /// <paramref name="language"/>.</summary>
    public WordDocument(string? language = "fr-FR")
    {
        Styles = WordStyleSheet.CreateDefault(language: language);
        Sections.Add(new WordSection());
    }

    internal WordDocument(bool empty)
    {
        _ = empty;
        Styles = new WordStyleSheet();
    }

    /// <summary>Sections in order; a new document has one.</summary>
    public List<WordSection> Sections { get; } = [];

    /// <summary>All blocks of all sections, in order.</summary>
    public IEnumerable<WordBlock> Blocks => Sections.SelectMany(s => s.Blocks);

    /// <summary>The blocks of the last section, where the <c>Add</c> helpers append.</summary>
    public List<WordBlock> Body => Sections[^1].Blocks;

    /// <summary>Styles and document defaults.</summary>
    public WordStyleSheet Styles { get; set; }

    /// <summary>Numbering (list) definitions.</summary>
    public WordNumbering Numbering { get; set; } = new();

    /// <summary>Footnotes by id (separators excluded).</summary>
    public Dictionary<int, WordNote> Footnotes { get; } = [];

    /// <summary>Endnotes by id (separators excluded).</summary>
    public Dictionary<int, WordNote> Endnotes { get; } = [];

    /// <summary>Comments.</summary>
    public List<WordComment> Comments { get; } = [];

    /// <summary>Settings.</summary>
    public WordSettings Settings { get; set; } = new();

    /// <summary>Core properties.</summary>
    public WordInformation Information { get; set; } = new();

    /// <summary>Features met while loading that the model does not hold (charts, linked pictures...).</summary>
    public List<string> Gaps { get; } = [];

    /// <summary>The visible text of the body: one line per paragraph, table cells separated by tabs.</summary>
    public string Text => WordBlock.TextOf(Blocks);

    /// <summary>Reads a .docx (or .dotx) package.</summary>
    /// <exception cref="DocumentFormatException">The package is invalid, breaks the limits, holds macros
    /// or is not a Word document.</exception>
    public static WordDocument Load(Stream stream, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return WordDocumentReader.Read(OpcPackage.Open(stream, limits));
    }

    /// <summary>Reads a .docx package from bytes.</summary>
    public static WordDocument Load(byte[] bytes, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Load(new MemoryStream(bytes, writable: false), limits);
    }

    /// <summary>Writes the document as a .docx package; equal documents give equal bytes.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        WordDocumentWriter.Write(this, stream);
    }

    /// <summary>The document as .docx bytes.</summary>
    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return stream.ToArray();
    }

    /// <summary>Appends a paragraph to the last section.</summary>
    public WordParagraph AddParagraph(string text = "", string? styleId = null)
    {
        var paragraph = new WordParagraph(text, styleId);
        Body.Add(paragraph);
        return paragraph;
    }

    /// <summary>Appends a heading (<c>Heading1</c> to <c>Heading6</c>).</summary>
    public WordParagraph AddHeading(string text, int level = 1) => AddParagraph(text, "Heading" + Math.Clamp(level, 1, 6).ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Appends a table to the last section.</summary>
    public WordTable AddTable(WordTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Body.Add(table);
        return table;
    }

    /// <summary>Appends a paragraph holding a page break.</summary>
    public WordParagraph AddPageBreak()
    {
        var paragraph = new WordParagraph().Add(new WordBreak(WordBreakKind.Page));
        Body.Add(paragraph);
        return paragraph;
    }

    /// <summary>Starts a new section; its headers and footers are copied from the previous one.</summary>
    public WordSection AddSection(WordPageSetup page)
    {
        var section = new WordSection(page);
        foreach (var (kind, header) in Sections[^1].Headers)
        {
            section.Headers[kind] = header;
        }

        foreach (var (kind, footer) in Sections[^1].Footers)
        {
            section.Footers[kind] = footer;
        }

        Sections.Add(section);
        return section;
    }

    /// <summary>Creates a footnote with one paragraph of text and returns the reference to place in the text.</summary>
    public WordNoteReference AddFootnote(string text) => AddNote(Footnotes, WordNoteKind.Footnote, "Footnote", text);

    /// <summary>Creates an endnote with one paragraph of text and returns the reference to place in the text.</summary>
    public WordNoteReference AddEndnote(string text) => AddNote(Endnotes, WordNoteKind.Endnote, "Endnote", text);

    private static WordNoteReference AddNote(Dictionary<int, WordNote> notes, WordNoteKind kind, string style, string text)
    {
        var id = notes.Count == 0 ? 1 : Math.Max(1, notes.Keys.Max() + 1);
        var note = new WordNote(id);
        var paragraph = new WordParagraph { StyleId = style + "Text" };
        paragraph.Add(new WordNoteReference(kind, id, isMark: true) { Properties = new WordRunProperties { StyleId = style + "Reference" } });
        paragraph.AddText(" " + text);
        note.Blocks.Add(paragraph);
        notes[id] = note;
        return new WordNoteReference(kind, id) { Properties = new WordRunProperties { StyleId = style + "Reference" } };
    }
}
