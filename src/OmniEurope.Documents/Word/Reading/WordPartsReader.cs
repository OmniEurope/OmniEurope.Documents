// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>Reads the styles, numbering, settings, theme and core properties parts.</summary>
internal static class WordPartsReader
{
    public static WordStyleSheet Styles(XDocument? styles, XDocument? theme)
    {
        var fonts = theme?.Root?.Descendants(A + "fontScheme").FirstOrDefault();
        var sheet = new WordStyleSheet { MajorFont = ThemeFont(fonts, "majorFont"), MinorFont = ThemeFont(fonts, "minorFont") };
        var root = styles?.Root;
        if (root?.Element(W + "docDefaults") is null)
        {
            // Without document defaults Word sets paragraphs 8 pt apart at 1.15 lines (measured against Word).
            sheet.DefaultParagraphProperties = new WordParagraphProperties { SpacingAfter = 8, LineSpacing = 1.15, LineSpacingRule = WordLineSpacingRule.Multiple };
        }

        if (root is null)
        {
            return sheet;
        }

        var defaults = root.Element(W + "docDefaults");
        sheet.DefaultRunProperties = WordPropertyReader.Run(Child(defaults, "rPrDefault", "rPr")) ?? WordRunProperties.Empty;
        if (defaults is not null)
        {
            sheet.DefaultParagraphProperties = WordPropertyReader.Paragraph(Child(defaults, "pPrDefault", "pPr")) ?? WordParagraphProperties.Empty;
        }

        foreach (var element in root.Elements(W + "style"))
        {
            if (Style(element) is { } style)
            {
                sheet.Styles.TryAdd(style.Id, style);
            }
        }

        return sheet;
    }

    private static string? ThemeFont(XElement? scheme, string slot) =>
        NonEmpty((string?)scheme?.Element(A + slot)?.Element(A + "latin")?.Attribute("typeface"));

    private static XElement? Child(XElement? parent, string first, string second) => parent?.Element(W + first)?.Element(W + second);

    private static WordStyle? Style(XElement element)
    {
        var id = Attr(element, "styleId");
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var type = Attr(element, "type") switch
        {
            "character" => WordStyleType.Character,
            "table" => WordStyleType.Table,
            "numbering" => WordStyleType.Numbering,
            _ => WordStyleType.Paragraph,
        };
        return new WordStyle(id, type)
        {
            Name = Val(element, "name"),
            BasedOn = Val(element, "basedOn"),
            Next = Val(element, "next"),
            Link = Val(element, "link"),
            IsDefault = Attr(element, "default") is "1" or "true" or "on",
            IsPrimary = OnOff(element, "qFormat") ?? false,
            ParagraphProperties = WordPropertyReader.Paragraph(element.Element(W + "pPr")),
            RunProperties = WordPropertyReader.Run(element.Element(W + "rPr")),
            TableProperties = WordStructureReader.Table(element.Element(W + "tblPr")),
            CellProperties = WordStructureReader.Cell(element.Element(W + "tcPr")),
            ConditionalFormats = element.Elements(W + "tblStylePr").Select(Conditional).OfType<WordTableConditionalFormat>().ToList(),
        };
    }

    private static WordTableConditionalFormat? Conditional(XElement element) => WordStructureReader.Region(Attr(element, "type")) is { } region
        ? new WordTableConditionalFormat(
            region,
            WordPropertyReader.Paragraph(element.Element(W + "pPr")),
            WordPropertyReader.Run(element.Element(W + "rPr")),
            WordStructureReader.Table(element.Element(W + "tblPr")),
            WordStructureReader.Cell(element.Element(W + "tcPr")))
        : null;

    public static WordNumbering Numbering(XDocument? numbering, WordStyleSheet styles)
    {
        var result = new WordNumbering();
        var root = numbering?.Root;
        if (root is null)
        {
            return result;
        }

        var links = new Dictionary<int, string>();
        foreach (var element in root.Elements(W + "abstractNum"))
        {
            if (Int(Attr(element, "abstractNumId")) is not { } id)
            {
                continue;
            }

            var definition = new WordAbstractNumbering(id);
            foreach (var level in element.Elements(W + "lvl").Select(Level).OfType<WordNumberingLevel>())
            {
                definition.Levels[level.Level] = level;
            }

            if (Val(element, "numStyleLink") is { } link)
            {
                links[id] = link;
            }

            result.Abstracts[id] = definition;
        }

        foreach (var element in root.Elements(W + "num"))
        {
            if (Instance(element) is { } instance)
            {
                result.Instances[instance.Id] = instance;
            }
        }

        ResolveStyleLinks(result, styles, links);
        return result;
    }

    // An abstract definition can borrow its levels from a numbering style, which names an instance.
    private static void ResolveStyleLinks(WordNumbering numbering, WordStyleSheet styles, Dictionary<int, string> links)
    {
        foreach (var (id, styleId) in links)
        {
            var target = styles.Get(styleId)?.ParagraphProperties?.NumberingId;
            if (target is null || !numbering.Instances.TryGetValue(target.Value, out var instance) || instance.AbstractId == id
                || !numbering.Abstracts.TryGetValue(instance.AbstractId, out var source) || numbering.Abstracts[id].Levels.Count > 0)
            {
                continue;
            }

            foreach (var (level, definition) in source.Levels)
            {
                numbering.Abstracts[id].Levels[level] = definition;
            }
        }
    }

    private static WordNumberingInstance? Instance(XElement element)
    {
        if (Int(Attr(element, "numId")) is not { } id || Int(Val(element, "abstractNumId")) is not { } abstractId)
        {
            return null;
        }

        var instance = new WordNumberingInstance(id, abstractId);
        foreach (var levelOverride in element.Elements(W + "lvlOverride"))
        {
            if (Int(Attr(levelOverride, "ilvl")) is not { } level)
            {
                continue;
            }

            if (Int(Val(levelOverride, "startOverride")) is { } start)
            {
                instance.StartOverrides[level] = start;
            }

            if (levelOverride.Element(W + "lvl") is { } replaced && Level(replaced) is { } definition)
            {
                instance.LevelOverrides[level] = definition with { Level = level };
            }
        }

        return instance;
    }

    private static WordNumberingLevel? Level(XElement element)
    {
        if (Int(Attr(element, "ilvl")) is not { } level)
        {
            return null;
        }

        return new WordNumberingLevel(level)
        {
            Start = Int(Val(element, "start")) ?? 0,
            Format = WordStructureReader.NumberFormat(Val(element, "numFmt")) ?? WordNumberFormat.Decimal,
            Text = Val(element, "lvlText") ?? string.Empty,
            Suffix = Val(element, "suff") switch
            {
                "space" => WordLabelSuffix.Space,
                "nothing" => WordLabelSuffix.Nothing,
                _ => WordLabelSuffix.Tab,
            },
            Alignment = WordPropertyReader.Alignment(Val(element, "lvlJc")) ?? WordAlignment.Left,
            IsLegal = OnOff(element, "isLgl") ?? false,
            RestartAfter = Int(Val(element, "lvlRestart")),
            StyleId = Val(element, "pStyle"),
            ParagraphProperties = WordPropertyReader.Paragraph(element.Element(W + "pPr")),
            RunProperties = WordPropertyReader.Run(element.Element(W + "rPr")),
        };
    }

    public static WordSettings Settings(XDocument? settings)
    {
        var root = settings?.Root;
        var result = new WordSettings();
        if (root is null)
        {
            return result;
        }

        var footnotes = root.Element(W + "footnotePr");
        var endnotes = root.Element(W + "endnotePr");
        return result with
        {
            DefaultTabStop = Twips(Val(root, "defaultTabStop")) ?? result.DefaultTabStop,
            EvenAndOddHeaders = OnOff(root, "evenAndOddHeaders") ?? false,
            UpdateFieldsOnOpen = OnOff(root, "updateFields") ?? false,
            FootnoteFormat = WordStructureReader.NumberFormat(Val(footnotes, "numFmt")) ?? result.FootnoteFormat,
            FootnoteStart = Int(Val(footnotes, "numStart")) ?? result.FootnoteStart,
            FootnoteRestart = WordStructureReader.NoteRestart(Val(footnotes, "numRestart")) ?? WordNoteRestart.Continuous,
            EndnoteFormat = WordStructureReader.NumberFormat(Val(endnotes, "numFmt")) ?? result.EndnoteFormat,
            EndnoteStart = Int(Val(endnotes, "numStart")) ?? result.EndnoteStart,
        };
    }

    public static WordInformation Information(XDocument? core)
    {
        var root = core?.Root;
        if (root is null)
        {
            return new WordInformation();
        }

        return new WordInformation
        {
            Title = NonEmpty(root.Element(Dc + "title")?.Value),
            Subject = NonEmpty(root.Element(Dc + "subject")?.Value),
            Author = NonEmpty(root.Element(Dc + "creator")?.Value),
            Keywords = NonEmpty(root.Element(Cp + "keywords")?.Value),
            Description = NonEmpty(root.Element(Dc + "description")?.Value),
            Category = NonEmpty(root.Element(Cp + "category")?.Value),
            LastModifiedBy = NonEmpty(root.Element(Cp + "lastModifiedBy")?.Value),
            Created = Date(root.Element(DcTerms + "created")?.Value),
            Modified = Date(root.Element(DcTerms + "modified")?.Value),
        };
    }

    private static DateTimeOffset? Date(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
