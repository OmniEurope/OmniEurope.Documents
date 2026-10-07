// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;

namespace OmniEurope.Documents.Excel;

/// <summary>Collects the distinct styles of a workbook and writes <c>xl/styles.xml</c>.</summary>
internal sealed class XlsxStyleTable
{
    internal const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    // Number formats Excel knows by id; anything else gets a custom id from 164.
    internal static readonly IReadOnlyDictionary<int, string> BuiltInFormats = new Dictionary<int, string>
    {
        [0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%",
        [11] = "0.00E+00", [12] = "# ?/?", [13] = "# ??/??", [14] = "m/d/yyyy", [15] = "d-mmm-yy", [16] = "d-mmm",
        [17] = "mmm-yy", [18] = "h:mm AM/PM", [19] = "h:mm:ss AM/PM", [20] = "h:mm", [21] = "h:mm:ss",
        [22] = "m/d/yyyy h:mm", [37] = "#,##0 ;(#,##0)", [38] = "#,##0 ;[Red](#,##0)", [39] = "#,##0.00;(#,##0.00)",
        [40] = "#,##0.00;[Red](#,##0.00)", [45] = "mm:ss", [46] = "[h]:mm:ss", [47] = "mmss.0", [48] = "##0.0E+0", [49] = "@",
    };

    private readonly List<XlsxStyle> _styles = [XlsxStyle.Default];
    private readonly Dictionary<XlsxStyle, int> _index = new() { [XlsxStyle.Default] = 0 };

    public int IndexOf(XlsxStyle style)
    {
        if (!_index.TryGetValue(style, out var index))
        {
            index = _styles.Count;
            _styles.Add(style);
            _index.Add(style, index);
        }

        return index;
    }

    public void Write(XmlWriter xml)
    {
        var formats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, code) in BuiltInFormats)
        {
            formats.TryAdd(code, id);
        }

        var custom = _styles.Select(s => s.NumberFormat).Distinct(StringComparer.Ordinal).Where(f => !formats.ContainsKey(f)).ToList();
        for (var i = 0; i < custom.Count; i++)
        {
            formats[custom[i]] = 164 + i;
        }

        var fonts = Distinct(_styles.Select(FontKey));
        var fills = Distinct(_styles.Select(s => s.FillColor ?? string.Empty).Prepend(string.Empty));
        var borders = Distinct(_styles.Select(BorderKey).Prepend(string.Empty));

        xml.WriteStartElement("styleSheet", Main);
        WriteNumberFormats(xml, custom, formats);
        WriteFonts(xml, fonts);
        WriteFills(xml, fills);
        WriteBorders(xml, borders);
        xml.WriteStartElement("cellStyleXfs");
        xml.WriteAttributeString("count", "1");
        WriteXf(xml, 0, 0, 0, 0, null);
        xml.WriteEndElement();
        xml.WriteStartElement("cellXfs");
        xml.WriteAttributeString("count", Text(_styles.Count));
        foreach (var style in _styles)
        {
            var fill = string.IsNullOrEmpty(style.FillColor) ? 0 : fills.IndexOf(style.FillColor) + 1;
            WriteXf(xml, formats[style.NumberFormat], fonts.IndexOf(FontKey(style)), fill, borders.IndexOf(BorderKey(style)), style);
        }

        xml.WriteEndElement();
        xml.WriteStartElement("cellStyles");
        xml.WriteAttributeString("count", "1");
        xml.WriteStartElement("cellStyle");
        xml.WriteAttributeString("name", "Normal");
        xml.WriteAttributeString("xfId", "0");
        xml.WriteAttributeString("builtinId", "0");
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static List<T> Distinct<T>(IEnumerable<T> items) => items.Distinct().ToList();

    private static (string Name, double Size, bool Bold, bool Italic, bool Underline, bool Strike, string? Color) FontKey(XlsxStyle s) =>
        (s.FontName, s.FontSize, s.Bold, s.Italic, s.Underline, s.Strike, s.FontColor);

    private static string BorderKey(XlsxStyle s) => s.Border ? "thin:" + (s.BorderColor ?? string.Empty) : string.Empty;

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void WriteNumberFormats(XmlWriter xml, List<string> custom, Dictionary<string, int> formats)
    {
        if (custom.Count == 0)
        {
            return;
        }

        xml.WriteStartElement("numFmts");
        xml.WriteAttributeString("count", Text(custom.Count));
        foreach (var code in custom)
        {
            xml.WriteStartElement("numFmt");
            xml.WriteAttributeString("numFmtId", Text(formats[code]));
            xml.WriteAttributeString("formatCode", code);
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteFonts(XmlWriter xml, List<(string Name, double Size, bool Bold, bool Italic, bool Underline, bool Strike, string? Color)> fonts)
    {
        xml.WriteStartElement("fonts");
        xml.WriteAttributeString("count", Text(fonts.Count));
        foreach (var font in fonts)
        {
            xml.WriteStartElement("font");
            Flag(xml, "b", font.Bold);
            Flag(xml, "i", font.Italic);
            Flag(xml, "strike", font.Strike);
            Flag(xml, "u", font.Underline);
            Value(xml, "sz", font.Size.ToString(CultureInfo.InvariantCulture));
            if (font.Color is not null)
            {
                Color(xml, "color", font.Color);
            }

            Value(xml, "name", font.Name);
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteFills(XmlWriter xml, List<string> fills)
    {
        xml.WriteStartElement("fills");
        xml.WriteAttributeString("count", Text(fills.Count + 1));
        Pattern(xml, "none", null);
        Pattern(xml, "gray125", null);
        foreach (var color in fills.Where(f => f.Length > 0))
        {
            Pattern(xml, "solid", color);
        }

        xml.WriteEndElement();
    }

    private static void Pattern(XmlWriter xml, string type, string? color)
    {
        xml.WriteStartElement("fill");
        xml.WriteStartElement("patternFill");
        xml.WriteAttributeString("patternType", type);
        if (color is not null)
        {
            Color(xml, "fgColor", color);
            xml.WriteStartElement("bgColor");
            xml.WriteAttributeString("indexed", "64");
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    private static void WriteBorders(XmlWriter xml, List<string> borders)
    {
        xml.WriteStartElement("borders");
        xml.WriteAttributeString("count", Text(borders.Count));
        foreach (var border in borders)
        {
            xml.WriteStartElement("border");
            foreach (var side in (string[])["left", "right", "top", "bottom"])
            {
                xml.WriteStartElement(side);
                if (border.Length > 0)
                {
                    xml.WriteAttributeString("style", "thin");
                    var color = border[5..];
                    if (color.Length > 0)
                    {
                        Color(xml, "color", color);
                    }
                }

                xml.WriteEndElement();
            }

            xml.WriteElementString("diagonal", Main, string.Empty);
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void WriteXf(XmlWriter xml, int numFmt, int font, int fill, int border, XlsxStyle? style)
    {
        xml.WriteStartElement("xf");
        xml.WriteAttributeString("numFmtId", Text(numFmt));
        xml.WriteAttributeString("fontId", Text(font));
        xml.WriteAttributeString("fillId", Text(fill));
        xml.WriteAttributeString("borderId", Text(border));
        if (style is not null)
        {
            xml.WriteAttributeString("xfId", "0");
            xml.WriteAttributeString("applyNumberFormat", numFmt == 0 ? "0" : "1");
            xml.WriteAttributeString("applyFont", font == 0 ? "0" : "1");
            xml.WriteAttributeString("applyFill", fill == 0 ? "0" : "1");
            xml.WriteAttributeString("applyBorder", border == 0 ? "0" : "1");
            var aligned = style.HorizontalAlignment != XlsxHorizontalAlignment.General || style.VerticalAlignment != XlsxVerticalAlignment.Bottom || style.WrapText;
            if (aligned)
            {
                xml.WriteAttributeString("applyAlignment", "1");
            }

            if (style.QuotePrefix)
            {
                xml.WriteAttributeString("quotePrefix", "1");
            }

            if (aligned)
            {
                WriteAlignment(xml, style);
            }
        }

        xml.WriteEndElement();
    }

    private static void WriteAlignment(XmlWriter xml, XlsxStyle style)
    {
        xml.WriteStartElement("alignment");
        if (style.HorizontalAlignment != XlsxHorizontalAlignment.General)
        {
            xml.WriteAttributeString("horizontal", style.HorizontalAlignment.ToString().ToLowerInvariant());
        }

        if (style.VerticalAlignment != XlsxVerticalAlignment.Bottom)
        {
            xml.WriteAttributeString("vertical", style.VerticalAlignment.ToString().ToLowerInvariant());
        }

        if (style.WrapText)
        {
            xml.WriteAttributeString("wrapText", "1");
        }

        xml.WriteEndElement();
    }

    private static void Flag(XmlWriter xml, string name, bool on)
    {
        if (on)
        {
            xml.WriteElementString(name, Main, string.Empty);
        }
    }

    private static void Value(XmlWriter xml, string name, string value)
    {
        xml.WriteStartElement(name);
        xml.WriteAttributeString("val", value);
        xml.WriteEndElement();
    }

    private static void Color(XmlWriter xml, string name, string rgb)
    {
        xml.WriteStartElement(name);
        xml.WriteAttributeString("rgb", "FF" + XlsxStyle.NormalizeColor(rgb));
        xml.WriteEndElement();
    }
}
