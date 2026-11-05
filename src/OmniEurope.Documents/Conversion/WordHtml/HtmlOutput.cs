// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// The HTML being written. Text and attribute values always go through <see cref="Encode"/>; tag and
/// attribute names are constants of the converter, never document content. A <c>style</c> attribute is never
/// written: its declarations become a class of <see cref="Classes"/>, added to the element's classes.
/// </summary>
internal sealed class HtmlOutput
{
    private readonly StringBuilder _html = new();

    /// <summary>The classes that replace the <c>style</c> attributes given to <see cref="Open"/>.</summary>
    public HtmlStyleClasses Classes { get; } = new();

    public void Raw(string markup) => _html.Append(markup);

    public void Line() => _html.Append('\n');

    public void Text(string text) => Encode(text, _html);

    /// <summary>An opening tag; attributes whose value is null are left out, a <c>style</c> attribute becomes a
    /// class (joined to the <c>class</c> attribute when there is one).</summary>
    public void Open(string tag, params ReadOnlySpan<(string Name, string? Value)> attributes)
    {
        string? generated = null;
        foreach (var (name, value) in attributes)
        {
            if (name == "style" && value is not null)
            {
                generated = Classes.Of(value);
            }
        }

        _html.Append('<').Append(tag);
        foreach (var (name, value) in attributes)
        {
            if (name == "class")
            {
                Attribute(name, value is null ? generated : generated is null ? value : value + " " + generated);
                generated = null;
            }
            else if (name != "style")
            {
                Attribute(name, value);
            }
        }

        Attribute("class", generated);
        _html.Append('>');
    }

    private void Attribute(string name, string? value)
    {
        if (value is not null)
        {
            _html.Append(' ').Append(name).Append("=\"");
            Encode(value, _html);
            _html.Append('"');
        }
    }

    public void Close(string tag) => _html.Append("</").Append(tag).Append('>');

    public override string ToString() => _html.ToString();

    /// <summary>
    /// Escapes the characters that end text or a double-quoted attribute value (<c>&amp; &lt; &gt; "</c>) and drops
    /// the characters HTML does not allow: controls other than tab, line feed and carriage return, unpaired
    /// surrogates and the non-characters U+FFFE and U+FFFF.
    /// </summary>
    public static void Encode(string text, StringBuilder output)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                output.Append(c).Append(text[++i]);
                continue;
            }

            _ = c switch
            {
                '&' => output.Append("&amp;"),
                '<' => output.Append("&lt;"),
                '>' => output.Append("&gt;"),
                '"' => output.Append("&quot;"),
                _ when IsAllowed(c) => output.Append(c),
                _ => output,
            };
        }
    }

    private static bool IsAllowed(char c) =>
        c is '\t' or '\n' or '\r' || (!char.IsControl(c) && !char.IsSurrogate(c) && c is not ('￾' or '￿'));
}
