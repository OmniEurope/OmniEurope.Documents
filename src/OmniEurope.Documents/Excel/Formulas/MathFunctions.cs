// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Excel.Formulas.FormulaFunctions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>Mathematical and statistical functions (ECMA-376 part 1, §18.17.7).</summary>
internal static class MathFunctions
{
    private const int Many = 255;

    public static void Register(Dictionary<string, FormulaFunction> registry)
    {
        registry["SUM"] = new(1, Many, (e, a) => Aggregate(e, a, n => n.Sum()));
        registry["PRODUCT"] = new(1, Many, (e, a) => Aggregate(e, a, n => n.Aggregate(1.0, (x, y) => x * y)));
        registry["AVERAGE"] = new(1, Many, (e, a) => Aggregate(e, a, n => n.Count == 0 ? double.NaN : n.Average(), FormulaValue.Div0));
        registry["MIN"] = new(1, Many, (e, a) => Aggregate(e, a, n => n.Count == 0 ? 0 : n.Min()));
        registry["MAX"] = new(1, Many, (e, a) => Aggregate(e, a, n => n.Count == 0 ? 0 : n.Max()));
        registry["COUNT"] = new(1, Many, (e, a) => FormulaValue.Of(Elements(e, a).Count(v => v.Value.Kind == ValueKind.Number || (v.Direct && !v.Value.ToNumber().IsError && v.Value.Kind != ValueKind.Blank))));
        registry["COUNTA"] = new(1, Many, (e, a) => FormulaValue.Of(Elements(e, a).Count(v => v.Value.Kind != ValueKind.Blank || v.Direct)));
        registry["COUNTBLANK"] = new(1, 1, (e, a) => FormulaValue.Of(Grid(e.Evaluate(a[0])).Cast<FormulaValue>().Count(v => v.Kind == ValueKind.Blank || (v.Kind == ValueKind.Text && v.Text.Length == 0))));
        registry["COUNTIF"] = new(2, 2, (e, a) => Counted(Matches(e, [(a[0], a[1])])));
        registry["COUNTIFS"] = new(2, Many, (e, a) => a.Count % 2 == 0 ? Counted(Matches(e, Pairs(a, 0))) : FormulaValue.ValueError);
        registry["SUMIF"] = new(2, 3, (e, a) => ConditionalSum(e, a.Count == 3 ? a[2] : a[0], [(a[0], a[1])], average: false));
        registry["SUMIFS"] = new(3, Many, (e, a) => a.Count % 2 == 1 ? ConditionalSum(e, a[0], Pairs(a, 1), average: false) : FormulaValue.ValueError);
        registry["AVERAGEIF"] = new(2, 3, (e, a) => ConditionalSum(e, a.Count == 3 ? a[2] : a[0], [(a[0], a[1])], average: true));
        registry["AVERAGEIFS"] = new(3, Many, (e, a) => a.Count % 2 == 1 ? ConditionalSum(e, a[0], Pairs(a, 1), average: true) : FormulaValue.ValueError);
        registry["SUMPRODUCT"] = new(1, Many, SumProduct);
        registry["ROUND"] = new(2, 2, (e, a) => Round(e, a, MidpointRounding.AwayFromZero));
        registry["ROUNDUP"] = new(2, 2, (e, a) => Round(e, a, MidpointRounding.AwayFromZero, Math.Ceiling));
        registry["ROUNDDOWN"] = new(2, 2, (e, a) => Round(e, a, MidpointRounding.ToZero, Math.Floor));
        registry["TRUNC"] = new(1, 2, (e, a) => Round(e, a, MidpointRounding.ToZero, Math.Floor));
        registry["INT"] = new(1, 1, (e, a) => Unary(e, a, Math.Floor));
        registry["ABS"] = new(1, 1, (e, a) => Unary(e, a, Math.Abs));
        registry["SIGN"] = new(1, 1, (e, a) => Unary(e, a, x => Math.Sign(x)));
        registry["SQRT"] = new(1, 1, (e, a) => Unary(e, a, x => x < 0 ? double.NaN : Math.Sqrt(x)));
        registry["EXP"] = new(1, 1, (e, a) => Unary(e, a, Math.Exp));
        registry["LN"] = new(1, 1, (e, a) => Unary(e, a, x => x <= 0 ? double.NaN : Math.Log(x)));
        registry["LOG10"] = new(1, 1, (e, a) => Unary(e, a, x => x <= 0 ? double.NaN : Math.Log10(x)));
        registry["LOG"] = new(1, 2, (e, a) => Binary(e, a[0], a.Count > 1 ? a[1] : null, 10, (x, b) => x <= 0 || b <= 0 || b == 1 ? double.NaN : Math.Log(x, b)));
        registry["POWER"] = new(2, 2, (e, a) => Binary(e, a[0], a[1], 0, (x, y) => x == 0 && y == 0 ? double.NaN : Math.Pow(x, y)));
        registry["MOD"] = new(2, 2, Mod);
        registry["PI"] = new(0, 0, (_, _) => FormulaValue.Of(Math.PI));
        registry["CEILING"] = new(1, 2, (e, a) => Multiple(e, a, Math.Ceiling));
        registry["FLOOR"] = new(1, 2, (e, a) => Multiple(e, a, Math.Floor));
    }

    private static FormulaValue Aggregate(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<List<double>, double> compute, FormulaValue? onNaN = null)
    {
        var (numbers, error) = Numbers(evaluator, arguments);
        if (error is { } failure)
        {
            return failure;
        }

        var result = compute(numbers);
        return double.IsNaN(result) && onNaN is { } empty ? empty : FormulaValue.Of(result);
    }

    private static List<(FormulaNode Range, FormulaNode Criterion)> Pairs(IReadOnlyList<FormulaNode> arguments, int start)
    {
        var pairs = new List<(FormulaNode, FormulaNode)>();
        for (var i = start; i + 1 < arguments.Count; i += 2)
        {
            pairs.Add((arguments[i], arguments[i + 1]));
        }

        return pairs;
    }

    // For each cell of the ranges (all of one size), whether it meets every criterion; null when sizes differ.
    private static bool[]? Matches(FormulaEvaluator evaluator, List<(FormulaNode Range, FormulaNode Criterion)> pairs)
    {
        bool[]? result = null;
        foreach (var (rangeNode, criterionNode) in pairs)
        {
            var range = Grid(evaluator.Evaluate(rangeNode)).Cast<FormulaValue>().ToList();
            var test = Criterion(evaluator.Evaluate(criterionNode));
            result ??= Enumerable.Repeat(true, range.Count).ToArray();
            if (range.Count != result.Length)
            {
                return null;
            }

            for (var i = 0; i < range.Count; i++)
            {
                result[i] &= test(range[i]);
            }
        }

        return result;
    }

    private static FormulaValue Counted(bool[]? matches) => matches is null ? FormulaValue.ValueError : FormulaValue.Of(matches.Count(m => m));

    private static FormulaValue ConditionalSum(FormulaEvaluator evaluator, FormulaNode sumNode, List<(FormulaNode, FormulaNode)> pairs, bool average)
    {
        var matches = Matches(evaluator, pairs);
        var values = Grid(evaluator.Evaluate(sumNode)).Cast<FormulaValue>().ToList();
        if (matches is null || values.Count != matches.Length)
        {
            return FormulaValue.ValueError;
        }

        var selected = values.Where((_, i) => matches[i]).ToList();
        if (selected.Find(v => v.IsError) is { Kind: ValueKind.Error } error)
        {
            return error;
        }

        var numbers = selected.Where(v => v.Kind == ValueKind.Number).Select(v => v.Number).ToList();
        if (average)
        {
            return numbers.Count == 0 ? FormulaValue.Div0 : FormulaValue.Of(numbers.Average());
        }

        return FormulaValue.Of(numbers.Sum());
    }

    // Arrays of one size multiplied element by element and summed; text and blanks count as 0.
    private static FormulaValue SumProduct(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var arrays = arguments.Select(a => Grid(evaluator.Evaluate(a))).ToList();
        if (arrays.Any(a => a.GetLength(0) != arrays[0].GetLength(0) || a.GetLength(1) != arrays[0].GetLength(1)))
        {
            return FormulaValue.ValueError;
        }

        var total = 0.0;
        for (var r = 0; r < arrays[0].GetLength(0); r++)
        {
            for (var c = 0; c < arrays[0].GetLength(1); c++)
            {
                var product = 1.0;
                foreach (var array in arrays)
                {
                    var value = array[r, c];
                    if (value.IsError)
                    {
                        return value;
                    }

                    product *= value.Kind == ValueKind.Number ? value.Number : 0;
                }

                total += product;
            }
        }

        return FormulaValue.Of(total);
    }

    private static FormulaValue Unary(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<double, double> compute) =>
        FormulaEvaluator.Map(evaluator.Evaluate(arguments[0]), v => v.ToNumber() is { IsError: false } n ? FormulaValue.Of(compute(n.Number)) : v.ToNumber());

    private static FormulaValue Binary(FormulaEvaluator evaluator, FormulaNode first, FormulaNode? second, double fallback, Func<double, double, double> compute)
    {
        var x = Number(evaluator, first);
        var y = second is null ? FormulaValue.Of(fallback) : Number(evaluator, second);
        return x.IsError ? x : y.IsError ? y : FormulaValue.Of(compute(x.Number, y.Number));
    }

    // The remainder with the sign of the divisor: MOD(-3, 2) is 1.
    private static FormulaValue Mod(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var x = Number(evaluator, arguments[0]);
        var y = Number(evaluator, arguments[1]);
        if (x.IsError || y.IsError)
        {
            return x.IsError ? x : y;
        }

        return y.Number == 0 ? FormulaValue.Div0 : FormulaValue.Of(x.Number - (y.Number * Math.Floor(x.Number / y.Number)));
    }

    /// <summary>
    /// Rounds to a number of digits (negative digits round left of the point). The value is rounded as the decimal
    /// it shows (2.675 is 2.68), not as its binary approximation.
    /// </summary>
    private static FormulaValue Round(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, MidpointRounding mode, Func<double, double>? whole = null)
    {
        var x = Number(evaluator, arguments[0]);
        var digits = Number(evaluator, arguments, 1, 0);
        if (x.IsError || digits.IsError)
        {
            return x.IsError ? x : digits;
        }

        var places = (int)Math.Clamp(Math.Truncate(digits.Number), -308, 308);
        var scale = Math.Pow(10, places);

        // Past 15 significant digits a double holds no more decimals to round.
        if (Math.Abs(x.Number) * scale >= 1e15 || places > 28)
        {
            return x;
        }

        if (whole is not null)
        {
            // ROUNDUP and ROUNDDOWN move away from or towards zero whatever the next digit.
            var scaled = (double)Math.Round((decimal)(Math.Abs(x.Number) * scale), 9);
            var rounded = (mode == MidpointRounding.AwayFromZero ? Math.Ceiling(scaled) : Math.Floor(scaled)) / scale;
            return FormulaValue.Of(Math.CopySign(rounded, x.Number));
        }

        if (places >= 0)
        {
            return FormulaValue.Of((double)Math.Round((decimal)x.Number, places, mode));
        }

        var factor = (decimal)Math.Pow(10, -places);
        return FormulaValue.Of((double)(Math.Round((decimal)x.Number / factor, 0, mode) * factor));
    }

    // CEILING and FLOOR to a multiple of significance (1 when omitted), which must share the number's sign.
    private static FormulaValue Multiple(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<double, double> direction)
    {
        var x = Number(evaluator, arguments[0]);
        var significance = Number(evaluator, arguments, 1, 1);
        if (x.IsError || significance.IsError)
        {
            return x.IsError ? x : significance;
        }

        if (significance.Number == 0)
        {
            return x.Number == 0 ? FormulaValue.Of(0) : FormulaValue.Div0;
        }

        if (x.Number > 0 && significance.Number < 0)
        {
            return FormulaValue.Num;
        }

        var quotient = (double)Math.Round((decimal)(x.Number / significance.Number), 9);
        return FormulaValue.Of(direction(quotient) * significance.Number);
    }
}
