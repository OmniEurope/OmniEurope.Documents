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
        var style = Css((string?)shape?.Attribute("style"));
        var (width, height) = (CssLength(style.GetValueOrDefault("width", "0")), CssLength(style.GetValueOrDefault("height", "0")));
        var imageData = container.Descendants(V + "imagedata").FirstOrDefault();
        var box = container.Descendants(W + "txbxContent").FirstOrDefault();
        WordShape? result = imageData is not null ? Picture(imageData.Attribute(R + "id")?.Value, width, height)
            : box is null ? Unsupported("VML shape")
            : TextBox(box, width, height, "VML shape");
        if (result is not null)
        {
            result.Floating = VmlFloating(style, container);
        }

        return result;
    }

    // A VML shape positioned absolutely floats: its CSS margins place it from the column (or margin, page, character)
    // and the paragraph (or margin, page, line); without a w10:wrap it lies over the text, a negative z-index behind it.
    private static WordFloatingPosition? VmlFloating(Dictionary<string, string> style, XElement container)
    {
        if (!string.Equals(style.GetValueOrDefault("position"), "absolute", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var wrapElement = container.Descendants(W10 + "wrap").FirstOrDefault();
        var wrap = (string?)wrapElement?.Attribute("type") ?? string.Empty;
        return new WordFloatingPosition(
            CssLength(style.GetValueOrDefault("margin-left", "0")),
            VmlHorizontal.GetValueOrDefault(style.GetValueOrDefault("mso-position-horizontal-relative", string.Empty), "column"),
            CssLength(style.GetValueOrDefault("margin-top", "0")),
            VmlVertical.GetValueOrDefault(style.GetValueOrDefault("mso-position-vertical-relative", string.Empty), "paragraph"),
            VmlWraps.GetValueOrDefault(wrap, WordWrap.None),
            int.TryParse(style.GetValueOrDefault("z-index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) && z < 0)
        {
            DistanceTop = CssLength(style.GetValueOrDefault("mso-wrap-distance-top", "0")),
            DistanceBottom = CssLength(style.GetValueOrDefault("mso-wrap-distance-bottom", "0")),
            DistanceLeft = CssLength(style.GetValueOrDefault("mso-wrap-distance-left", "0")),
            DistanceRight = CssLength(style.GetValueOrDefault("mso-wrap-distance-right", "0")),
            WrapSide = Side((string?)wrapElement?.Attribute("side")),
        };
    }

    private static WordWrapSide Side(string? value) => value switch
    {
        "left" => WordWrapSide.Left,
        "right" => WordWrapSide.Right,
        "largest" => WordWrapSide.Largest,
        _ => WordWrapSide.BothSides,
    };

    // What VML positions from and how it wraps, as the anchored drawing's names say it.
    private static readonly Dictionary<string, string> VmlHorizontal = new(StringComparer.Ordinal) { ["margin"] = "margin", ["page"] = "page", ["char"] = "character" };

    private static readonly Dictionary<string, string> VmlVertical = new(StringComparer.Ordinal) { ["margin"] = "margin", ["page"] = "page", ["line"] = "line" };

    private static readonly Dictionary<string, WordWrap> VmlWraps = new(StringComparer.Ordinal)
    {
        ["square"] = WordWrap.Square,
        ["tight"] = WordWrap.Tight,
        ["through"] = WordWrap.Through,
        ["topAndBottom"] = WordWrap.TopAndBottom,
    };

    // The declarations of a CSS style attribute ("width:120pt;height:3cm"), by property name.
    private static Dictionary<string, string> Css(string? style)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in (style ?? string.Empty).Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon > 0)
            {
                declarations[declaration[..colon].Trim()] = declaration[(colon + 1)..].Trim();
            }
        }

        return declarations;
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

    // The anchor's position (an offset or an alignment in its reference, or the simple position from the page corner),
    // the room kept free around it and the sides text wraps on.
    private static WordFloatingPosition Floating(XElement anchor)
    {
        var horizontal = anchor.Element(Wp + "positionH");
        var vertical = anchor.Element(Wp + "positionV");
        var wrap = Wrap(anchor);
        var position = new WordFloatingPosition(
            Measure(horizontal?.Element(Wp + "posOffset")?.Value, EmuPerPoint) ?? 0,
            (string?)horizontal?.Attribute("relativeFrom") ?? "column",
            Measure(vertical?.Element(Wp + "posOffset")?.Value, EmuPerPoint) ?? 0,
            (string?)vertical?.Attribute("relativeFrom") ?? "paragraph",
            wrap.Wrap,
            (string?)anchor.Attribute("behindDoc") is "1" or "true")
        {
            HorizontalAlignment = NonEmpty(horizontal?.Element(Wp + "align")?.Value),
            VerticalAlignment = NonEmpty(vertical?.Element(Wp + "align")?.Value),
            DistanceTop = Emu(anchor, "distT"),
            DistanceBottom = Emu(anchor, "distB"),
            DistanceLeft = Emu(anchor, "distL"),
            DistanceRight = Emu(anchor, "distR"),
            WrapSide = wrap.Side,
        };
        if ((string?)anchor.Attribute("simplePos") is "1" or "true" && anchor.Element(Wp + "simplePos") is { } simple)
        {
            position = position with
            {
                HorizontalOffset = Emu(simple, "x"),
                HorizontalRelativeTo = "page",
                VerticalOffset = Emu(simple, "y"),
                VerticalRelativeTo = "page",
                HorizontalAlignment = null,
                VerticalAlignment = null,
            };
        }

        return position;
    }

    private static double Emu(XElement element, string attribute) => Measure((string?)element.Attribute(attribute), EmuPerPoint) ?? 0;

    private static (WordWrap Wrap, WordWrapSide Side) Wrap(XElement anchor)
    {
        foreach (var element in anchor.Elements())
        {
            var side = Side((string?)element.Attribute("wrapText"));
            switch (element.Name.LocalName)
            {
                case "wrapNone":
                    return (WordWrap.None, side);
                case "wrapSquare":
                    return (WordWrap.Square, side);
                case "wrapTight":
                    return (WordWrap.Tight, side);
                case "wrapThrough":
                    return (WordWrap.Through, side);
                case "wrapTopAndBottom":
                    return (WordWrap.TopAndBottom, side);
            }
        }

        return (WordWrap.None, WordWrapSide.BothSides);
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
