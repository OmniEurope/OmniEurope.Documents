// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Text;

/// <summary>The bidirectional character types of the Unicode Bidirectional Algorithm (UAX #9, table 4).</summary>
internal enum BidiClass : byte
{
    L, R, AL, EN, ES, ET, AN, CS, NSM, BN, B, S, WS, ON, LRE, LRO, RLE, RLO, PDF, LRI, RLI, FSI, PDI,
}

/// <summary>
/// The <c>Bidi_Class</c> of a code point. The base class library publishes no bidirectional property, so the class
/// is derived: explicit formatting characters, separators, terminators, Arabic numbers and the other characters
/// whose class differs from what their general category suggests are listed one by one; letters take R in the
/// Hebrew and other right-to-left blocks, AL in the Arabic, Syriac and Thaana blocks, L elsewhere; non-spacing and
/// enclosing marks are NSM, other format characters BN, currency signs ET, other punctuation and symbols ON.
/// Unassigned code points take the default of their block (R, AL, ET in the currency block, else L). What this
/// leaves approximate: a letter, digit or punctuation mark whose class is not the one its block and category give
/// (rare outside the lists below), and the scripts added after the base class library's Unicode version.
/// </summary>
internal static class BidiClasses
{
    private static readonly Dictionary<int, BidiClass> Listed = Build();

    public static BidiClass Of(int codePoint)
    {
        if (Listed.TryGetValue(codePoint, out var listed))
        {
            return listed;
        }

        var block = Block(codePoint);
        return CharUnicodeInfo.GetUnicodeCategory(codePoint) switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark => BidiClass.NSM,
            UnicodeCategory.DecimalDigitNumber => block == BidiClass.R ? BidiClass.R : BidiClass.L,
            UnicodeCategory.OtherNumber => block ?? BidiClass.ON,
            UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator => BidiClass.WS,
            UnicodeCategory.ParagraphSeparator => BidiClass.B,
            UnicodeCategory.Control or UnicodeCategory.Format => BidiClass.BN,
            UnicodeCategory.CurrencySymbol => BidiClass.ET,
            UnicodeCategory.OtherNotAssigned => Unassigned(codePoint, block),
            var category when IsPunctuationOrSymbol(category) => block ?? BidiClass.ON,
            _ => block ?? BidiClass.L,
        };
    }

    /// <summary>True for the classes that make a run right to left (R and AL).</summary>
    public static bool IsRightToLeft(BidiClass type) => type is BidiClass.R or BidiClass.AL;

    private static bool IsPunctuationOrSymbol(UnicodeCategory category) => category is UnicodeCategory.ConnectorPunctuation
        or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
        or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation
        or UnicodeCategory.MathSymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;

    // Noncharacters are BN; other unassigned code points take their block's default.
    private static BidiClass Unassigned(int codePoint, BidiClass? block)
    {
        if ((codePoint & 0xFFFE) == 0xFFFE || codePoint is >= 0xFDD0 and <= 0xFDEF)
        {
            return BidiClass.BN;
        }

        return block ?? (codePoint is >= 0x20A0 and <= 0x20CF ? BidiClass.ET : BidiClass.L);
    }

    // The right-to-left blocks, first to last code point (the first range holding a code point wins): R for Hebrew, NKo,
    // Samaritan, Mandaic and the historic scripts, AL for the Arabic, Syriac and Thaana ones.
    private static readonly (int First, int Last, BidiClass Type)[] Blocks =
    [
        (0x0590, 0x05FF, BidiClass.R), (0x0600, 0x07BF, BidiClass.AL), (0x07C0, 0x085F, BidiClass.R), (0x0860, 0x08FF, BidiClass.AL),
        (0xFB1D, 0xFB4F, BidiClass.R), (0xFB50, 0xFDCF, BidiClass.AL), (0xFDF0, 0xFDFF, BidiClass.AL), (0xFE70, 0xFEFF, BidiClass.AL),
        (0x10D00, 0x10D3F, BidiClass.AL), (0x10EC0, 0x10EFF, BidiClass.AL), (0x10F30, 0x10F6F, BidiClass.AL), (0x10800, 0x10FFF, BidiClass.R),
        (0x1EC70, 0x1ECBF, BidiClass.AL), (0x1ED00, 0x1ED4F, BidiClass.AL), (0x1EE00, 0x1EEFF, BidiClass.AL), (0x1E800, 0x1EFFF, BidiClass.R),
    ];

    private static BidiClass? Block(int codePoint)
    {
        foreach (var (first, last, type) in Blocks)
        {
            if (codePoint >= first && codePoint <= last)
            {
                return type;
            }
        }

        return null;
    }


    private static Dictionary<int, BidiClass> Build()
    {
        var map = new Dictionary<int, BidiClass>();
        Add(map, BidiClass.B, 0x0A, 0x0D, 0x1C, 0x1D, 0x1E, 0x85, 0x2029);
        Add(map, BidiClass.S, 0x09, 0x0B, 0x1F);
        Add(map, BidiClass.WS, 0x0C, 0x20, 0x2028);
        Add(map, BidiClass.CS, 0x2C, 0x2E, 0x2F, 0x3A, 0xA0, 0x060C, 0x202F, 0x2044, 0xFE50, 0xFE52, 0xFE55, 0xFF0C, 0xFF0E, 0xFF0F, 0xFF1A);
        Add(map, BidiClass.ES, 0x2B, 0x2D, 0x207A, 0x207B, 0x208A, 0x208B, 0x2212, 0xFB29, 0xFE62, 0xFE63, 0xFF0B, 0xFF0D);
        Add(map, BidiClass.ET, 0x23, 0x24, 0x25, 0xB0, 0xB1, 0x0609, 0x060A, 0x066A, 0x09F2, 0x09F3, 0x0AF1, 0x0BF9, 0x0E3F, 0x17DB, 0x212E, 0x2213, 0xFE5F, 0xFE69, 0xFE6A, 0xFF03, 0xFF04, 0xFF05);
        AddRange(map, BidiClass.ET, 0x2030, 0x2034);
        Add(map, BidiClass.AN, 0x066B, 0x066C, 0x06DD, 0x0890, 0x0891, 0x08E2);
        AddRange(map, BidiClass.AN, 0x0600, 0x0605);
        AddRange(map, BidiClass.AN, 0x0660, 0x0669);
        AddRange(map, BidiClass.AN, 0x10D30, 0x10D39);
        AddRange(map, BidiClass.AN, 0x10E60, 0x10E7E);
        Add(map, BidiClass.EN, 0xB2, 0xB3, 0xB9, 0x2070);
        AddRange(map, BidiClass.EN, 0x30, 0x39);
        AddRange(map, BidiClass.EN, 0x06F0, 0x06F9);
        AddRange(map, BidiClass.EN, 0x2074, 0x2079);
        AddRange(map, BidiClass.EN, 0x2080, 0x2089);
        AddRange(map, BidiClass.EN, 0x2488, 0x249B);
        AddRange(map, BidiClass.EN, 0xFF10, 0xFF19);
        AddRange(map, BidiClass.EN, 0x1D7CE, 0x1D7FF);
        AddRange(map, BidiClass.EN, 0x1F100, 0x1F10A);
        Add(map, BidiClass.ON, 0x0606, 0x0607, 0x060E, 0x060F, 0x06DE, 0x06E9, 0x07F6, 0x07F7, 0x07F8, 0x07F9);
        Add(map, BidiClass.L, 0x200E);
        Add(map, BidiClass.R, 0x200F);
        Add(map, BidiClass.AL, 0x061C);
        Add(map, BidiClass.LRE, 0x202A);
        Add(map, BidiClass.RLE, 0x202B);
        Add(map, BidiClass.PDF, 0x202C);
        Add(map, BidiClass.LRO, 0x202D);
        Add(map, BidiClass.RLO, 0x202E);
        Add(map, BidiClass.LRI, 0x2066);
        Add(map, BidiClass.RLI, 0x2067);
        Add(map, BidiClass.FSI, 0x2068);
        Add(map, BidiClass.PDI, 0x2069);
        return map;
    }

    private static void Add(Dictionary<int, BidiClass> map, BidiClass type, params int[] codePoints)
    {
        foreach (var codePoint in codePoints)
        {
            map[codePoint] = type;
        }
    }

    private static void AddRange(Dictionary<int, BidiClass> map, BidiClass type, int first, int last)
    {
        for (var codePoint = first; codePoint <= last; codePoint++)
        {
            map[codePoint] = type;
        }
    }
}
