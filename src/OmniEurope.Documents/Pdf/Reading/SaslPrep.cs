// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>
/// The SASLprep profile (RFC 4013) of stringprep (RFC 3454) that ISO 32000-2 §7.6.4.3.3 applies to AES-256
/// passwords before their UTF-8 bytes are hashed: non-ASCII spaces (table C.1.2) become spaces, the characters
/// commonly mapped to nothing (B.1) are removed, the result is normalized to NFKC, the prohibited characters of
/// tables C.1.2 and C.2.1 to C.9 are refused, and the bidirectional rule of RFC 3454 §6 is checked. Unassigned code
/// points are allowed (the stringprep query rule). Normalization uses the Unicode version of the runtime, not
/// Unicode 3.2; the bidirectional check uses table D.1 (RandALCat) and takes for LCat (D.2) every letter or spacing
/// mark outside D.1.
/// </summary>
internal static class SaslPrep
{
    // RFC 3454 table B.1, commonly mapped to nothing.
    private static readonly (int First, int Last)[] MappedToNothing =
    [
        (0x00AD, 0x00AD), (0x034F, 0x034F), (0x1806, 0x1806), (0x180B, 0x180D), (0x200B, 0x200D), (0x2060, 0x2060), (0xFE00, 0xFE0F), (0xFEFF, 0xFEFF),
    ];

    // RFC 3454 table C.1.2, non-ASCII space characters.
    private static readonly (int First, int Last)[] NonAsciiSpaces =
    [
        (0x00A0, 0x00A0), (0x1680, 0x1680), (0x2000, 0x200B), (0x202F, 0x202F), (0x205F, 0x205F), (0x3000, 0x3000),
    ];

    // RFC 3454 tables C.2.1 to C.9 (C.4 non-characters are tested apart: U+FDD0 to U+FDEF and the last two of each plane).
    private static readonly (int First, int Last)[] Prohibited =
    [
        (0x0000, 0x001F), (0x007F, 0x009F), (0x0340, 0x0341), (0x06DD, 0x06DD), (0x070F, 0x070F), (0x180E, 0x180E), (0x200C, 0x200F),
        (0x2028, 0x202E), (0x2060, 0x2063), (0x206A, 0x206F), (0x2FF0, 0x2FFB), (0xD800, 0xDFFF), (0xE000, 0xF8FF), (0xFDD0, 0xFDEF),
        (0xFEFF, 0xFEFF), (0xFFF9, 0xFFFD), (0x1D173, 0x1D17A), (0xE0001, 0xE0001), (0xE0020, 0xE007F), (0xF0000, 0xFFFFD),
        (0x100000, 0x10FFFD),
    ];

    // RFC 3454 table D.1, characters with bidirectional property R or AL.
    private static readonly (int First, int Last)[] RandALCat =
    [
        (0x05BE, 0x05BE), (0x05C0, 0x05C0), (0x05C3, 0x05C3), (0x05D0, 0x05EA), (0x05F0, 0x05F4), (0x061B, 0x061B), (0x061F, 0x061F),
        (0x0621, 0x063A), (0x0640, 0x064A), (0x066D, 0x066F), (0x0671, 0x06D5), (0x06DD, 0x06DD), (0x06E5, 0x06E6), (0x06FA, 0x06FE),
        (0x0700, 0x070D), (0x0710, 0x0710), (0x0712, 0x072C), (0x0780, 0x07A5), (0x07B1, 0x07B1), (0x200F, 0x200F), (0xFB1D, 0xFB1D),
        (0xFB1F, 0xFB28), (0xFB2A, 0xFB36), (0xFB38, 0xFB3C), (0xFB3E, 0xFB3E), (0xFB40, 0xFB41), (0xFB43, 0xFB44), (0xFB46, 0xFBB1),
        (0xFBD3, 0xFD3D), (0xFD50, 0xFD8F), (0xFD92, 0xFDC7), (0xFDF0, 0xFDFC), (0xFE70, 0xFE74), (0xFE76, 0xFEFC),
    ];

    /// <summary>The prepared string.</summary>
    /// <exception cref="ArgumentException">The string holds a prohibited character or breaks the bidirectional rule.</exception>
    public static string Prepare(string text)
    {
        var mapped = new StringBuilder(text.Length);
        foreach (var rune in Runes(text))
        {
            if (!In(rune, MappedToNothing))
            {
                mapped.Append(In(rune, NonAsciiSpaces) ? " " : char.ConvertFromUtf32(rune));
            }
        }

        var normalized = mapped.ToString().Normalize(NormalizationForm.FormKC);
        var runes = Runes(normalized).ToList();
        foreach (var rune in runes)
        {
            if (In(rune, NonAsciiSpaces) || In(rune, Prohibited) || IsNonCharacter(rune))
            {
                throw new ArgumentException($"The password holds the prohibited character U+{rune:X4}.");
            }
        }

        if (runes.Exists(r => In(r, RandALCat)) && (runes.Exists(IsLeftToRight) || !In(runes[0], RandALCat) || !In(runes[^1], RandALCat)))
        {
            throw new ArgumentException("The password mixes right-to-left and left-to-right text, or does not start and end with right-to-left text.");
        }

        return normalized;
    }

    /// <summary>The bytes ISO 32000-2 hashes: the prepared password in UTF-8, cut to its first 127 bytes.</summary>
    public static byte[] PasswordBytes(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(Prepare(password));
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }

    // Lone surrogates come out as their own code points, so the prohibition of table C.5 sees them.
    private static IEnumerable<int> Runes(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                yield return char.ConvertToUtf32(text[i], text[++i]);
            }
            else
            {
                yield return text[i];
            }
        }
    }

    private static bool In(int codePoint, (int First, int Last)[] ranges) => Array.Exists(ranges, r => codePoint >= r.First && codePoint <= r.Last);

    private static bool IsNonCharacter(int codePoint) => (codePoint & 0xFFFE) == 0xFFFE;

    private static bool IsLeftToRight(int codePoint)
    {
        if (In(codePoint, RandALCat) || codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
        {
            return false;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
        return category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.SpacingCombiningMark;
    }
}
