// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;
using static OmniEurope.Documents.Word.Writing.WordPropertyWriter;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>Writes the styles, numbering, settings, notes, comments and core properties parts.</summary>
internal static class WordPartsWriter
{
    public static XElement Styles(WordStyleSheet sheet)
    {
        var defaults = new XElement(
            W + "docDefaults",
            new XElement(W + "rPrDefault", Run(sheet.DefaultRunProperties)),
            new XElement(W + "pPrDefault", Paragraph(sheet.DefaultParagraphProperties)));
        return new XElement(W + "styles", Namespaces(), defaults, sheet.Styles.Values.Select(Style));
    }

    private static XElement Style(WordStyle style)
    {
        var element = new XElement(
            W + "style",
            new XAttribute(W + "type", style.Type switch
            {
                WordStyleType.Character => "character",
                WordStyleType.Table => "table",
                WordStyleType.Numbering => "numbering",
                _ => "paragraph",
            }));
        if (style.IsDefault)
        {
            element.Add(new XAttribute(W + "default", "1"));
        }

        element.Add(new XAttribute(W + "styleId", style.Id));
        element.Add(ValElement("name", style.Name ?? style.Id));
        Add(element, style.BasedOn is null ? null : ValElement("basedOn", style.BasedOn));
        Add(element, style.Next is null ? null : ValElement("next", style.Next));
        Add(element, style.Link is null ? null : ValElement("link", style.Link));
        Add(element, style.IsPrimary ? new XElement(W + "qFormat") : null);
        AddFormatting(element, style.ParagraphProperties, style.RunProperties, style.TableProperties, style.CellProperties);
        foreach (var format in style.ConditionalFormats)
        {
            var conditional = new XElement(W + "tblStylePr", new XAttribute(W + "type", WordStructureWriter.Region(format.Region)));
            AddFormatting(conditional, format.ParagraphProperties, format.RunProperties, format.TableProperties, format.CellProperties);
            element.Add(conditional);
        }

        return element;
    }

    private static void AddFormatting(XElement element, WordParagraphProperties? paragraph, WordRunProperties? run, WordTableProperties? table, WordTableCellProperties? cell)
    {
        Add(element, Paragraph(paragraph));
        Add(element, Run(run));
        Add(element, table is null ? null : WordStructureWriter.Table(table));
        Add(element, cell is null ? null : WordStructureWriter.Cell(cell));
    }

    public static XElement Numbering(WordNumbering numbering)
    {
        var root = new XElement(W + "numbering", Namespaces());
        foreach (var definition in numbering.Abstracts.Values)
        {
            root.Add(new XElement(
                W + "abstractNum",
                new XAttribute(W + "abstractNumId", Format(definition.Id)),
                ValElement("multiLevelType", definition.Levels.Count > 1 ? "hybridMultilevel" : "singleLevel"),
                definition.Levels.Values.Select(Level)));
        }

        foreach (var instance in numbering.Instances.Values)
        {
            var element = new XElement(W + "num", new XAttribute(W + "numId", Format(instance.Id)), ValElement("abstractNumId", Format(instance.AbstractId)));
            foreach (var level in instance.StartOverrides.Keys.Union(instance.LevelOverrides.Keys).Order())
            {
                var levelOverride = new XElement(W + "lvlOverride", new XAttribute(W + "ilvl", Format(level)));
                Add(levelOverride, instance.StartOverrides.TryGetValue(level, out var start) ? ValElement("startOverride", Format(start)) : null);
                Add(levelOverride, instance.LevelOverrides.TryGetValue(level, out var replaced) ? Level(replaced) : null);
                element.Add(levelOverride);
            }

            root.Add(element);
        }

        return root;
    }

    private static XElement Level(WordNumberingLevel level)
    {
        var element = new XElement(
            W + "lvl",
            new XAttribute(W + "ilvl", Format(level.Level)),
            ValElement("start", Format(level.Start)),
            ValElement("numFmt", WordStructureWriter.NumberFormat(level.Format)));
        Add(element, level.RestartAfter is { } restart ? ValElement("lvlRestart", Format(restart)) : null);
        Add(element, level.StyleId is null ? null : ValElement("pStyle", level.StyleId));
        Add(element, level.IsLegal ? new XElement(W + "isLgl") : null);
        Add(element, level.Suffix switch
        {
            WordLabelSuffix.Space => ValElement("suff", "space"),
            WordLabelSuffix.Nothing => ValElement("suff", "nothing"),
            _ => null,
        });
        element.Add(ValElement("lvlText", level.Text));
        element.Add(ValElement("lvlJc", Alignment(level.Alignment)));
        Add(element, Paragraph(level.ParagraphProperties));
        Add(element, Run(level.RunProperties));
        return element;
    }

    public static XElement Settings(WordSettings settings, bool hasFootnotes, bool hasEndnotes)
    {
        var root = new XElement(W + "settings", Namespaces(), ValElement("defaultTabStop", ToTwips(settings.DefaultTabStop)));
        Add(root, settings.EvenAndOddHeaders ? new XElement(W + "evenAndOddHeaders") : null);
        Add(root, settings.UpdateFieldsOnOpen ? new XElement(W + "updateFields") : null);
        var footnotes = NoteProperties("footnotePr", settings.FootnoteFormat, WordNumberFormat.Decimal, settings.FootnoteStart);
        if (settings.FootnoteRestart != WordNoteRestart.Continuous)
        {
            footnotes.Add(ValElement("numRestart", settings.FootnoteRestart == WordNoteRestart.EachPage ? "eachPage" : "eachSect"));
        }

        AddSeparators(footnotes, "footnote", hasFootnotes);
        Add(root, footnotes.HasElements ? footnotes : null);
        var endnotes = NoteProperties("endnotePr", settings.EndnoteFormat, WordNumberFormat.LowerRoman, settings.EndnoteStart);
        AddSeparators(endnotes, "endnote", hasEndnotes);
        Add(root, endnotes.HasElements ? endnotes : null);
        root.Add(new XElement(
            W + "compat",
            new XElement(
                W + "compatSetting",
                new XAttribute(W + "name", "compatibilityMode"),
                new XAttribute(W + "uri", "http://schemas.microsoft.com/office/word"),
                new XAttribute(W + "val", "15"))));
        return root;
    }

    private static XElement NoteProperties(string name, WordNumberFormat format, WordNumberFormat defaultFormat, int start)
    {
        var element = new XElement(W + name);
        Add(element, format != defaultFormat ? ValElement("numFmt", WordStructureWriter.NumberFormat(format)) : null);
        Add(element, start != 1 ? ValElement("numStart", Format(start)) : null);
        return element;
    }

    private static void AddSeparators(XElement properties, string kind, bool present)
    {
        if (present)
        {
            properties.Add(new XElement(W + kind, new XAttribute(W + "id", "-1")), new XElement(W + kind, new XAttribute(W + "id", "0")));
        }
    }

    /// <summary>A footnotes or endnotes part: the two separators, then the notes.</summary>
    public static XElement Notes(string kind, IEnumerable<WordNote> notes, WordContentWriter writer)
    {
        var root = new XElement(W + kind + "s", Namespaces());
        root.Add(Separator(kind, -1, "separator"), Separator(kind, 0, "continuationSeparator"));
        foreach (var note in notes.Where(n => n.Id > 0).OrderBy(n => n.Id))
        {
            root.Add(new XElement(W + kind, new XAttribute(W + "id", Format(note.Id)), writer.Container(note.Blocks)));
        }

        return root;
    }

    private static XElement Separator(string kind, int id, string type) => new(
        W + kind,
        new XAttribute(W + "type", type),
        new XAttribute(W + "id", Format(id)),
        new XElement(
            W + "p",
            new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", "0"), new XAttribute(W + "line", "240"), new XAttribute(W + "lineRule", "auto"))),
            new XElement(W + "r", new XElement(W + type))));

    public static XElement Comments(IEnumerable<WordComment> comments, WordContentWriter writer)
    {
        var root = new XElement(W + "comments", Namespaces());
        foreach (var comment in comments)
        {
            var element = new XElement(W + "comment", new XAttribute(W + "id", Format(comment.Id)), new XAttribute(W + "author", comment.Author ?? string.Empty));
            if (comment.Date is { } date)
            {
                element.Add(new XAttribute(W + "date", W3cDate(date)));
            }

            if (comment.Initials is not null)
            {
                element.Add(new XAttribute(W + "initials", comment.Initials));
            }

            element.Add(writer.Container(comment.Blocks));
            root.Add(element);
        }

        return root;
    }

    public static XElement Core(WordInformation information)
    {
        var root = new XElement(
            Cp + "coreProperties",
            new XAttribute(XNamespace.Xmlns + "cp", Cp),
            new XAttribute(XNamespace.Xmlns + "dc", Dc),
            new XAttribute(XNamespace.Xmlns + "dcterms", DcTerms),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi));
        AddText(root, Dc + "title", information.Title);
        AddText(root, Dc + "subject", information.Subject);
        AddText(root, Dc + "creator", information.Author);
        AddText(root, Cp + "keywords", information.Keywords);
        AddText(root, Dc + "description", information.Description);
        AddText(root, Cp + "lastModifiedBy", information.LastModifiedBy);
        AddText(root, Cp + "category", information.Category);
        AddDate(root, "created", information.Created);
        AddDate(root, "modified", information.Modified);
        return root;
    }

    private static void AddText(XElement root, XName name, string? value)
    {
        if (value is not null)
        {
            root.Add(new XElement(name, WordContentWriter.Clean(value)));
        }
    }

    private static void AddDate(XElement root, string name, DateTimeOffset? value)
    {
        if (value is { } date)
        {
            root.Add(new XElement(DcTerms + name, new XAttribute(Xsi + "type", "dcterms:W3CDTF"), W3cDate(date)));
        }
    }

    private static string W3cDate(DateTimeOffset date) => date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Prefix declarations put on each part's root so prefixes are the conventional ones.</summary>
    public static IEnumerable<XAttribute> Namespaces() =>
    [
        new(XNamespace.Xmlns + "w", W),
        new(XNamespace.Xmlns + "r", R),
        new(XNamespace.Xmlns + "wp", Wp),
        new(XNamespace.Xmlns + "a", A),
        new(XNamespace.Xmlns + "pic", Pic),
        new(XNamespace.Xmlns + "wps", Wps),
        new(XNamespace.Xmlns + "mc", Mc),
    ];
}
