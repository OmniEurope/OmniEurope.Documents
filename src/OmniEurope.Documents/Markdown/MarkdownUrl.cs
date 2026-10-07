// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Markdown;

/// <summary>URL helpers of the Markdown renderer.</summary>
public static class MarkdownUrl
{
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];
    private static readonly string[] SafeImageData = ["data:image/png", "data:image/gif", "data:image/jpeg", "data:image/webp"];

    /// <summary>
    /// The default URL filter: relative URLs and fragments, <c>http</c>, <c>https</c> and <c>mailto</c>; for
    /// images also base64 PNG, GIF, JPEG and WebP data URLs. Everything else (<c>javascript:</c>,
    /// <c>vbscript:</c>, <c>file:</c>, other <c>data:</c>) is refused. Tabs, line breaks and control
    /// characters are ignored when reading the scheme, as browsers do.
    /// </summary>
    public static bool IsSafe(string url, bool isImage)
    {
        ArgumentNullException.ThrowIfNull(url);
        var compact = Compact(url);
        var scheme = SchemeOf(compact);
        if (scheme is null)
        {
            return true;
        }

        if (Array.Exists(SafeSchemes, s => s.Equals(scheme, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return isImage && Array.Exists(SafeImageData, p => compact.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The scheme of <paramref name="url"/> (without the colon), or null for a relative URL.</summary>
    public static string? SchemeOf(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var colon = url.IndexOf(':');
        if (colon < 1)
        {
            return null;
        }

        var end = url.AsSpan(0, colon).IndexOfAny("/?#");
        if (end >= 0)
        {
            return null;
        }

        var scheme = url[..colon];
        return char.IsAsciiLetter(scheme[0]) && scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.')
            ? scheme
            : null;
    }

    /// <summary>
    /// Percent-encodes what may not appear in an <c>href</c>: spaces, controls, non-ASCII characters (as
    /// UTF-8) and a few unsafe ASCII characters. Existing <c>%XX</c> escapes are kept.
    /// </summary>
    public static string Normalize(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var builder = new StringBuilder(url.Length + 8);
        Span<byte> utf8 = stackalloc byte[4];
        for (var i = 0; i < url.Length; i++)
        {
            var c = url[i];
            if (c == '%' && i + 2 < url.Length && char.IsAsciiHexDigit(url[i + 1]) && char.IsAsciiHexDigit(url[i + 2]))
            {
                builder.Append(c);
                continue;
            }

            if (IsKept(c))
            {
                builder.Append(c);
                continue;
            }

            var length = char.IsHighSurrogate(c) && i + 1 < url.Length && char.IsLowSurrogate(url[i + 1])
                ? Encoding.UTF8.GetBytes(url.AsSpan(i++, 2), utf8)
                : Encoding.UTF8.GetBytes(url.AsSpan(i, 1), utf8);
            foreach (var b in utf8[..length])
            {
                builder.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static bool IsKept(char c) =>
        char.IsAsciiLetterOrDigit(c) || "-._~:/?#@!$&'()*+,;=".Contains(c);

    private static string Compact(string url)
    {
        var builder = new StringBuilder(url.Length);
        foreach (var c in url)
        {
            if (!char.IsControl(c) && c != ' ')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
