// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Conversion.HtmlLayout;

/// <summary>
/// The class rules of a page's own <c>style</c> elements, so a page that carries its formatting in classes (as
/// Word to HTML writes it) converts like one with <c>style</c> attributes. Only rules whose selector is a single
/// class (<c>.name</c>), or a list of single classes, are read; any other selector, at-rules with their content and
/// comments are ignored, and linked style sheets are never fetched. The sheet is read in one pass, without
/// regular expressions, so its size costs linear time.
/// </summary>
internal sealed class CssClassRules
{
    /// <summary>No rules: the conversion of a tree without its page.</summary>
    public static readonly CssClassRules None = new([]);

    private readonly List<(string Class, string Declarations)> _rules;
    private readonly Dictionary<string, List<int>> _byClass = new(StringComparer.Ordinal);

    private CssClassRules(List<(string Class, string Declarations)> rules)
    {
        _rules = rules;
        for (var i = 0; i < rules.Count; i++)
        {
            if (!_byClass.TryGetValue(rules[i].Class, out var indices))
            {
                indices = [];
                _byClass.Add(rules[i].Class, indices);
            }

            indices.Add(i);
        }
    }

    /// <summary>The rules of every <c>style</c> element of the document, in source order.</summary>
    public static CssClassRules From(HtmlDocument document)
    {
        var rules = new List<(string, string)>();
        foreach (var style in document.QuerySelectorAll("style"))
        {
            Read(WithoutComments(style.TextContent), rules);
        }

        return new CssClassRules(rules);
    }

    /// <summary>The declarations of the element: those of its classes in style sheet order, then its
    /// <c>style</c> attribute, so that a later one wins over an earlier one for the same property.</summary>
    public Dictionary<string, string> Of(HtmlElement element)
    {
        var inline = element.GetAttribute("style");
        if (_rules.Count == 0)
        {
            return CssStyle.Parse(inline);
        }

        var declarations = element.ClassList.SelectMany(c => _byClass.GetValueOrDefault(c) ?? []).Distinct().Order()
            .Select(i => _rules[i].Declarations).Append(inline ?? string.Empty);
        return CssStyle.Parse(string.Join(';', declarations));
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
