// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// The General rendering of a number, the text a spreadsheet's CSV export writes: the value rounded to 15
/// significant digits, trailing zeros dropped. Fixed notation is used when the decimal exponent of the first
/// digit (the power of ten of the rounded value, so 12345 has exponent 4 and 0.00123 has exponent -3) lies
/// between -10 and 14 inclusive and the fixed text (without sign) takes at most 20 characters; otherwise
/// scientific notation (<c>1.5E+21</c>, <c>1E-20</c>) with an exponent of at least two digits. Integers of up
/// to 15 digits therefore print in full, and fixed text never carries more than 15 significant digits.
/// </summary>
internal static class GeneralFormat
{
    private const string Scientific14 = "E14";
    private const int MinFixedExponent = -10;
    private const int MaxFixedExponent = 14;
    private const int MaxFixedLength = 20;

    public static string Render(double value, CultureInfo culture)
    {
        if (!double.IsFinite(value))
        {
            return value.ToString(culture);
        }

        if (value == 0)
        {
            return "0";
        }

        var text = Math.Abs(value).ToString(Scientific14, CultureInfo.InvariantCulture);
        var at = text.IndexOf('E');
        var exponent = int.Parse(text.AsSpan(at + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var digits = text[..at].Replace(".", string.Empty, StringComparison.Ordinal).TrimEnd('0');
        var numbers = culture.NumberFormat;
        var output = new StringBuilder();
        if (value < 0)
        {
            output.Append(numbers.NegativeSign);
        }

        if (UsesFixed(digits.Length, exponent))
        {
            AppendFixed(output, digits, exponent, numbers.NumberDecimalSeparator);
        }
        else
        {
            AppendScientific(output, digits, exponent, numbers.NumberDecimalSeparator);
        }

        return output.ToString();
    }

    // A negative exponent costs "0." and the zeros before the first digit; a non-negative one never exceeds 16 characters.
    private static bool UsesFixed(int digitCount, int exponent) =>
        exponent is >= MinFixedExponent and <= MaxFixedExponent && (exponent >= 0 || 1 - exponent + digitCount <= MaxFixedLength);

    private static void AppendFixed(StringBuilder output, string digits, int exponent, string separator)
    {
        if (exponent < 0)
        {
            output.Append('0').Append(separator).Append('0', -exponent - 1).Append(digits);
        }
        else if (digits.Length <= exponent + 1)
        {
            output.Append(digits).Append('0', exponent + 1 - digits.Length);
        }
        else
        {
            output.Append(digits, 0, exponent + 1).Append(separator).Append(digits, exponent + 1, digits.Length - exponent - 1);
        }
    }

    private static void AppendScientific(StringBuilder output, string digits, int exponent, string separator)
    {
        output.Append(digits[0]);
        if (digits.Length > 1)
        {
            output.Append(separator).Append(digits, 1, digits.Length - 1);
        }

        output.Append('E').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
    }
}
