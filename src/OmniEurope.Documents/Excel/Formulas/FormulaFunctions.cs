// SPDX-License-Identifier: EUPL-1.2
using System.Text.RegularExpressions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>A worksheet function: its argument count and how it computes from its unevaluated arguments.</summary>
internal sealed record FormulaFunction(int Min, int Max, Func<FormulaEvaluator, IReadOnlyList<FormulaNode>, FormulaValue> Compute);

/// <summary>
/// The functions the engine computes, and the helpers they share: number lists as SUM reads them, criteria as
/// COUNTIF reads them. Arguments arrive unevaluated, so IF and IFERROR evaluate only the branch they return.
/// </summary>
internal static class FormulaFunctions
{
    private static readonly Dictionary<string, FormulaFunction> Registry = Build();

    public static IEnumerable<string> Names => Registry.Keys;

    public static bool IsKnown(string name) => Registry.ContainsKey(name);

    public static FormulaValue Call(FormulaEvaluator evaluator, CallNode call) =>
        Registry.TryGetValue(call.Name, out var function)
            ? call.Arguments.Count < function.Min || call.Arguments.Count > function.Max ? FormulaValue.ValueError : function.Compute(evaluator, call.Arguments)
            : FormulaValue.Name;

    private static Dictionary<string, FormulaFunction> Build()
    {
        var registry = new Dictionary<string, FormulaFunction>(StringComparer.Ordinal);
        MathFunctions.Register(registry);
        LogicalFunctions.Register(registry);
        LookupFunctions.Register(registry);
        TextFunctions.Register(registry);
        DateFunctions.Register(registry);
        return registry;
    }

    /// <summary>
    /// The values of the arguments for a function that reads lists (SUM, COUNT): every element of a range or an
    /// array, then each direct argument; <c>Direct</c> tells which values came from a direct argument.
    /// </summary>
    public static IEnumerable<(FormulaValue Value, bool Direct)> Elements(FormulaEvaluator evaluator, IEnumerable<FormulaNode> arguments)
    {
        foreach (var argument in arguments)
        {
            var value = evaluator.Evaluate(argument);
            if (value.Kind == ValueKind.Array)
            {
                foreach (var element in value.Array)
                {
                    yield return (element, false);
                }
            }
            else
            {
                // A single-cell reference is read like a range: its text and booleans are not numbers.
                yield return (value, argument is not ReferenceNode);
            }
        }
    }

    /// <summary>
    /// The numbers SUM, AVERAGE, MIN and MAX use: numbers of ranges and arrays (their text, booleans and blanks
    /// are skipped), and direct arguments converted (text that reads as a number, TRUE as 1). The first error met
    /// is returned instead.
    /// </summary>
    public static (List<double> Numbers, FormulaValue? Error) Numbers(FormulaEvaluator evaluator, IEnumerable<FormulaNode> arguments)
    {
        var numbers = new List<double>();
        foreach (var (value, direct) in Elements(evaluator, arguments))
        {
            if (value.IsError)
            {
                return (numbers, value);
            }

            if (value.Kind == ValueKind.Number)
            {
                numbers.Add(value.Number);
            }
            else if (direct && value.Kind is ValueKind.Boolean or ValueKind.Text or ValueKind.Blank)
            {
                var number = value.ToNumber();
                if (number.IsError)
                {
                    return (numbers, number);
                }

                numbers.Add(number.Number);
            }
        }

        return (numbers, null);
    }

    /// <summary>One number argument, or the error it gives.</summary>
    public static FormulaValue Number(FormulaEvaluator evaluator, FormulaNode argument) => evaluator.Evaluate(argument).ToNumber();

    /// <summary>An optional number argument (a blank or missing argument gives <paramref name="fallback"/>).</summary>
    public static FormulaValue Number(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, int index, double fallback)
    {
        if (index >= arguments.Count)
        {
            return FormulaValue.Of(fallback);
        }

        var value = evaluator.Evaluate(arguments[index]).Scalar();
        return value.Kind == ValueKind.Blank ? FormulaValue.Of(fallback) : value.ToNumber();
    }

    /// <summary>One text argument, or the error it gives.</summary>
    public static FormulaValue TextOf(FormulaEvaluator evaluator, FormulaNode argument) => evaluator.Evaluate(argument).ToText();

    /// <summary>The elements of an argument as a two-dimensional array (a single value is a 1 x 1 array).</summary>
    public static FormulaValue[,] Grid(FormulaValue value) => value.Kind == ValueKind.Array ? value.Array : new[,] { { value } };

    /// <summary>
    /// A test from a criterion as COUNTIF, SUMIF and their kin read it: <c>"&gt;5"</c>, <c>"&lt;&gt;x"</c>, <c>"=a*"</c>
    /// (<c>*</c> and <c>?</c> are wildcards, <c>~</c> escapes them), or a value that must be equal. Text is
    /// compared without regard to case; a number criterion matches numbers and text that reads as that number.
    /// </summary>
    public static Func<FormulaValue, bool> Criterion(FormulaValue criterion)
    {
        criterion = criterion.Scalar();
        if (criterion.Kind != ValueKind.Text)
        {
            return value => Equal(value, criterion);
        }

        var text = criterion.Text;
        var op = new[] { "<=", ">=", "<>", "=", "<", ">" }.FirstOrDefault(o => text.StartsWith(o, StringComparison.Ordinal)) ?? string.Empty;
        var operand = text[op.Length..];
        FormulaValue target = FormulaValue.TryParseNumber(operand, out var number) ? FormulaValue.Of(number)
            : operand.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? FormulaValue.True
            : operand.Equals("FALSE", StringComparison.OrdinalIgnoreCase) ? FormulaValue.False
            : FormulaValue.Of(operand);
        return op switch
        {
            "" or "=" when operand.Length == 0 => value => value.Kind == ValueKind.Blank || (value.Kind == ValueKind.Text && value.Text.Length == 0),
            "<>" when operand.Length == 0 => value => !(value.Kind == ValueKind.Blank || (value.Kind == ValueKind.Text && value.Text.Length == 0)),
            "" or "=" => value => Equal(value, target),
            "<>" => value => !Equal(value, target),
            _ => value => Ordered(op, value, target),
        };
    }

    private static bool Equal(FormulaValue value, FormulaValue target)
    {
        if (target.Kind == ValueKind.Number)
        {
            return value.Kind == ValueKind.Number ? value.Number == target.Number
                : value.Kind == ValueKind.Text && FormulaValue.TryParseNumber(value.Text, out var number) && number == target.Number;
        }

        if (target.Kind == ValueKind.Text)
        {
            return value.Kind == ValueKind.Text && Wildcard(target.Text).IsMatch(value.Text);
        }

        return value.Kind == target.Kind && value.Number == target.Number && (target.Kind != ValueKind.Error || value.Text == target.Text);
    }

    // < > <= >= compare a number with numbers only, a text with texts only.
    private static bool Ordered(string op, FormulaValue value, FormulaValue target)
    {
        if (value.Kind != target.Kind || value.Kind is not (ValueKind.Number or ValueKind.Text))
        {
            return false;
        }

        var order = FormulaEvaluator.CompareValues(value, target);
        return op switch
        {
            "<" => order < 0,
            ">" => order > 0,
            "<=" => order <= 0,
            _ => order >= 0,
        };
    }

    /// <summary>A case-insensitive pattern, of the whole text or found anywhere in it, where <c>*</c> is any run,
    /// <c>?</c> any character and <c>~</c> escapes.</summary>
    public static Regex Wildcard(string pattern, bool whole = true)
    {
        var builder = new System.Text.StringBuilder(whole ? "^" : string.Empty);
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '~' && i + 1 < pattern.Length && pattern[i + 1] is '*' or '?' or '~')
            {
                builder.Append(Regex.Escape(pattern[++i].ToString()));
            }
            else
            {
                builder.Append(c switch { '*' => ".*", '?' => ".", _ => Regex.Escape(c.ToString()) });
            }
        }

        return new Regex((whole ? builder.Append('$') : builder).ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
    }
}
