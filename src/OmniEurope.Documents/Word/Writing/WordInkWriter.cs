// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>
/// Writes freehand ink as a Word 2010 drawing (<c>w14:contentPart</c>) naming an InkML part. The InkML coordinates are
/// whole thousandths of a centimetre from the top-left corner of the box, and <c>w14:xfrm</c> says how large the
/// strokes reach, so the drawing reads back with its strokes where they were. The VML picture Word keeps as a fallback
/// for older readers is not written.
/// </summary>
internal static class WordInkWriter
{
    private const double PointsPerCentimetre = 72 / 2.54;
    private const double UnitsPerCentimetre = 1000;
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    private static readonly XNamespace Wpi = "http://schemas.microsoft.com/office/word/2010/wordprocessingInk";
    private static readonly XNamespace InkMl = "http://www.w3.org/2003/InkML";

    /// <summary>The drawing, in alternate content because the ink namespace is a Word 2010 extension.</summary>
    public static XElement Ink(int id, WordInk ink, string relationshipId, long? relativeHeight = null)
    {
        var points = ink.Strokes.SelectMany(s => s.Points).ToList();
        var (cx, cy) = points.Count == 0 ? (ToEmu(ink.Width), ToEmu(ink.Height)) : (ToEmu(Math.Max(0, points.Max(p => p.X))), ToEmu(Math.Max(0, points.Max(p => p.Y))));
        var graphic = new XElement(
            A + "graphic",
            new XElement(
                A + "graphicData",
                new XAttribute("uri", InkUri),
                new XElement(
                    W14 + "contentPart",
                    new XAttribute(XNamespace.Xmlns + "w14", W14),
                    new XAttribute("bwMode", "auto"),
                    new XAttribute(R + "id", relationshipId),
                    new XElement(W14 + "nvContentPartPr", new XElement(W14 + "cNvContentPartPr")),
                    new XElement(
                        W14 + "xfrm",
                        new XElement(A + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
                        new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))))));
        var drawing = WordDrawingWriter.Frame(ink, id, "Ink", graphic, new XElement(Wp + "cNvGraphicFramePr"), relativeHeight);
        return new XElement(
            Mc + "AlternateContent",
            new XElement(Mc + "Choice", new XAttribute(XNamespace.Xmlns + "wpi", Wpi), new XAttribute("Requires", "wpi"), drawing));
    }

    /// <summary>The InkML part: one brush per distinct pen and one trace per stroke.</summary>
    public static XElement Part(WordInk ink)
    {
        var pens = ink.Strokes.Select(s => (s.Width, s.Color, s.Opacity)).Distinct().ToList();
        var definitions = new XElement(
            InkMl + "definitions",
            new XElement(
                InkMl + "context",
                new XAttribute(XNamespace.Xml + "id", "ctx0"),
                new XElement(
                    InkMl + "inkSource",
                    new XAttribute(XNamespace.Xml + "id", "inkSrc0"),
                    new XElement(InkMl + "traceFormat", Channel("X"), Channel("Y")),
                    new XElement(InkMl + "channelProperties", Resolution("X"), Resolution("Y")))));
        for (var i = 0; i < pens.Count; i++)
        {
            definitions.Add(Brush(i, pens[i].Width, pens[i].Color, pens[i].Opacity));
        }

        var root = new XElement(InkMl + "ink", new XAttribute(XNamespace.Xmlns + "inkml", InkMl), definitions);
        foreach (var stroke in ink.Strokes.Where(s => s.Points.Count > 0))
        {
            var pen = pens.IndexOf((stroke.Width, stroke.Color, stroke.Opacity));
            root.Add(new XElement(InkMl + "trace", new XAttribute("contextRef", "#ctx0"), new XAttribute("brushRef", "#br" + Format(pen)), Trace(stroke)));
        }

        return root;
    }

    private static string Trace(WordInkStroke stroke) =>
        string.Join(',', stroke.Points.Select(p => Units(p.X).ToString(CultureInfo.InvariantCulture) + " " + Units(p.Y).ToString(CultureInfo.InvariantCulture)));

    private static long Units(double points) => (long)Math.Round(points / PointsPerCentimetre * UnitsPerCentimetre);

    private static XElement Channel(string name) =>
        new(InkMl + "channel", new XAttribute("name", name), new XAttribute("type", "integer"), new XAttribute("units", "cm"));

    private static XElement Resolution(string channel) =>
        new(InkMl + "channelProperty", new XAttribute("channel", channel), new XAttribute("name", "resolution"), new XAttribute("value", Format(UnitsPerCentimetre)), new XAttribute("units", "1/cm"));

    private static XElement Brush(int index, double width, string color, double opacity)
    {
        var centimetres = Format(Math.Round(width / PointsPerCentimetre, 4));
        var brush = new XElement(
            InkMl + "brush",
            new XAttribute(XNamespace.Xml + "id", "br" + Format(index)),
            Property("width", centimetres, "cm"),
            Property("height", centimetres, "cm"),
            Property("color", "#" + color, null));
        if (opacity < 1)
        {
            brush.Add(Property("transparency", Format((int)Math.Round((1 - opacity) * 255)), null));
        }

        return brush;
    }

    private static XElement Property(string name, string value, string? units)
    {
        var property = new XElement(InkMl + "brushProperty", new XAttribute("name", name), new XAttribute("value", value));
        if (units is not null)
        {
            property.Add(new XAttribute("units", units));
        }

        return property;
    }
}
