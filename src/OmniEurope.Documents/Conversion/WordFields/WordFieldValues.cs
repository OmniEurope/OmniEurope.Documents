// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion.WordFields;

/// <summary>Formats field values: date and time pictures (<c>\@</c>) and number formats (<c>\*</c>) of ECMA-376 Part 1 §17.16.4.</summary>
internal static class WordFieldValues
{
    /// <summary>
    /// A date and time picture: <c>d dd ddd dddd</c> (day), <c>M MM MMM MMMM</c> (month), <c>yy yyyy</c> (year),
    /// <c>h hh</c> (12-hour), <c>H HH</c> (24-hour), <c>m mm</c> (minutes), <c>s ss</c> (seconds), <c>AM/PM</c> and
    /// <c>am/pm</c>, text between apostrophes kept as it is; any other character is written as it is.
    /// </summary>
    public static string Date(DateTimeOffset value, string picture, CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        var builder = new StringBuilder();
        for (var i = 0; i < picture.Length;)
        {
            var c = picture[i];
            if (c == '\'')
            {
                var close = picture.IndexOf('\'', i + 1);
                var end = close < 0 ? picture.Length : close;
                builder.Append(picture, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }

            if (Designator(picture, i) is { } designator)
            {
                var text = value.Hour < 12 ? format.AMDesignator : format.PMDesignator;
                builder.Append(char.IsLower(designator[0]) ? text.ToLower(culture) : text.ToUpper(culture));
                i += designator.Length;
                continue;
            }

            var count = 1;
            while (i + count < picture.Length && picture[i + count] == c)
            {
                count++;
            }

            builder.Append(Part(value, c, count, format) ?? new string(c, count));
            i += count;
        }

        return builder.ToString();
    }

    private static string? Designator(string picture, int index)
    {
        foreach (var designator in new[] { "AM/PM", "am/pm" })
        {
            if (string.CompareOrdinal(picture, index, designator, 0, designator.Length) == 0)
            {
                return designator;
            }
        }

        return null;
    }

    private static string? Part(DateTimeOffset value, char letter, int count, DateTimeFormatInfo format) => letter switch
    {
        'd' or 'D' => Named(count, value.Day, () => format.GetAbbreviatedDayName(value.DayOfWeek), () => format.GetDayName(value.DayOfWeek)),
        'M' => Named(count, value.Month, () => format.GetAbbreviatedMonthName(value.Month), () => format.GetMonthName(value.Month)),
        'y' or 'Y' => count <= 2 ? (value.Year % 100).ToString("00", CultureInfo.InvariantCulture) : value.Year.ToString("0000", CultureInfo.InvariantCulture),
        'h' => Digits(value.Hour % 12 == 0 ? 12 : value.Hour % 12, count),
        'H' => Digits(value.Hour, count),
        'm' => Digits(value.Minute, count),
        's' or 'S' => Digits(value.Second, count),
        _ => null,
    };

    // One or two letters give the number (two with a leading zero), three the abbreviated name, four the full name.
    private static string Named(int count, int number, Func<string> abbreviated, Func<string> full) =>
        count <= 2 ? Digits(number, count) : count == 3 ? abbreviated() : full();

    private static string Digits(int number, int count) => number.ToString(count >= 2 ? "00" : "0", CultureInfo.InvariantCulture);

    /// <summary>A number with the <c>\*</c> format of the instruction (<c>Arabic</c>, <c>roman</c>, <c>Roman</c>,
    /// <c>alphabetic</c>, <c>ALPHABETIC</c>); other formats leave the decimal number.</summary>
    public static string Number(int value, IReadOnlyList<string> tokens)
    {
        var format = WordXmlFields.Switch(tokens, "*");
        return format switch
        {
            "roman" => WordNumbering.FormatNumber(value, WordNumberFormat.LowerRoman),
            "Roman" or "ROMAN" => WordNumbering.FormatNumber(value, WordNumberFormat.UpperRoman),
            "alphabetic" => WordNumbering.FormatNumber(value, WordNumberFormat.LowerLetter),
            "ALPHABETIC" => WordNumbering.FormatNumber(value, WordNumberFormat.UpperLetter),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>A page label (already in the section's number format), with a <c>\*</c> format applied when the
    /// label is a decimal number.</summary>
    public static string Page(string label, IReadOnlyList<string> tokens) =>
        int.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? Number(number, tokens) : label;
}
