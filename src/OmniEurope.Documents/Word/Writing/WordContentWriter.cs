// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>Relationships of one part being written; equal targets share one id.</summary>
internal sealed class PartRelationships(string partName)
{
    private readonly List<(string Id, string Type, string Target, bool External)> _items = [];

    public string PartName { get; } = partName;

    public int Count => _items.Count;

    public string Add(string type, string target, bool external = false)
    {
        var existing = _items.FindIndex(i => i.Type == type && i.Target == target && i.External == external);
        if (existing >= 0)
        {
            return _items[existing].Id;
        }

        var id = "rId" + (_items.Count + 1).ToString(CultureInfo.InvariantCulture);
        _items.Add((id, type, target, external));
        return id;
    }

    public XElement ToXml() => new(
        Rel + "Relationships",
        _items.Select(i =>
        {
            var element = new XElement(Rel + "Relationship", new XAttribute("Id", i.Id), new XAttribute("Type", i.Type), new XAttribute("Target", i.Target));
            if (i.External)
            {
                element.Add(new XAttribute("TargetMode", "External"));
            }

            return element;
        }));
}

/// <summary>Writes blocks and inline content of one part as WordprocessingML.</summary>
internal sealed class WordContentWriter(WordWriteContext context, PartRelationships relationships)
{
    public WordWriteContext Context { get; } = context;

    /// <summary>Blocks of a container that must end with a paragraph (cell, note, header...).</summary>
    public IEnumerable<XElement> Container(IReadOnlyList<WordBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return Block(block);
        }

        if (blocks.Count == 0 || blocks[^1] is not WordParagraph)
        {
            yield return new XElement(W + "p");
        }
    }

    public XElement Block(WordBlock block) => block switch
    {
        WordParagraph paragraph => Paragraph(paragraph),
        WordTable table => Table(table),
        _ => throw new NotSupportedException(block.GetType().Name),
    };

    public XElement Paragraph(WordParagraph paragraph, XElement? sectPr = null)
    {
        var element = new XElement(W + "p");
        WordPropertyWriter.Add(element, WordPropertyWriter.Paragraph(paragraph.Properties, sectPr));
        element.Add(Inlines(paragraph.Inlines, null));
        return element;
    }

    private XElement Table(WordTable table)
    {
        var columns = table.Columns.Count > 0 ? table.Columns : DefaultGrid(table);
        var element = new XElement(
            W + "tbl",
            WordStructureWriter.Table(table.Properties),
            new XElement(W + "tblGrid", columns.Select(c => new XElement(W + "gridCol", new XAttribute(W + "w", ToTwips(c))))));
        foreach (var row in table.Rows)
        {
            var tr = new XElement(W + "tr");
            WordPropertyWriter.Add(tr, WordStructureWriter.Row(row.Properties, row.Revision, RevisionElement));
            foreach (var cell in row.Cells)
            {
                var tc = new XElement(W + "tc");
                WordPropertyWriter.Add(tc, WordStructureWriter.Cell(cell.Properties));
                tc.Add(Container(cell.Blocks));
                tr.Add(tc);
            }

            element.Add(tr);
        }

        return element;
    }

    // Without explicit columns the grid is the widest row, sharing the text width equally.
    private List<double> DefaultGrid(WordTable table)
    {
        var count = Math.Max(1, table.Rows.Select(r => r.Cells.Sum(c => Math.Max(1, c.Properties.GridSpan ?? 1))).DefaultIfEmpty(1).Max());
        return Enumerable.Repeat(Context.ContentWidth / count, count).ToList();
    }

    public IEnumerable<XElement> Inlines(IEnumerable<WordInline> inlines, WordRevision? inherited)
    {
        foreach (var inline in inlines)
        {
            var revision = inline.Revision ?? inherited;
            var deleted = revision?.Kind == WordRevisionKind.Deleted;
            var elements = inline is WordHyperlink link ? [Hyperlink(link, revision)] : Inline(inline, deleted, wrapped: revision is not null).ToList();
            if (revision is null || inline is WordHyperlink)
            {
                foreach (var element in elements)
                {
                    yield return element;
                }

                continue;
            }

            var wrapper = RevisionElement(revision, deleted ? "del" : "ins");
            wrapper.Add(elements);
            yield return wrapper;
        }
    }

    private XElement Hyperlink(WordHyperlink link, WordRevision? revision)
    {
        var element = new XElement(W + "hyperlink");
        if (link.Target is not null)
        {
            element.Add(new XAttribute(R + "id", relationships.Add(HyperlinkType, link.Target, external: true)));
        }

        if (link.Anchor is not null)
        {
            element.Add(new XAttribute(W + "anchor", link.Anchor));
        }

        element.Add(new XAttribute(W + "history", "1"));
        element.Add(Inlines(link.Inlines, revision));
        return element;
    }

    // Wrapped: the caller puts the runs in a revision element (w:ins, w:del), which may hold runs only.
    private IEnumerable<XElement> Inline(WordInline inline, bool deleted, bool wrapped)
    {
        var p = inline.Properties;
        switch (inline)
        {
            case WordText text:
                yield return Run(p, TextElement(Clean(text.Value), deleted));
                break;
            case WordTab tab:
                yield return Run(p, Tab(tab));
                break;
            case WordBreak lineBreak:
                yield return Run(p, lineBreak.Kind == WordBreakKind.Line ? new XElement(W + "br") : new XElement(W + "br", new XAttribute(W + "type", lineBreak.Kind == WordBreakKind.Page ? "page" : "column")));
                break;
            case WordSymbol symbol:
                yield return Run(p, new XElement(W + "sym", new XAttribute(W + "font", symbol.Font), new XAttribute(W + "char", ((int)symbol.Character).ToString("X4", CultureInfo.InvariantCulture))));
                break;
            case WordField field:
                foreach (var element in Field(field, deleted, wrapped))
                {
                    yield return element;
                }

                break;
            default:
                yield return Run(p, Special(inline));
                break;
        }
    }

    private XElement Special(WordInline inline) => inline switch
    {
        WordNoteReference { IsMark: true } mark => new XElement(W + (mark.Kind == WordNoteKind.Footnote ? "footnoteRef" : "endnoteRef")),
        WordNoteReference note => new XElement(W + (note.Kind == WordNoteKind.Footnote ? "footnoteReference" : "endnoteReference"), new XAttribute(W + "id", Format(note.Id))),
        WordCommentReference comment => new XElement(W + "commentReference", new XAttribute(W + "id", Format(comment.Id))),
        WordPicture picture => WordDrawingWriter.Picture(this, picture, relationships.Add(ImageType, Relative(Context.ImagePart(picture.Image)))),
        WordTextBox box => WordDrawingWriter.TextBox(this, box),
        _ => throw new NotSupportedException(inline.GetType().Name),
    };

    private static XElement Tab(WordTab tab)
    {
        if (tab.Alignment is not { } alignment)
        {
            return new XElement(W + "tab");
        }

        return new XElement(
            W + "ptab",
            new XAttribute(W + "relativeTo", "margin"),
            new XAttribute(W + "alignment", alignment switch
            {
                WordTabAlignment.Center => "center",
                WordTabAlignment.Right => "right",
                _ => "left",
            }),
            new XAttribute(W + "leader", WordPropertyWriter.TabLeader(tab.Leader)));
    }

    // A hyperlink in the result is written as one (a paragraph-level sibling of the field runs), or as its runs
    // when the field sits in a revision element, which cannot hold a hyperlink.
    private IEnumerable<XElement> Field(WordField field, bool deleted, bool wrapped)
    {
        var p = field.Properties;
        yield return Run(p, new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "begin")));
        var instruction = new XElement(W + (deleted ? "delInstrText" : "instrText"), " " + Clean(field.Instruction) + " ");
        instruction.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
        yield return Run(p, instruction);
        yield return Run(p, new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "separate")));
        foreach (var element in Result(field.Result, deleted, wrapped))
        {
            yield return element;
        }

        yield return Run(p, new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "end")));
    }

    private IEnumerable<XElement> Result(IEnumerable<WordInline> inlines, bool deleted, bool wrapped)
    {
        foreach (var inline in inlines)
        {
            var elements = inline switch
            {
                WordHyperlink link when !wrapped => [Hyperlink(link, link.Revision)],
                WordHyperlink link => Result(link.Inlines, deleted, wrapped),
                _ => Inline(inline, deleted, wrapped),
            };
            foreach (var element in elements)
            {
                yield return element;
            }
        }
    }

    private static XElement Run(WordRunProperties properties, XElement content)
    {
        var run = new XElement(W + "r");
        WordPropertyWriter.Add(run, WordPropertyWriter.Run(properties));
        run.Add(content);
        return run;
    }

    public XElement RevisionElement(WordRevision revision, string name)
    {
        var element = new XElement(W + name, new XAttribute(W + "id", Format(Context.NextRevisionId())), new XAttribute(W + "author", revision.Author ?? string.Empty));
        if (revision.Date is { } date)
        {
            element.Add(new XAttribute(W + "date", date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
        }

        return element;
    }

    private string Relative(string target)
    {
        var directory = relationships.PartName[..(relationships.PartName.LastIndexOf('/') + 1)];
        return target.StartsWith(directory, StringComparison.Ordinal) ? target[directory.Length..] : "/" + target;
    }

    /// <summary>Drops what XML cannot hold: control characters other than tab and line feeds, lone surrogates.</summary>
    public static string Clean(string text)
    {
        if (text.All(XmlConvert.IsXmlChar))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(c).Append(text[++i]);
            }
            else if (XmlConvert.IsXmlChar(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
