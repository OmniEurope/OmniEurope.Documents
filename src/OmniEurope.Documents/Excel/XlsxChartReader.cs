// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Charts;
using OmniEurope.Documents.Excel.Formulas;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel;

/// <summary>
/// Reads the charts of a worksheet's drawing (ECMA-376 part 1, §20.5): each two-cell, one-cell or absolute anchor whose
/// graphic frame names a chart part, read by <see cref="ChartReader"/>, with the cells it lies over. A chart of a type
/// introduced by Office 2016 (<c>cx:chart</c>) is kept as an unsupported chart. Once every sheet is read, a series
/// without a cache takes its name, categories and values from the cells its formulas name, a formula cell without a
/// stored result being computed by the formula engine.
/// </summary>
internal static class XlsxChartReader
{
    private const double EmuPerPoint = 12700;

    /// <summary>The largest offset, extent or position read from an anchor, in points: a little over the width of the
    /// 16 384 widest columns of a sheet. A larger (or non-finite) number is a damaged one.</summary>
    internal const double MaxExtent = 1e8;
    private const string ChartNamespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    private const string StrictChartNamespace = "http://purl.oclc.org/ooxml/drawingml/chart";
    private const string ChartExNamespace = "http://schemas.microsoft.com/office/drawing/2014/chartex";
    private static readonly XNamespace C = ChartNamespace;

    public static void Read(SafeZip zip, string sheetPath, XlsxWorksheet sheet, Lazy<ChartPalette> palette)
    {
        foreach (var drawing in Drawings(zip, sheetPath, sheet))
        {
            try
            {
                ReadDrawing(zip, drawing, sheet, palette);
            }
            catch (Exception exception) when (ChartReader.IsUnreadable(exception))
            {
                // A drawing that cannot be read costs the charts it holds, not the workbook.
                sheet.Workbook.Gaps.Add("unreadable drawing skipped");
            }
        }
    }

    // The drawing parts a sheet names; none when its relationships cannot be read.
    private static List<string> Drawings(SafeZip zip, string sheetPath, XlsxWorksheet sheet)
    {
        try
        {
            return OpcRelationships.Read(zip, sheetPath).Where(r => !r.External && r.Type.EndsWith("/drawing", StringComparison.Ordinal)).Select(r => r.Target).ToList();
        }
        catch (Exception exception) when (ChartReader.IsUnreadable(exception))
        {
            sheet.Workbook.Gaps.Add("unreadable drawing skipped");
            return [];
        }
    }

    private static void ReadDrawing(SafeZip zip, string path, XlsxWorksheet sheet, Lazy<ChartPalette> palette)
    {
        var root = Load(zip, path);
        var relationships = OpcRelationships.Read(zip, path);
        foreach (var anchor in root?.Elements().Where(e => e.Name.LocalName is "twoCellAnchor" or "oneCellAnchor" or "absoluteAnchor") ?? [])
        {
            try
            {
                if (Chart(zip, anchor, relationships, palette) is { } chart)
                {
                    var (cells, offset, size) = Place(anchor, sheet);
                    sheet.AddLoadedChart(new XlsxChart(chart.Model, chart.Part, cells, offset, size));
                }
            }
            catch (Exception exception) when (ChartReader.IsUnreadable(exception))
            {
                sheet.Workbook.Gaps.Add("unreadable chart skipped");
            }
        }
    }

    private static XElement? Load(SafeZip zip, string path) =>
        zip.ReadBytes(path) is { } bytes ? OpcPackage.ParseXml(path, bytes, LoadOptions.None).Root : null;

    private static (Chart Model, XElement Part)? Chart(SafeZip zip, XElement anchor, List<OpcRelationship> relationships, Lazy<ChartPalette> palette)
    {
        var reference = anchor.Descendants().FirstOrDefault(e => e.Name.LocalName == "chart" && e.Name.NamespaceName is ChartNamespace or StrictChartNamespace);
        if (reference is null)
        {
            return anchor.Descendants().Any(e => e.Name.NamespaceName == ChartExNamespace) ? Placeholder() : null;
        }

        var id = reference.Attributes().FirstOrDefault(a => a.Name.LocalName == "id" && a.Name.NamespaceName.Length > 0)?.Value;
        var target = relationships.Find(r => r.Id == id && !r.External)?.Target;
        return target is not null && Load(zip, target) is { } part ? (ChartReader.Read(part, palette.Value), part) : null;
    }

    // An Office 2016 chart (waterfall, treemap, funnel...): no model, drawn as an empty frame and reported.
    private static (Chart, XElement) Placeholder()
    {
        var part = new XElement(C + "chartSpace", new XElement(C + "chart", new XElement(C + "plotArea", new XElement(C + "layout"))));
        var chart = ChartReader.Read(part, ChartPalette.Default);
        chart.UnsupportedTypes = ["chart of a type introduced by Office 2016 (waterfall, treemap, funnel...)"];
        return (chart, part);
    }

    // The cells under the anchor, the offset of its corner in the first one and its size, in points.
    private static (XlsxRange Cells, (double X, double Y) Offset, (double Width, double Height) Size) Place(XElement anchor, XlsxWorksheet sheet)
    {
        var (fromColumn, fromX, fromRow, fromY) = anchor.Name.LocalName == "absoluteAnchor"
            ? Locate(sheet, Emu(Child(anchor, "pos"), "x"), Emu(Child(anchor, "pos"), "y"))
            : Marker(Child(anchor, "from"));
        double width, height;
        if (anchor.Name.LocalName == "twoCellAnchor")
        {
            var (toColumn, toX, toRow, toY) = Marker(Child(anchor, "to"));
            width = ColumnStart(sheet, toColumn) + toX - ColumnStart(sheet, fromColumn) - fromX;
            height = ((toRow - fromRow) * XlsxWorksheet.DefaultRowHeight) + toY - fromY;
        }
        else
        {
            (width, height) = (Emu(Child(anchor, "ext"), "cx"), Emu(Child(anchor, "ext"), "cy"));
        }

        (width, height) = (Math.Max(1, width), Math.Max(1, height));
        var (lastColumn, _, lastRow, _) = Locate(sheet, ColumnStart(sheet, fromColumn) + fromX + width, (fromRow * XlsxWorksheet.DefaultRowHeight) + fromY + height);
        return (new XlsxRange(fromRow + 1, fromColumn + 1, Math.Max(lastRow, fromRow) + 1, Math.Max(lastColumn, fromColumn) + 1), (fromX, fromY), (width, height));
    }

    // A marker's 0-based column and row with the offsets inside them, in points.
    private static (int Column, double X, int Row, double Y) Marker(XElement? marker)
    {
        int Index(string name) => Math.Clamp(int.TryParse(Child(marker, name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0, 0, CellReference.MaxRow - 1);
        double Offset(string name) => Points(Child(marker, name)?.Value);
        return (Math.Min(Index("col"), CellReference.MaxColumn - 1), Offset("colOff"), Index("row"), Offset("rowOff"));
    }

    // The 0-based column and row holding a point of the sheet, and the point's offsets inside them.
    private static (int Column, double X, int Row, double Y) Locate(XlsxWorksheet sheet, double x, double y)
    {
        var column = 0;
        for (var width = ColumnPoints(sheet, 1); x >= width && column < CellReference.MaxColumn - 1; width = ColumnPoints(sheet, column + 1))
        {
            x -= width;
            column++;
        }

        var row = (int)Math.Min(CellReference.MaxRow - 1, Math.Floor(Math.Max(0, y) / XlsxWorksheet.DefaultRowHeight));
        return (column, Math.Max(0, x), row, Math.Max(0, y - (row * XlsxWorksheet.DefaultRowHeight)));
    }

    // The distance in points from the sheet's left edge to 0-based column `column`.
    private static double ColumnStart(XlsxWorksheet sheet, int column)
    {
        double start = 0;
        for (var c = 1; c <= column; c++)
        {
            start += ColumnPoints(sheet, c);
        }

        return start;
    }

    // The width in points of a 1-based column. Excel lays columns out in whole pixels (96 dpi, 0.75 point each), so the
    // default column of 8.43 characters is 64 pixels, 48 points, not 64.01 pixels: an extent of two default columns then
    // ends exactly on a column edge, as in the file Excel wrote it from.
    private static double ColumnPoints(XlsxWorksheet sheet, int column) => Math.Truncate(sheet.ColumnPixels(column)) * 0.75;

    private static double Emu(XElement? element, string attribute) => Points((string?)element?.Attribute(attribute));

    // A length in EMU as points: within 0 and MaxExtent, and 0 when missing, not a number or not finite.
    private static double Points(string? emu) =>
        double.TryParse(emu, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? Math.Clamp(value / EmuPerPoint, 0, MaxExtent) : 0;

    private static XElement? Child(XElement? parent, string name) => ChartPalette.Child(parent, name);

    /// <summary>
    /// Gives the series without a cache the names, categories and values of the cells their formulas name. A chart plots
    /// its series times its longest series, so each series of a chart may take its share of
    /// <see cref="ChartReader.MaxChartPoints"/>: a series naming more cells than that reads as empty and the chart is
    /// marked <see cref="Chart.Truncated"/>.
    /// </summary>
    public static void Fill(XlsxWorkbook workbook)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            foreach (var chart in sheet.Charts.Select(c => c.Chart))
            {
                var source = new CellSource(workbook, sheet, Math.Min(ChartReader.MaxPoints, ChartReader.MaxChartPoints / Math.Max(1, chart.Series.Count)));
                foreach (var series in chart.Series)
                {
                    Fill(series, source);
                }

                chart.Truncated |= source.Exceeded;
            }
        }
    }

    private static void Fill(ChartSeries series, CellSource source)
    {
        if (series.Values.Count == 0 && source.Cells(series.ValuesReference) is { Count: > 0 } values)
        {
            series.Values = values.Select(source.Number).ToList();
        }

        if (series.Categories.Count == 0 && source.Cells(series.CategoriesReference) is { Count: > 0 } categories)
        {
            series.Categories = categories.Select(source.Text).ToList();
        }

        if (series.Name is null && source.Cells(series.NameReference) is { Count: > 0 } name)
        {
            series.Name = source.Text(name[0]);
        }
    }

    /// <summary>The cells a series formula names, and their values for a chart.</summary>
    private sealed class CellSource(XlsxWorkbook workbook, XlsxWorksheet sheet, int limit)
    {
        private readonly FormulaEvaluator _evaluator = new(workbook, DateTime.Now) { Sheet = sheet };

        /// <summary>A reference named more cells than the limit and was read as empty.</summary>
        public bool Exceeded { get; private set; }

        // The cells of a reference to one area, row by row (missing cells are null); empty for anything else.
        public List<(XlsxWorksheet Sheet, int Row, int Column)> Cells(string? formula)
        {
            if (formula is null || Parse(formula) is not ReferenceNode { Area: var area } || _evaluator.SheetOf(area) is not { } owner)
            {
                return [];
            }

            // Whole rows and columns stop at the sheet's used range, as the engine reads them.
            var used = owner.UsedRange;
            area = area with { Bottom = Math.Min(area.Bottom, Math.Max(area.Top, used?.LastRow ?? area.Top)), Right = Math.Min(area.Right, Math.Max(area.Left, used?.LastColumn ?? area.Left)) };
            if ((long)area.Rows * area.Columns > limit)
            {
                Exceeded = true;
                return [];
            }

            var cells = new List<(XlsxWorksheet, int, int)>();
            for (var r = area.Top; r <= area.Bottom; r++)
            {
                for (var c = area.Left; c <= area.Right; c++)
                {
                    cells.Add((owner, r, c));
                }
            }

            return cells;
        }

        public double? Number((XlsxWorksheet Sheet, int Row, int Column) at) =>
            Value(at) is { Kind: ValueKind.Number } value ? value.Number : null;

        public string Text((XlsxWorksheet Sheet, int Row, int Column) at)
        {
            var cell = at.Sheet.FindCell(at.Row, at.Column);
            if (cell is null || cell.ValueType != XlsxValueType.Empty || cell.Formula is null)
            {
                return cell?.FormattedText ?? string.Empty;
            }

            var value = Value(at);
            return value.Kind == ValueKind.Number ? NumberFormatter.Format(value.Number, cell.Style.NumberFormat, CultureInfo.InvariantCulture, workbook.Date1904) : value.ToText().Text;
        }

        // The cell's stored value, or for a formula without a stored result the result the engine computes for it.
        private FormulaValue Value((XlsxWorksheet Sheet, int Row, int Column) at)
        {
            var cell = at.Sheet.FindCell(at.Row, at.Column);
            if (cell is not { ValueType: XlsxValueType.Empty, Formula: { } formula } || Parse(formula) is not { } node)
            {
                return FormulaValue.FromCell(cell);
            }

            _evaluator.Sheet = at.Sheet;
            _evaluator.Current = (at.Row, at.Column);
            try
            {
                return _evaluator.Calculate(node).Scalar();
            }
            catch (FormulaLimitException)
            {
                // Past the engine's memory bound, as the recalculation does: the cell keeps its stored (empty) value.
                return FormulaValue.FromCell(cell);
            }
            finally
            {
                _evaluator.Sheet = sheet;
            }
        }

        private static FormulaNode? Parse(string formula)
        {
            try
            {
                return FormulaParser.Parse(formula);
            }
            catch (Exception exception) when (exception is FormatException or FormulaLimitException)
            {
                return null;
            }
        }
    }
}
