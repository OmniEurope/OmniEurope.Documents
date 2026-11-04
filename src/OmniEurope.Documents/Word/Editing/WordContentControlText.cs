// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>Replaces the content of a content control with text, keeping the formatting its first run shows.</summary>
internal static class WordContentControlText
{
    private static readonly HashSet<string> TextRunContent = new(StringComparer.Ordinal)
    {
        "rPr", "t", "tab", "br", "cr", "noBreakHyphen", "softHyphen", "lastRenderedPageBreak",
    };

    private static readonly HashSet<string> TransparentParagraphContent = new(StringComparer.Ordinal)
    {
        "pPr", "r", "proofErr", "bookmarkStart", "bookmarkEnd",
    };

    /// <summary>Replaces the content with <paramref name="text"/>; returns null, or the reason the control was left unchanged.</summary>
    /// <param name="control">The control.</param>
    /// <param name="text">The new text (<c>\n</c> becomes a line break, or a new paragraph in a block control).</param>
    /// <param name="isPlaceholderStyle">Tells whether a character style id is the placeholder text style, dropped once the control is filled.</param>
    /// <param name="font">A font to set on the new run (the glyph font of a check box state), or null.</param>
    public static string? Replace(ScannedControl control, string text, Func<string, bool> isPlaceholderStyle, string? font = null)
    {
        var sdt = control.Sdt;
        if (control.Info.Level is WordContentControlLevel.Row or WordContentControlLevel.Cell)
        {
            return "a row or cell control holds table structure, not text";
        }

        var content = sdt.Element(W + "sdtContent");
        if (content is null)
        {
            content = new XElement(W + "sdtContent");
            sdt.Add(content);
        }

        var runs = WordContentControlScanner.Runs(content, sdt).ToList();
        if (runs.Any(r => r.Elements().Any(e => !TextRunContent.Contains(e.Name.LocalName))))
        {
            return "the control holds more than text (a field, picture or note)";
        }

        var template = Template(sdt, runs, control.Info.ShowingPlaceholder, isPlaceholderStyle, font);
        var reason = control.Info.Level == WordContentControlLevel.Block
            ? Block(content, text, template)
            : Inline(content, runs, text, template);
        if (reason is null)
        {
            sdt.Element(W + "sdtPr")?.Element(W + "showingPlcHdr")?.Remove();
        }

        return reason;
    }

    private static string? Inline(XElement content, List<XElement> runs, string text, XElement? template)
    {
        var run = WordRunBuilder.NewRun(new WordTextPiece(text), template, null);
        if (runs.Count == 0)
        {
            content.Add(run);
            return null;
        }

        runs[0].AddBeforeSelf(run);
        foreach (var old in runs)
        {
            var parent = old.Parent;
            old.Remove();
            WordRunBuilder.RemoveIfEmpty(content, parent);
        }

        return null;
    }

    private static string? Block(XElement content, string text, XElement? template)
    {
        if (content.Elements().Any(e => e.Name != W + "p") || content.Elements(W + "p").Any(p => p.Elements().Any(e => !TransparentParagraphContent.Contains(e.Name.LocalName))))
        {
            return "the control holds more than paragraphs of text (a table, nested control or field)";
        }

        var properties = content.Elements(W + "p").FirstOrDefault()?.Element(W + "pPr");
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line =>
        {
            var paragraph = new XElement(W + "p");
            if (properties is not null)
            {
                paragraph.Add(new XElement(properties));
            }

            paragraph.Add(WordRunBuilder.NewRun(new WordTextPiece(line), template, null));
            return paragraph;
        }).ToList();
        content.RemoveNodes();
        content.Add(paragraphs);
        return null;
    }

    // The run properties of the first text run, else the control's own run properties; the placeholder style
    // is dropped when the control showed its placeholder.
    private static XElement? Template(XElement sdt, List<XElement> runs, bool placeholder, Func<string, bool> isPlaceholderStyle, string? font)
    {
        var source = runs.Count > 0
            ? (runs.FirstOrDefault(r => r.Element(W + "t") is not null) ?? runs[0]).Element(W + "rPr")
            : sdt.Element(W + "sdtPr")?.Element(W + "rPr");
        var copy = source is null ? new XElement(W + "rPr") : new XElement(source);
        if (placeholder && Val(copy, "rStyle") is { } style && isPlaceholderStyle(style))
        {
            copy.Element(W + "rStyle")!.Remove();
        }

        if (font is not null)
        {
            SetFont(copy, font);
        }

        return copy.HasElements ? copy : null;
    }

    private static void SetFont(XElement properties, string font)
    {
        var fonts = properties.Element(W + "rFonts");
        if (fonts is null)
        {
            fonts = new XElement(W + "rFonts");
            var style = properties.Element(W + "rStyle");
            if (style is null)
            {
                properties.AddFirst(fonts);
            }
            else
            {
                style.AddAfterSelf(fonts);
            }
        }

        fonts.Attributes().Where(a => a.Name.Namespace == W && a.Name.LocalName is not "hint").Remove();
        foreach (var name in new[] { "ascii", "hAnsi", "eastAsia" })
        {
            fonts.SetAttributeValue(W + name, font);
        }
    }
}
