// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// The HTML being written. Text and attribute values always go through <see cref="Encode"/>; tag and
/// attribute names are constants of the converter, never document content.
/// </summary>
internal sealed class HtmlOutput
{
    private readonly StringBuilder _html = new();

    public void Raw(string markup) => _html.Append(markup);

    public void Line() => _html.Append('\n');

    public void Text(string text) => Encode(text, _html);

    /// <summary>An opening tag; attributes whose value is null are left out.</summary>
    public void Open(string tag, params ReadOnlySpan<(string Name, string? Value)> attributes)
    {
        _html.Append('<').Append(tag);
        foreach (var (name, value) in attributes)
        {
            if (value is not null)
            {
                _html.Append(' ').Append(name).Append("=\"");
                Encode(value, _html);
                _html.Append('"');
            }
        }

        _html.Append('>');
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
