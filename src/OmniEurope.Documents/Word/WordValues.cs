// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word;

/// <summary>Correspondence between WordprocessingML attribute values and the model's enumerations.</summary>
internal static class WordValues
{
    public static readonly IReadOnlyDictionary<string, WordAlignment> Alignments = new Dictionary<string, WordAlignment>(StringComparer.Ordinal)
    {
        ["left"] = WordAlignment.Left,
        ["start"] = WordAlignment.Left,
        ["center"] = WordAlignment.Center,
        ["right"] = WordAlignment.Right,
        ["end"] = WordAlignment.Right,
        ["both"] = WordAlignment.Justify,
        ["justify"] = WordAlignment.Justify,
        ["lowKashida"] = WordAlignment.Justify,
        ["mediumKashida"] = WordAlignment.Justify,
        ["highKashida"] = WordAlignment.Justify,
        ["thaiDistribute"] = WordAlignment.Justify,
        ["distribute"] = WordAlignment.Distribute,
    };

    public static readonly IReadOnlyDictionary<string, WordUnderline> Underlines = new Dictionary<string, WordUnderline>(StringComparer.Ordinal)
    {
        ["none"] = WordUnderline.None,
        ["single"] = WordUnderline.Single,
        ["words"] = WordUnderline.Words,
        ["double"] = WordUnderline.Double,
        ["thick"] = WordUnderline.Thick,
        ["dotted"] = WordUnderline.Dotted,
        ["dottedHeavy"] = WordUnderline.Dotted,
        ["dash"] = WordUnderline.Dash,
        ["dashedHeavy"] = WordUnderline.Dash,
        ["dashLong"] = WordUnderline.Dash,
        ["dashLongHeavy"] = WordUnderline.Dash,
        ["dotDash"] = WordUnderline.Dash,
        ["dashDotHeavy"] = WordUnderline.Dash,
        ["dotDotDash"] = WordUnderline.Dash,
        ["dashDotDotHeavy"] = WordUnderline.Dash,
        ["wave"] = WordUnderline.Wave,
        ["wavyHeavy"] = WordUnderline.Wave,
        ["wavyDouble"] = WordUnderline.Wave,
    };

    public static readonly IReadOnlyDictionary<string, WordTabAlignment> TabAlignments = new Dictionary<string, WordTabAlignment>(StringComparer.Ordinal)
    {
        ["left"] = WordTabAlignment.Left,
        ["start"] = WordTabAlignment.Left,
        ["center"] = WordTabAlignment.Center,
        ["right"] = WordTabAlignment.Right,
        ["end"] = WordTabAlignment.Right,
        ["decimal"] = WordTabAlignment.Decimal,
        ["bar"] = WordTabAlignment.Bar,
        ["clear"] = WordTabAlignment.Clear,
    };

    public static readonly IReadOnlyDictionary<string, WordTabLeader> TabLeaders = new Dictionary<string, WordTabLeader>(StringComparer.Ordinal)
    {
        ["none"] = WordTabLeader.None,
        ["dot"] = WordTabLeader.Dot,
        ["hyphen"] = WordTabLeader.Hyphen,
        ["underscore"] = WordTabLeader.Underscore,
        ["middleDot"] = WordTabLeader.MiddleDot,
        ["heavy"] = WordTabLeader.Heavy,
    };

    public static readonly IReadOnlyList<(WordTableRegion Region, string Name)> Regions =
    [
        (WordTableRegion.WholeTable, "wholeTable"),
        (WordTableRegion.FirstRow, "firstRow"),
        (WordTableRegion.LastRow, "lastRow"),
        (WordTableRegion.FirstColumn, "firstCol"),
        (WordTableRegion.LastColumn, "lastCol"),
        (WordTableRegion.OddRows, "band1Horz"),
        (WordTableRegion.EvenRows, "band2Horz"),
        (WordTableRegion.OddColumns, "band1Vert"),
        (WordTableRegion.EvenColumns, "band2Vert"),
        (WordTableRegion.TopLeftCell, "nwCell"),
        (WordTableRegion.TopRightCell, "neCell"),
        (WordTableRegion.BottomLeftCell, "swCell"),
        (WordTableRegion.BottomRightCell, "seCell"),
    ];

    public static readonly IReadOnlyList<(WordNumberFormat Format, string Name)> NumberFormats =
    [
        (WordNumberFormat.Decimal, "decimal"),
        (WordNumberFormat.DecimalZero, "decimalZero"),
        (WordNumberFormat.LowerLetter, "lowerLetter"),
        (WordNumberFormat.UpperLetter, "upperLetter"),
        (WordNumberFormat.LowerRoman, "lowerRoman"),
        (WordNumberFormat.UpperRoman, "upperRoman"),
        (WordNumberFormat.Ordinal, "ordinal"),
        (WordNumberFormat.Bullet, "bullet"),
        (WordNumberFormat.None, "none"),
        (WordNumberFormat.Chicago, "chicago"),
    ];

    /// <summary>Bits of the legacy hexadecimal <c>w:tblLook/@w:val</c> and the matching attributes.</summary>
    public static readonly IReadOnlyList<(WordTableLook Flag, string Attribute, int Mask)> LookBits =
    [
        (WordTableLook.FirstRow, "firstRow", 0x20),
        (WordTableLook.LastRow, "lastRow", 0x40),
        (WordTableLook.FirstColumn, "firstColumn", 0x80),
        (WordTableLook.LastColumn, "lastColumn", 0x100),
        (WordTableLook.NoHorizontalBanding, "noHBand", 0x200),
        (WordTableLook.NoVerticalBanding, "noVBand", 0x400),
    ];

    public static T? Find<T>(IReadOnlyDictionary<string, T> map, string? key)
        where T : struct => key is not null && map.TryGetValue(key, out var value) ? value : null;

    public static TValue? Find<TValue>(IReadOnlyList<(TValue Value, string Name)> pairs, string? name)
        where TValue : struct
    {
        foreach (var (value, candidate) in pairs)
        {
            if (candidate == name)
            {
                return value;
            }
        }

        return null;
    }

    public static string Name<TValue>(IReadOnlyList<(TValue Value, string Name)> pairs, TValue value)
        where TValue : struct
    {
        foreach (var (candidate, name) in pairs)
        {
            if (EqualityComparer<TValue>.Default.Equals(candidate, value))
            {
                return name;
            }
        }

        return pairs[0].Name;
    }
}
