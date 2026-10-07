// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Html;

/// <summary>An attribute: a lower-case name and its decoded value.</summary>
public sealed class HtmlAttribute(string name, string value)
{
    /// <summary>The lower-case name.</summary>
    public string Name { get; } = name;

    /// <summary>The decoded value.</summary>
    public string Value { get; set; } = value;
}

/// <summary>An element.</summary>
public sealed class HtmlElement : HtmlNode
{
    private readonly List<HtmlAttribute> _attributes = [];

    /// <summary>Creates a detached element.</summary>
    /// <param name="tagName">The tag name; stored in lower case.</param>
    public HtmlElement(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        TagName = tagName.ToLowerInvariant();
    }

    /// <summary>The lower-case tag name.</summary>
    public string TagName { get; }

    /// <summary>Same as <see cref="TagName"/>.</summary>
    public string LocalName => TagName;

    /// <summary>The attributes, in source order.</summary>
    public IReadOnlyList<HtmlAttribute> Attributes => _attributes;

    /// <summary>The <c>id</c> attribute, empty when absent.</summary>
    public string Id => GetAttribute("id") ?? string.Empty;

    /// <summary>The classes of the <c>class</c> attribute.</summary>
    public IReadOnlyList<string> ClassList =>
        (GetAttribute("class") ?? string.Empty).Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>True for an element that never has children (<c>br</c>, <c>img</c>...).</summary>
    public bool IsVoid => HtmlTags.IsVoid(TagName);

    /// <summary>The value of the attribute <paramref name="name"/> (case-insensitive), or null.</summary>
    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _attributes.Find(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    /// <summary>True when the attribute exists.</summary>
    public bool HasAttribute(string name) => GetAttribute(name) is not null;

    /// <summary>Sets an attribute, replacing its value when it exists.</summary>
    public void SetAttribute(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        var existing = _attributes.Find(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _attributes.Add(new HtmlAttribute(name.ToLowerInvariant(), value));
        }
        else
        {
            existing.Value = value;
        }
    }

    /// <summary>Removes an attribute; returns false when it did not exist.</summary>
    public bool RemoveAttribute(string name) =>
        _attributes.RemoveAll(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>True when this element matches the CSS selector.</summary>
    public bool Matches(string selector) => HtmlSelector.Parse(selector).Matches(this);

    /// <summary>The closest ancestor-or-self matching the CSS selector.</summary>
    public HtmlElement? Closest(string selector)
    {
        var parsed = HtmlSelector.Parse(selector);
        for (var element = this; element is not null; element = element.ParentElement)
        {
            if (parsed.Matches(element))
            {
                return element;
            }
        }

        return null;
    }

    internal void AddParsedAttribute(string name, string value)
    {
        if (!HasAttribute(name))
        {
            _attributes.Add(new HtmlAttribute(name, value));
        }
    }
}
