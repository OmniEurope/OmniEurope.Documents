// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>Date and time sections of a number format.</summary>
internal static class DateFormatter
{
    public static string Format(double serial, List<FormatToken> tokens, CultureInfo culture, bool date1904)
    {
        var fractionDigits = tokens.Where(t => t.Kind == FormatTokenKind.FractionalSeconds).Select(t => t.Text.Length).DefaultIfEmpty(0).Max();
        var rounding = Math.Pow(10, fractionDigits) * 86400;
        serial = Math.Round(serial * rounding, MidpointRounding.AwayFromZero) / rounding;

        // Excel shows '#' for a number that is no date of its date system, checked once rounded.
        if (!ExcelDate.IsInRange(serial, date1904))
        {
            return new string('#', 8);
        }

        var date = ExcelDate.FromSerial(serial, date1904);
        var twelveHour = tokens.Exists(t => t.Kind == FormatTokenKind.AmPm);
        var minutes = MinuteTokens(tokens);
        var output = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            output.Append(Render(tokens[i], minutes.Contains(i), date, serial, twelveHour, culture));
        }

        return output.ToString();
    }

    // "m" means minutes right after an hour or right before seconds; months otherwise.
    private static HashSet<int> MinuteTokens(List<FormatToken> tokens)
    {
        var minutes = new HashSet<int>();
        var dateParts = tokens.Select((t, i) => (Token: t, Index: i))
            .Where(x => x.Token.Kind is FormatTokenKind.DatePart or FormatTokenKind.Elapsed)
            .ToList();
        for (var k = 0; k < dateParts.Count; k++)
        {
            var (token, index) = dateParts[k];
            if (token.Kind != FormatTokenKind.DatePart || token.Text[0] != 'm' || token.Text.Length > 2)
            {
                continue;
            }

            var afterHour = k > 0 && dateParts[k - 1].Token.Text[0] == 'h';
            var beforeSeconds = k + 1 < dateParts.Count && dateParts[k + 1].Token.Text[0] == 's';
            if (afterHour || beforeSeconds)
            {
                minutes.Add(index);
            }
        }

        return minutes;
    }

    private static string Render(FormatToken token, bool isMinute, DateTime date, double serial, bool twelveHour, CultureInfo culture)
    {
        var names = culture.DateTimeFormat;
        return token.Kind switch
        {
            FormatTokenKind.DatePart => DatePart(token.Text, isMinute, date, twelveHour, names),
            FormatTokenKind.AmPm => AmPm(token.Text, date),
            FormatTokenKind.Elapsed => Elapsed(token.Text, serial),
            FormatTokenKind.FractionalSeconds => "." + FractionOfSecond(date, token.Text.Length),
            FormatTokenKind.Literal => token.Text,
            FormatTokenKind.Digit => token.Text == "0" ? "0" : string.Empty,
            FormatTokenKind.Point => ".",
            FormatTokenKind.Comma => ",",
            FormatTokenKind.Percent => "%",
            _ => string.Empty,
        };
    }

    private static string DatePart(string code, bool isMinute, DateTime date, bool twelveHour, DateTimeFormatInfo names)
    {
        var length = code.Length;
        var invariant = CultureInfo.InvariantCulture;
        switch (code[0])
        {
            case 'y':
                return length <= 2 ? (date.Year % 100).ToString("00", invariant) : date.Year.ToString("0000", invariant);
            case 'm' when isMinute:
                return date.Minute.ToString(length == 1 ? "0" : "00", invariant);
            case 'm':
                return Month(length, date, names);
            case 'd':
                return Day(length, date, names);
            case 'h':
                var hour = twelveHour ? (date.Hour % 12 == 0 ? 12 : date.Hour % 12) : date.Hour;
                return hour.ToString(length == 1 ? "0" : "00", invariant);
            default:
                return date.Second.ToString(length == 1 ? "0" : "00", invariant);
        }
    }

    // m, mm, mmm (Jan), mmmm (January), mmmmm (J).
    private static string Month(int length, DateTime date, DateTimeFormatInfo names) => length switch
    {
        1 => date.Month.ToString(CultureInfo.InvariantCulture),
        2 => date.Month.ToString("00", CultureInfo.InvariantCulture),
        3 => names.GetAbbreviatedMonthName(date.Month),
        4 => names.GetMonthName(date.Month),
        _ => names.GetMonthName(date.Month)[..1],
    };

    // d, dd, ddd (Mon), dddd (Monday).
    private static string Day(int length, DateTime date, DateTimeFormatInfo names) => length switch
    {
        1 => date.Day.ToString(CultureInfo.InvariantCulture),
        2 => date.Day.ToString("00", CultureInfo.InvariantCulture),
        3 => names.GetAbbreviatedDayName(date.DayOfWeek),
        _ => names.GetDayName(date.DayOfWeek),
    };

    private static string AmPm(string marker, DateTime date)
    {
        // Excel shows AM/PM in capitals whatever the case of the code; A/P keeps the case of its letters.
        var pm = date.Hour >= 12;
        return marker.Length == 5 ? (pm ? "PM" : "AM")
            : char.IsUpper(marker[0]) ? (pm ? "P" : "A")
            : (pm ? "p" : "a");
    }

    private static string Elapsed(string code, double serial)
    {
        var total = code[0] switch
        {
            'h' => Math.Floor(serial * 24),
            'm' => Math.Floor(serial * 1440),
            _ => Math.Floor(Math.Round(serial * 86400, 6)),
        };
        return total.ToString(new string('0', code.Length), CultureInfo.InvariantCulture);
    }

    private static string FractionOfSecond(DateTime date, int digits)
    {
        digits = Math.Clamp(digits, 0, 3);
        return digits == 0
            ? string.Empty
            : (date.Millisecond / (int)Math.Pow(10, 3 - digits)).ToString(new string('0', digits), CultureInfo.InvariantCulture);
    }
}
