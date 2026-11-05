// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;

namespace OmniEurope.Documents.Conversion.WordHtml;

/// <summary>
/// The style sheet of a converted page in its head: embedded in a <c>style</c> element, or linked with a
/// <c>link</c> element when the consumer serves it as a file; either one carries the nonce when there is one.
/// </summary>
internal static class HtmlPageStyle
{
    // The base64-value of a CSP nonce source: letters, digits, + / - _, then at most two padding signs.
    private static readonly SearchValues<char> NonceCharacters = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/-_");

    /// <summary>The nonce when it is a CSP <c>base64-value</c>; throws otherwise, so it cannot add markup.</summary>
    public static string? CheckNonce(string? nonce)
    {
        if (nonce is null)
        {
            return null;
        }

        var value = nonce.AsSpan().TrimEnd('=');
        if (value.Length == 0 || nonce.Length - value.Length > 2 || value.ContainsAnyExcept(NonceCharacters))
        {
            throw new ArgumentException("A style sheet nonce holds only ASCII letters, digits, '+', '/', '-' and '_', followed by at most two '='.", nameof(nonce));
        }

        return nonce;
    }

    /// <summary>The <c>link</c> element when the sheet has an address, else the <c>style</c> element holding it.</summary>
    public static void Write(HtmlOutput head, string css, string? href, string? nonce)
    {
        if (href is not null)
        {
            head.Open("link", ("rel", "stylesheet"), ("href", href), ("nonce", nonce));
            return;
        }

        head.Open("style", ("nonce", nonce));
        head.Raw(css);
        head.Close("style");
    }
}
