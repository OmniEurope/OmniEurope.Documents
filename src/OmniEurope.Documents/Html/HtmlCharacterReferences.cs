// SPDX-License-Identifier: EUPL-1.2
using System.Net;
using System.Text;

namespace OmniEurope.Documents.Html;

/// <summary>
/// Decodes character references as the HTML standard reads them: besides the full <c>&amp;name;</c> and
/// numeric forms, the legacy Latin-1 names (<c>&amp;amp</c>, <c>&amp;nbsp</c>, <c>&amp;eacute</c>…) and numeric
/// references are also read without their semicolon, and the upper-case legacy spellings (<c>&amp;AMP;</c>,
/// <c>&amp;COPY</c>) as their lower-case names. In an attribute value, a legacy name without its semicolon that
/// is followed by a letter, a digit or <c>=</c> stays as written (so a query string keeps <c>&amp;copy=1</c>).
/// </summary>
internal static class HtmlCharacterReferences
{
    private static readonly string[] Legacy = [.. """
        AElig AMP Aacute Acirc Agrave Aring Atilde Auml COPY Ccedil ETH Eacute Ecirc Egrave Euml GT Iacute Icirc
        Igrave Iuml LT Ntilde Oacute Ocirc Ograve Oslash Otilde Ouml QUOT REG THORN Uacute Ucirc Ugrave Uuml Yacute
        aacute acirc acute aelig agrave amp aring atilde auml brvbar ccedil cedil cent copy curren deg divide eacute
        ecirc egrave eth euml frac12 frac14 frac34 gt iacute icirc iexcl igrave iquest iuml laquo lt macr micro
        middot nbsp not ntilde oacute ocirc ograve ordf ordm oslash otilde ouml para plusmn pound quot raquo reg sect
        shy sup1 sup2 sup3 szlig thorn times uacute ucirc ugrave uml uuml yacute yen yuml
        """.Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).OrderByDescending(n => n.Length)];

    private static readonly Dictionary<string, string> UpperCase = new(StringComparer.Ordinal)
    {
        ["AMP"] = "amp", ["COPY"] = "copy", ["GT"] = "gt", ["LT"] = "lt", ["QUOT"] = "quot", ["REG"] = "reg",
    };

    public static string Decode(string text, bool attribute)
    {
        if (!text.Contains('&'))
        {
            return text;
        }

        StringBuilder? prepared = null;
        var copied = 0;
        for (var i = text.IndexOf('&'); i >= 0; i = i + 1 < text.Length ? text.IndexOf('&', i + 1) : -1)
        {
            var (replacement, end) = i + 1 < text.Length && text[i + 1] == '#' ? Numeric(text, i) : Named(text, i, attribute);
            if (replacement is null)
            {
                continue;
            }

            prepared ??= new StringBuilder(text.Length + 8);
            prepared.Append(text, copied, i - copied).Append(replacement);
            copied = end;
        }

        return WebUtility.HtmlDecode(prepared is null ? text : prepared.Append(text, copied, text.Length - copied).ToString());
    }

    // "&#38" or "&#x26" without its semicolon gets one; anything else is left to the decoder.
    private static (string? Replacement, int End) Numeric(string text, int i)
    {
        var hex = i + 2 < text.Length && text[i + 2] is 'x' or 'X';
        var start = i + (hex ? 3 : 2);
        var end = start;
        while (end < text.Length && (hex ? char.IsAsciiHexDigit(text[end]) : char.IsAsciiDigit(text[end])))
        {
            end++;
        }

        return end > start && (end == text.Length || text[end] != ';') ? (text[i..end] + ";", end) : (null, 0);
    }

    private static (string? Replacement, int End) Named(string text, int i, bool attribute)
    {
        var runEnd = i + 1;
        while (runEnd < text.Length && char.IsAsciiLetterOrDigit(text[runEnd]))
        {
            runEnd++;
        }

        if (runEnd == i + 1 || runEnd < text.Length && text[runEnd] == ';' && Known(text[i..(runEnd + 1)]))
        {
            return (null, 0);
        }

        var name = Array.Find(Legacy, n => n.Length <= runEnd - i - 1 && string.CompareOrdinal(text, i + 1, n, 0, n.Length) == 0);
        if (name is null)
        {
            return (null, 0);
        }

        var after = i + 1 + name.Length;
        var semicolon = after < text.Length && text[after] == ';';
        if (!semicolon && attribute && after < text.Length && (char.IsAsciiLetterOrDigit(text[after]) || text[after] == '='))
        {
            return (null, 0);
        }

        return ($"&{UpperCase.GetValueOrDefault(name, name)};", semicolon ? after + 1 : after);
    }

    private static bool Known(string reference) => WebUtility.HtmlDecode(reference) != reference;
}
