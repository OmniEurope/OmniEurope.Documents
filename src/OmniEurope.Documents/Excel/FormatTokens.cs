// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Excel;

/// <summary>Kinds of token in an Excel number format section.</summary>
internal enum FormatTokenKind
{
    Literal,
    Digit,
    Point,
    Comma,
    Percent,
    Exponent,
    DatePart,
    AmPm,
    Elapsed,
    FractionalSeconds,
    TextPlaceholder,
}

/// <summary>One token: its kind and text (the placeholder character, the literal, the date code...).</summary>
internal readonly record struct FormatToken(FormatTokenKind Kind, string Text);

/// <summary>Splits a number format into sections and sections into tokens.</summary>
internal static class FormatTokens
{
    public static List<string> SplitSections(string format)
    {
        var sections = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var inBrackets = false;
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (inQuotes || inBrackets)
            {
                current.Append(c);
                inQuotes &= c != '"';
                inBrackets &= c != ']';
                continue;
            }

            if (c == '\\' && i + 1 < format.Length)
            {
                current.Append(c).Append(format[++i]);
                continue;
            }

            if (c == ';')
            {
                sections.Add(current.ToString());
                current.Clear();
                continue;
            }

            inQuotes = c == '"';
            inBrackets = c == '[';
            current.Append(c);
        }

        sections.Add(current.ToString());
        return sections;
    }

    public static List<FormatToken> Tokenize(string section)
    {
        var tokens = new List<FormatToken>();
        var i = 0;
        while (i < section.Length)
        {
            i = ReadToken(section, i, tokens);
        }

        MarkFractionalSeconds(tokens);
        return tokens;
    }

    public static bool HasDateParts(List<FormatToken> tokens) =>
        tokens.Exists(t => t.Kind is FormatTokenKind.DatePart or FormatTokenKind.Elapsed or FormatTokenKind.AmPm);

    private static readonly Dictionary<char, FormatTokenKind> SingleTokens = new()
    {
        ['0'] = FormatTokenKind.Digit,
        ['#'] = FormatTokenKind.Digit,
        ['?'] = FormatTokenKind.Digit,
        ['.'] = FormatTokenKind.Point,
        [','] = FormatTokenKind.Comma,
        ['%'] = FormatTokenKind.Percent,
        ['@'] = FormatTokenKind.TextPlaceholder,
    };

    private static int ReadToken(string s, int i, List<FormatToken> tokens)
    {
        var c = s[i];
        if (SingleTokens.TryGetValue(c, out var kind))
        {
            tokens.Add(new FormatToken(kind, c.ToString()));
            return i + 1;
        }

        if (c == '"')
        {
            var close = s.IndexOf('"', i + 1);
            var end = close < 0 ? s.Length : close;
            tokens.Add(new FormatToken(FormatTokenKind.Literal, s[(i + 1)..end]));
            return end + 1;
        }

        if (c == '[')
        {
            return ReadBracket(s, i, tokens);
        }

        var next = i + 1 < s.Length ? ReadPair(s, i, tokens) : i;
        return next > i ? next : ReadDateOrLiteral(s, i, tokens);
    }

    // Two-character forms: \x a literal, _x a space as wide as x, *x a fill (dropped), E+ or E- an exponent.
    // Returns i when none starts here.
    private static int ReadPair(string s, int i, List<FormatToken> tokens)
    {
        switch (s[i])
        {
            case '\\':
                tokens.Add(new FormatToken(FormatTokenKind.Literal, s[i + 1].ToString()));
                return i + 2;
            case '_':
                tokens.Add(new FormatToken(FormatTokenKind.Literal, " "));
                return i + 2;
            case '*':
                return i + 2;
            case 'E' or 'e' when s[i + 1] is '+' or '-':
                tokens.Add(new FormatToken(FormatTokenKind.Exponent, s.Substring(i, 2)));
                return i + 2;
            default:
                return i;
        }
    }

    private static int ReadDateOrLiteral(string s, int i, List<FormatToken> tokens)
    {
        foreach (var marker in (string[])["AM/PM", "am/pm", "A/P", "a/p"])
        {
            if (string.CompareOrdinal(s, i, marker, 0, marker.Length) == 0)
            {
                tokens.Add(new FormatToken(FormatTokenKind.AmPm, marker));
                return i + marker.Length;
            }
        }

        var lower = char.ToLowerInvariant(s[i]);
        if (lower is 'y' or 'm' or 'd' or 'h' or 's')
        {
            var end = i;
            while (end < s.Length && char.ToLowerInvariant(s[end]) == lower)
            {
                end++;
            }

            tokens.Add(new FormatToken(FormatTokenKind.DatePart, new string(lower, end - i)));
            return end;
        }

        if (lower is 'g' or 'b')
        {
            return i + 1;
        }

        tokens.Add(new FormatToken(FormatTokenKind.Literal, s[i].ToString()));
        return i + 1;
    }

    private static int ReadBracket(string s, int i, List<FormatToken> tokens)
    {
        var close = s.IndexOf(']', i + 1);
        if (close < 0)
        {
            return s.Length;
        }

        var content = s[(i + 1)..close];
        var lower = content.ToLowerInvariant();
        if (lower.Length > 0 && lower.All(ch => ch == lower[0]) && lower[0] is 'h' or 'm' or 's')
        {
            tokens.Add(new FormatToken(FormatTokenKind.Elapsed, lower));
        }
        else if (content.StartsWith('$'))
        {
            var dash = content.IndexOf('-');
            var symbol = dash < 0 ? content[1..] : content[1..dash];
            if (symbol.Length > 0)
            {
                tokens.Add(new FormatToken(FormatTokenKind.Literal, symbol));
            }
        }

        // Colours ([Red]) and conditions ([<0]) do not change the text.
        return close + 1;
    }

    // ".0", ".00" right after seconds are fractions of a second, not a decimal point.
    private static void MarkFractionalSeconds(List<FormatToken> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var afterSeconds = i > 0 && tokens[i - 1].Kind is FormatTokenKind.DatePart or FormatTokenKind.Elapsed && tokens[i - 1].Text[0] == 's';
            if (tokens[i].Kind != FormatTokenKind.Point || !afterSeconds)
            {
                continue;
            }

            var digits = 0;
            while (i + 1 + digits < tokens.Count && tokens[i + 1 + digits] is { Kind: FormatTokenKind.Digit, Text: "0" })
            {
                digits++;
            }

            tokens.RemoveRange(i, digits + 1);
            tokens.Insert(i, new FormatToken(FormatTokenKind.FractionalSeconds, new string('0', digits)));
        }
    }
}
