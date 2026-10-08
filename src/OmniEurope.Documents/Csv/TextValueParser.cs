// SPDX-License-Identifier: EUPL-1.2
using System.Buffers;
using System.Globalization;

namespace OmniEurope.Documents.Csv;

/// <summary>
/// Tolerant parsing of the values found in accounting and time-tracking exports: amounts written the
/// French way (<c>1 234,56</c>) or the English way (<c>1,234.56</c>), compact dates (<c>20221231</c>)
/// and durations (<c>1:30</c>, <c>1:30:15</c>, <c>1,5</c> hours).
/// </summary>
public static class TextValueParser
{
    // Group separators (space, no-break space, narrow no-break space, apostrophe) and the separators that are not the decimal point.
    private static readonly SearchValues<char> Skipped = SearchValues.Create(" " + (char)0xA0 + (char)0x202F + "',.");

    /// <summary>
    /// Parses an amount. Spaces, no-break spaces (U+00A0, U+202F) and apostrophes are digit-group
    /// separators. With both a comma and a dot, the last one is the decimal separator; with only one of
    /// them, it is the decimal separator unless it repeats (<c>1.234.567</c>). A leading or trailing minus sign is accepted.
    /// </summary>
    public static bool TryParseDecimal(ReadOnlySpan<char> text, out decimal value)
    {
        value = 0;
        Span<char> clean = stackalloc char[Math.Min(text.Trim().Length, 128)];
        if (!Normalize(text.Trim(), clean, out var length))
        {
            return false;
        }

        return decimal.TryParse(clean[..length], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses a compact <c>yyyyMMdd</c> date.</summary>
    public static bool TryParseCompactDate(ReadOnlySpan<char> text, out DateOnly date) =>
        DateOnly.TryParseExact(text.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Parses <c>h:mm</c>, <c>h:mm:ss</c> (hours may exceed 24) or a decimal number of hours.</summary>
    public static bool TryParseDuration(ReadOnlySpan<char> text, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        text = text.Trim();
        if (text.IndexOf(':') < 0)
        {
            // From the last whole hour a TimeSpan holds (256,204,778) on, the text is refused: the conversion
            // below cannot overflow.
            if (!TryParseDecimal(text, out var hours) || hours < 0 || hours >= TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerHour)
            {
                return false;
            }

            duration = TimeSpan.FromHours((double)hours);
            return true;
        }

        Span<Range> parts = stackalloc Range[4];
        var count = text.Split(parts, ':');
        if (count is < 2 or > 3)
        {
            return false;
        }

        if (!int.TryParse(text[parts[0]], NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(text[parts[1]], NumberStyles.None, CultureInfo.InvariantCulture, out var m) || m > 59)
        {
            return false;
        }

        var s = 0;
        if (count == 3 && (!int.TryParse(text[parts[2]], NumberStyles.None, CultureInfo.InvariantCulture, out s) || s > 59))
        {
            return false;
        }

        if (((long)h * 3600) + (m * 60) + s > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond)
        {
            return false;
        }

        duration = new TimeSpan(h, m, s);
        return true;
    }

    private static bool Normalize(ReadOnlySpan<char> text, Span<char> output, out int length)
    {
        length = 0;
        if (text.IsEmpty || text.Length > output.Length)
        {
            return false;
        }

        // A trailing minus sign (accounting exports) moves to the front.
        var negative = text[^1] == '-';
        var body = negative ? text[..^1].TrimEnd() : text;
        if (negative)
        {
            output[length++] = '-';
        }

        var decimalAt = DecimalPoint(body);
        for (var i = 0; i < body.Length; i++)
        {
            if (i == decimalAt)
            {
                output[length++] = '.';
            }
            else if (!Skipped.Contains(body[i]))
            {
                output[length++] = body[i];
            }
        }

        return length > (negative ? 1 : 0);
    }

    // The last ',' or '.', unless it repeats (1.234.567): a repeated separator groups digits.
    private static int DecimalPoint(ReadOnlySpan<char> text)
    {
        var at = text.LastIndexOfAny(',', '.');
        return at >= 0 && text.Count(text[at]) == 1 ? at : -1;
    }}
