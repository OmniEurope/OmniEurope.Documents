// SPDX-License-Identifier: EUPL-1.2
using System.Text.RegularExpressions;
using OmniEurope.Documents.Html;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>
/// Walks an HTML tree and writes Word blocks: headings, paragraphs, lists (nested, numbered or bulleted),
/// block quotes, preformatted text, tables (with column and row spans), rules, line breaks, links and inline
/// formatting (tags and the inline style attribute). Images are taken from <c>data:</c> URIs only: nothing is
/// ever fetched. Scripts, styles and hidden elements are skipped.
/// </summary>
internal sealed class HtmlWordWriter
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "head", "template", "noscript", "title", "meta", "link", "iframe", "object", "embed", "svg", "canvas", "button", "select", "textarea",
    };

    private static readonly HashSet<string> ParagraphTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "header", "footer", "main", "nav", "aside", "address", "figure", "figcaption", "center",
        "dd", "dt", "dl", "form", "fieldset", "details", "summary", "body", "html", "caption", "legend",
    };

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);

    private readonly WordDocument _document;
    private readonly Stack<List<WordBlock>> _containers = new();
    private readonly List<int> _lists = [];
    private WordParagraph? _paragraph;
    private List<WordInline>? _sink;
    private WordRunProperties _run = WordRunProperties.Empty;
    private WordParagraphProperties _block = WordParagraphProperties.Empty;
    private bool _pre;

    public HtmlWordWriter(WordDocument document)
    {
        _document = document;
        _containers.Push(document.Body);
    }

    private double BaseSize => _run.FontSize ?? _document.Styles.DefaultRunProperties.FontSize ?? 11;

    public void Write(HtmlNode root)
    {
        Children(root);
        Flush();
    }

    /// <summary>Writes the children of <paramref name="node"/> into another block list (a table cell).</summary>
    public void WriteInto(List<WordBlock> target, HtmlElement node, WordRunProperties? overlay)
    {
        var saved = (_paragraph, _sink, _run, _block, _pre);
        var lists = _lists.ToList();
        Flush();
        _containers.Push(target);
        var alignment = CssStyle.Alignment(CssStyle.Parse(node.GetAttribute("style")), node.GetAttribute("align"));
        (_paragraph, _sink, _block) = (null, null, new WordParagraphProperties { Alignment = alignment });
        _run = overlay is null ? _run : _run.Overlay(overlay);
        _lists.Clear();
        Children(node);
        Flush();
        _containers.Pop();
        (_paragraph, _sink, _run, _block, _pre) = saved;
        _lists.Clear();
        _lists.AddRange(lists);
    }

    public void AddGap(string gap)
    {
        if (!_document.Gaps.Contains(gap))
        {
            _document.Gaps.Add(gap);
        }
    }

    public WordDocument Document => _document;

    public List<WordBlock> Container => _containers.Peek();

    private void Children(HtmlNode node)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case HtmlText text:
                    Text(text.Data);
                    break;
                case HtmlElement element:
                    Element(element);
                    break;
            }
        }
    }

    private void Element(HtmlElement element)
    {
        var name = element.TagName.ToLowerInvariant();
        var css = CssStyle.Parse(element.GetAttribute("style"));
        if (Skipped.Contains(name) || css.GetValueOrDefault("display") == "none" || element.HasAttribute("hidden"))
        {
            return;
        }

        var savedRun = _run;
        _run = CssStyle.Run(css, Inline(name, element, _run), BaseSize);
        if (!BlockElement(element, name, css))
        {
            InlineElement(element, name);
        }

        _run = savedRun;
    }

    // Character formatting a tag implies, given the current size.
    private static readonly Dictionary<string, Func<WordRunProperties, double, WordRunProperties>> TagFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["b"] = (r, _) => r with { Bold = true },
        ["strong"] = (r, _) => r with { Bold = true },
        ["th"] = (r, _) => r with { Bold = true },
        ["dt"] = (r, _) => r with { Bold = true },
        ["i"] = (r, _) => r with { Italic = true },
        ["em"] = (r, _) => r with { Italic = true },
        ["cite"] = (r, _) => r with { Italic = true },
        ["dfn"] = (r, _) => r with { Italic = true },
        ["var"] = (r, _) => r with { Italic = true },
        ["u"] = (r, _) => r with { Underline = WordUnderline.Single },
        ["ins"] = (r, _) => r with { Underline = WordUnderline.Single },
        ["s"] = (r, _) => r with { Strike = true },
        ["strike"] = (r, _) => r with { Strike = true },
        ["del"] = (r, _) => r with { Strike = true },
        ["code"] = (r, _) => Monospace(r),
        ["kbd"] = (r, _) => Monospace(r),
        ["samp"] = (r, _) => Monospace(r),
        ["tt"] = (r, _) => Monospace(r),
        ["pre"] = (r, _) => Monospace(r),
        ["sub"] = (r, _) => r with { VerticalPosition = WordVerticalPosition.Subscript },
        ["sup"] = (r, _) => r with { VerticalPosition = WordVerticalPosition.Superscript },
        ["mark"] = (r, _) => r with { Highlight = "yellow" },
        ["small"] = (r, size) => r with { FontSize = size * 0.83 },
        ["big"] = (r, size) => r with { FontSize = size * 1.2 },
    };

    // Paragraph formatting a block tag adds to the enclosing block's.
    private static readonly Dictionary<string, Func<WordParagraphProperties, WordParagraphProperties>> BlockFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["h1"] = b => b with { StyleId = "Heading1" },
        ["h2"] = b => b with { StyleId = "Heading2" },
        ["h3"] = b => b with { StyleId = "Heading3" },
        ["h4"] = b => b with { StyleId = "Heading4" },
        ["h5"] = b => b with { StyleId = "Heading5" },
        ["h6"] = b => b with { StyleId = "Heading6" },
        ["blockquote"] = b => b with { IndentLeft = (b.IndentLeft ?? 0) + 36, IndentRight = (b.IndentRight ?? 0) + 36 },
        ["dd"] = b => b with { IndentLeft = (b.IndentLeft ?? 0) + 36 },
        ["pre"] = b => b with { Shading = "F2F2F2", SpacingBefore = 6, SpacingAfter = 8 },
        ["figcaption"] = b => b with { StyleId = "Caption" },
        ["caption"] = b => b with { StyleId = "Caption" },
        ["center"] = b => b with { Alignment = WordAlignment.Center },
    };

    private static WordRunProperties Monospace(WordRunProperties run) => run with { Font = "Courier New", FontTheme = null };

    private WordRunProperties Inline(string name, HtmlElement element, WordRunProperties run)
    {
        if (name == "font")
        {
            return FontTag(element, run);
        }

        return TagFormats.TryGetValue(name, out var format) ? format(run, BaseSize) : run;
    }

    private static WordRunProperties FontTag(HtmlElement element, WordRunProperties run)
    {
        if (element.GetAttribute("color") is { } color && CssStyle.Color(color) is { } hex)
        {
            run = run with { Color = hex };
        }

        return element.GetAttribute("face") is { Length: > 0 } face ? run with { Font = face.Split(',')[0].Trim(), FontTheme = null } : run;
    }

    private bool BlockElement(HtmlElement element, string name, Dictionary<string, string> css)
    {
        switch (name)
        {
            case "ul" or "ol" or "menu":
                List(element, ordered: name == "ol");
                return true;
            case "li":
                ListItem(element, css);
                return true;
            case "table":
                Flush();
                HtmlTableBuilder.Build(element, this);
                return true;
            case "hr":
                Flush();
                Container.Add(new WordParagraph { Properties = new WordParagraphProperties { Borders = new WordParagraphBorders(Bottom: new WordBorder("single", 0.75, 1, "A0A0A0")) } });
                return true;
        }

        var format = BlockFormats.GetValueOrDefault(name);
        if (format is null && !ParagraphTags.Contains(name))
        {
            return false;
        }

        var wasPre = _pre;
        _pre |= name == "pre";
        Paragraph(element, css, format is null ? _block : format(_block));
        _pre = wasPre;
        return true;
    }
    private void Paragraph(HtmlElement element, Dictionary<string, string> css, WordParagraphProperties block)
    {
        Flush();
        var saved = _block;
        _block = CssStyle.Alignment(css, element.GetAttribute("align")) is { } alignment ? block with { Alignment = alignment } : block;
        Children(element);
        Flush();
        _block = saved;
    }

    private void List(HtmlElement element, bool ordered)
    {
        Flush();
        var numbering = _document.Numbering;
        var id = ordered ? numbering.AddNumberedList() : numbering.AddBulletList();
        if (ordered && int.TryParse(element.GetAttribute("start"), out var start) && start != 1)
        {
            numbering.Instances[id].StartOverrides[0] = start;
        }

        _lists.Add(id);
        Children(element);
        Flush();
        _lists.RemoveAt(_lists.Count - 1);
    }

    private void ListItem(HtmlElement element, Dictionary<string, string> css)
    {
        if (_lists.Count == 0)
        {
            _lists.Add(_document.Numbering.AddBulletList());
        }

        var item = _block with { NumberingId = _lists[^1], NumberingLevel = Math.Min(_lists.Count - 1, 8), StyleId = _block.StyleId ?? "ListParagraph" };
        Paragraph(element, css, item);
    }

    private void InlineElement(HtmlElement element, string name)
    {
        switch (name)
        {
            case "br":
                AddInline(new WordBreak(WordBreakKind.Line));
                return;
            case "img":
                Image(element);
                return;
            case "input" when string.Equals(element.GetAttribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase):
                AddText(element.HasAttribute("checked") ? "[x] " : "[ ] ");
                return;
            case "a" when Link(element.GetAttribute("href")) is { } target:
                Hyperlink(element, target);
                return;
        }

        Children(element);
    }

    private static string? Link(string? href) =>
        href is not null && (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) ? href : null;

    private void Hyperlink(HtmlElement element, string target)
    {
        var link = WordHyperlink.Create(target, null);
        AddInline(link);
        var (savedSink, savedRun) = (_sink, _run);
        _sink = link.Inlines;
        _run = _run with { StyleId = _document.Styles.Get("Hyperlink") is null ? _run.StyleId : "Hyperlink" };
        Children(element);
        (_sink, _run) = (savedSink, savedRun);
    }

    private void Image(HtmlElement element)
    {
        var source = element.GetAttribute("src") ?? string.Empty;
        var data = DataUri(source);
        if (data is null)
        {
            AddGap("images that are not data: URIs are not loaded");
            return;
        }

        WordImage image;
        try
        {
            image = WordImage.FromBytes(data);
        }
        catch (ArgumentException)
        {
            AddGap("unrecognised embedded image skipped");
            return;
        }

        var (width, height) = ImageSize(element, data);
        var limit = _document.Sections[^1].Page.ContentWidth;
        if (width > limit)
        {
            (width, height) = (limit, height * limit / width);
        }

        AddInline(new WordPicture(image, width, height) { Description = element.GetAttribute("alt") });
    }

    private static byte[]? DataUri(string source)
    {
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || source.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase) is not (> 0 and var at))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(source[(at + 8)..].Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Size from the width and height attributes or styles (CSS pixels), else the image's own size at its resolution.
    private static (double Width, double Height) ImageSize(HtmlElement element, byte[] data)
    {
        var css = CssStyle.Parse(element.GetAttribute("style"));
        var width = Dimension(css, element, "width");
        var height = Dimension(css, element, "height");
        var (naturalWidth, naturalHeight) = NaturalSize(data);
        if (width > 0 && height > 0)
        {
            return (width, height);
        }

        if (width > 0)
        {
            return (width, width * naturalHeight / naturalWidth);
        }

        return height > 0 ? (height * naturalWidth / naturalHeight, height) : (naturalWidth, naturalHeight);
    }

    private static double Dimension(Dictionary<string, string> css, HtmlElement element, string name) =>
        CssStyle.Length(css.GetValueOrDefault(name) ?? element.GetAttribute(name) ?? string.Empty, 0) ?? 0;

    private static (double Width, double Height) NaturalSize(byte[] data)
    {
        if (!ImageInfo.TryIdentify(data, out var info))
        {
            return (100, 100);
        }

        return (info.Width * 72 / (info.DpiX > 0 ? info.DpiX : 96), info.Height * 72 / (info.DpiY > 0 ? info.DpiY : 96));
    }

    private void Text(string data)
    {
        if (_pre)
        {
            EnsureParagraph();
            foreach (var inline in WordText.Split(data.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'), _run))
            {
                Sink.Add(inline);
            }

            return;
        }

        var text = Spaces.Replace(data, " ");
        if (_paragraph is null || EndsWithSpace())
        {
            text = text.TrimStart();
        }

        if (text.Length > 0)
        {
            AddText(text);
        }
    }

    private void AddText(string text) => AddInline(new WordText(text, _run));

    private void AddInline(WordInline inline)
    {
        EnsureParagraph();
        Sink.Add(inline);
    }

    private List<WordInline> Sink => _sink ?? _paragraph!.Inlines;

    private bool EndsWithSpace()
    {
        var last = Sink.LastOrDefault();
        return last is null || last is WordBreak || (last is WordText text && text.Value.EndsWith(' '));
    }

    private void EnsureParagraph()
    {
        if (_paragraph is not null)
        {
            return;
        }

        _paragraph = new WordParagraph { Properties = _block };
        Container.Add(_paragraph);
    }

    // Ends the open paragraph: trailing spaces are dropped, and an empty paragraph is removed.
    private void Flush()
    {
        if (_paragraph is null)
        {
            return;
        }

        while (_paragraph.Inlines.LastOrDefault() is WordText { } last && last.Value.EndsWith(' '))
        {
            last.Value = last.Value.TrimEnd();
            if (last.Value.Length > 0)
            {
                break;
            }

            _paragraph.Inlines.RemoveAt(_paragraph.Inlines.Count - 1);
        }

        if (_paragraph.Inlines.Count == 0)
        {
            Container.Remove(_paragraph);
        }

        _paragraph = null;
        _sink = null;
    }
}
