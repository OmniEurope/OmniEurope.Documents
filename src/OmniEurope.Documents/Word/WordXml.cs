// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;

namespace OmniEurope.Documents.Word;

/// <summary>Namespaces, relationship types, content types and value conversions of WordprocessingML.</summary>
internal static class WordXml
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    public static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    public static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    public static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    public static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    public static readonly XNamespace W10 = "urn:schemas-microsoft-com:office:word";
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    public static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    public static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    public static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    public static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
    public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    public static readonly XNamespace StrictW = "http://purl.oclc.org/ooxml/wordprocessingml/main";

    public const string PictureUri = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    public const string ShapeUri = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    private const string RelationshipBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    public const string OfficeDocumentType = RelationshipBase + "officeDocument";
    public const string StylesType = RelationshipBase + "styles";
    public const string NumberingType = RelationshipBase + "numbering";
    public const string SettingsType = RelationshipBase + "settings";
    public const string FootnotesType = RelationshipBase + "footnotes";
    public const string EndnotesType = RelationshipBase + "endnotes";
    public const string CommentsType = RelationshipBase + "comments";
    public const string HeaderType = RelationshipBase + "header";
    public const string FooterType = RelationshipBase + "footer";
    public const string ImageType = RelationshipBase + "image";
    public const string HyperlinkType = RelationshipBase + "hyperlink";
    public const string ThemeType = RelationshipBase + "theme";
    public const string ExtendedPropertiesType = RelationshipBase + "extended-properties";
    public const string CorePropertiesType = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";

    private const string WordContent = "application/vnd.openxmlformats-officedocument.wordprocessingml.";
    public const string MainContentType = WordContent + "document.main+xml";
    public const string TemplateContentType = WordContent + "template.main+xml";
    public const string StylesContentType = WordContent + "styles+xml";
    public const string NumberingContentType = WordContent + "numbering+xml";
    public const string SettingsContentType = WordContent + "settings+xml";
    public const string FootnotesContentType = WordContent + "footnotes+xml";
    public const string EndnotesContentType = WordContent + "endnotes+xml";
    public const string CommentsContentType = WordContent + "comments+xml";
    public const string HeaderContentType = WordContent + "header+xml";
    public const string FooterContentType = WordContent + "footer+xml";
    public const string CoreContentType = "application/vnd.openxmlformats-package.core-properties+xml";
    public const string ExtendedContentType = "application/vnd.openxmlformats-officedocument.extended-properties+xml";
    public const string RelationshipsContentType = "application/vnd.openxmlformats-package.relationships+xml";

    /// <summary>The <c>w:val</c> attribute of a child element, or null.</summary>
    public static string? Val(XElement? parent, string child) => parent?.Element(W + child)?.Attribute(W + "val")?.Value;

    public static string? Attr(XElement? element, string name) => element?.Attribute(W + name)?.Value;

    /// <summary>An on/off property: absent is null, present without value or with a true value is true.</summary>
    public static bool? OnOff(XElement? parent, string child)
    {
        var element = parent?.Element(W + child);
        if (element is null)
        {
            return null;
        }

        var value = element.Attribute(W + "val")?.Value;
        return value is null || value is "1" or "true" or "on";
    }

    public static int? Int(string? value) =>
        value is not null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    /// <summary>A twentieth-of-a-point measure (or a universal measure such as <c>2.5cm</c>) in points.</summary>
    public static double? Twips(string? value) => Measure(value, 20);

    /// <summary>
    /// A number in units where <paramref name="perPoint"/> units make a point, or a universal measure
    /// (<c>mm</c>, <c>cm</c>, <c>in</c>, <c>pt</c>, <c>pc</c>, <c>pi</c>); the result is in points.
    /// </summary>
    public static double? Measure(string? value, double perPoint)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        var unit = value.Length > 2 && char.IsAsciiLetter(value[^1]) ? value[^2..] : null;
        var number = unit is null ? value : value[..^2];
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            return null;
        }

        return unit switch
        {
            null => n / perPoint,
            "pt" => n,
            "in" => n * 72,
            "cm" => n * 72 / 2.54,
            "mm" => n * 72 / 25.4,
            "pc" or "pi" => n * 12,
            _ => null,
        };
    }

    public static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Points to whole twentieths of a point.</summary>
    public static string ToTwips(double points) => Format((int)Math.Round(points * 20));

    public static long ToEmu(double points) => (long)Math.Round(points * 12700);

    public static XElement ValElement(string name, string value) => new(W + name, new XAttribute(W + "val", value));

    /// <summary>A <c>w:t</c> (or <c>w:delText</c>) element, preserving surrounding spaces.</summary>
    public static XElement TextElement(string text, bool deleted = false)
    {
        var element = new XElement(W + (deleted ? "delText" : "t"), text);
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
        {
            element.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
        }

        return element;
    }
}
