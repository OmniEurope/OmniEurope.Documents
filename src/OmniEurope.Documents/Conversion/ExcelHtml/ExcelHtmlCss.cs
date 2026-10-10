// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Excel;

namespace OmniEurope.Documents.Conversion.ExcelHtml;

/// <summary>
/// The style sheet of <see cref="ExcelToHtml"/>: one class per distinct cell look and per column width, in
/// order of first use. Rules are built only from numbers, enumerations, colours checked as <c>RRGGBB</c> and
/// font names reduced to letters, digits, spaces, hyphens and underscores, so no workbook value can close the
/// style element or add a rule.
/// </summary>
internal sealed class ExcelHtmlCss(bool gridlines)
{
    private readonly Dictionary<string, string> _cells = new(StringComparer.Ordinal);
    private readonly Dictionary<double, string> _widths = [];

    /// <summary>The class of a cell with this style and value type.</summary>
    public string CellClass(XlsxStyle style, XlsxValueType type)
    {
        var rule = Declarations(style, type);
        if (!_cells.TryGetValue(rule, out var name))
        {
            name = "c" + _cells.Count.ToString(CultureInfo.InvariantCulture);
            _cells.Add(rule, name);
        }

        return name;
    }

    /// <summary>The class giving a width in pixels.</summary>
    public string WidthClass(double pixels)
    {
        if (!_widths.TryGetValue(pixels, out var name))
        {
            name = "w" + _widths.Count.ToString(CultureInfo.InvariantCulture);
            _widths.Add(pixels, name);
        }

        return name;
    }

    public string StyleSheet()
    {
        var css = new StringBuilder();
        css.Append("body{font-family:Calibri,Carlito,sans-serif;margin:16px}\n");
        css.Append("table.").Append(ExcelToHtml.TableClass).Append("{border-collapse:collapse;table-layout:fixed}\n");
        css.Append("table.").Append(ExcelToHtml.TableClass).Append(" td{padding:1px 2px;overflow:hidden;font-size:11pt;vertical-align:bottom;white-space:pre;")
            .Append(gridlines ? "border:1px solid #D9D9D9" : "border:none").Append("}\n");
        foreach (var (pixels, name) in _widths)
        {
            css.Append('.').Append(name).Append("{width:").Append(pixels.ToString(CultureInfo.InvariantCulture)).Append("px}\n");
        }

        foreach (var (rule, name) in _cells)
        {
            css.Append("table.").Append(ExcelToHtml.TableClass).Append(" td.").Append(name).Append('{').Append(rule).Append("}\n");
        }

        return css.ToString();
    }

    private static string Declarations(XlsxStyle style, XlsxValueType type)
    {
        var css = new StringBuilder();
        if (FontName(style.FontName) is { Length: > 0 } font)
        {
            css.Append("font-family:'").Append(font).Append("';");
        }

        css.Append("font-size:").Append(style.FontSize.ToString(CultureInfo.InvariantCulture)).Append("pt;");
        Append(css, "font-weight:bold;", style.Bold);
        Append(css, "font-style:italic;", style.Italic);
        if (style.Underline || style.Strike)
        {
            css.Append("text-decoration:").Append(style.Underline ? "underline" : string.Empty)
                .Append(style.Underline && style.Strike ? " " : string.Empty).Append(style.Strike ? "line-through" : string.Empty).Append(';');
        }

        AppendColor(css, "color", style.FontColor);
        AppendColor(css, "background-color", style.FillColor);
        css.Append("text-align:").Append(HorizontalAlignment(style.HorizontalAlignment, type)).Append(';');
        css.Append("vertical-align:").Append(style.VerticalAlignment switch
        {
            XlsxVerticalAlignment.Top => "top",
            XlsxVerticalAlignment.Center => "middle",
            _ => "bottom",
        }).Append(';');
        Append(css, "white-space:pre-wrap;", style.WrapText);
        if (style.Border)
        {
            css.Append("border:1px solid #").Append(Color(style.BorderColor) ?? "000000").Append(';');
        }

        return css.ToString();
    }

    private static void Append(StringBuilder css, string declaration, bool condition)
    {
        if (condition)
        {
            css.Append(declaration);
        }
    }

    private static void AppendColor(StringBuilder css, string property, string? color)
    {
        if (Color(color) is { } hex)
        {
            css.Append(property).Append(":#").Append(hex).Append(';');
        }
    }

    // "General" alignment puts numbers and dates on the right, text on the left, booleans and errors in the centre.
    private static string HorizontalAlignment(XlsxHorizontalAlignment alignment, XlsxValueType type) => alignment switch
    {
        XlsxHorizontalAlignment.Left => "left",
        XlsxHorizontalAlignment.Center => "center",
        XlsxHorizontalAlignment.Right => "right",
        XlsxHorizontalAlignment.Justify => "justify",
        _ => type switch
        {
            XlsxValueType.Number or XlsxValueType.DateTime => "right",
            XlsxValueType.Boolean or XlsxValueType.Error => "center",
            _ => "left",
        },
    };

    /// <summary>The colour as <c>RRGGBB</c>, or null when it is not one.</summary>
    internal static string? Color(string? color)
    {
        if (color is null)
        {
            return null;
        }

        try
        {
            return XlsxStyle.NormalizeColor(color);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The font name reduced to letters, digits, spaces, hyphens and underscores.</summary>
    internal static string FontName(string? name) =>
        name is null ? string.Empty : new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
}
