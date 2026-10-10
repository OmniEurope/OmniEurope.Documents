// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Text;

/// <summary>
/// Paired brackets (<c>Bidi_Paired_Bracket</c>, used by rule N0) and mirrored glyphs (<c>Bidi_Mirroring_Glyph</c>, used
/// by rule L4) of UAX #9, written out from the Unicode character names: the brackets of Basic Latin, the Tibetan,
/// Ogham and general punctuation pairs, the mathematical and CJK brackets and their full-width and small forms. The
/// mirrored glyphs cover these brackets, the angle quotation marks and the mathematical relations that come in
/// pairs (less and greater, subset and superset, element of and contains); a mirrored character outside these
/// pairs is drawn unmirrored.
/// </summary>
internal static class BidiBrackets
{
    private static readonly int[] Pairs =
    [
        0x0028, 0x0029, 0x005B, 0x005D, 0x007B, 0x007D, 0x0F3A, 0x0F3B, 0x0F3C, 0x0F3D, 0x169B, 0x169C, 0x2045, 0x2046,
        0x207D, 0x207E, 0x208D, 0x208E, 0x2308, 0x2309, 0x230A, 0x230B, 0x2329, 0x232A, 0x2768, 0x2769, 0x276A, 0x276B,
        0x276C, 0x276D, 0x276E, 0x276F, 0x2770, 0x2771, 0x2772, 0x2773, 0x2774, 0x2775, 0x27C5, 0x27C6, 0x27E6, 0x27E7,
        0x27E8, 0x27E9, 0x27EA, 0x27EB, 0x27EC, 0x27ED, 0x27EE, 0x27EF, 0x2983, 0x2984, 0x2985, 0x2986, 0x2987, 0x2988,
        0x2989, 0x298A, 0x298B, 0x298C, 0x298D, 0x298E, 0x298F, 0x2990, 0x2991, 0x2992, 0x2993, 0x2994, 0x2995, 0x2996,
        0x2997, 0x2998, 0x29D8, 0x29D9, 0x29DA, 0x29DB, 0x29FC, 0x29FD, 0x2E22, 0x2E23, 0x2E24, 0x2E25, 0x2E26, 0x2E27,
        0x2E28, 0x2E29, 0x3008, 0x3009, 0x300A, 0x300B, 0x300C, 0x300D, 0x300E, 0x300F, 0x3010, 0x3011, 0x3014, 0x3015,
        0x3016, 0x3017, 0x3018, 0x3019, 0x301A, 0x301B, 0xFE59, 0xFE5A, 0xFE5B, 0xFE5C, 0xFE5D, 0xFE5E, 0xFF08, 0xFF09,
        0xFF3B, 0xFF3D, 0xFF5B, 0xFF5D, 0xFF5F, 0xFF60, 0xFF62, 0xFF63,
    ];

    private static readonly int[] MirroredOnly =
    [
        0x003C, 0x003E, 0x00AB, 0x00BB, 0x2039, 0x203A, 0x2208, 0x220B, 0x2209, 0x220C, 0x220A, 0x220D, 0x2264, 0x2265,
        0x2266, 0x2267, 0x226A, 0x226B, 0x226E, 0x226F, 0x2270, 0x2271, 0x2282, 0x2283, 0x2284, 0x2285, 0x2286, 0x2287,
        0x2288, 0x2289, 0x228A, 0x228B, 0x22B0, 0x22B1, 0x22D6, 0x22D7, 0xFE64, 0xFE65, 0xFF1C, 0xFF1E,
    ];

    private static readonly Dictionary<int, int> Openers = [];
    private static readonly Dictionary<int, int> Closers = [];
    private static readonly Dictionary<int, int> Mirrors = [];

    static BidiBrackets()
    {
        for (var i = 0; i < Pairs.Length; i += 2)
        {
            Openers[Pairs[i]] = Pairs[i + 1];
            Closers[Pairs[i + 1]] = Pairs[i];
            Mirrors[Pairs[i]] = Pairs[i + 1];
            Mirrors[Pairs[i + 1]] = Pairs[i];
        }

        for (var i = 0; i < MirroredOnly.Length; i += 2)
        {
            Mirrors[MirroredOnly[i]] = MirroredOnly[i + 1];
            Mirrors[MirroredOnly[i + 1]] = MirroredOnly[i];
        }
    }

    /// <summary>The closing bracket an opening bracket pairs with, or null when it opens nothing.</summary>
    public static int? ClosingOf(int codePoint) => Openers.TryGetValue(Canonical(codePoint), out var closing) ? closing : null;

    /// <summary>True for a closing bracket.</summary>
    public static bool IsClosing(int codePoint) => Closers.ContainsKey(Canonical(codePoint));

    /// <summary>The bracket a closing bracket is compared with, canonical equivalents made one (BD16).</summary>
    public static int Canonical(int codePoint) => codePoint switch
    {
        0x2329 => 0x3008,
        0x232A => 0x3009,
        _ => codePoint,
    };

    /// <summary>The mirrored glyph of a character drawn right to left, or the character itself.</summary>
    public static int Mirror(int codePoint) => Mirrors.TryGetValue(codePoint, out var mirror) ? mirror : codePoint;
}
