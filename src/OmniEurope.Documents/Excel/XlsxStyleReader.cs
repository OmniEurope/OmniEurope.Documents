// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Excel;

/// <summary>Reads <c>xl/styles.xml</c> into one <see cref="XlsxStyle"/> per cell format (<c>cellXfs</c> entry).</summary>
internal static class XlsxStyleReader
{
    private sealed record Font(string Name, double Size, bool Bold, bool Italic, bool Underline, bool Strike, string? Color);

    public static List<XlsxStyle> Read(SafeZip zip, string? path)
    {
        using var xml = path is null ? null : zip.ReadXml(path);
        if (xml is null)
        {
            return [XlsxStyle.Default];
        }

        var tables = new StyleTables();
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                tables.Visit(xml);
            }
        }

        return tables.Styles.Count > 0 ? tables.Styles : [XlsxStyle.Default];
    }

    /// <summary>The tables of styles.xml as they are read: number formats, fonts, fills, borders and cell formats.</summary>
    private sealed class StyleTables
    {
        private static readonly HashSet<string> Sections = ["fonts", "fills", "borders", "cellXfs", "cellStyleXfs", "dxfs"];

        // Only the entries of their own section count: a font inside dxfs is not a font of the font table.
        private static readonly Dictionary<(string? Section, string Element), Action<StyleTables, XmlReader>> Readers = new()
        {
            [("fonts", "font")] = (tables, xml) => tables.Fonts.Add(ReadFont(xml)),
            [("fills", "fill")] = (tables, xml) => tables.Fills.Add(ReadFill(xml)),
            [("borders", "border")] = (tables, xml) => tables.Borders.Add(ReadBorder(xml)),
            [("cellXfs", "xf")] = (tables, xml) => tables.Styles.Add(ReadXf(xml, tables.Formats, tables.Fonts, tables.Fills, tables.Borders)),
        };

        private string? _section;

        public Dictionary<int, string> Formats { get; } = new(XlsxStyleTable.BuiltInFormats);

        public List<Font> Fonts { get; } = [];

        public List<string?> Fills { get; } = [];

        public List<(bool Border, string? Color)> Borders { get; } = [];

        public List<XlsxStyle> Styles { get; } = [];

        public void Visit(XmlReader xml)
        {
            var name = xml.LocalName;
            if (name == "numFmt")
            {
                Formats[int.Parse(xml.GetAttribute("numFmtId") ?? "0", CultureInfo.InvariantCulture)] = xml.GetAttribute("formatCode") ?? "General";
            }
            else if (Sections.Contains(name))
            {
                _section = name;
            }
            else if (Readers.TryGetValue((_section, name), out var read))
            {
                read(this, xml);
            }
        }
    }
    private static Font ReadFont(XmlReader xml)
    {
        var font = new Font("Calibri", 11, false, false, false, false, null);
        foreach (var (name, reader) in Children(xml))
        {
            font = name switch
            {
                "b" => font with { Bold = On(reader) },
                "i" => font with { Italic = On(reader) },
                "u" => font with { Underline = reader.GetAttribute("val") != "none" },
                "strike" => font with { Strike = On(reader) },
                "sz" => font with { Size = XlsxPackageReader.ParseDouble(reader.GetAttribute("val")) },
                "name" => font with { Name = reader.GetAttribute("val") ?? font.Name },
                "color" => font with { Color = Rgb(reader) },
                _ => font,
            };
        }

        return font;
    }

    private static string? ReadFill(XmlReader xml)
    {
        string? color = null;
        var solid = false;
        foreach (var (name, reader) in Children(xml))
        {
            if (name == "patternFill")
            {
                solid = reader.GetAttribute("patternType") == "solid";
            }
            else if (name == "fgColor")
            {
                color = Rgb(reader);
            }
        }

        return solid ? color : null;
    }

    private static (bool Border, string? Color) ReadBorder(XmlReader xml)
    {
        var border = false;
        string? color = null;
        foreach (var (name, reader) in Children(xml))
        {
            if (name is "left" or "right" or "top" or "bottom" && reader.GetAttribute("style") is { } style && style != "none")
            {
                border = true;
            }
            else if (name == "color" && border)
            {
                color ??= Rgb(reader);
            }
        }

        return (border, color);
    }

    private static XlsxStyle ReadXf(XmlReader xml, Dictionary<int, string> formats, List<Font> fonts, List<string?> fills, List<(bool Border, string? Color)> borders)
    {
        var font = Pick(fonts, xml.GetAttribute("fontId")) ?? new Font("Calibri", 11, false, false, false, false, null);
        var border = Pick(borders, xml.GetAttribute("borderId"));
        var formatId = int.TryParse(xml.GetAttribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;
        var style = new XlsxStyle
        {
            FontName = font.Name,
            FontSize = font.Size > 0 ? font.Size : 11,
            Bold = font.Bold,
            Italic = font.Italic,
            Underline = font.Underline,
            Strike = font.Strike,
            FontColor = font.Color,
            FillColor = Pick(fills, xml.GetAttribute("fillId")),
            NumberFormat = formats.GetValueOrDefault(formatId, "General"),
            Border = border.Border,
            BorderColor = border.Color,
            QuotePrefix = XlsxPackageReader.IsTrue(xml.GetAttribute("quotePrefix")),
        };
        foreach (var (name, reader) in Children(xml))
        {
            if (name == "alignment")
            {
                style = style with
                {
                    HorizontalAlignment = Enum.TryParse<XlsxHorizontalAlignment>(reader.GetAttribute("horizontal"), ignoreCase: true, out var h) ? h : XlsxHorizontalAlignment.General,
                    VerticalAlignment = Enum.TryParse<XlsxVerticalAlignment>(reader.GetAttribute("vertical"), ignoreCase: true, out var v) ? v : XlsxVerticalAlignment.Bottom,
                    WrapText = XlsxPackageReader.IsTrue(reader.GetAttribute("wrapText")),
                };
            }
        }

        return style;
    }

    private static T? Pick<T>(List<T> items, string? index) =>
        int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i >= 0 && i < items.Count ? items[i] : default;

    private static bool On(XmlReader xml) => xml.GetAttribute("val") is null or "1" or "true";

    private static string? Rgb(XmlReader xml)
    {
        var rgb = xml.GetAttribute("rgb");
        return rgb is { Length: 8 or 6 } && rgb.All(char.IsAsciiHexDigit) ? XlsxStyle.NormalizeColor(rgb) : null;
    }

    // The descendant elements of the current element, leaving the reader on its end tag.
    private static IEnumerable<(string Name, XmlReader Reader)> Children(XmlReader xml)
    {
        if (xml.IsEmptyElement)
        {
            yield break;
        }

        var depth = xml.Depth;
        while (xml.Read() && xml.Depth > depth)
        {
            if (xml.NodeType == XmlNodeType.Element)
            {
                yield return (xml.LocalName, xml);
            }
        }
    }
}
