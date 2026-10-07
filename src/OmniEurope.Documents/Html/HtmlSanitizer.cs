// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Html;

/// <summary>Allow-lists of <see cref="HtmlSanitizer"/>. Names are compared case-insensitively.</summary>
public sealed class HtmlSanitizerOptions
{
    /// <summary>A conservative policy for rich text: formatting, lists, tables, links and images; no
    /// <c>id</c>, no <c>style</c>, no form or embedding element.</summary>
    public static HtmlSanitizerOptions Default { get; } = new();

    /// <summary>Elements kept. Anything else is removed.</summary>
    public IReadOnlySet<string> AllowedTags { get; init; } = Set(
        "a", "abbr", "b", "blockquote", "br", "caption", "cite", "code", "col", "colgroup", "dd", "del", "div", "dl",
        "dt", "em", "figcaption", "figure", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img", "ins", "kbd", "li",
        "mark", "ol", "p", "pre", "q", "s", "small", "span", "strike", "strong", "sub", "sup", "table", "tbody", "td",
        "tfoot", "th", "thead", "tr", "u", "ul");

    /// <summary>Attributes kept on allowed elements. An entry ending in <c>*</c> is a prefix (<c>data-*</c>).
    /// Event handlers (<c>on...</c>) are always removed.</summary>
    public IReadOnlySet<string> AllowedAttributes { get; init; } = Set(
        "href", "src", "alt", "title", "class", "width", "height", "colspan", "rowspan", "start", "lang", "dir");

    /// <summary>URL schemes allowed in URL attributes; relative URLs are always allowed.</summary>
    public IReadOnlySet<string> AllowedSchemes { get; init; } = Set("http", "https", "mailto");

    /// <summary>Attributes holding a URL, checked against <see cref="AllowedSchemes"/>.</summary>
    public IReadOnlySet<string> UrlAttributes { get; init; } = Set(
        "href", "src", "action", "formaction", "cite", "background", "poster", "longdesc", "xlink:href", "data");

    /// <summary>True to keep the children of a removed element (its text survives). Default false: the
    /// element goes with its content. Script-like elements always go with their content.</summary>
    public bool KeepChildrenOfRemovedElements { get; init; }

    /// <summary>Called for a URL whose scheme is not allowed, with the element, the attribute name and the
    /// URL; return true to keep it (for instance <c>data:image/png</c> on <c>img</c> only).</summary>
    public Func<HtmlElement, string, string, bool>? AllowUrl { get; init; }

    /// <summary>Builds a case-insensitive set.</summary>
    public static IReadOnlySet<string> Set(params IEnumerable<string> names) => new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Removes everything a policy does not allow from untrusted HTML: elements, attributes, event handlers,
/// inline CSS, comments and URLs with a forbidden scheme (<c>javascript:</c> even when written with
/// entities, tabs or line breaks). The result is serialised again, so malformed markup comes out well formed.
/// </summary>
public sealed class HtmlSanitizer(HtmlSanitizerOptions? options = null)
{
    private static readonly HashSet<string> AlwaysDropContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "template", "iframe", "object", "embed", "noscript", "noembed", "noframes", "frameset",
        "frame", "applet", "svg", "math", "textarea", "select", "xmp", "title", "head",
    };

    private readonly HtmlSanitizerOptions _options = options ?? HtmlSanitizerOptions.Default;

    /// <summary>Sanitises an HTML fragment and returns the cleaned HTML.</summary>
    public string Sanitize(string html)
    {
        var fragment = HtmlParser.ParseFragment(html);
        SanitizeChildren(fragment);
        return fragment.InnerHtml;
    }

    /// <summary>Sanitises the descendants of <paramref name="root"/> in place.</summary>
    public void SanitizeChildren(HtmlNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var node in root.ChildNodes.ToList())
        {
            SanitizeNode(node);
        }
    }

    private void SanitizeNode(HtmlNode node)
    {
        switch (node)
        {
            case HtmlComment:
                node.Remove();
                return;
            case HtmlElement element when !_options.AllowedTags.Contains(element.TagName):
                RemoveElement(element);
                return;
            case HtmlElement element:
                CleanAttributes(element);
                SanitizeChildren(element);
                return;
        }
    }

    private void RemoveElement(HtmlElement element)
    {
        if (!_options.KeepChildrenOfRemovedElements || AlwaysDropContent.Contains(element.TagName))
        {
            element.Remove();
            return;
        }

        var children = element.ChildNodes.ToList();
        element.ReplaceWith(children);
        foreach (var child in children)
        {
            SanitizeNode(child);
        }
    }

    private void CleanAttributes(HtmlElement element)
    {
        foreach (var attribute in element.Attributes.ToList())
        {
            if (!IsAllowedAttribute(attribute.Name)
                || (_options.UrlAttributes.Contains(attribute.Name) && !IsAllowedUrl(element, attribute.Name, attribute.Value)))
            {
                element.RemoveAttribute(attribute.Name);
            }
        }
    }

    private bool IsAllowedAttribute(string name)
    {
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            || (name.Equals("style", StringComparison.OrdinalIgnoreCase) && !_options.AllowedAttributes.Contains("style")))
        {
            return false;
        }

        return _options.AllowedAttributes.Contains(name)
            || _options.AllowedAttributes.Any(a => a.EndsWith('*') && name.StartsWith(a[..^1], StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAllowedUrl(HtmlElement element, string attribute, string value)
    {
        var compact = Compact(value);
        var scheme = SchemeOf(compact);
        if (scheme is null || _options.AllowedSchemes.Contains(scheme))
        {
            return true;
        }

        return _options.AllowUrl?.Invoke(element, attribute, compact) == true;
    }

    private static string Compact(string url)
    {
        var builder = new StringBuilder(url.Length);
        foreach (var c in url)
        {
            if (!char.IsControl(c) && !char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string? SchemeOf(string url)
    {
        var colon = url.IndexOf(':');
        if (colon < 1 || url.AsSpan(0, colon).IndexOfAny("/?#") >= 0)
        {
            return null;
        }

        // A colon after a character that cannot be in a scheme still makes browsers fail closed: refuse it.
        var scheme = url[..colon];
        return scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.') ? scheme.ToLowerInvariant() : scheme;
    }
}
