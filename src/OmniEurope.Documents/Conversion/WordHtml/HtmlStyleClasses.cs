// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// The formatting of single elements as classes of the style sheet, so a page needs no <c>style</c> attribute
/// (blocked by a Content-Security-Policy without <c>'unsafe-inline'</c>). Identical declarations share one class
/// (<c>omni-s0</c>, <c>omni-s1</c>... in order of first use). Declarations are built by the converter from checked
/// values; one that could end a rule or the style element is a converter fault and throws.
/// </summary>
internal sealed class HtmlStyleClasses
{
    /// <summary>The prefix of every generated class.</summary>
    public const string Prefix = "omni-s";

    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    /// <summary>The class holding these declarations.</summary>
    public string Of(string declarations)
    {
        if (declarations.AsSpan().IndexOfAny("{}<>\\") >= 0)
        {
            throw new InvalidOperationException("A generated declaration holds a character that cannot go into a style sheet.");
        }

        if (!_names.TryGetValue(declarations, out var name))
        {
            name = Prefix + _names.Count.ToString(CultureInfo.InvariantCulture);
            _names.Add(declarations, name);
        }

        return name;
    }

    /// <summary>One rule per class, in order of first use, each on its own line.</summary>
    public void Write(StringBuilder css)
    {
        foreach (var (declarations, name) in _names)
        {
            css.Append("\n.").Append(name).Append('{').Append(declarations).Append('}');
        }
    }
}
