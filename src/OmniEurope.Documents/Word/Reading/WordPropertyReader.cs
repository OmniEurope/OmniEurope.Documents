// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>Reads <c>w:pPr</c>, <c>w:rPr</c> and their parts. Tracked property changes are ignored (only the current values are read).</summary>
internal static class WordPropertyReader
{
    public static WordRunProperties? Run(XElement? rPr)
    {
        if (rPr is null)
        {
            return null;
        }

        var fonts = rPr.Element(W + "rFonts");
        var language = rPr.Element(W + "lang");
        return new WordRunProperties
        {
            StyleId = Val(rPr, "rStyle"),
            Font = Attr(fonts, "ascii") ?? Attr(fonts, "hAnsi"),
            FontEastAsia = Attr(fonts, "eastAsia"),
            FontComplex = Attr(fonts, "cs"),
            FontTheme = Attr(fonts, "asciiTheme") ?? Attr(fonts, "hAnsiTheme"),
            Bold = OnOff(rPr, "b"),
            Italic = OnOff(rPr, "i"),
            Underline = Underline(Val(rPr, "u"), rPr.Element(W + "u") is not null),
            Strike = OnOff(rPr, "strike"),
            DoubleStrike = OnOff(rPr, "dstrike"),
            Caps = OnOff(rPr, "caps"),
            SmallCaps = OnOff(rPr, "smallCaps"),
            Hidden = OnOff(rPr, "vanish"),
            VerticalPosition = Val(rPr, "vertAlign") switch
            {
                "superscript" => WordVerticalPosition.Superscript,
                "subscript" => WordVerticalPosition.Subscript,
                "baseline" => WordVerticalPosition.Baseline,
                _ => null,
            },
            FontSize = Measure(Val(rPr, "sz"), 2),
            FontSizeComplex = Measure(Val(rPr, "szCs"), 2),
            Color = Val(rPr, "color"),
            Highlight = Val(rPr, "highlight"),
            Shading = Fill(rPr.Element(W + "shd")),
            CharacterSpacing = Twips(Val(rPr, "spacing")),
            Position = Measure(Val(rPr, "position"), 2),
            Language = Attr(language, "val"),
            LanguageEastAsia = Attr(language, "eastAsia"),
            LanguageComplex = Attr(language, "bidi"),
            RightToLeft = OnOff(rPr, "rtl"),
        };
    }

    public static WordParagraphProperties? Paragraph(XElement? pPr)
    {
        if (pPr is null)
        {
            return null;
        }

        var spacing = pPr.Element(W + "spacing");
        var numbering = pPr.Element(W + "numPr");
        var (line, rule) = LineSpacing(spacing);
        return Indentation(pPr.Element(W + "ind"), new WordParagraphProperties
        {
            StyleId = Val(pPr, "pStyle"),
            Alignment = Alignment(Val(pPr, "jc")),
            SpacingBefore = Twips(Attr(spacing, "before")),
            SpacingAfter = Twips(Attr(spacing, "after")),
            LineSpacing = line,
            LineSpacingRule = rule,
            ContextualSpacing = OnOff(pPr, "contextualSpacing"),
            KeepNext = OnOff(pPr, "keepNext"),
            KeepLines = OnOff(pPr, "keepLines"),
            PageBreakBefore = OnOff(pPr, "pageBreakBefore"),
            WidowControl = OnOff(pPr, "widowControl"),
            OutlineLevel = Int(Val(pPr, "outlineLvl")),
            NumberingId = Int(Val(numbering, "numId")),
            NumberingLevel = Int(Val(numbering, "ilvl")),
            Tabs = Tabs(pPr.Element(W + "tabs")),
            Shading = Fill(pPr.Element(W + "shd")),
            Borders = ParagraphBorders(pPr.Element(W + "pBdr")),
            RightToLeft = OnOff(pPr, "bidi"),
            MarkProperties = Run(pPr.Element(W + "rPr")),
        });
    }

    private static WordParagraphProperties Indentation(XElement? ind, WordParagraphProperties properties)
    {
        if (ind is null)
        {
            return properties;
        }

        var hanging = Twips(Attr(ind, "hanging"));
        return properties with
        {
            IndentLeft = Twips(Attr(ind, "start") ?? Attr(ind, "left")),
            IndentRight = Twips(Attr(ind, "end") ?? Attr(ind, "right")),
            FirstLineIndent = hanging is { } h ? -h : Twips(Attr(ind, "firstLine")),
        };
    }

    private static (double? Value, WordLineSpacingRule? Rule) LineSpacing(XElement? spacing)
    {
        var line = Attr(spacing, "line");
        if (line is null)
        {
            return (null, null);
        }

        return Attr(spacing, "lineRule") switch
        {
            "exact" => (Twips(line), WordLineSpacingRule.Exact),
            "atLeast" => (Twips(line), WordLineSpacingRule.AtLeast),
            _ => (Measure(line, 240), WordLineSpacingRule.Multiple),
        };
    }

    public static WordAlignment? Alignment(string? value) => WordValues.Find(WordValues.Alignments, value);

    private static WordUnderline? Underline(string? value, bool present) =>
        value is null ? (present ? WordUnderline.Single : null) : WordValues.Find(WordValues.Underlines, value) ?? WordUnderline.Single;

    private static List<WordTabStop>? Tabs(XElement? tabs)
    {
        if (tabs is null)
        {
            return null;
        }

        var result = new List<WordTabStop>();
        foreach (var tab in tabs.Elements(W + "tab"))
        {
            if (Twips(Attr(tab, "pos")) is not { } position)
            {
                continue;
            }

            result.Add(new WordTabStop(position, TabAlignment(Attr(tab, "val")), TabLeader(Attr(tab, "leader"))));
        }

        return result;
    }

    public static WordTabAlignment TabAlignment(string? value) => WordValues.Find(WordValues.TabAlignments, value) ?? WordTabAlignment.Left;

    public static WordTabLeader TabLeader(string? value) => WordValues.Find(WordValues.TabLeaders, value) ?? WordTabLeader.None;

    public static string? Fill(XElement? shd)
    {
        var fill = Attr(shd, "fill");
        return fill is null or "auto" ? null : fill;
    }

    public static WordBorder? Border(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return new WordBorder(
            Attr(element, "val") ?? "single",
            Measure(Attr(element, "sz"), 8) ?? 0,
            Measure(Attr(element, "space"), 1) ?? 0,
            Attr(element, "color") ?? "auto");
    }

    private static WordParagraphBorders? ParagraphBorders(XElement? pBdr) => pBdr is null ? null : new WordParagraphBorders(
        Border(pBdr.Element(W + "top")),
        Border(pBdr.Element(W + "left") ?? pBdr.Element(W + "start")),
        Border(pBdr.Element(W + "bottom")),
        Border(pBdr.Element(W + "right") ?? pBdr.Element(W + "end")),
        Border(pBdr.Element(W + "between")));
}

