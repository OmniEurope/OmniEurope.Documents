// SPDX-License-Identifier: EUPL-1.2
using static OmniEurope.Documents.Excel.Formulas.FormulaFunctions;

namespace OmniEurope.Documents.Excel.Formulas;

/// <summary>
/// Date and time functions (ECMA-376 part 1, §18.17.7) on serial numbers of the workbook's date system. TODAY and
/// NOW read the clock given to the recalculation.
/// </summary>
internal static class DateFunctions
{
    public static void Register(Dictionary<string, FormulaFunction> registry)
    {
        registry["DATE"] = new(3, 3, Date);
        registry["TIME"] = new(3, 3, Time);
        registry["TODAY"] = new(0, 0, (e, _) => FormulaValue.Of(ExcelDate.ToSerial(e.Now.Date, e.Date1904)));
        registry["NOW"] = new(0, 0, (e, _) => FormulaValue.Of(ExcelDate.ToSerial(e.Now, e.Date1904)));
        registry["YEAR"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Year));
        registry["MONTH"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Month));
        registry["DAY"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Day));
        registry["HOUR"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Hour));
        registry["MINUTE"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Minute));
        registry["SECOND"] = new(1, 1, (e, a) => Part(e, a[0], d => d.Second));
        registry["WEEKDAY"] = new(1, 2, Weekday);
        registry["EDATE"] = new(2, 2, (e, a) => Months(e, a, endOfMonth: false));
        registry["EOMONTH"] = new(2, 2, (e, a) => Months(e, a, endOfMonth: true));
        registry["DAYS"] = new(2, 2, (e, a) => Serial(e, a[0]) is { IsError: false } end && Serial(e, a[1]) is { IsError: false } start
            ? FormulaValue.Of(Math.Truncate(end.Number) - Math.Truncate(start.Number)) : FormulaValue.ValueError);
        registry["DATEDIF"] = new(3, 3, DateDif);
    }

    // DATE(year, month, day): a year below 1900 counts from 1900; months and days past their range carry over.
    private static FormulaValue Date(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var parts = arguments.Select(a => Number(evaluator, a)).ToList();
        if (parts.Find(p => p.IsError) is { IsError: true } error)
        {
            return error;
        }

        var year = Math.Truncate(parts[0].Number);
        year = year < 1900 ? year + 1900 : year;
        if (year is < 1900 or > 9999)
        {
            return FormulaValue.Num;
        }

        try
        {
            var date = new DateTime((int)year, 1, 1).AddMonths((int)Math.Truncate(parts[1].Number) - 1).AddDays(Math.Truncate(parts[2].Number) - 1);
            return ToSerial(evaluator, date);
        }
        catch (ArgumentOutOfRangeException)
        {
            return FormulaValue.Num;
        }
    }

    private static FormulaValue Time(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var parts = arguments.Select(a => Number(evaluator, a)).ToList();
        if (parts.Find(p => p.IsError) is { IsError: true } error)
        {
            return error;
        }

        var seconds = (Math.Truncate(parts[0].Number) * 3600) + (Math.Truncate(parts[1].Number) * 60) + Math.Truncate(parts[2].Number);
        return seconds < 0 ? FormulaValue.Num : FormulaValue.Of(seconds % 86400 / 86400);
    }

    private static FormulaValue ToSerial(FormulaEvaluator evaluator, DateTime date)
    {
        var serial = ExcelDate.ToSerial(date, evaluator.Date1904);
        return ExcelDate.IsInRange(serial, evaluator.Date1904) ? FormulaValue.Of(serial) : FormulaValue.Num;
    }

    // A serial number argument: a number, or text that reads as one.
    private static FormulaValue Serial(FormulaEvaluator evaluator, FormulaNode argument)
    {
        var value = Number(evaluator, argument);
        return value.IsError || ExcelDate.IsInRange(value.Number, evaluator.Date1904) ? value : FormulaValue.Num;
    }

    private static FormulaValue Part(FormulaEvaluator evaluator, FormulaNode argument, Func<DateTime, int> part)
    {
        var serial = Serial(evaluator, argument);
        return serial.IsError ? serial : FormulaValue.Of(part(ExcelDate.FromSerial(serial.Number, evaluator.Date1904)));
    }

    // WEEKDAY(serial, [type]): 1 Sunday is 1, 2 Monday is 1, 3 Monday is 0.
    private static FormulaValue Weekday(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var serial = Serial(evaluator, arguments[0]);
        var type = Number(evaluator, arguments, 1, 1);
        if (serial.IsError || type.IsError)
        {
            return serial.IsError ? serial : type;
        }

        var day = (int)ExcelDate.FromSerial(serial.Number, evaluator.Date1904).DayOfWeek;
        return type.Number switch
        {
            1 => FormulaValue.Of(day + 1),
            2 => FormulaValue.Of(day == 0 ? 7 : day),
            3 => FormulaValue.Of(day == 0 ? 6 : day - 1),
            _ => FormulaValue.Num,
        };
    }

    // EDATE moves by whole months (the day is kept, or the last day of a shorter month); EOMONTH gives that month's last day.
    private static FormulaValue Months(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments, bool endOfMonth)
    {
        var serial = Serial(evaluator, arguments[0]);
        var months = Number(evaluator, arguments[1]);
        if (serial.IsError || months.IsError)
        {
            return serial.IsError ? serial : months;
        }

        try
        {
            var date = ExcelDate.FromSerial(Math.Truncate(serial.Number), evaluator.Date1904).AddMonths((int)Math.Truncate(months.Number));
            return ToSerial(evaluator, endOfMonth ? new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)) : date);
        }
        catch (ArgumentOutOfRangeException)
        {
            return FormulaValue.Num;
        }
    }

    // DATEDIF(start, end, unit): whole years (Y), months (M) or days (D) between two dates.
    private static FormulaValue DateDif(FormulaEvaluator evaluator, IReadOnlyList<FormulaNode> arguments)
    {
        var start = Serial(evaluator, arguments[0]);
        var end = Serial(evaluator, arguments[1]);
        var unit = TextOf(evaluator, arguments[2]);
        if (start.IsError || end.IsError || unit.IsError)
        {
            return start.IsError ? start : end.IsError ? end : unit;
        }

        if (start.Number > end.Number)
        {
            return FormulaValue.Num;
        }

        var from = ExcelDate.FromSerial(Math.Truncate(start.Number), evaluator.Date1904);
        var to = ExcelDate.FromSerial(Math.Truncate(end.Number), evaluator.Date1904);
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month - (to.Day < from.Day ? 1 : 0);
        return unit.Text.ToUpperInvariant() switch
        {
            "Y" => FormulaValue.Of(months / 12),
            "M" => FormulaValue.Of(months),
            "D" => FormulaValue.Of(Math.Truncate(end.Number) - Math.Truncate(start.Number)),
            _ => FormulaValue.Num,
        };
    }
}
