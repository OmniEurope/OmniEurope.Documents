// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// The condition of a number format section: a bracket such as <c>[&gt;=1000]</c> made of a comparison
/// (<c>&lt; &lt;= &gt; &gt;= = &lt;&gt;</c>) and a number, found anywhere in the section prefix (next to a colour
/// bracket), outside quotes.
/// </summary>
internal readonly record struct FormatCondition(string Operator, double Bound)
{
    private static readonly string[] Operators = ["<=", ">=", "<>", "<", ">", "="];

    /// <summary>True when the value satisfies the comparison.</summary>
    public bool Matches(double value) => Operator switch
    {
        "<" => value < Bound,
        "<=" => value <= Bound,
        ">" => value > Bound,
        ">=" => value >= Bound,
        "=" => value == Bound,
        _ => value != Bound,
    };

    /// <summary>True when the condition admits only negative numbers (<c>&lt;0</c>, <c>&lt;=0</c>, <c>&lt;</c> or
    /// <c>&lt;=</c> a negative bound, <c>=</c> a negative number): the section then writes the sign itself.</summary>
    public bool AdmitsOnlyNegatives => Operator switch
    {
        "<" or "<=" => Bound <= 0,
        "=" => Bound < 0,
        _ => false,
    };

    /// <summary>The first condition bracket of the section, or null when it has none.</summary>
    public static FormatCondition? Find(string section)
    {
        var i = 0;
        while (i < section.Length)
        {
            switch (section[i])
            {
                case '"':
                    var close = section.IndexOf('"', i + 1);
                    i = close < 0 ? section.Length : close + 1;
                    break;
                case '\\' or '_' or '*':
                    i += 2;
                    break;
                case '[':
                    var end = section.IndexOf(']', i + 1);
                    if (end < 0)
                    {
                        return null;
                    }

                    if (Parse(section[(i + 1)..end]) is { } condition)
                    {
                        return condition;
                    }

                    i = end + 1;
                    break;
                default:
                    i++;
                    break;
            }
        }

        return null;
    }

    private static FormatCondition? Parse(string content)
    {
        var text = content.Trim();
        foreach (var op in Operators)
        {
            if (text.StartsWith(op, StringComparison.Ordinal))
            {
                var number = text[op.Length..].Trim();
                if (double.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var bound))
                {
                    return new FormatCondition(op, bound);
                }
            }
        }

        return null;
    }
}
