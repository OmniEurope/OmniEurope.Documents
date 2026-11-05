// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>
/// The class rules of a page's own <c>style</c> elements, so a page that carries its formatting in classes (as
/// Word to HTML writes it) converts like one with <c>style</c> attributes. Only rules whose selector is a single
/// class (<c>.name</c>), or a list of single classes, are read; any other selector, at-rules with their content,
/// comments, and style elements meant for another medium (<c>media</c> other than <c>all</c> or <c>screen</c>) or
/// another language (<c>type</c> other than <c>text/css</c>) are ignored, and linked style sheets are never fetched.
/// Each rule is parsed once and only the properties the conversion reads are kept, merged per class with the
/// position of the rule that set them: the sheet costs linear time to read and an element costs at most its
/// classes times those few properties, however hostile the sheet.
/// </summary>
internal sealed class CssClassRules
{
    /// <summary>No rules: the conversion of a tree without its page.</summary>
    public static readonly CssClassRules None = new([]);

    // The properties HTML to Word reads (CssStyle and HtmlWordWriter); any other declaration of a rule is dropped.
    private static readonly HashSet<string> ReadProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "background-color", "font-weight", "font-style", "text-decoration", "text-decoration-line", "font-size",
        "font-family", "text-align", "display", "width", "height",
    };

    // Per class, each read property with the value and the source position of the last rule that set it.
    private readonly Dictionary<string, Dictionary<string, (int Position, string Value)>> _byClass;

    private CssClassRules(List<(string Class, string Declarations)> rules)
    {
        _byClass = new(StringComparer.Ordinal);
        for (var i = 0; i < rules.Count; i++)
        {
            if (!_byClass.TryGetValue(rules[i].Class, out var properties))
            {
                properties = new(StringComparer.OrdinalIgnoreCase);
                _byClass.Add(rules[i].Class, properties);
            }

            foreach (var (name, value) in CssStyle.Parse(rules[i].Declarations))
            {
                if (ReadProperties.Contains(name))
                {
                    properties[name] = (i, value);
                }
            }
        }
    }

    /// <summary>The rules of every <c>style</c> element of the document meant for the screen, in source order.</summary>
    public static CssClassRules From(HtmlDocument document)
    {
        var rules = new List<(string, string)>();
        foreach (var style in document.QuerySelectorAll("style").Where(ForScreen))
        {
            Read(WithoutComments(style.TextContent), rules);
        }

        return new CssClassRules(rules);
    }

    /// <summary>The declarations of the element: those of its classes, the later rule winning for a property, then
    /// its <c>style</c> attribute, which wins over them.</summary>
    public Dictionary<string, string> Of(HtmlElement element)
    {
        var inline = CssStyle.Parse(element.GetAttribute("style"));
        if (_byClass.Count == 0)
        {
            return inline;
        }

        var merged = new Dictionary<string, (int Position, string Value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in element.ClassList.Distinct(StringComparer.Ordinal))
        {
            foreach (var (property, set) in _byClass.GetValueOrDefault(name) ?? [])
            {
                if (!merged.TryGetValue(property, out var current) || set.Position > current.Position)
                {
                    merged[property] = set;
                }
            }
        }

        var result = merged.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (property, value) in inline)
        {
            result[property] = value;
        }

        return result;
    }

    // A style element applies to the screen when it has no media or type restriction that excludes it.
    private static bool ForScreen(HtmlElement style)
    {
        var type = style.GetAttribute("type")?.Trim();
        if (type is { Length: > 0 } && !type.Equals("text/css", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var media = style.GetAttribute("media")?.Trim();
        return media is null or { Length: 0 } || media.Equals("all", StringComparison.OrdinalIgnoreCase) || media.Equals("screen", StringComparison.OrdinalIgnoreCase);
    }

    private static void Read(string css, List<(string, string)> rules)
    {
        var position = 0;
        while (position < css.Length)
        {
            var open = css.IndexOf('{', position);
            if (open < 0)
            {
                return;
            }

            var prelude = css[position..open].Trim();
            var semicolon = prelude.StartsWith('@') ? css.IndexOf(';', position, open - position) : -1;
            if (semicolon >= 0)
            {
                position = semicolon + 1; // a statement at-rule (@import, @charset)
                continue;
            }

            var close = BlockEnd(css, open);
            var body = css[(open + 1)..Math.Max(open + 1, close)];
            if (!prelude.StartsWith('@') && !body.Contains('{', StringComparison.Ordinal))
            {
                Add(prelude, body, rules);
            }

            position = close + 1;
        }
    }

    private static void Add(string prelude, string body, List<(string, string)> rules)
    {
        var selectors = prelude.Split(',', StringSplitOptions.TrimEntries);
        if (selectors.All(IsSingleClass))
        {
            rules.AddRange(selectors.Select(s => (s[1..], body)));
        }
    }

    // The index of the brace closing the block opened at 'open' (nested blocks counted), or the end of the text.
    private static int BlockEnd(string css, int open)
    {
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            depth += css[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return i;
            }
        }

        return css.Length;
    }

    private static bool IsSingleClass(string selector) =>
        selector.Length > 1 && selector[0] == '.' && (char.IsAsciiLetter(selector[1]) || selector[1] is '_' or '-')
        && selector.AsSpan(1).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-") < 0;

    private static string WithoutComments(string css)
    {
        var result = new System.Text.StringBuilder(css.Length);
        var position = 0;
        while (position < css.Length)
        {
            var start = css.IndexOf("/*", position, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(css, position, css.Length - position);
                break;
            }

            result.Append(css, position, start - position);
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            position = end < 0 ? css.Length : end + 2;
        }

        return result.ToString();
    }
}
