// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>Shared state of one document read: the package, decoded images and the gaps met.</summary>
internal sealed class WordReadContext(OpcPackage package)
{
    private readonly Dictionary<string, WordImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<XElement, Dictionary<XElement, int>> _paragraphIndexes = [];

    public OpcPackage Package { get; } = package;

    public SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);

    /// <summary>The editor address (<c>part#index</c>) of a paragraph element of <paramref name="part"/>, counted
    /// as <see cref="Editing.WordEditor.Paragraphs"/> counts them; null when the editor does not see it.</summary>
    public string? Address(string part, XElement paragraph)
    {
        var root = paragraph.AncestorsAndSelf().Last();
        if (!_paragraphIndexes.TryGetValue(root, out var indexes))
        {
            indexes = new Dictionary<XElement, int>(ReferenceEqualityComparer.Instance);
            foreach (var element in Editing.WordRunScanner.Paragraphs(root))
            {
                indexes[element] = indexes.Count;
            }

            _paragraphIndexes[root] = indexes;
        }

        return indexes.TryGetValue(paragraph, out var index) ? part + "#" + index.ToString(CultureInfo.InvariantCulture) : null;
    }

    public WordImage? Image(string partName)
    {
        if (_images.TryGetValue(partName, out var cached))
        {
            return cached;
        }

        var bytes = Package.GetBytes(partName);
        if (bytes is null)
        {
            Gaps.Add("missing image part");
            return null;
        }

        var image = new WordImage(bytes, Package.ContentTypeOf(partName) ?? "application/octet-stream") { PartName = partName };
        _images[partName] = image;
        return image;
    }
}

/// <summary>
/// Reads blocks and inline content of one part (body, header, footer, notes, comments). Content controls,
/// custom XML and smart tags are transparent; for alternate content the first choice is read. Complex fields
/// (<c>fldChar</c>) are rebuilt with their instruction and result; a field still open at the end of a
/// paragraph is closed there and the rest of its result reads as ordinary text.
/// </summary>
internal sealed class WordContentReader
{
    private static readonly Dictionary<string, WordRevisionKind> RevisionWrappers = new(StringComparer.Ordinal)
    {
        ["ins"] = WordRevisionKind.Inserted,
        ["moveTo"] = WordRevisionKind.Inserted,
        ["del"] = WordRevisionKind.Deleted,
        ["moveFrom"] = WordRevisionKind.Deleted,
    };

    private static readonly HashSet<string> Transparent = new(StringComparer.Ordinal) { "sdt", "smartTag", "customXml", "dir", "bdo" };

    private readonly List<FieldFrame> _fields = [];
    private readonly Dictionary<string, OpcRelationship> _relationships;
    private readonly WordDrawingReader _drawings;
    private readonly int? _noteId;

    public WordContentReader(WordReadContext context, string part, int? noteId = null)
    {
        Context = context;
        Part = part;
        _noteId = noteId;
        _relationships = context.Package.Relationships(part).GroupBy(r => r.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _drawings = new WordDrawingReader(this);
    }

    public WordReadContext Context { get; }

    public string Part { get; }

    public OpcRelationship? Relationship(string? id) => id is not null && _relationships.TryGetValue(id, out var relationship) ? relationship : null;

    public List<WordBlock> Blocks(XElement container)
    {
        var blocks = new List<WordBlock>();
        foreach (var child in container.Elements())
        {
            AddBlock(child, blocks);
        }

        return blocks;
    }

    public void AddBlock(XElement element, List<WordBlock> blocks)
    {
        if (element.Name == Mc + "AlternateContent")
        {
            ForEachChoice(element, child => AddBlock(child, blocks));
        }
        else if (element.Name == W + "p")
        {
            blocks.Add(Paragraph(element));
        }
        else if (element.Name == W + "tbl")
        {
            blocks.Add(Table(element));
        }
        else if (element.Name == W + "sdt" || element.Name == W + "customXml")
        {
            foreach (var child in (element.Element(W + "sdtContent") ?? element).Elements())
            {
                AddBlock(child, blocks);
            }
        }
        else if (element.Name == W + "altChunk")
        {
            Context.Gaps.Add("imported content (altChunk) skipped");
        }
    }

    public WordParagraph Paragraph(XElement p)
    {
        var paragraph = new WordParagraph { Properties = WordPropertyReader.Paragraph(p.Element(W + "pPr")) ?? WordParagraphProperties.Empty, SourceAddress = Context.Address(Part, p) };
        _fields.Clear();
        Inlines(p, paragraph.Inlines, null);
        while (_fields.Count > 0)
        {
            EndField();
        }

        return paragraph;
    }

    private WordTable Table(XElement tbl)
    {
        var table = new WordTable { Properties = WordStructureReader.Table(tbl.Element(W + "tblPr")) ?? WordTableProperties.Empty };
        table.Columns.AddRange(tbl.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(c => Twips(Attr(c, "w")) ?? 0) ?? []);
        foreach (var tr in Unwrap(tbl, "tr"))
        {
            var trPr = tr.Element(W + "trPr");
            var row = new WordTableRow { Properties = WordStructureReader.Row(trPr), Revision = RowRevision(trPr) };
            foreach (var tc in Unwrap(tr, "tc"))
            {
                AddCell(row, tc);
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private void AddCell(WordTableRow row, XElement tc)
    {
        var tcPr = tc.Element(W + "tcPr");
        var cell = new WordTableCell { Properties = WordStructureReader.Cell(tcPr) ?? WordTableCellProperties.Empty };
        var merge = tcPr?.Element(W + "hMerge");
        if (merge is not null && Attr(merge, "val") != "restart" && row.Cells.Count > 0)
        {
            // Legacy horizontal merge: the continuing cell widens the one before it.
            var previous = row.Cells[^1];
            previous.Properties = previous.Properties with { GridSpan = (previous.Properties.GridSpan ?? 1) + (cell.Properties.GridSpan ?? 1) };
            return;
        }

        cell.Blocks.AddRange(Blocks(tc));
        row.Cells.Add(cell);
    }

    // Rows and cells may sit inside content controls or custom XML.
    private static IEnumerable<XElement> Unwrap(XElement parent, string name)
    {
        foreach (var child in parent.Elements())
        {
            if (child.Name == W + name)
            {
                yield return child;
            }
            else if (child.Name == W + "sdt" || child.Name == W + "customXml")
            {
                foreach (var inner in Unwrap(child.Name == W + "sdt" ? child.Element(W + "sdtContent") ?? child : child, name))
                {
                    yield return inner;
                }
            }
        }
    }

    private static WordRevision? RowRevision(XElement? trPr)
    {
        if (trPr?.Element(W + "del") is { } deleted)
        {
            return Revision(deleted, WordRevisionKind.Deleted);
        }

        return trPr?.Element(W + "ins") is { } inserted ? Revision(inserted, WordRevisionKind.Inserted) : null;
    }

    private void Inlines(XElement container, List<WordInline> list, WordRevision? revision)
    {
        foreach (var child in container.Elements())
        {
            Inline(child, list, revision);
        }
    }

    private void Inline(XElement child, List<WordInline> list, WordRevision? revision)
    {
        if (child.Name.Namespace == Mc && child.Name.LocalName == "AlternateContent")
        {
            ForEachChoice(child, inner => Inline(inner, list, revision));
        }
        else if (child.Name.Namespace == M)
        {
            Equation(child, list, revision);
        }
        else if (child.Name.Namespace == W)
        {
            MainInline(child, child.Name.LocalName, list, revision);
        }
    }

    private void MainInline(XElement child, string name, List<WordInline> list, WordRevision? revision)
    {
        if (RevisionWrappers.TryGetValue(name, out var kind))
        {
            Inlines(child, list, Revision(child, kind));
        }
        else if (Transparent.Contains(name))
        {
            Inlines(child.Element(W + "sdtContent") ?? child, list, revision);
        }
        else if (name == "r")
        {
            Run(child, list, revision);
        }
        else if (name == "hyperlink")
        {
            Hyperlink(child, list, revision);
        }
        else if (name == "fldSimple")
        {
            var field = new WordField(Attr(child, "instr") ?? string.Empty) { Revision = revision };
            Inlines(child, field.Result, revision);
            Sink(list)?.Add(field);
        }
    }

    private void Equation(XElement element, List<WordInline> list, WordRevision? revision)
    {
        if (element.Name.LocalName is "oMath" or "oMathPara")
        {
            Context.Gaps.Add("equations read as plain text");
            Emit(list, new WordText(string.Concat(element.Descendants(M + "t").Select(t => t.Value))), WordRunProperties.Empty, revision);
        }
    }

    private void Hyperlink(XElement element, List<WordInline> list, WordRevision? revision)
    {
        var relationship = Relationship(element.Attribute(R + "id")?.Value);
        var link = WordHyperlink.Create(relationship?.External == true ? relationship.Target : null, Attr(element, "anchor"));
        link.Revision = revision;
        Sink(list)?.Add(link);
        Inlines(element, link.Inlines, revision);
    }

    private void Run(XElement r, List<WordInline> list, WordRevision? revision)
    {
        var properties = WordPropertyReader.Run(r.Element(W + "rPr")) ?? WordRunProperties.Empty;
        foreach (var child in r.Elements())
        {
            RunChild(child, list, properties, revision);
        }
    }

    private void RunChild(XElement child, List<WordInline> list, WordRunProperties properties, WordRevision? revision)
    {
        if (child.Name.Namespace == Mc && child.Name.LocalName == "AlternateContent")
        {
            ForEachChoice(child, inner => RunChild(inner, list, properties, revision));
            return;
        }

        if (child.Name.Namespace != W)
        {
            return;
        }

        var name = child.Name.LocalName;
        if (name is "fldChar" or "instrText" or "delInstrText")
        {
            FieldPart(child, list, properties, revision);
            return;
        }

        if (RunContent(child, name) is { } inline)
        {
            Emit(list, inline, properties, revision);
        }
    }

    private WordInline? RunContent(XElement child, string name) =>
        RunContents.TryGetValue(name, out var read) ? read(this, child, name) : null;

    // What each run child becomes; children not listed (proofing marks, rendered page breaks...) are skipped.
    private static readonly Dictionary<string, Func<WordContentReader, XElement, string, WordInline?>> RunContents = new(StringComparer.Ordinal)
    {
        ["t"] = (_, e, _) => new WordText(e.Value),
        ["delText"] = (_, e, _) => new WordText(e.Value),
        ["tab"] = (_, _, _) => new WordTab(),
        ["ptab"] = (_, e, _) => new WordTab { Alignment = WordPropertyReader.TabAlignment(Attr(e, "alignment")), Leader = WordPropertyReader.TabLeader(Attr(e, "leader")) },
        ["br"] = (_, e, _) => Break(Attr(e, "type")),
        ["cr"] = (_, _, _) => new WordBreak(WordBreakKind.Line),
        ["noBreakHyphen"] = (_, _, _) => new WordText("\u2011"),
        ["softHyphen"] = (_, _, _) => new WordText("\u00AD"),
        ["sym"] = (_, e, _) => Symbol(e),
        ["drawing"] = (r, e, _) => r._drawings.Drawing(e),
        ["pict"] = (r, e, _) => r._drawings.Vml(e),
        ["object"] = (r, e, _) => r._drawings.Vml(e),
        ["footnoteReference"] = (r, e, n) => r.NoteReference(e, n),
        ["endnoteReference"] = (r, e, n) => r.NoteReference(e, n),
        ["footnoteRef"] = (r, e, n) => r.NoteReference(e, n),
        ["endnoteRef"] = (r, e, n) => r.NoteReference(e, n),
        ["commentReference"] = (_, e, _) => new WordCommentReference(Int(Attr(e, "id")) ?? 0),
    };

    private static WordBreak Break(string? type) => new(type switch
    {
        "page" => WordBreakKind.Page,
        "column" => WordBreakKind.Column,
        _ => WordBreakKind.Line,
    });

    private WordNoteReference NoteReference(XElement child, string name)
    {
        var kind = name.StartsWith("footnote", StringComparison.Ordinal) ? WordNoteKind.Footnote : WordNoteKind.Endnote;
        return name.EndsWith("Ref", StringComparison.Ordinal)
            ? new WordNoteReference(kind, _noteId ?? 0, isMark: true)
            : new WordNoteReference(kind, Int(Attr(child, "id")) ?? 0);
    }

    private static WordSymbol? Symbol(XElement sym)
    {
        var code = Attr(sym, "char");
        return code is not null && int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) && value is > 0 and <= 0xFFFF
            ? new WordSymbol(Attr(sym, "font") ?? string.Empty, (char)value)
            : null;
    }

    private void Emit(List<WordInline> list, WordInline inline, WordRunProperties properties, WordRevision? revision)
    {
        inline.Properties = properties;
        inline.Revision = revision;
        Sink(list)?.Add(inline);
    }

    // Where content goes: the list itself, the result of the field open in this list, or nowhere while
    // that field's instruction is being read.
    private List<WordInline>? Sink(List<WordInline> list)
    {
        if (_fields.Count == 0 || !ReferenceEquals(_fields[^1].Owner, list))
        {
            return list;
        }

        return _fields[^1].InResult ? _fields[^1].Result : null;
    }

    private void FieldPart(XElement element, List<WordInline> list, WordRunProperties properties, WordRevision? revision)
    {
        if (element.Name.LocalName != "fldChar")
        {
            if (_fields.Count > 0 && !_fields[^1].InResult)
            {
                _fields[^1].Instruction.Append(element.Value);
            }

            return;
        }

        switch (Attr(element, "fldCharType"))
        {
            case "begin":
                _fields.Add(new FieldFrame(list, properties, revision));
                break;
            case "separate" when _fields.Count > 0:
                _fields[^1].InResult = true;
                break;
            case "end" when _fields.Count > 0:
                EndField();
                break;
        }
    }

    private void EndField()
    {
        var frame = _fields[^1];
        _fields.RemoveAt(_fields.Count - 1);
        var field = new WordField(frame.Instruction.ToString()) { Properties = frame.Properties, Revision = frame.Revision };
        field.Result.AddRange(frame.Result);
        if (_fields.Count > 0 && ReferenceEquals(_fields[^1].Owner, frame.Owner) && !_fields[^1].InResult)
        {
            // A field nested in an instruction contributes its result text to that instruction.
            _fields[^1].Instruction.Append(WordInline.TextOf(field.Result));
            return;
        }

        Sink(frame.Owner)?.Add(field);
    }

    public static WordRevision Revision(XElement element, WordRevisionKind kind)
    {
        var date = Attr(element, "date");
        return new WordRevision(
            kind,
            Attr(element, "author"),
            date is not null && DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null);
    }

    private static void ForEachChoice(XElement alternate, Action<XElement> read)
    {
        var chosen = alternate.Element(Mc + "Choice") ?? alternate.Element(Mc + "Fallback");
        foreach (var child in chosen?.Elements() ?? [])
        {
            read(child);
        }
    }

    private sealed class FieldFrame(List<WordInline> owner, WordRunProperties properties, WordRevision? revision)
    {
        public List<WordInline> Owner { get; } = owner;

        public WordRunProperties Properties { get; } = properties;

        public WordRevision? Revision { get; } = revision;

        public StringBuilder Instruction { get; } = new();

        public List<WordInline> Result { get; } = [];

        public bool InResult { get; set; }
    }
}
