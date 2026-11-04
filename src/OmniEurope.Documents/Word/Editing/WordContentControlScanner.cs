// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>A content control: its element and the snapshot read from it.</summary>
internal sealed record ScannedControl(XElement Sdt, WordContentControl Info);

/// <summary>Finds the content controls of the text parts and reads their properties and current text.</summary>
internal static class WordContentControlScanner
{
    public static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    private static readonly HashSet<string> OtherTypes = new(StringComparer.Ordinal)
    {
        "docPartObj", "docPartList", "citation", "bibliography", "equation", "group", "repeatingSection", "repeatingSectionItem",
    };

    private static readonly HashSet<XName> Containers =
        [W + "p", W + "tc", W + "tr", W + "tbl", W + "txbxContent", W + "body", W + "hdr", W + "ftr", W + "footnote", W + "endnote", W + "comment"];

    public static List<ScannedControl> Scan(IEnumerable<(string PartName, XElement Root)> parts)
    {
        var result = new List<ScannedControl>();
        foreach (var (partName, root) in parts)
        {
            result.AddRange(root.Descendants(W + "sdt").Where(s => !s.Ancestors(Mc + "Fallback").Any()).Select(s => new ScannedControl(s, Read(s, partName))));
        }

        return result;
    }

    public static WordContentControl Read(XElement sdt, string partName)
    {
        var properties = sdt.Element(W + "sdtPr");
        var kind = KindOf(properties);
        return new WordContentControl
        {
            Tag = Val(properties, "tag"),
            Alias = Val(properties, "alias"),
            Id = Int(Val(properties, "id")),
            Kind = kind,
            Text = Text(sdt),
            ShowingPlaceholder = properties?.Element(W + "showingPlcHdr") is not null,
            PartName = partName,
            Level = LevelOf(sdt),
            Date = kind == WordContentControlKind.Date ? ReadDate(properties!.Element(W + "date")!) : null,
            ListItems = Items(properties, kind),
            Checked = kind == WordContentControlKind.Checkbox ? IsChecked(properties!.Element(W14 + "checkbox")!) : null,
        };
    }

    public static bool IsChecked(XElement checkbox) => checkbox.Element(W14 + "checked")?.Attribute(W14 + "val")?.Value is "1" or "true" or "on";

    private static WordContentControlKind KindOf(XElement? properties)
    {
        foreach (var child in properties?.Elements() ?? [])
        {
            var kind = child.Name switch
            {
                var n when n == W + "text" => WordContentControlKind.Text,
                var n when n == W + "richText" => WordContentControlKind.RichText,
                var n when n == W + "date" => WordContentControlKind.Date,
                var n when n == W + "comboBox" => WordContentControlKind.ComboBox,
                var n when n == W + "dropDownList" => WordContentControlKind.DropDownList,
                var n when n == W14 + "checkbox" => WordContentControlKind.Checkbox,
                var n when n == W + "picture" => WordContentControlKind.Picture,
                var n when OtherTypes.Contains(n.LocalName) => WordContentControlKind.Other,
                _ => (WordContentControlKind?)null,
            };
            if (kind is not null)
            {
                return kind.Value;
            }
        }

        return WordContentControlKind.RichText;
    }

    private static WordContentControlLevel LevelOf(XElement sdt)
    {
        var container = sdt.Ancestors().FirstOrDefault(a => Containers.Contains(a.Name));
        return container?.Name.LocalName switch
        {
            "tbl" => WordContentControlLevel.Row,
            "tr" => WordContentControlLevel.Cell,
            "p" => WordContentControlLevel.Inline,
            _ => WordContentControlLevel.Block,
        };
    }

    private static WordContentControlDate ReadDate(XElement date)
    {
        var full = date.Attribute(W + "fullDate")?.Value;
        DateTimeOffset? parsed = full is not null && DateTimeOffset.TryParse(full, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
        return new WordContentControlDate(Val(date, "dateFormat"), Val(date, "lid"), parsed);
    }

    private static List<WordContentControlItem> Items(XElement? properties, WordContentControlKind kind)
    {
        var list = kind switch
        {
            WordContentControlKind.ComboBox => properties!.Element(W + "comboBox"),
            WordContentControlKind.DropDownList => properties!.Element(W + "dropDownList"),
            _ => null,
        };
        return (list?.Elements(W + "listItem") ?? []).Select(item =>
        {
            var value = Attr(item, "value");
            var display = Attr(item, "displayText");
            return new WordContentControlItem(display ?? value ?? string.Empty, value ?? display ?? string.Empty);
        }).ToList();
    }

    /// <summary>The visible text of the content of a control: the text of its runs (deleted revisions excluded),
    /// one line per paragraph.</summary>
    public static string Text(XElement sdt)
    {
        var content = sdt.Element(W + "sdtContent");
        if (content is null)
        {
            return string.Empty;
        }

        var lines = new List<(XElement? Paragraph, System.Text.StringBuilder Text)>();
        foreach (var run in Runs(content, sdt))
        {
            var paragraph = run.Ancestors(W + "p").FirstOrDefault();
            if (lines.Count == 0 || lines[^1].Paragraph != paragraph)
            {
                lines.Add((paragraph, new System.Text.StringBuilder()));
            }

            lines[^1].Text.Append(WordRunScanner.Text(run));
        }

        return string.Join("\n", lines.Select(l => l.Text.ToString()));
    }

    /// <summary>The runs of the content of a control that it owns: not those of a nested control, a deleted
    /// revision or an alternate-content fallback.</summary>
    public static IEnumerable<XElement> Runs(XElement content, XElement sdt) =>
        content.Descendants(W + "r").Where(r =>
            r.Ancestors(W + "sdt").First() == sdt && !r.Ancestors().TakeWhile(a => a != content).Any(a => a.Name == W + "del" || a.Name == W + "moveFrom" || a.Name == Mc + "Fallback"));
}
