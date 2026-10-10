// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using static OmniEurope.Documents.Excel.Formulas.FormulaFunctions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>Text functions (ECMA-376 part 1, §18.17.7). Positions are 1-based and count UTF-16 characters, as Excel does.</summary>
internal static class TextFunctions
{
    private const int Many = 255;

    public static void Register(Dictionary<string, FormulaFunction> registry)
    {
        registry["CONCATENATE"] = new(1, Many, (e, a) => Join(e, a, string.Empty, skipEmpty: false, flatten: false));
        registry["CONCAT"] = new(1, Many, (e, a) => Join(e, a, string.Empty, skipEmpty: false, flatten: true));
        registry["TEXTJOIN"] = new(3, Many, TextJoin);
        registry["LEFT"] = new(1, 2, (e, a) => Cut(e, a, (text, count) => text[..Math.Min(count, text.Length)]));
        registry["RIGHT"] = new(1, 2, (e, a) => Cut(e, a, (text, count) => text[Math.Max(0, text.Length - count)..]));
        registry["MID"] = new(3, 3, Mid);
        registry["LEN"] = new(1, 1, (e, a) => TextOf(e, a[0]) is { IsError: false } t ? FormulaValue.Of(t.Text.Length) : TextOf(e, a[0]));
        registry["UPPER"] = new(1, 1, (e, a) => Change(e, a, t => t.ToUpperInvariant()));
        registry["LOWER"] = new(1, 1, (e, a) => Change(e, a, t => t.ToLowerInvariant()));
        registry["PROPER"] = new(1, 1, (e, a) => Change(e, a, Proper));
        registry["TRIM"] = new(1, 1, (e, a) => Change(e, a, t => string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries))));
        registry["REPT"] = new(2, 2, Repeat);
        registry["EXACT"] = new(2, 2, (e, a) => TextOf(e, a[0]) is { IsError: false } x && TextOf(e, a[1]) is { IsError: false } y ? FormulaValue.Of(x.Text == y.Text) : FormulaValue.ValueError);
        registry["SUBSTITUTE"] = new(3, 4, Substitute);
        registry["REPLACE"] = new(4, 4, Replace);
        registry["FIND"] = new(2, 3, (e, a) => Find(e, a, ignoreCase: false));
        registry["SEARCH"] = new(2, 3, (e, a) => Find(e, a, ignoreCase: true));
        registry["VALUE"] = new(1, 1, (e, a) => e.Evaluate(a[0]).Scalar() is { Kind: ValueKind.Text or ValueKind.Number or ValueKind.Blank } v ? v.ToNumber() : FormulaValue.ValueError);
        registry["TEXT"] = new(2, 2, Format);
        registry["CHAR"] = new(1, 1, (e, a) => Number(e, a[0]) is { IsError: false } n && n.Number is >= 1 and < 256 ? FormulaValue.Of(Encoding.Latin1.GetString([(byte)n.Number])) : FormulaValue.ValueError);
        registry["CODE"] = new(1, 1, (e, a) => TextOf(e, a[0]) is { IsError: false, Text.Length: > 0 } t ? FormulaValue.Of(t.Text[0]) : FormulaValue.ValueError);
        registry["T"] = new(1, 1, (e, a) => e.Evaluate(a[0]).Scalar() is { Kind: ValueKind.Text } t ? t : FormulaValue.Of(string.Empty));
    }

    private static FormulaValue Join(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, string delimiter, bool skipEmpty, bool flatten)
    {
        var parts = new List<string>();
        foreach (var argument in arguments)
        {
            var value = evaluator.Evaluate(argument);
            foreach (var element in flatten ? Grid(value).Cast<FormulaValue>() : [value.Scalar()])
            {
                var text = element.ToText();
                if (text.IsError)
                {
                    return text;
                }

                if (!skipEmpty || text.Text.Length > 0)
                {
                    parts.Add(text.Text);
                }
            }
        }

        var joined = string.Join(delimiter, parts);
        return joined.Length > 32767 ? FormulaValue.ValueError : FormulaValue.Of(joined);
    }

    private static FormulaValue TextJoin(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var delimiter = TextOf(evaluator, arguments[0]);
        var skip = evaluator.Evaluate(arguments[1]).ToBoolean();
        return delimiter.IsError ? delimiter : skip.IsError ? skip : Join(evaluator, arguments.Skip(2).ToList(), delimiter.Text, skip.Boolean, flatten: true);
    }

    private static FormulaValue Cut(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<string, int, string> cut)
    {
        var text = TextOf(evaluator, arguments[0]);
        var count = Number(evaluator, arguments, 1, 1);
        if (text.IsError || count.IsError)
        {
            return text.IsError ? text : count;
        }

        return count.Number < 0 ? FormulaValue.ValueError : FormulaValue.Of(cut(text.Text, (int)Math.Min(int.MaxValue, Math.Truncate(count.Number))));
    }

    private static FormulaValue Mid(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var text = TextOf(evaluator, arguments[0]);
        var start = Number(evaluator, arguments[1]);
        var count = Number(evaluator, arguments[2]);
        if (text.IsError || start.IsError || count.IsError)
        {
            return text.IsError ? text : start.IsError ? start : count;
        }

        if (start.Number < 1 || count.Number < 0)
        {
            return FormulaValue.ValueError;
        }

        var from = (int)Math.Min(int.MaxValue, Math.Truncate(start.Number)) - 1;
        return from >= text.Text.Length
            ? FormulaValue.Of(string.Empty)
            : FormulaValue.Of(text.Text.Substring(from, (int)Math.Min(text.Text.Length - from, Math.Truncate(count.Number))));
    }

    private static FormulaValue Change(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<string, string> change) =>
        TextOf(evaluator, arguments[0]) is { IsError: false } text ? FormulaValue.Of(change(text.Text)) : TextOf(evaluator, arguments[0]);

    // Each letter after a non-letter is capital, the others small.
    private static string Proper(string text)
    {
        var builder = new StringBuilder(text.Length);
        var afterLetter = false;
        foreach (var c in text)
        {
            builder.Append(afterLetter ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
            afterLetter = char.IsLetter(c);
        }

        return builder.ToString();
    }

    private static FormulaValue Repeat(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var text = TextOf(evaluator, arguments[0]);
        var count = Number(evaluator, arguments[1]);
        if (text.IsError || count.IsError)
        {
            return text.IsError ? text : count;
        }

        var times = Math.Truncate(count.Number);
        return times < 0 || text.Text.Length * times > 32767 ? FormulaValue.ValueError : FormulaValue.Of(string.Concat(Enumerable.Repeat(text.Text, (int)times)));
    }

    // SUBSTITUTE(text, old, new, [instance]): every occurrence, or only the given one.
    private static FormulaValue Substitute(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var text = TextOf(evaluator, arguments[0]);
        var old = TextOf(evaluator, arguments[1]);
        var replacement = TextOf(evaluator, arguments[2]);
        var instance = arguments.Count > 3 ? Number(evaluator, arguments[3]) : FormulaValue.Blank;
        var error = new[] { text, old, replacement, instance }.FirstOrDefault(v => v.IsError);
        if (error.IsError)
        {
            return error;
        }

        if (old.Text.Length == 0)
        {
            return text;
        }

        if (instance.Kind == ValueKind.Blank)
        {
            return FormulaValue.Of(text.Text.Replace(old.Text, replacement.Text, StringComparison.Ordinal));
        }

        if (instance.Number < 1)
        {
            return FormulaValue.ValueError;
        }

        var at = -1;
        for (var n = 0; n < (int)instance.Number; n++)
        {
            at = text.Text.IndexOf(old.Text, at + 1, StringComparison.Ordinal);
            if (at < 0)
            {
                return text;
            }
        }

        return FormulaValue.Of(string.Concat(text.Text.AsSpan(0, at), replacement.Text, text.Text.AsSpan(at + old.Text.Length)));
    }

    // REPLACE(text, start, count, new): characters by position.
    private static FormulaValue Replace(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var text = TextOf(evaluator, arguments[0]);
        var start = Number(evaluator, arguments[1]);
        var count = Number(evaluator, arguments[2]);
        var replacement = TextOf(evaluator, arguments[3]);
        var error = new[] { text, start, count, replacement }.FirstOrDefault(v => v.IsError);
        if (error.IsError)
        {
            return error;
        }

        if (start.Number < 1 || count.Number < 0)
        {
            return FormulaValue.ValueError;
        }

        var from = (int)Math.Min(text.Text.Length, Math.Truncate(start.Number) - 1);
        var length = (int)Math.Min(text.Text.Length - from, Math.Truncate(count.Number));
        return FormulaValue.Of(string.Concat(text.Text.AsSpan(0, from), replacement.Text, text.Text.AsSpan(from + length)));
    }

    // FIND is case-sensitive without wildcards; SEARCH ignores case and accepts * and ?.
    private static FormulaValue Find(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, bool ignoreCase)
    {
        var sought = TextOf(evaluator, arguments[0]);
        var within = TextOf(evaluator, arguments[1]);
        var start = Number(evaluator, arguments, 2, 1);
        var error = new[] { sought, within, start }.FirstOrDefault(v => v.IsError);
        if (error.IsError)
        {
            return error;
        }

        var from = (int)Math.Truncate(start.Number) - 1;
        if (from < 0 || from > within.Text.Length)
        {
            return FormulaValue.ValueError;
        }

        if (!ignoreCase || sought.Text.IndexOfAny(['*', '?', '~']) < 0)
        {
            var at = within.Text.IndexOf(sought.Text, from, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            return at < 0 ? FormulaValue.ValueError : FormulaValue.Of(at + 1);
        }

        var match = Wildcard(sought.Text, whole: false).Match(within.Text, from);
        return match.Success ? FormulaValue.Of(match.Index + 1) : FormulaValue.ValueError;
    }

    // TEXT(value, format) formats as a cell with that number format would show it (invariant separators).
    private static FormulaValue Format(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var value = evaluator.Evaluate(arguments[0]).Scalar();
        var format = TextOf(evaluator, arguments[1]);
        if (value.IsError || format.IsError)
        {
            return value.IsError ? value : format;
        }

        var number = value.ToNumber();
        return number.IsError
            ? FormulaValue.Of(NumberFormatter.FormatText(value.ToText().Text, format.Text))
            : FormulaValue.Of(NumberFormatter.Format(number.Number, format.Text, CultureInfo.InvariantCulture, evaluator.Date1904));
    }
}
