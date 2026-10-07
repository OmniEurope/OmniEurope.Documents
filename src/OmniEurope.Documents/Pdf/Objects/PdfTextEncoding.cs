// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Pdf.Objects;

/// <summary>Decoding of PDF text strings: UTF-16BE or UTF-8 with a byte order mark, else PDFDocEncoding.</summary>
internal static class PdfTextEncoding
{
    // PDFDocEncoding differs from Latin-1 at 0x18-0x1F and 0x80-0xA0.
    private static readonly Dictionary<byte, char> Differences = new()
    {
        [0x18] = '˘', [0x19] = 'ˇ', [0x1A] = 'ˆ', [0x1B] = '˙', [0x1C] = '˝', [0x1D] = '˛',
        [0x1E] = '˚', [0x1F] = '˜', [0x80] = '•', [0x81] = '†', [0x82] = '‡', [0x83] = '…',
        [0x84] = '—', [0x85] = '–', [0x86] = 'ƒ', [0x87] = '⁄', [0x88] = '‹', [0x89] = '›',
        [0x8A] = '−', [0x8B] = '‰', [0x8C] = '„', [0x8D] = '“', [0x8E] = '”', [0x8F] = '‘',
        [0x90] = '’', [0x91] = '‚', [0x92] = '™', [0x93] = 'ﬁ', [0x94] = 'ﬂ', [0x95] = 'Ł',
        [0x96] = 'Œ', [0x97] = 'Š', [0x98] = 'Ÿ', [0x99] = 'Ž', [0x9A] = 'ı', [0x9B] = 'ł',
        [0x9C] = 'œ', [0x9D] = 'š', [0x9E] = 'ž', [0xA0] = '€',
    };

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..(bytes.Length & ~1)]);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        var builder = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            builder.Append(Differences.TryGetValue(b, out var c) ? c : (char)b);
        }

        return builder.ToString();
    }
}
