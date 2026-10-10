// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// Fraction sections (<c># ?/?</c>, <c># ??/??</c>, <c># ?/8</c>, <c>?/100</c>): an optional integer part, a
/// numerator, a slash and a denominator. A denominator of placeholders takes the fraction closest to the value
/// whose denominator has at most that many digits (<c>?</c> up to 9, <c>??</c> up to 99); a written denominator
/// is kept and the numerator rounded. Without an integer part the fraction is improper (<c>3/2</c>). When the
/// fraction is zero and an integer part is shown, the fraction is replaced by spaces of its width so that the
/// integers stay aligned; <c>?</c> pads the numerator on the left and the denominator on the right with spaces.
/// </summary>
internal static class FractionSection
{
    private const int MaxDenominatorDigits = 9;

    /// <summary>The section's text, or null when the section holds no fraction.</summary>
    public static string? TryFormat(double value, List<FormatToken> tokens, CultureInfo culture, bool autoMinus)
    {
        var layout = FractionLayout.Find(tokens);
        if (layout is null || !double.IsFinite(value))
        {
            return null;
        }

        var negative = value < 0;
        var magnitude = Math.Abs(value) * Math.Pow(100, tokens.Count(t => t.Kind == FormatTokenKind.Percent));
        var (whole, numerator, denominator) = Split(magnitude, layout);
        var output = new StringBuilder();
        if (negative && autoMinus && (whole > 0 || numerator > 0))
        {
            output.Append(culture.NumberFormat.NegativeSign);
        }

        Write(tokens, layout, whole, numerator, denominator, culture.NumberFormat, output);
        return output.ToString();
    }

    /// <summary>The fraction closest to <paramref name="value"/> (between 0 and 1) whose denominator is at most
    /// <paramref name="maxDenominator"/>, from the convergents of its continued fraction and the last
    /// intermediate fraction; on a tie the smaller denominator wins.</summary>
    internal static (long Numerator, long Denominator) Closest(double value, long maxDenominator)
    {
        long p0 = 0, q0 = 1, p1 = 1, q1 = 0;
        var rest = value;
        while (true)
        {
            var a = Math.Floor(rest);
            if (a > maxDenominator || q0 + ((long)a * q1) > maxDenominator)
            {
                break;
            }

            (p0, q0, p1, q1) = (p1, q1, p0 + ((long)a * p1), q0 + ((long)a * q1));
            var remainder = rest - a;
            if (remainder < 1e-10)
            {
                break;
            }

            rest = 1 / remainder;
        }

        var k = (maxDenominator - q0) / q1;
        var (pk, qk) = (p0 + (k * p1), q0 + (k * q1));
        var convergentError = Math.Abs(value - ((double)p1 / q1));
        var intermediateError = Math.Abs(value - ((double)pk / qk));
        return intermediateError < convergentError || (intermediateError == convergentError && qk < q1) ? (pk, qk) : (p1, q1);
    }

    private static (double Whole, double Numerator, double Denominator) Split(double value, FractionLayout layout)
    {
        var whole = Math.Floor(value);
        var part = value - whole;
        double numerator;
        double denominator;
        if (layout.FixedDenominator is { } fixedDenominator)
        {
            denominator = fixedDenominator;
            numerator = Math.Round(part * denominator, MidpointRounding.AwayFromZero);
        }
        else
        {
            var digits = Math.Min(layout.Denominator.Count, MaxDenominatorDigits);
            var (n, d) = Closest(part, (long)Math.Pow(10, digits) - 1);
            (numerator, denominator) = (n, d);
        }

        if (numerator >= denominator)
        {
            whole++;
            numerator = 0;
        }

        if (layout.Integer.Count == 0)
        {
            numerator += whole * denominator;
            whole = 0;
        }

        return (whole, numerator, denominator);
    }

    private static void Write(List<FormatToken> tokens, FractionLayout layout, double whole, double numerator, double denominator, NumberFormatInfo numbers, StringBuilder output)
    {
        var filled = new Dictionary<int, string>();
        if (layout.Integer.Count > 0)
        {
            FillInteger(tokens, layout, whole, numerator, numbers, filled);
        }

        var blank = layout.Integer.Count > 0 && numerator == 0;
        if (!blank)
        {
            FillNumber(tokens, layout.Numerator, Digits(numerator), filled);
            FillDenominator(tokens, layout.Denominator, Digits(denominator), filled);
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (blank && i >= layout.FractionStart && i < layout.FractionEnd)
            {
                output.Append(' ', token.Text.Length);
                continue;
            }

            output.Append(token.Kind switch
            {
                FormatTokenKind.Digit => filled.GetValueOrDefault(i, token.Text), // the zeros of a written denominator
                FormatTokenKind.Literal => token.Text,
                FormatTokenKind.Percent => "%",
                _ => string.Empty,
            });
        }
    }

    private static void FillInteger(List<FormatToken> tokens, FractionLayout layout, double whole, double numerator, NumberFormatInfo numbers, Dictionary<int, string> filled)
    {
        // A zero integer part is left to its placeholders, except when the whole value is zero: then 0 shows.
        var digits = whole > 0 ? Digits(whole) : numerator == 0 ? "0" : string.Empty;
        FillNumber(tokens, layout.Integer, digits, filled);
        if (!layout.Grouping)
        {
            return;
        }

        var placeholders = layout.Integer;
        var text = string.Concat(placeholders.Select(p => filled[p]));
        filled[placeholders[0]] = Group(text, numbers.NumberGroupSeparator);
        foreach (var p in placeholders.Skip(1))
        {
            filled[p] = string.Empty;
        }
    }

    // Digits fill the placeholders from the right; the first one takes any overflow, missing ones give
    // 0 for '0', a space for '?' and nothing for '#'.
    private static void FillNumber(List<FormatToken> tokens, List<int> placeholders, string digits, Dictionary<int, string> filled)
    {
        var remaining = digits.Length;
        for (var k = placeholders.Count - 1; k >= 0; k--)
        {
            var position = placeholders[k];
            if (remaining > 0)
            {
                filled[position] = k == 0 ? digits[..remaining] : digits[remaining - 1].ToString();
                remaining = k == 0 ? 0 : remaining - 1;
            }
            else
            {
                filled[position] = Padding(tokens[position].Text);
            }
        }
    }

    // A denominator is written from the left: missing placeholders after it give a space for '?' (the slashes
    // of a column stay aligned) and nothing for '#'; a '0' gives a leading zero.
    private static void FillDenominator(List<FormatToken> tokens, List<int> placeholders, string digits, Dictionary<int, string> filled)
    {
        var spare = Math.Max(placeholders.Count - digits.Length, 0);
        var leading = new StringBuilder();
        var trailing = new StringBuilder();
        for (var k = 0; k < spare; k++)
        {
            var text = tokens[placeholders[placeholders.Count - 1 - k]].Text;
            (text == "0" ? leading : trailing).Append(Padding(text));
        }

        for (var k = 0; k < placeholders.Count; k++)
        {
            filled[placeholders[k]] = string.Empty;
        }

        if (placeholders.Count > 0)
        {
            filled[placeholders[0]] = leading.ToString() + digits + trailing;
        }
    }

    private static string Padding(string placeholder) => placeholder switch { "0" => "0", "?" => " ", _ => string.Empty };

    private static string Digits(double value) => value.ToString("F0", CultureInfo.InvariantCulture);

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
}

/// <summary>Where the parts of a fraction section sit among its tokens.</summary>
internal sealed record FractionLayout(List<int> Integer, List<int> Numerator, List<int> Denominator, int? FixedDenominator, int FractionStart, int FractionEnd, bool Grouping)
{
    /// <summary>The layout of a section with a fraction (placeholders, a slash, placeholders or a written
    /// denominator), or null. A section with a decimal point or an exponent is not a fraction.</summary>
    public static FractionLayout? Find(List<FormatToken> tokens)
    {
        if (tokens.Exists(t => t.Kind is FormatTokenKind.Point or FormatTokenKind.Exponent))
        {
            return null;
        }

        for (var slash = 0; slash < tokens.Count; slash++)
        {
            if (tokens[slash] is { Kind: FormatTokenKind.Literal, Text: "/" } && At(tokens, slash) is { } layout)
            {
                return layout;
            }
        }

        return null;
    }

    private static FractionLayout? At(List<FormatToken> tokens, int slash)
    {
        var numerator = new List<int>();
        var start = SkipSpaces(tokens, slash - 1, -1);
        for (; start >= 0 && tokens[start].Kind == FormatTokenKind.Digit; start--)
        {
            numerator.Insert(0, start);
        }

        var (denominator, fixedDenominator, end) = ReadDenominator(tokens, SkipSpaces(tokens, slash + 1, 1));
        if (numerator.Count == 0 || (denominator.Count == 0 && fixedDenominator is null))
        {
            return null;
        }

        var integer = new List<int>();
        for (var i = 0; i < numerator[0]; i++)
        {
            if (tokens[i].Kind == FormatTokenKind.Digit)
            {
                integer.Add(i);
            }
        }

        var grouping = integer.Count > 1 && Enumerable.Range(integer[0], integer[^1] - integer[0]).Any(i => tokens[i].Kind == FormatTokenKind.Comma);
        return new FractionLayout(integer, numerator, denominator, fixedDenominator, numerator[0], end, grouping);
    }

    private static int SkipSpaces(List<FormatToken> tokens, int i, int step)
    {
        while (i >= 0 && i < tokens.Count && tokens[i] is { Kind: FormatTokenKind.Literal, Text: " " })
        {
            i += step;
        }

        return i;
    }

    // Placeholders give a variable denominator; a written number (1 to 9 then any digits, the zeros being read
    // as '0' placeholders) a fixed one.
    private static (List<int> Placeholders, int? Fixed, int End) ReadDenominator(List<FormatToken> tokens, int i)
    {
        var placeholders = new List<int>();
        if (i < tokens.Count && tokens[i] is { Kind: FormatTokenKind.Literal, Text: [>= '1' and <= '9'] })
        {
            var written = new StringBuilder();
            for (; i < tokens.Count && IsWrittenDigit(tokens[i]); i++)
            {
                written.Append(tokens[i].Text);
            }

            return written.Length <= MaxWrittenDigits
                ? (placeholders, int.Parse(written.ToString(), CultureInfo.InvariantCulture), i)
                : (placeholders, null, i);
        }

        for (; i < tokens.Count && tokens[i].Kind == FormatTokenKind.Digit; i++)
        {
            placeholders.Add(i);
        }

        return (placeholders, null, i);
    }

    private const int MaxWrittenDigits = 9;

    private static bool IsWrittenDigit(FormatToken token) =>
        token is { Kind: FormatTokenKind.Literal, Text: [>= '0' and <= '9'] } or { Kind: FormatTokenKind.Digit, Text: "0" };
}
