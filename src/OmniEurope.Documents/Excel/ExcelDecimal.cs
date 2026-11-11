// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// Converts a double to decimal at the 15 significant digits Excel works with, so that 2.675 is the decimal 2.675
/// and not 2.67499999999999982236431605997495353221893310546875. The runtime's own conversion is exact from .NET 11
/// on (it rounded to 15 digits before), which would change ROUND and the displayed digits between frameworks.
/// </summary>
internal static class ExcelDecimal
{
    /// <summary>The decimal nearest to <paramref name="value"/> rounded to 15 significant digits.</summary>
    /// <exception cref="OverflowException">The value is not finite or lies outside the decimal range.</exception>
    public static decimal From(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new OverflowException("Value was either too large or too small for a Decimal.");
        }

        // "G15" holds at most 15 digits, a sign, a point and an exponent: 32 characters are enough.
        Span<char> text = stackalloc char[32];
        value.TryFormat(text, out var written, "G15", CultureInfo.InvariantCulture);
        return decimal.Parse(text[..written], NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
