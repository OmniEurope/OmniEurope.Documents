// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Excel.Formulas.FormulaFunctions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Lookup and reference functions (ECMA-376 part 1, §18.17.7). An exact match compares text without regard to case
/// and accepts wildcards; an approximate match expects the lookup values sorted and takes the last one not
/// greater than the value sought.
/// </summary>
internal static class LookupFunctions
{
    public static void Register(Dictionary<string, FormulaFunction> registry)
    {
        registry["VLOOKUP"] = new(3, 4, (e, a) => TableLookup(e, a, vertical: true));
        registry["HLOOKUP"] = new(3, 4, (e, a) => TableLookup(e, a, vertical: false));
        registry["MATCH"] = new(2, 3, Match);
        registry["INDEX"] = new(2, 3, Index);
        registry["XLOOKUP"] = new(3, 5, XLookup);
        registry["ROW"] = new(0, 1, (e, a) => Position(e, a, area => area.Top, () => e.Current.Row));
        registry["COLUMN"] = new(0, 1, (e, a) => Position(e, a, area => area.Left, () => e.Current.Column));
        registry["ROWS"] = new(1, 1, (e, a) => Size(e, a[0], area => area.Rows, grid => grid.GetLength(0)));
        registry["COLUMNS"] = new(1, 1, (e, a) => Size(e, a[0], area => area.Columns, grid => grid.GetLength(1)));
    }

    private static FormulaValue TableLookup(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, bool vertical)
    {
        var sought = evaluator.Evaluate(arguments[0]).Scalar();
        var table = Grid(evaluator.Evaluate(arguments[1]));
        var index = Number(evaluator, arguments[2]);
        var approximate = arguments.Count < 4 || evaluator.Evaluate(arguments[3]).ToBoolean() is { IsError: false, Boolean: true };
        if (sought.IsError || index.IsError)
        {
            return sought.IsError ? sought : index;
        }

        var offset = (int)Math.Truncate(index.Number) - 1;
        var lines = vertical ? table.GetLength(0) : table.GetLength(1);
        var width = vertical ? table.GetLength(1) : table.GetLength(0);
        if (offset < 0)
        {
            return FormulaValue.ValueError;
        }

        if (offset >= width)
        {
            return FormulaValue.Ref;
        }

        var keys = Enumerable.Range(0, lines).Select(i => vertical ? table[i, 0] : table[0, i]).ToList();
        var found = approximate ? Approximate(keys, sought, ascending: true) : Exact(keys, sought);
        return found < 0 ? FormulaValue.NotAvailable : Blank(vertical ? table[found, offset] : table[offset, found]);
    }

    // A blank cell found by a lookup gives 0, as in Excel.
    private static FormulaValue Blank(FormulaValue value) => value.Kind == ValueKind.Blank ? FormulaValue.Of(0) : value;

    // MATCH(value, array, [type]): 1 the last not greater (sorted ascending), 0 exact, -1 the last not less (descending).
    private static FormulaValue Match(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var sought = evaluator.Evaluate(arguments[0]).Scalar();
        var array = Grid(evaluator.Evaluate(arguments[1]));
        var type = Number(evaluator, arguments, 2, 1);
        if (sought.IsError || type.IsError)
        {
            return sought.IsError ? sought : type;
        }

        if (array.GetLength(0) > 1 && array.GetLength(1) > 1)
        {
            return FormulaValue.NotAvailable;
        }

        var values = array.Cast<FormulaValue>().ToList();
        var found = type.Number switch
        {
            0 => Exact(values, sought),
            > 0 => Approximate(values, sought, ascending: true),
            _ => Approximate(values, sought, ascending: false),
        };
        return found < 0 ? FormulaValue.NotAvailable : FormulaValue.Of(found + 1);
    }

    // INDEX(array, row, [column]): 1-based; a single row or column takes one index for its length.
    private static FormulaValue Index(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var array = Grid(evaluator.Evaluate(arguments[0]));
        var row = Number(evaluator, arguments[1]);
        var column = Number(evaluator, arguments, 2, 0);
        if (row.IsError || column.IsError)
        {
            return row.IsError ? row : column;
        }

        var (r, c) = ((int)Math.Truncate(row.Number), (int)Math.Truncate(column.Number));
        if (arguments.Count == 2 && array.GetLength(0) == 1)
        {
            (r, c) = (1, r);
        }

        if (r < 0 || c < 0)
        {
            return FormulaValue.ValueError;
        }

        (r, c) = (Math.Max(r, 1), Math.Max(c, 1));
        return r <= array.GetLength(0) && c <= array.GetLength(1) ? Blank(array[r - 1, c - 1]) : FormulaValue.Ref;
    }

    // XLOOKUP(value, lookup, return, [if not found], [mode]): mode 0 exact, -1 exact or next smaller, 1 exact or next larger, 2 wildcards.
    private static FormulaValue XLookup(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var sought = evaluator.Evaluate(arguments[0]).Scalar();
        var keys = Grid(evaluator.Evaluate(arguments[1]));
        var results = Grid(evaluator.Evaluate(arguments[2]));
        var mode = Number(evaluator, arguments, 4, 0);
        if (sought.IsError || mode.IsError)
        {
            return sought.IsError ? sought : mode;
        }

        var vertical = keys.GetLength(1) == 1;
        var list = keys.Cast<FormulaValue>().ToList();
        var length = vertical ? results.GetLength(0) : results.GetLength(1);
        if (keys.GetLength(0) > 1 && keys.GetLength(1) > 1 || length != list.Count)
        {
            return FormulaValue.ValueError;
        }

        var found = mode.Number switch
        {
            0 => list.FindIndex(k => Same(k, sought)),
            2 => Exact(list, sought),
            _ => Nearest(list, sought, larger: mode.Number > 0),
        };
        if (found < 0)
        {
            return arguments.Count > 3 ? evaluator.Evaluate(arguments[3]) : FormulaValue.NotAvailable;
        }

        return Blank(vertical ? results[found, 0] : results[0, found]);
    }

    private static FormulaValue Position(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, Func<FormulaArea, int> corner, Func<int> current) =>
        arguments.Count == 0 ? FormulaValue.Of(current())
            : arguments[0] is ReferenceNode reference ? FormulaValue.Of(corner(reference.Area)) : FormulaValue.ValueError;

    private static FormulaValue Size(FormulaEvaluator evaluator, FormulaNode argument, Func<FormulaArea, int> ofArea, Func<FormulaValue[,], int> ofArray) =>
        argument is ReferenceNode reference ? FormulaValue.Of(ofArea(reference.Area)) : FormulaValue.Of(ofArray(Grid(evaluator.Evaluate(argument))));

    // Equal values of one kind; text without regard to case.
    private static bool Same(FormulaValue a, FormulaValue b) =>
        a.Kind == b.Kind && FormulaEvaluator.CompareValues(a, b) == 0;

    private static int Exact(List<FormulaValue> values, FormulaValue sought)
    {
        if (sought.Kind == ValueKind.Text && sought.Text.IndexOfAny(['*', '?', '~']) >= 0)
        {
            var pattern = Wildcard(sought.Text);
            return values.FindIndex(v => v.Kind == ValueKind.Text && pattern.IsMatch(v.Text));
        }

        return values.FindIndex(v => Same(v, sought));
    }

    // Binary search as Excel does it on sorted data: the last position whose value of the same kind is not past the one sought.
    private static int Approximate(List<FormulaValue> values, FormulaValue sought, bool ascending)
    {
        var found = -1;
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value.Kind != sought.Kind)
            {
                continue;
            }

            var order = FormulaEvaluator.CompareValues(value, sought);
            if (order == 0)
            {
                return i;
            }

            if (ascending ? order < 0 : order > 0)
            {
                found = i;
            }
            else
            {
                break;
            }
        }

        return found;
    }

    // XLOOKUP's next smaller or larger value: any order, the closest one wins.
    private static int Nearest(List<FormulaValue> values, FormulaValue sought, bool larger)
    {
        var exact = values.FindIndex(v => Same(v, sought));
        if (exact >= 0)
        {
            return exact;
        }

        var best = -1;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].Kind != sought.Kind || (FormulaEvaluator.CompareValues(values[i], sought) > 0) != larger)
            {
                continue;
            }

            if (best < 0 || (FormulaEvaluator.CompareValues(values[i], values[best]) < 0) == larger)
            {
                best = i;
            }
        }

        return best;
    }
}
