// SPDX-License-Identifier: EUPL-1.2
using System.Text.RegularExpressions;
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// Reads the formatting of single elements back from a converted page: an element has no <c>style</c> attribute,
/// its declarations are the rule of its generated class (<c>omni-sN</c>) in the page's style sheet.
/// </summary>
internal static partial class HtmlStyleRules
{
    /// <summary>The declarations of the element's generated class; null when it has none. Fails when the element
    /// still carries a <c>style</c> attribute.</summary>
    public static string? Of(HtmlElement element)
    {
        Assert.Null(element.GetAttribute("style"));
        var generated = element.ClassList.Where(c => GeneratedClass().IsMatch(c)).ToList();
        Assert.True(generated.Count <= 1, "an element has at most one generated class");
        return generated.Count == 0 ? null : Rules(SheetOf(element))[generated[0]];
    }

    /// <summary>The element's classes other than its generated one, space-separated.</summary>
    public static string Named(HtmlElement element) => string.Join(' ', element.ClassList.Where(c => !GeneratedClass().IsMatch(c)));

    /// <summary>The generated class whose rule holds exactly these declarations, in the sheet of the page.</summary>
    public static string ClassOf(string html, string declarations) =>
        Assert.Single(Rules(Sheet(HtmlParser.ParseDocument(html))), r => r.Value == declarations).Key;

    /// <summary>Every generated class of a style sheet and its declarations.</summary>
    public static Dictionary<string, string> Rules(string css) =>
        RuleText().Matches(css).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);

    private static string SheetOf(HtmlNode node)
    {
        while (node.Parent is { } parent)
        {
            node = parent;
        }

        return Sheet((HtmlDocument)node);
    }

    private static string Sheet(HtmlDocument page) => page.Head.QuerySelector("style")!.TextContent;

    [GeneratedRegex(@"^omni-s\d+$")]
    private static partial Regex GeneratedClass();

    [GeneratedRegex(@"\.(omni-s\d+)\{([^}]*)\}")]
    private static partial Regex RuleText();
}
