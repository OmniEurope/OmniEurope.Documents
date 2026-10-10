// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Recalculates every formula of a workbook: each formula is parsed, its references give the formula cells it
/// depends on, and cells are computed in dependency order (no recursion, so chains of any length are safe). A
/// cycle is refused. A formula the engine cannot compute (unknown function, defined name, structured or dynamic
/// reference, syntax it does not read) keeps its stored result, and is reported.
/// </summary>
internal sealed class XlsxCalculator(XlsxWorkbook workbook, DateTime now)
{
    // Functions whose references are built at run time: their dependencies cannot be known in advance.
    private static readonly HashSet<string> Dynamic = new(StringComparer.Ordinal) { "INDIRECT", "OFFSET" };

    public XlsxRecalculation Run()
    {
        var formulas = new List<(XlsxCell Cell, FormulaNode Node)>();
        var unsupported = new List<string>();
        foreach (var cell in workbook.Worksheets.SelectMany(s => s.Cells).Where(c => c.Formula is not null).ToList())
        {
            var node = Parse(cell.Formula!, out var reason);
            if (node is null || (reason = Unsupported(node)) is not null)
            {
                unsupported.Add($"{Name(cell)}: {reason}");
            }
            else
            {
                formulas.Add((cell, node));
            }
        }

        var evaluator = new FormulaEvaluator(workbook, now);
        foreach (var index in Order(formulas))
        {
            var (cell, node) = formulas[index];
            evaluator.Sheet = cell.Worksheet;
            evaluator.Current = (cell.Row, cell.Column);
            Store(cell, evaluator.Evaluate(node));
        }

        return new XlsxRecalculation(formulas.Count, unsupported);
    }

    private static FormulaNode? Parse(string formula, out string? reason)
    {
        try
        {
            reason = null;
            return FormulaParser.Parse(formula);
        }
        catch (FormatException exception)
        {
            reason = exception.Message;
            return null;
        }
    }

    // The first thing of the tree the engine does not compute, or null.
    private static string? Unsupported(FormulaNode node) => node switch
    {
        UnsupportedNode unsupported => $"'{unsupported.Text}' is not computed",
        CallNode call when !FormulaFunctions.IsKnown(call.Name) || Dynamic.Contains(call.Name) => $"function {call.Name} is not computed",
        CallNode call => call.Arguments.Select(Unsupported).FirstOrDefault(r => r is not null),
        UnaryNode unary => Unsupported(unary.Operand),
        PercentNode percent => Unsupported(percent.Operand),
        BinaryNode binary => Unsupported(binary.Left) ?? Unsupported(binary.Right),
        _ => null,
    };

    // Formulas in an order where each comes after the formulas it reads (Kahn's algorithm); a cycle throws.
    private List<int> Order(List<(XlsxCell Cell, FormulaNode Node)> formulas)
    {
        var index = new Dictionary<XlsxWorksheet, SortedDictionary<int, List<(int Row, int Formula)>>>();
        for (var i = 0; i < formulas.Count; i++)
        {
            var cell = formulas[i].Cell;
            var columns = index.TryGetValue(cell.Worksheet, out var found) ? found : index[cell.Worksheet] = [];
            (columns.TryGetValue(cell.Column, out var rows) ? rows : columns[cell.Column] = []).Add((cell.Row, i));
        }

        var dependents = Enumerable.Range(0, formulas.Count).Select(_ => new List<int>()).ToList();
        var waiting = new int[formulas.Count];
        for (var i = 0; i < formulas.Count; i++)
        {
            foreach (var source in Sources(formulas[i], index).Distinct())
            {
                dependents[source].Add(i);
                waiting[i]++;
            }
        }

        var ready = new Queue<int>(Enumerable.Range(0, formulas.Count).Where(i => waiting[i] == 0));
        var order = new List<int>(formulas.Count);
        while (ready.Count > 0)
        {
            var next = ready.Dequeue();
            order.Add(next);
            foreach (var dependent in dependents[next])
            {
                if (--waiting[dependent] == 0)
                {
                    ready.Enqueue(dependent);
                }
            }
        }

        if (order.Count < formulas.Count)
        {
            var cycle = Enumerable.Range(0, formulas.Count).Where(i => waiting[i] > 0).Select(i => Name(formulas[i].Cell)).ToList();
            throw new XlsxCircularReferenceException(cycle);
        }

        return order;
    }

    // The formula cells inside every area the formula references.
    private IEnumerable<int> Sources((XlsxCell Cell, FormulaNode Node) formula, Dictionary<XlsxWorksheet, SortedDictionary<int, List<(int Row, int Formula)>>> index)
    {
        foreach (var area in Areas(formula.Node))
        {
            var sheet = area.Sheet is null ? formula.Cell.Worksheet : workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(area.Sheet, StringComparison.OrdinalIgnoreCase));
            if (sheet is null || !index.TryGetValue(sheet, out var columns))
            {
                continue;
            }

            foreach (var (column, rows) in columns)
            {
                if (column < area.Left || column > area.Right)
                {
                    continue;
                }

                // Rows are in ascending order (cells are listed by row): start at the first one inside the area.
                for (var i = FirstAtOrAfter(rows, area.Top); i < rows.Count && rows[i].Row <= area.Bottom; i++)
                {
                    yield return rows[i].Formula;
                }
            }
        }
    }

    private static int FirstAtOrAfter(List<(int Row, int Formula)> rows, int row)
    {
        var (low, high) = (0, rows.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            (low, high) = rows[middle].Row < row ? (middle + 1, high) : (low, middle);
        }

        return low;
    }

    private static IEnumerable<FormulaArea> Areas(FormulaNode node) => node switch
    {
        ReferenceNode reference => [reference.Area],
        CallNode call => call.Arguments.SelectMany(Areas),
        UnaryNode unary => Areas(unary.Operand),
        PercentNode percent => Areas(percent.Operand),
        BinaryNode binary => Areas(binary.Left).Concat(Areas(binary.Right)),
        _ => [],
    };

    // The result goes into the cell; a cell that showed a date keeps showing one.
    private void Store(XlsxCell cell, FormulaValue result)
    {
        var value = result.ToCellValue();
        if (cell.ValueType == XlsxValueType.DateTime && value is double serial && ExcelDate.IsInRange(serial, workbook.Date1904))
        {
            value = ExcelDate.FromSerial(serial, workbook.Date1904);
        }

        cell.Value = value;
    }

    private static string Name(XlsxCell cell) => $"{cell.Worksheet.Name}!{cell.Reference}";
}
