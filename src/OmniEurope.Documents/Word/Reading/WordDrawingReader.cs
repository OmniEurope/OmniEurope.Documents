// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Reading;

/// <summary>
/// Reads DrawingML (<c>w:drawing</c>) and VML (<c>w:pict</c>, <c>w:object</c>) objects into pictures and text
/// boxes. Charts, diagrams, grouped and geometric shapes have no model: they are skipped and reported as gaps.
/// Linked (not embedded) pictures are skipped too: nothing is ever fetched.
/// </summary>
internal sealed class WordDrawingReader(WordContentReader owner)
{
    private const double EmuPerPoint = 12700;

    public WordShape? Drawing(XElement drawing)
    {
        var frame = drawing.Element(Wp + "inline") ?? drawing.Element(Wp + "anchor");
        if (frame is null)
        {
            return null;
        }

        var extent = frame.Element(Wp + "extent");
        var width = Measure((string?)extent?.Attribute("cx"), EmuPerPoint) ?? 0;
        var height = Measure((string?)extent?.Attribute("cy"), EmuPerPoint) ?? 0;
        var data = frame.Element(A + "graphic")?.Element(A + "graphicData");
        var shape = (string?)data?.Attribute("uri") switch
        {
            PictureUri => Picture(data!.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed")?.Value, width, height),
            ShapeUri => TextBox(data!.Descendants(W + "txbxContent").FirstOrDefault(), width, height, "shape without text"),
            var uri => Unsupported(uri),
        };
        if (shape is null)
        {
            return null;
        }

        var properties = frame.Element(Wp + "docPr");
        shape.Description = NonEmpty((string?)properties?.Attribute("descr")) ?? NonEmpty((string?)properties?.Attribute("title"));
        shape.Floating = frame.Name.LocalName == "anchor" ? Floating(frame) : null;
        return shape;
    }

    public WordShape? Vml(XElement container)
    {
        var shape = container.Descendants(V + "shape").FirstOrDefault() ?? container.Descendants(V + "rect").FirstOrDefault();
        var (width, height) = VmlSize((string?)shape?.Attribute("style"));
        var imageData = container.Descendants(V + "imagedata").FirstOrDefault();
        if (imageData is not null)
        {
            return Picture(imageData.Attribute(R + "id")?.Value, width, height);
        }

        var box = container.Descendants(W + "txbxContent").FirstOrDefault();
        return box is null ? Unsupported("VML shape") : TextBox(box, width, height, "VML shape");
    }

    private WordPicture? Picture(string? relationshipId, double width, double height)
    {
        var relationship = owner.Relationship(relationshipId);
        if (relationship is null || relationship.External)
        {
            owner.Context.Gaps.Add("linked or missing picture skipped");
            return null;
        }

        var image = owner.Context.Image(relationship.Target);
        return image is null ? null : new WordPicture(image, width, height);
    }

    private WordTextBox? TextBox(XElement? content, double width, double height, string gap)
    {
        if (content is null)
        {
            owner.Context.Gaps.Add(gap + " skipped");
            return null;
        }

        var box = new WordTextBox(width, height);
        box.Blocks.AddRange(new WordContentReader(owner.Context, owner.Part).Blocks(content));
        return box;
    }

    private WordShape? Unsupported(string? kind)
    {
        var name = kind switch
        {
            null => "drawing",
            _ when kind.Contains("chart", StringComparison.OrdinalIgnoreCase) => "chart",
            _ when kind.Contains("diagram", StringComparison.OrdinalIgnoreCase) => "diagram",
            _ when kind.Contains("Group", StringComparison.OrdinalIgnoreCase) => "group of shapes",
            _ => "drawing",
        };
        owner.Context.Gaps.Add(name + " skipped");
        return null;
    }

    private static WordFloatingPosition Floating(XElement anchor)
    {
        var horizontal = anchor.Element(Wp + "positionH");
        var vertical = anchor.Element(Wp + "positionV");
        return new WordFloatingPosition(
            Measure(horizontal?.Element(Wp + "posOffset")?.Value, EmuPerPoint) ?? 0,
            (string?)horizontal?.Attribute("relativeFrom") ?? "column",
            Measure(vertical?.Element(Wp + "posOffset")?.Value, EmuPerPoint) ?? 0,
            (string?)vertical?.Attribute("relativeFrom") ?? "paragraph",
            Wrap(anchor),
            (string?)anchor.Attribute("behindDoc") is "1" or "true");
    }

    private static WordWrap Wrap(XElement anchor)
    {
        foreach (var element in anchor.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "wrapNone":
                    return WordWrap.None;
                case "wrapSquare":
                    return WordWrap.Square;
                case "wrapTight":
                    return WordWrap.Tight;
                case "wrapThrough":
                    return WordWrap.Through;
                case "wrapTopAndBottom":
                    return WordWrap.TopAndBottom;
            }
        }

        return WordWrap.None;
    }

    // VML sizes come from CSS: "width:120pt;height:3cm".
    private static (double Width, double Height) VmlSize(string? style)
    {
        double width = 0, height = 0;
        foreach (var declaration in (style ?? string.Empty).Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var name = declaration[..colon].Trim();
            var value = CssLength(declaration[(colon + 1)..].Trim());
            if (name == "width")
            {
                width = value;
            }
            else if (name == "height")
            {
                height = value;
            }
        }

        return (width, height);
    }

    private static double CssLength(string value)
    {
        if (value.EndsWith("px", StringComparison.Ordinal))
        {
            return double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels) ? pixels * 0.75 : 0;
        }

        return Measure(value, 1) ?? 0;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
