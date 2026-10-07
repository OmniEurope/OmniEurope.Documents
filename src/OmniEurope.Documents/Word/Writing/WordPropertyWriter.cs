// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>Writes run and paragraph properties in the element order the schema requires.</summary>
internal static class WordPropertyWriter
{
    public static XElement? Run(WordRunProperties? p, string name = "rPr")
    {
        if (p is null || ReferenceEquals(p, WordRunProperties.Empty))
        {
            return null;
        }

        var element = new XElement(W + name);
        Add(element, p.StyleId is null ? null : ValElement("rStyle", p.StyleId));
        Add(element, Fonts(p));
        Add(element, Flag("b", p.Bold));
        Add(element, Flag("i", p.Italic));
        Add(element, Flag("caps", p.Caps));
        Add(element, Flag("smallCaps", p.SmallCaps));
        Add(element, Flag("strike", p.Strike));
        Add(element, Flag("dstrike", p.DoubleStrike));
        Add(element, Flag("vanish", p.Hidden));
        Add(element, p.Color is null ? null : ValElement("color", p.Color));
        Add(element, p.CharacterSpacing is { } spacing ? ValElement("spacing", ToTwips(spacing)) : null);
        Add(element, p.Position is { } position ? ValElement("position", Format((int)Math.Round(position * 2))) : null);
        Add(element, p.FontSize is { } size ? ValElement("sz", Format((int)Math.Round(size * 2))) : null);
        Add(element, p.FontSizeComplex is { } sizeComplex ? ValElement("szCs", Format((int)Math.Round(sizeComplex * 2))) : null);
        Add(element, p.Highlight is null ? null : ValElement("highlight", p.Highlight));
        Add(element, p.Underline is { } underline ? ValElement("u", Underline(underline)) : null);
        Add(element, Shading(p.Shading));
        Add(element, p.VerticalPosition is { } vertical ? ValElement("vertAlign", vertical switch
        {
            WordVerticalPosition.Superscript => "superscript",
            WordVerticalPosition.Subscript => "subscript",
            _ => "baseline",
        }) : null);
        Add(element, Flag("rtl", p.RightToLeft));
        Add(element, Language(p));
        return element.HasElements ? element : null;
    }

    private static XElement? Fonts(WordRunProperties p)
    {
        if (p.Font is null && p.FontEastAsia is null && p.FontComplex is null && p.FontTheme is null)
        {
            return null;
        }

        var element = new XElement(W + "rFonts");
        if (p.Font is not null)
        {
            element.Add(new XAttribute(W + "ascii", p.Font), new XAttribute(W + "hAnsi", p.Font));
        }

        if (p.FontEastAsia is not null)
        {
            element.Add(new XAttribute(W + "eastAsia", p.FontEastAsia));
        }

        if (p.FontComplex is not null)
        {
            element.Add(new XAttribute(W + "cs", p.FontComplex));
        }

        if (p.FontTheme is not null)
        {
            element.Add(new XAttribute(W + "asciiTheme", p.FontTheme), new XAttribute(W + "hAnsiTheme", p.FontTheme));
        }

        return element;
    }

    private static XElement? Language(WordRunProperties p)
    {
        if (p.Language is null && p.LanguageEastAsia is null && p.LanguageComplex is null)
        {
            return null;
        }

        var element = new XElement(W + "lang");
        AddAttribute(element, "val", p.Language);
        AddAttribute(element, "eastAsia", p.LanguageEastAsia);
        AddAttribute(element, "bidi", p.LanguageComplex);
        return element;
    }

    private static string Underline(WordUnderline underline) => underline switch
    {
        WordUnderline.None => "none",
        WordUnderline.Words => "words",
        WordUnderline.Double => "double",
        WordUnderline.Thick => "thick",
        WordUnderline.Dotted => "dotted",
        WordUnderline.Dash => "dash",
        WordUnderline.Wave => "wave",
        _ => "single",
    };

    /// <summary>A <c>w:pPr</c>; <paramref name="sectPr"/> goes in it when the paragraph ends a section.</summary>
    public static XElement? Paragraph(WordParagraphProperties? p, XElement? sectPr = null)
    {
        var element = new XElement(W + "pPr");
        if (p is not null && !ReferenceEquals(p, WordParagraphProperties.Empty))
        {
            Add(element, p.StyleId is null ? null : ValElement("pStyle", p.StyleId));
            Add(element, Flag("keepNext", p.KeepNext));
            Add(element, Flag("keepLines", p.KeepLines));
            Add(element, Flag("pageBreakBefore", p.PageBreakBefore));
            Add(element, Flag("widowControl", p.WidowControl));
            Add(element, Numbering(p));
            Add(element, ParagraphBorders(p.Borders));
            Add(element, Shading(p.Shading));
            Add(element, Tabs(p.Tabs));
            Add(element, Flag("bidi", p.RightToLeft));
            Add(element, Spacing(p));
            Add(element, Indentation(p));
            Add(element, Flag("contextualSpacing", p.ContextualSpacing));
            Add(element, p.Alignment is { } alignment ? ValElement("jc", Alignment(alignment)) : null);
            Add(element, p.OutlineLevel is { } level ? ValElement("outlineLvl", Format(level)) : null);
            Add(element, Run(p.MarkProperties));
        }

        Add(element, sectPr);
        return element.HasElements ? element : null;
    }

    private static XElement? Numbering(WordParagraphProperties p)
    {
        if (p.NumberingId is null && p.NumberingLevel is null)
        {
            return null;
        }

        var element = new XElement(W + "numPr");
        Add(element, p.NumberingLevel is { } level ? ValElement("ilvl", Format(level)) : null);
        Add(element, p.NumberingId is { } id ? ValElement("numId", Format(id)) : null);
        return element;
    }

    private static XElement? Spacing(WordParagraphProperties p)
    {
        if (p.SpacingBefore is null && p.SpacingAfter is null && p.LineSpacing is null)
        {
            return null;
        }

        var element = new XElement(W + "spacing");
        AddAttribute(element, "before", p.SpacingBefore is { } before ? ToTwips(before) : null);
        AddAttribute(element, "after", p.SpacingAfter is { } after ? ToTwips(after) : null);
        if (p.LineSpacing is { } line)
        {
            var rule = p.LineSpacingRule ?? WordLineSpacingRule.Multiple;
            AddAttribute(element, "line", rule == WordLineSpacingRule.Multiple ? Format((int)Math.Round(line * 240)) : ToTwips(line));
            AddAttribute(element, "lineRule", rule switch
            {
                WordLineSpacingRule.Exact => "exact",
                WordLineSpacingRule.AtLeast => "atLeast",
                _ => "auto",
            });
        }

        return element;
    }

    private static XElement? Indentation(WordParagraphProperties p)
    {
        if (p.IndentLeft is null && p.IndentRight is null && p.FirstLineIndent is null)
        {
            return null;
        }

        var element = new XElement(W + "ind");
        AddAttribute(element, "left", p.IndentLeft is { } left ? ToTwips(left) : null);
        AddAttribute(element, "right", p.IndentRight is { } right ? ToTwips(right) : null);
        if (p.FirstLineIndent is { } first)
        {
            AddAttribute(element, first < 0 ? "hanging" : "firstLine", ToTwips(Math.Abs(first)));
        }

        return element;
    }

    private static XElement? Tabs(IReadOnlyList<WordTabStop>? tabs)
    {
        if (tabs is null || tabs.Count == 0)
        {
            return null;
        }

        return new XElement(W + "tabs", tabs.Select(t =>
        {
            var tab = new XElement(W + "tab", new XAttribute(W + "val", TabAlignment(t.Alignment)));
            if (t.Leader != WordTabLeader.None)
            {
                tab.Add(new XAttribute(W + "leader", TabLeader(t.Leader)));
            }

            tab.Add(new XAttribute(W + "pos", ToTwips(t.Position)));
            return tab;
        }));
    }

    public static string TabAlignment(WordTabAlignment alignment) => WordValues.TabAlignments.First(p => p.Value == alignment).Key;

    public static string TabLeader(WordTabLeader leader) => WordValues.TabLeaders.First(p => p.Value == leader).Key;

    public static string Alignment(WordAlignment alignment) => alignment switch
    {
        WordAlignment.Center => "center",
        WordAlignment.Right => "right",
        WordAlignment.Justify => "both",
        WordAlignment.Distribute => "distribute",
        _ => "left",
    };

    private static XElement? ParagraphBorders(WordParagraphBorders? borders)
    {
        if (borders is null)
        {
            return null;
        }

        var element = new XElement(W + "pBdr");
        Add(element, Border("top", borders.Top));
        Add(element, Border("left", borders.Left));
        Add(element, Border("bottom", borders.Bottom));
        Add(element, Border("right", borders.Right));
        Add(element, Border("between", borders.Between));
        return element.HasElements ? element : null;
    }

    public static XElement? Border(string name, WordBorder? border) => border is null ? null : new XElement(
        W + name,
        new XAttribute(W + "val", border.Style),
        new XAttribute(W + "sz", Format((int)Math.Round(border.Width * 8))),
        new XAttribute(W + "space", Format((int)Math.Round(border.Space))),
        new XAttribute(W + "color", border.Color));

    public static XElement? Shading(string? fill) => fill is null ? null : new XElement(
        W + "shd",
        new XAttribute(W + "val", "clear"),
        new XAttribute(W + "color", "auto"),
        new XAttribute(W + "fill", fill));

    public static XElement? Flag(string name, bool? value) => value switch
    {
        true => new XElement(W + name),
        false => ValElement(name, "0"),
        null => null,
    };

    public static void Add(XElement parent, XElement? child)
    {
        if (child is not null)
        {
            parent.Add(child);
        }
    }

    public static void AddAttribute(XElement element, string name, string? value)
    {
        if (value is not null)
        {
            element.Add(new XAttribute(W + name, value));
        }
    }
}
