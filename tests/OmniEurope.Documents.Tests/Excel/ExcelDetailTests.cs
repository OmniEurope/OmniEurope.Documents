// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Tests.Excel;

/// <summary>
/// Number formats (ECMA-376 part 1, §18.8.31) where Excel's own display is the reference: month initials, the
/// four 12-hour markers, elapsed minutes and seconds, separators inside dates, rounding of the mantissa, the
/// 15 significant digits Excel shows; cell references, the 1904 date system, damaged workbooks.
/// </summary>
public sealed class ExcelDetailTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    // 16 July 2023, 16:30.
    private const double Afternoon = 45123.6875;

    [Theory]
    [InlineData(Afternoon, "mmmmm", "J")]
    [InlineData(Afternoon, "ddd", "Sun")]
    [InlineData(Afternoon, "h AM/PM", "4 PM")]
    [InlineData(Afternoon, "h A/P", "4 P")]
    [InlineData(Afternoon, "h a/p", "4 p")]
    [InlineData(Afternoon, "h am/pm", "4 PM")]
    [InlineData(1.5, "[m]", "2160")]
    [InlineData(0.001, "[s]", "86")]
    [InlineData(Afternoon, "mm,yy", "07,23")]
    [InlineData(Afternoon, "yyyy.mm", "2023.07")]
    [InlineData(Afternoon, "mm%", "07%")]
    [InlineData(-1, "mm/yy", "########")]
    public void Date_sections_render_like_excel(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, English));
    }

    [Theory]
    [InlineData(99.999, "0.00E+00", "1.00E+02")]
    [InlineData(0.000123, "0.0E+0", "1.2E-4")]
    [InlineData(0.5, "0%", "50%")]
    [InlineData(3, "0\" kg\"", "3 kg")]
    [InlineData(1e30, "0", "1000000000000000000000000000000")]
    [InlineData(123456789012345678, "0", "123456789012346000")]
    public void Numbers_render_like_excel(double value, string format, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(value, format, English));
    }

    [Theory]
    [InlineData("$B$12", true, 12, 2)]
    [InlineData("12", false, 0, 0)]
    [InlineData("XFE1", false, 0, 0)]
    public void Cell_references_parse_absolute_markers_and_refuse_bad_columns(string text, bool ok, int row, int column)
    {
        Assert.Equal(ok, CellReference.TryParse(text, out var r, out var c));
        if (ok)
        {
            Assert.Equal((row, column), (r, c));
        }
    }

    [Fact]
    public void A_single_cell_range_has_equal_corners()
    {
        Assert.True(CellReference.TryParseRange("C3", out var firstRow, out var firstColumn, out var lastRow, out var lastColumn));
        Assert.Equal((3, 3, 3, 3), (firstRow, firstColumn, lastRow, lastColumn));
        Assert.False(CellReference.TryParseRange("A1:9", out _, out _, out _, out _));
    }

    [Fact]
    public void The_1904_date_system_counts_from_1_january_1904()
    {
        var date = new DateTime(1904, 1, 2, 12, 0, 0);

        Assert.Equal(1.5, ExcelDate.ToSerial(date, date1904: true));
        Assert.Equal(date, ExcelDate.FromSerial(1.5, date1904: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExcelDate.FromSerial(-1));
    }

    [Fact]
    public void Workbooks_read_the_1904_flag_and_refuse_damage()
    {
        var workbook = new XlsxWorkbook();
        workbook.AddWorksheet("Feuille").Cell("A1").Value = 1;
        var bytes = workbook.ToArray();

        var flagged = Rewrite(bytes, "xl/workbook.xml", xml => xml.Replace("<sheets>", "<workbookPr date1904=\"1\"/><sheets>", StringComparison.Ordinal));
        var malformed = Rewrite(bytes, "xl/workbook.xml", xml => xml[..^10]);
        var empty = Rewrite(bytes, "xl/workbook.xml", xml => System.Text.RegularExpressions.Regex.Replace(xml, "<sheets>.*</sheets>", "<sheets/>"));

        Assert.True(XlsxWorkbook.Load(flagged).Date1904);
        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(malformed));
        Assert.Throws<DocumentFormatException>(() => XlsxWorkbook.Load(empty));
    }

    private static byte[] Rewrite(byte[] package, string part, Func<string, string> change)
    {
        using var input = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var source = entry.Open();
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                var bytes = entry.FullName == part ? Encoding.UTF8.GetBytes(change(Encoding.UTF8.GetString(buffer.ToArray()))) : buffer.ToArray();
                using var target = zip.CreateEntry(entry.FullName).Open();
                target.Write(bytes);
            }
        }

        return output.ToArray();
    }
}
