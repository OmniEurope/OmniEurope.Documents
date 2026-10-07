// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Text;

/// <summary>The outcome of <see cref="TextEncodingDetector.Detect"/>.</summary>
/// <param name="Encoding">The encoding to decode the text with.</param>
/// <param name="PreambleLength">Bytes of byte order mark to skip before decoding (0 when none).</param>
/// <param name="FromByteOrderMark">True when a byte order mark decided the encoding.</param>
public readonly record struct DetectedEncoding(Encoding Encoding, int PreambleLength, bool FromByteOrderMark);

/// <summary>
/// Picks the encoding of a text file from its first bytes: a byte order mark (UTF-8, UTF-16 LE/BE,
/// UTF-32 LE/BE) wins; otherwise the sample is UTF-8 when it is valid UTF-8, else the fallback
/// (Windows-1252 unless told otherwise). Only the sample is inspected: a file whose first non-ASCII
/// byte lies beyond it is reported as UTF-8.
/// </summary>
public static class TextEncodingDetector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Detects the encoding of <paramref name="sample"/>.</summary>
    /// <param name="sample">The first bytes of the file.</param>
    /// <param name="fallback">Encoding used when the sample is not valid UTF-8; Windows-1252 when null.</param>
    /// <param name="isComplete">True when the sample is the whole file, so a multi-byte sequence cut at its
    /// end is an error rather than a truncation.</param>
    public static DetectedEncoding Detect(ReadOnlySpan<byte> sample, Encoding? fallback = null, bool isComplete = false)
    {
        if (sample.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return new DetectedEncoding(Utf8NoBom, 3, true);
        }

        if (sample.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE, 0x00, 0x00]))
        {
            return new DetectedEncoding(new UTF32Encoding(bigEndian: false, byteOrderMark: false), 4, true);
        }

        if (sample.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0xFE, 0xFF]))
        {
            return new DetectedEncoding(new UTF32Encoding(bigEndian: true, byteOrderMark: false), 4, true);
        }

        if (sample.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return new DetectedEncoding(new UnicodeEncoding(bigEndian: false, byteOrderMark: false), 2, true);
        }

        if (sample.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return new DetectedEncoding(new UnicodeEncoding(bigEndian: true, byteOrderMark: false), 2, true);
        }

        var encoding = IsValidUtf8(sample, isComplete) ? Utf8NoBom : fallback ?? Windows1252Encoding.Instance;
        return new DetectedEncoding(encoding, 0, false);
    }

    /// <summary>
    /// True when <paramref name="bytes"/> is well-formed UTF-8 (no overlong form, no surrogate, nothing above
    /// U+10FFFF). A sequence cut by the end of the span is accepted unless <paramref name="isComplete"/>.
    /// </summary>
    public static bool IsValidUtf8(ReadOnlySpan<byte> bytes, bool isComplete = true)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            var b = bytes[i];
            if (b < 0x80)
            {
                i++;
                continue;
            }

            var (length, min) = b switch
            {
                >= 0xC2 and <= 0xDF => (2, 0x80),
                >= 0xE0 and <= 0xEF => (3, 0x800),
                >= 0xF0 and <= 0xF4 => (4, 0x10000),
                _ => (0, 0),
            };
            if (length == 0)
            {
                return false;
            }

            if (i + length > bytes.Length)
            {
                return !isComplete && TrailingBytesAreContinuations(bytes[(i + 1)..]);
            }

            var codePoint = b & (0xFF >> (length + 1));
            for (var k = 1; k < length; k++)
            {
                var c = bytes[i + k];
                if ((c & 0xC0) != 0x80)
                {
                    return false;
                }

                codePoint = (codePoint << 6) | (c & 0x3F);
            }

            if (codePoint < min || codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
            {
                return false;
            }

            i += length;
        }

        return true;
    }

    private static bool TrailingBytesAreContinuations(ReadOnlySpan<byte> tail)
    {
        foreach (var c in tail)
        {
            if ((c & 0xC0) != 0x80)
            {
                return false;
            }
        }

        return true;
    }
}
