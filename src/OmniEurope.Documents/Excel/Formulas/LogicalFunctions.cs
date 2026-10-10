// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Excel.Formulas.FormulaFunctions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>Logical and information functions (ECMA-376 part 1, §18.17.7). Only the branch returned is evaluated.</summary>
internal static class LogicalFunctions
{
    private const int Many = 255;

    public static void Register(Dictionary<string, FormulaFunction> registry)
    {
        registry["IF"] = new(1, 3, If);
        registry["IFS"] = new(2, Many, Ifs);
        registry["IFERROR"] = new(2, 2, (e, a) => e.Evaluate(a[0]) is { IsError: true } ? e.Evaluate(a[1]) : e.Evaluate(a[0]));
        registry["IFNA"] = new(2, 2, (e, a) => e.Evaluate(a[0]) is { IsError: true, Text: "#N/A" } ? e.Evaluate(a[1]) : e.Evaluate(a[0]));
        registry["AND"] = new(1, Many, (e, a) => Combine(e, a, values => values.All(v => v)));
        registry["OR"] = new(1, Many, (e, a) => Combine(e, a, values => values.Any(v => v)));
        registry["XOR"] = new(1, Many, (e, a) => Combine(e, a, values => values.Count(v => v) % 2 == 1));
        registry["NOT"] = new(1, 1, (e, a) => e.Evaluate(a[0]).ToBoolean() is { IsError: false } b ? FormulaValue.Of(!b.Boolean) : e.Evaluate(a[0]).ToBoolean());
        registry["TRUE"] = new(0, 0, (_, _) => FormulaValue.True);
        registry["FALSE"] = new(0, 0, (_, _) => FormulaValue.False);
        registry["CHOOSE"] = new(2, Many, Choose);
        registry["SWITCH"] = new(3, Many, Switch);
        registry["NA"] = new(0, 0, (_, _) => FormulaValue.NotAvailable);
        registry["ISBLANK"] = new(1, 1, (e, a) => Is(e, a, v => v.Kind == ValueKind.Blank));
        registry["ISNUMBER"] = new(1, 1, (e, a) => Is(e, a, v => v.Kind == ValueKind.Number));
        registry["ISTEXT"] = new(1, 1, (e, a) => Is(e, a, v => v.Kind == ValueKind.Text));
        registry["ISNONTEXT"] = new(1, 1, (e, a) => Is(e, a, v => v.Kind != ValueKind.Text));
        registry["ISLOGICAL"] = new(1, 1, (e, a) => Is(e, a, v => v.Kind == ValueKind.Boolean));
        registry["ISERROR"] = new(1, 1, (e, a) => Is(e, a, v => v.IsError));
        registry["ISERR"] = new(1, 1, (e, a) => Is(e, a, v => v.IsError && v.Text != "#N/A"));
        registry["ISNA"] = new(1, 1, (e, a) => Is(e, a, v => v.IsError && v.Text == "#N/A"));
        registry["ISEVEN"] = new(1, 1, (e, a) => Parity(e, a, even: true));
        registry["ISODD"] = new(1, 1, (e, a) => Parity(e, a, even: false));
    }

    private static FormulaValue If(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var condition = evaluator.Evaluate(arguments[0]).ToBoolean();
        if (condition.IsError)
        {
            return condition;
        }

        // A missing branch is FALSE; an empty one (IF(A1,,1)) is 0.
        var index = condition.Boolean ? 1 : 2;
        return index < arguments.Count ? Blank(evaluator.Evaluate(arguments[index])) : FormulaValue.False;
    }

    private static FormulaValue Blank(FormulaValue value) => value.Kind == ValueKind.Blank ? FormulaValue.Of(0) : value;

    private static FormulaValue Ifs(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        if (arguments.Count % 2 != 0)
        {
            return FormulaValue.ValueError;
        }

        for (var i = 0; i < arguments.Count; i += 2)
        {
            var condition = evaluator.Evaluate(arguments[i]).ToBoolean();
            if (condition.IsError || condition.Boolean)
            {
                return condition.IsError ? condition : Blank(evaluator.Evaluate(arguments[i + 1]));
            }
        }

        return FormulaValue.NotAvailable;
    }

    // Booleans of every argument (text in ranges is skipped, text given directly is #VALUE!); none at all is #VALUE!.
    private static FormulaValue Combine(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<List<bool>, bool> rule)
    {
        var values = new List<bool>();
        foreach (var (value, direct) in Elements(evaluator, arguments))
        {
            if (value.IsError)
            {
                return value;
            }

            if (value.Kind is ValueKind.Boolean or ValueKind.Number)
            {
                values.Add(value.Number != 0);
            }
            else if (direct && value.Kind == ValueKind.Text)
            {
                var boolean = value.ToBoolean();
                if (boolean.IsError)
                {
                    return boolean;
                }

                values.Add(boolean.Boolean);
            }
        }

        return values.Count == 0 ? FormulaValue.ValueError : FormulaValue.Of(rule(values));
    }

    private static FormulaValue Choose(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var index = Number(evaluator, arguments[0]);
        if (index.IsError)
        {
            return index;
        }

        var position = (int)Math.Truncate(index.Number);
        return position >= 1 && position < arguments.Count ? evaluator.Evaluate(arguments[position]) : FormulaValue.ValueError;
    }

    // SWITCH(value, match1, result1, ..., [default]).
    private static FormulaValue Switch(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var value = evaluator.Evaluate(arguments[0]).Scalar();
        if (value.IsError)
        {
            return value;
        }

        var i = 1;
        for (; i + 1 < arguments.Count; i += 2)
        {
            var candidate = evaluator.Evaluate(arguments[i]).Scalar();
            if (candidate.IsError)
            {
                return candidate;
            }

            if (FormulaEvaluator.CompareValues(value, candidate) == 0 && value.Kind == candidate.Kind)
            {
                return evaluator.Evaluate(arguments[i + 1]);
            }
        }

        return i < arguments.Count ? evaluator.Evaluate(arguments[i]) : FormulaValue.NotAvailable;
    }

    private static FormulaValue Is(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<FormulaValue, bool> test) =>
        FormulaEvaluator.Map(evaluator.Evaluate(arguments[0]), v => FormulaValue.Of(test(v)));

    private static FormulaValue Parity(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, bool even)
    {
        var value = Number(evaluator, arguments[0]);
        return value.IsError ? value : FormulaValue.Of((Math.Abs(Math.Truncate(value.Number)) % 2 == 0) == even);
    }
}
