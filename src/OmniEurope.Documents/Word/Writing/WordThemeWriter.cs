// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>
/// Writes the theme part of a style sheet. A theme loaded from a document (<see cref="WordStyleSheet.Theme"/>) is written back
/// unchanged, except that the latin typefaces of its major and minor fonts follow <see cref="WordStyleSheet.MajorFont"/> and
/// <see cref="WordStyleSheet.MinorFont"/> when those differ. Without a loaded theme, styles that name a theme font
/// (<c>asciiTheme="minorHAnsi"</c>) need one to resolve: a default theme is built from the model's two fonts, with Office's
/// default colour and format schemes, and none is written when no theme font is named.
/// </summary>
internal static class WordThemeWriter
{
    private static readonly (string Name, string Colour)[] Colours =
    [
        ("dk2", "44546A"), ("lt2", "E7E6E6"), ("accent1", "4472C4"), ("accent2", "ED7D31"), ("accent3", "A5A5A5"), ("accent4", "FFC000"),
        ("accent5", "5B9BD5"), ("accent6", "70AD47"), ("hlink", "0563C1"), ("folHlink", "954F72"),
    ];

    /// <summary>The theme, or null when the style sheet names no theme font.</summary>
    public static XElement? Theme(WordStyleSheet styles)
    {
        if (styles.Theme is { } original)
        {
            return Loaded(original, styles);
        }

        if (styles.MajorFont is null && styles.MinorFont is null)
        {
            return null;
        }

        var major = styles.MajorFont ?? styles.MinorFont!;
        var minor = styles.MinorFont ?? styles.MajorFont!;
        var colours = new XElement(
            A + "clrScheme",
            new XAttribute("name", "Office"),
            new XElement(A + "dk1", new XElement(A + "sysClr", new XAttribute("val", "windowText"), new XAttribute("lastClr", "000000"))),
            new XElement(A + "lt1", new XElement(A + "sysClr", new XAttribute("val", "window"), new XAttribute("lastClr", "FFFFFF"))));
        colours.Add(Colours.Select(c => new XElement(A + c.Name, new XElement(A + "srgbClr", new XAttribute("val", c.Colour)))));
        return new XElement(
            A + "theme",
            new XAttribute(XNamespace.Xmlns + "a", A),
            new XAttribute("name", "Office Theme"),
            new XElement(
                A + "themeElements",
                colours,
                new XElement(A + "fontScheme", new XAttribute("name", "Office"), FontSet("majorFont", major), FontSet("minorFont", minor)),
                Formats()));
    }

    // The loaded theme, copied, with the latin typefaces of its font scheme set to the model's fonts.
    private static XElement Loaded(XElement original, WordStyleSheet styles)
    {
        var theme = new XElement(original);
        var scheme = theme.Descendants(A + "fontScheme").FirstOrDefault();
        SetLatin(scheme?.Element(A + "majorFont"), styles.MajorFont);
        SetLatin(scheme?.Element(A + "minorFont"), styles.MinorFont);
        return theme;
    }

    private static void SetLatin(XElement? set, string? typeface)
    {
        if (typeface is not null && set?.Element(A + "latin") is { } latin && (string?)latin.Attribute("typeface") != typeface)
        {
            latin.SetAttributeValue("typeface", typeface);
        }
    }

    private static XElement FontSet(string name, string latin) =>
        new(A + name, new XElement(A + "latin", new XAttribute("typeface", latin)), new XElement(A + "ea", new XAttribute("typeface", string.Empty)), new XElement(A + "cs", new XAttribute("typeface", string.Empty)));

    // The format scheme the schema requires: three plain styles of each kind.
    private static XElement Formats() => new(
        A + "fmtScheme",
        new XAttribute("name", "Office"),
        new XElement(A + "fillStyleLst", Repeat(Fill)),
        new XElement(A + "lnStyleLst", Repeat(() => new XElement(A + "ln", new XAttribute("w", "6350"), Fill(), new XElement(A + "prstDash", new XAttribute("val", "solid"))))),
        new XElement(A + "effectStyleLst", Repeat(() => new XElement(A + "effectStyle", new XElement(A + "effectLst")))),
        new XElement(A + "bgFillStyleLst", Repeat(Fill)));

    private static XElement Fill() => new(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr")));

    private static IEnumerable<XElement> Repeat(Func<XElement> make) => Enumerable.Range(0, 3).Select(_ => make());
}
