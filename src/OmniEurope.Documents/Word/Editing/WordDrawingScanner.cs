// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;
using OmniEurope.Documents.Word.Reading;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// A drawing of a paragraph: <see cref="Unit"/> is what leaves the run when the drawing is removed (the
/// alternate content holding a DrawingML drawing and its VML fallback, else the <c>w:drawing</c> or <c>w:pict</c>
/// itself) and <see cref="Frame"/> is the <c>wp:anchor</c> or <c>wp:inline</c> (null for VML).
/// </summary>
internal sealed record ScannedDrawing(XElement Unit, XElement Content, XElement? Frame, WordDrawingKind Kind, double Width, double Height, WordFloatingPosition? Floating)
{
    public WordDrawingInfo Info(int index)
    {
        var docPr = Frame?.Element(Wp + "docPr");
        var shape = Frame is null ? Content.Descendants(V + "shape").FirstOrDefault() : null;
        var info = new WordDrawingInfo(index, Kind, Width, Height)
        {
            Name = NonEmpty((string?)docPr?.Attribute("name") ?? (string?)shape?.Attribute("id")),
            Description = NonEmpty((string?)docPr?.Attribute("descr") ?? (string?)docPr?.Attribute("title") ?? (string?)shape?.Attribute(O + "title")),
        };
        return Floating is not { } floating
            ? info
            : info with
            {
                IsFloating = true,
                OffsetX = floating.HorizontalAlignment is null ? floating.HorizontalOffset : 0,
                OffsetY = floating.VerticalAlignment is null ? floating.VerticalOffset : 0,
                HorizontalRelativeTo = floating.HorizontalRelativeTo,
                VerticalRelativeTo = floating.VerticalRelativeTo,
                Wrap = floating.Wrap,
                BehindText = floating.BehindText,
            };
    }

    private static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Finds the drawings a paragraph owns, in document order: those of its own runs, never those of a text box
/// it anchors, never deleted ones, never the VML fallback of an alternate content.</summary>
internal static class WordDrawingScanner
{
    public static List<ScannedDrawing> Scan(XElement paragraph)
    {
        var result = new List<ScannedDrawing>();
        foreach (var run in WordRunScanner.Runs(paragraph).Select(r => r.Element))
        {
            foreach (var child in run.Elements())
            {
                var content = Content(child);
                if (content is not null)
                {
                    result.Add(content.Name == W + "drawing" ? Drawing(child, content) : Vml(child, content));
                }
            }
        }

        return result;
    }

    // The w:drawing or w:pict a run child stands for: the child itself or the one chosen by an alternate content.
    private static XElement? Content(XElement child)
    {
        if (child.Name == W + "drawing" || child.Name == W + "pict")
        {
            return child;
        }

        return child.Name == Mc + "AlternateContent"
            ? child.Elements(Mc + "Choice").Select(c => c.Element(W + "drawing") ?? c.Element(W + "pict")).FirstOrDefault(e => e is not null)
            : null;
    }

    private static ScannedDrawing Drawing(XElement unit, XElement drawing)
    {
        var frame = drawing.Element(Wp + "anchor") ?? drawing.Element(Wp + "inline");
        var extent = frame?.Element(Wp + "extent");
        var (width, height) = (Measure((string?)extent?.Attribute("cx"), EmuPerPoint) ?? 0, Measure((string?)extent?.Attribute("cy"), EmuPerPoint) ?? 0);
        var floating = frame is not null && frame.Name == Wp + "anchor" ? WordDrawingReader.Floating(frame) : null;
        return new ScannedDrawing(unit, drawing, frame, KindOf(frame?.Element(A + "graphic")?.Element(A + "graphicData")), width, height, floating);
    }

    private static WordDrawingKind KindOf(XElement? data) => (string?)data?.Attribute("uri") switch
    {
        PictureUri => WordDrawingKind.Picture,
        InkUri => WordDrawingKind.Ink,
        ChartUri => WordDrawingKind.Chart,
        ShapeUri when data!.Descendants(W + "txbxContent").Any() => WordDrawingKind.TextBox,
        _ => WordDrawingKind.Other,
    };

    private static ScannedDrawing Vml(XElement unit, XElement pict)
    {
        var shape = pict.Descendants(V + "shape").FirstOrDefault() ?? pict.Descendants(V + "rect").FirstOrDefault();
        var style = WordDrawingReader.Css((string?)shape?.Attribute("style"));
        var width = WordDrawingReader.CssLength(style.GetValueOrDefault("width", "0"));
        var height = WordDrawingReader.CssLength(style.GetValueOrDefault("height", "0"));
        var kind = pict.Descendants(V + "imagedata").Any() ? WordDrawingKind.Picture : pict.Descendants(W + "txbxContent").Any() ? WordDrawingKind.TextBox : WordDrawingKind.Other;
        return new ScannedDrawing(unit, pict, null, kind, width, height, WordDrawingReader.VmlFloating(style, pict));
    }

    private const double EmuPerPoint = 12700;
}
