// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Html;

/// <summary>Tag categories used by the parser and the serializer.</summary>
internal static class HtmlTags
{
    private static readonly HashSet<string> Void = new(StringComparer.Ordinal)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
    };

    // Content is raw text: no tags, no character references.
    private static readonly HashSet<string> RawText = new(StringComparer.Ordinal)
    {
        "script", "style", "xmp", "iframe", "noembed", "noframes",
    };

    // Content is text with character references but no tags.
    private static readonly HashSet<string> EscapableRawText = new(StringComparer.Ordinal) { "textarea", "title" };

    // A start tag of one of these closes an open paragraph.
    private static readonly HashSet<string> ClosesParagraph = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "center", "details", "dialog", "dir", "div", "dl", "fieldset",
        "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hgroup", "hr",
        "main", "menu", "nav", "ol", "p", "pre", "section", "summary", "table", "ul", "li", "dd", "dt", "listing",
    };

    // Elements that only belong in head (before any body content).
    private static readonly HashSet<string> HeadOnly = new(StringComparer.Ordinal)
    {
        "base", "link", "meta", "title", "style", "script", "noscript",
    };

    // An end tag never closes an element beyond one of these.
    private static readonly HashSet<string> ScopeBoundaries = new(StringComparer.Ordinal)
    {
        "html", "table", "td", "th", "caption", "template", "applet", "marquee", "object",
    };

    public static bool IsVoid(string tag) => Void.Contains(tag);

    public static bool IsRawText(string tag) => RawText.Contains(tag);

    public static bool IsEscapableRawText(string tag) => EscapableRawText.Contains(tag);

    public static bool ClosesP(string tag) => ClosesParagraph.Contains(tag);

    public static bool IsHeadOnly(string tag) => HeadOnly.Contains(tag);

    public static bool IsScopeBoundary(string tag) => ScopeBoundaries.Contains(tag);

    public static bool IsHeading(string tag) => tag.Length == 2 && tag[0] == 'h' && tag[1] is >= '1' and <= '6';

    public static bool IsTableSection(string tag) => tag is "thead" or "tbody" or "tfoot";

    public static bool IsCell(string tag) => tag is "td" or "th";
}
