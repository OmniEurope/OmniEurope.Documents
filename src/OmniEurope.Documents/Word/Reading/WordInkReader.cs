// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>
/// Reads Word 2010 freehand ink (<c>w14:contentPart</c> naming an InkML part) into a <see cref="WordInk"/>. The InkML
/// coordinates are centimetres from the top-left corner of the drawing's box, which Word sizes to the strokes: the
/// box is scaled by what <c>w14:xfrm</c> says over the extent of the strokes, so a resized drawing scales its ink.
/// When the InkML part is missing or unreadable, the picture of the <c>mc:Fallback</c> (VML) stands in for it.
/// </summary>
internal sealed class WordInkReader(WordContentReader owner, WordDrawingReader drawings)
{
    private const double PointsPerCentimetre = 72 / 2.54;
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    public WordShape? Read(XElement data, double width, double height)
    {
        var traces = Traces(data);
        if (traces.Count > 0)
        {
            return Ink(traces, data, width, height);
        }

        var fallback = data.Ancestors(Mc + "Choice").FirstOrDefault()?.Parent?.Element(Mc + "Fallback");
        if (fallback is null || fallback.Descendants(V + "imagedata").FirstOrDefault() is null)
        {
            owner.Context.Gaps.Add("ink skipped");
            return null;
        }

        // The picture the ink was saved with, drawn in the anchor's box.
        var picture = drawings.Vml(fallback);
        if (picture is not null)
        {
            (picture.Width, picture.Height) = (width, height);
        }

        return picture;
    }

    private List<InkTrace> Traces(XElement data)
    {
        var relationship = owner.Relationship(data.Element(W14 + "contentPart")?.Attribute(R + "id")?.Value);
        if (relationship is null || relationship.External)
        {
            return [];
        }

        try
        {
            return owner.Context.Package.GetXml(relationship.Target)?.Root is { } root ? InkMlReader.Read(root) : [];
        }
        catch (Exception exception) when (exception is DocumentFormatException or System.Xml.XmlException)
        {
            // An ink part that cannot be read costs the strokes, not the document; its saved picture may stand in.
            owner.Context.Gaps.Add($"unreadable ink part: {exception.Message}");
            return [];
        }
    }

    private static WordInk Ink(List<InkTrace> traces, XElement data, double width, double height)
    {
        var points = traces.SelectMany(t => t.Points).ToList();
        var (reachX, reachY) = (points.Max(p => p.X) * PointsPerCentimetre, points.Max(p => p.Y) * PointsPerCentimetre);
        var frame = data.Element(W14 + "contentPart")?.Element(W14 + "xfrm")?.Element(A + "ext");
        var (boxWidth, boxHeight) = (Emu(frame, "cx") ?? width, Emu(frame, "cy") ?? height);
        var scaleX = reachX > 1 && boxWidth > 0 ? boxWidth / reachX : 1;
        var scaleY = reachY > 1 && boxHeight > 0 ? boxHeight / reachY : 1;
        var ink = new WordInk(width, height);
        foreach (var trace in traces)
        {
            var stroke = trace.Points.Select(p => (p.X * PointsPerCentimetre * scaleX, p.Y * PointsPerCentimetre * scaleY)).ToList();
            ink.Strokes.Add(new WordInkStroke(stroke, trace.Width * PointsPerCentimetre * Math.Sqrt(scaleX * scaleY), trace.Color, trace.Opacity));
        }

        return ink;
    }

    private static double? Emu(XElement? element, string attribute) => Measure((string?)element?.Attribute(attribute), 12700);
}
