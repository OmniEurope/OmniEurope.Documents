// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// Renders values the way Excel displays them under a number format: sections (positive; negative; zero;
/// text), digit placeholders <c>0 # ?</c>, grouping and scaling commas, percent, scientific notation,
/// literals, currency brackets, dates and times (including elapsed <c>[h]</c> and AM/PM), fractions
/// (<c># ?/?</c>, <c># ??/??</c>, <c># ?/8</c>, see <see cref="FractionSection"/>). The culture gives the decimal and group separators and the month
/// and day names.
/// </summary>
public static class NumberFormatter
{
    /// <summary>Formats a number (a date is passed as its serial number).</summary>
    public static string Format(double value, string format, CultureInfo culture, bool date1904 = false)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(culture);
        var sections = FormatTokens.SplitSections(format);
        var (section, autoMinus) = Pick(sections, value);
        if (sections.Count > 1 && value < 0)
        {
            value = -value;
        }

        if (section.Trim().Equals("General", StringComparison.OrdinalIgnoreCase) || section.Length == 0 && sections.Count == 1)
        {
            return General(value, culture);
        }

        var tokens = FormatTokens.Tokenize(section);
        if (FormatTokens.HasDateParts(tokens))
        {
            return DateFormatter.Format(value, tokens, culture, date1904);
        }

        return FractionSection.TryFormat(value, tokens, culture, autoMinus) ?? NumberSection.Format(value, tokens, culture, autoMinus);
    }

    /// <summary>Formats text with the text section of a format (<c>@</c>); other formats leave text unchanged.</summary>
    public static string FormatText(string text, string format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);
        var sections = FormatTokens.SplitSections(format);
        var section = sections.Count >= 4 ? sections[3] : sections.Find(s => s.Contains('@'));
        if (section is null)
        {
            return text;
        }

        var builder = new StringBuilder();
        foreach (var token in FormatTokens.Tokenize(section))
        {
            builder.Append(token.Kind == FormatTokenKind.TextPlaceholder ? text : token.Kind == FormatTokenKind.Literal ? token.Text : string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>True when the format shows a date or a time.</summary>
    public static bool IsDateFormat(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var first = FormatTokens.SplitSections(format)[0];
        return !first.Trim().Equals("General", StringComparison.OrdinalIgnoreCase) && FormatTokens.HasDateParts(FormatTokens.Tokenize(first));
    }

    /// <summary>True when the format shows elapsed time (<c>[h]</c>, <c>[m]</c>, <c>[s]</c>): the value is a
    /// duration in days, not a point in time.</summary>
    internal static bool IsDurationFormat(string format) =>
        FormatTokens.Tokenize(FormatTokens.SplitSections(format)[0]).Exists(t => t.Kind == FormatTokenKind.Elapsed);

    /// <summary>The General format: integers as is, otherwise up to 10 significant digits, scientific when
    /// very large or very small.</summary>
    public static string General(double value, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (value == Math.Floor(value) && Math.Abs(value) < 1e11)
        {
            return ((long)value).ToString(culture);
        }

        return value.ToString("G10", culture);
    }

    private static (string Section, bool AutoMinus) Pick(List<string> sections, double value)
    {
        if (sections.Count == 1)
        {
            return (sections[0], true);
        }

        if (value < 0)
        {
            return (sections[1], false);
        }

        return value == 0 && sections.Count >= 3 ? (sections[2], false) : (sections[0], false);
    }
}

/// <summary>Numeric sections: placeholders, separators, percent and exponent.</summary>
internal static class NumberSection
{
    public static string Format(double value, List<FormatToken> tokens, CultureInfo culture, bool autoMinus)
    {
        var numbers = culture.NumberFormat;
        var negative = value < 0;
        value = Math.Abs(value);
        var pointAt = tokens.FindIndex(t => t.Kind == FormatTokenKind.Point);
        var exponentAt = tokens.FindIndex(t => t.Kind == FormatTokenKind.Exponent);
        var integerEnd = pointAt >= 0 ? pointAt : exponentAt >= 0 ? exponentAt : tokens.Count;
        var (grouping, scale) = Commas(tokens, integerEnd);
        value *= Math.Pow(100, tokens.Count(t => t.Kind == FormatTokenKind.Percent));
        value /= Math.Pow(1000, scale);

        var fractionEnd = exponentAt >= 0 ? exponentAt : tokens.Count;
        var decimals = pointAt < 0 ? 0 : tokens.Skip(pointAt + 1).Take(fractionEnd - pointAt - 1).Count(t => t.Kind == FormatTokenKind.Digit);
        var exponent = 0;
        if (exponentAt >= 0 && value != 0)
        {
            exponent = (int)Math.Floor(Math.Log10(value));
            value /= Math.Pow(10, exponent);
            if (Math.Round(value, decimals) >= 10)
            {
                value /= 10;
                exponent++;
            }
        }

        var (integerDigits, fractionDigits) = Split(value, decimals);
        if (integerDigits.Trim('0').Length == 0 && fractionDigits.Trim('0').Length == 0)
        {
            negative = false;
        }

        var output = new StringBuilder();
        if (negative && autoMinus)
        {
            output.Append(numbers.NegativeSign);
        }

        WriteInteger(tokens, integerEnd, integerDigits, grouping, numbers, output);
        for (var i = integerEnd; i < tokens.Count; i++)
        {
            i = WriteTail(tokens, i, fractionDigits, exponent, numbers, output, fractionEnd);
        }

        return output.ToString();
    }

    private static (bool Grouping, int Scale) Commas(List<FormatToken> tokens, int integerEnd)
    {
        var lastDigit = integerEnd == 0 ? -1 : tokens.FindLastIndex(integerEnd - 1, integerEnd, t => t.Kind == FormatTokenKind.Digit);
        var firstDigit = tokens.FindIndex(t => t.Kind == FormatTokenKind.Digit);
        var grouping = false;
        var scale = 0;
        for (var i = 0; i < integerEnd; i++)
        {
            if (tokens[i].Kind != FormatTokenKind.Comma)
            {
                continue;
            }

            if (i > firstDigit && i < lastDigit)
            {
                grouping = true;
            }
            else if (i > lastDigit && lastDigit >= 0)
            {
                scale++;
            }
        }

        return (grouping, scale);
    }

    private static (string Integer, string Fraction) Split(double value, int decimals)
    {
        string text;
        if (value < 7.9e27)
        {
            var rounded = Math.Round((decimal)value, Math.Min(decimals, 28), MidpointRounding.AwayFromZero);
            text = rounded.ToString("F" + Math.Min(decimals, 28).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
        else
        {
            // Beyond the decimal range: the 15 significant digits Excel shows, then zeros.
            var scientific = value.ToString("E14", CultureInfo.InvariantCulture);
            var exponent = int.Parse(scientific.AsSpan(scientific.IndexOf('E') + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var digits = scientific[..scientific.IndexOf('E')].Replace(".", string.Empty, StringComparison.Ordinal);
            text = digits.PadRight(exponent + 1, '0') + (decimals > 0 ? "." + new string('0', decimals) : string.Empty);
        }

        var point = text.IndexOf('.');
        return point < 0 ? (text, string.Empty) : (text[..point], text[(point + 1)..].PadRight(decimals, '0'));
    }

    private static void WriteInteger(List<FormatToken> tokens, int integerEnd, string digits, bool grouping, NumberFormatInfo numbers, StringBuilder output)
    {
        var placeholders = new List<int>();
        for (var i = 0; i < integerEnd; i++)
        {
            if (tokens[i].Kind == FormatTokenKind.Digit)
            {
                placeholders.Add(i);
            }
        }

        if (digits == "0" && placeholders.Count > 0 && placeholders.TrueForAll(p => tokens[p].Text != "0"))
        {
            digits = string.Empty;
        }

        var filled = Fill(tokens, placeholders, digits);
        if (grouping)
        {
            filled[placeholders[0]] = Group(string.Concat(placeholders.Select(p => filled[p])), numbers.NumberGroupSeparator);
            placeholders.Skip(1).ToList().ForEach(p => filled[p] = string.Empty);
        }

        for (var i = 0; i < integerEnd; i++)
        {
            output.Append(tokens[i].Kind switch
            {
                FormatTokenKind.Digit => filled[i],
                FormatTokenKind.Literal => tokens[i].Text,
                FormatTokenKind.Percent => "%",
                _ => string.Empty,
            });
        }

        // A format without integer placeholders (".00") still shows a non-zero integer part.
        if (placeholders.Count == 0 && digits != "0")
        {
            output.Append(digits);
        }
    }

    private static Dictionary<int, string> Fill(List<FormatToken> tokens, List<int> placeholders, string digits)
    {
        var filled = new Dictionary<int, string>();
        var remaining = digits.Length;
        for (var k = placeholders.Count - 1; k >= 0; k--)
        {
            var position = placeholders[k];
            if (k == 0 && remaining > 0)
            {
                filled[position] = digits[..remaining];
                remaining = 0;
            }
            else if (remaining > 0)
            {
                filled[position] = digits[--remaining].ToString();
            }
            else
            {
                filled[position] = tokens[position].Text switch { "0" => "0", "?" => " ", _ => string.Empty };
            }
        }

        return filled;
    }

    private static string Group(string digits, string separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0 && char.IsAsciiDigit(digits[i - 1]))
            {
                builder.Append(separator);
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    private static int WriteTail(List<FormatToken> tokens, int i, string fraction, int exponent, NumberFormatInfo numbers, StringBuilder output, int fractionEnd)
    {
        var token = tokens[i];
        switch (token.Kind)
        {
            case FormatTokenKind.Point:
                output.Append(numbers.NumberDecimalSeparator);
                return WriteFraction(tokens, i + 1, fractionEnd, fraction, output) - 1;
            case FormatTokenKind.Exponent:
                var digits = 0;
                while (i + 1 + digits < tokens.Count && tokens[i + 1 + digits].Kind == FormatTokenKind.Digit)
                {
                    digits++;
                }

                output.Append(token.Text[0]).Append(exponent < 0 ? "-" : token.Text[1] == '+' ? "+" : string.Empty);
                output.Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(digits, 1), '0'));
                return i + digits;
            case FormatTokenKind.Literal:
                output.Append(token.Text);
                return i;
            case FormatTokenKind.Percent:
                output.Append('%');
                return i;
            default:
                return i;
        }
    }

    private static int WriteFraction(List<FormatToken> tokens, int start, int end, string fraction, StringBuilder output)
    {
        var lastSignificant = fraction.TrimEnd('0').Length;
        var digit = 0;
        var i = start;
        for (; i < end; i++)
        {
            var token = tokens[i];
            if (token.Kind == FormatTokenKind.Digit)
            {
                var c = digit < fraction.Length ? fraction[digit] : '0';
                output.Append(digit < lastSignificant || token.Text == "0" ? c.ToString() : token.Text == "?" ? " " : string.Empty);
                digit++;
            }
            else if (token.Kind == FormatTokenKind.Literal)
            {
                output.Append(token.Text);
            }
            else if (token.Kind == FormatTokenKind.Percent)
            {
                output.Append('%');
            }
        }

        return i;
    }
}
