// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>
/// Writes the theme part a style sheet needs: styles that name a theme font (<c>asciiTheme="minorHAnsi"</c>) mean nothing
/// without one. Only the fonts of the model are its own (<see cref="WordStyleSheet.MajorFont"/>,
/// <see cref="WordStyleSheet.MinorFont"/>); the colour and format schemes are Office's defaults, the model keeping
/// neither.
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
