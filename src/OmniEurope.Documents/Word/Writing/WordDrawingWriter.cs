// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Writing;

/// <summary>State shared by the parts of one document being written.</summary>
internal sealed class WordWriteContext
{
    private readonly Dictionary<WordImage, string> _images = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, string> _byContent = new(StringComparer.Ordinal);
    private int _drawingId;
    private int _revisionId;

    /// <summary>Media parts by name with their bytes and content type, in creation order.</summary>
    public List<(string Name, byte[] Data, string ContentType)> Media { get; } = [];

    /// <summary>Text width of the section being written, for tables without explicit columns.</summary>
    public double ContentWidth { get; set; } = WordPageSetup.A4.ContentWidth;

    public int NextDrawingId() => ++_drawingId;

    public int NextRevisionId() => _revisionId++;

    /// <summary>The media part of an image; identical bytes share one part.</summary>
    public string ImagePart(WordImage image)
    {
        if (_images.TryGetValue(image, out var known))
        {
            return known;
        }

        var hash = Convert.ToHexString(SHA256.HashData(image.Data));
        if (!_byContent.TryGetValue(hash, out var name))
        {
            name = "word/media/image" + (Media.Count + 1).ToString(CultureInfo.InvariantCulture) + "." + image.Extension;
            Media.Add((name, image.Data, image.ContentType));
            _byContent[hash] = name;
        }

        _images[image] = name;
        return name;
    }
}

/// <summary>Writes pictures and text boxes as DrawingML, inline or anchored.</summary>
internal static class WordDrawingWriter
{
    public static XElement Picture(WordContentWriter writer, WordPicture picture, string relationshipId)
    {
        var id = writer.Context.NextDrawingId();
        var (cx, cy) = (ToEmu(picture.Width), ToEmu(picture.Height));
        var graphic = new XElement(
            A + "graphic",
            new XElement(
                A + "graphicData",
                new XAttribute("uri", PictureUri),
                new XElement(
                    Pic + "pic",
                    new XElement(Pic + "nvPicPr", new XElement(Pic + "cNvPr", new XAttribute("id", "0"), new XAttribute("name", "Picture " + Format(id))), new XElement(Pic + "cNvPicPr")),
                    new XElement(Pic + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", relationshipId)), new XElement(A + "stretch", new XElement(A + "fillRect"))),
                    new XElement(Pic + "spPr", Transform(cx, cy), Rectangle()))));
        return Frame(picture, id, "Picture", graphic, new XElement(Wp + "cNvGraphicFramePr", new XElement(A + "graphicFrameLocks", new XAttribute("noChangeAspect", "1"))));
    }

    /// <summary>A text box, wrapped in alternate content because the shape namespace is a Word 2010 extension.</summary>
    public static XElement TextBox(WordContentWriter writer, WordTextBox box)
    {
        var id = writer.Context.NextDrawingId();
        var (cx, cy) = (ToEmu(box.Width), ToEmu(box.Height));
        var graphic = new XElement(
            A + "graphic",
            new XElement(
                A + "graphicData",
                new XAttribute("uri", ShapeUri),
                new XElement(
                    Wps + "wsp",
                    new XElement(Wps + "cNvSpPr", new XAttribute("txBox", "1")),
                    new XElement(Wps + "spPr", Transform(cx, cy), Rectangle(), new XElement(A + "noFill")),
                    new XElement(Wps + "txbx", new XElement(W + "txbxContent", writer.Container(box.Blocks))),
                    new XElement(Wps + "bodyPr", new XAttribute("wrap", "square")))));
        var drawing = Frame(box, id, "Text Box", graphic, new XElement(Wp + "cNvGraphicFramePr"));
        return new XElement(Mc + "AlternateContent", new XElement(Mc + "Choice", new XAttribute("Requires", "wps"), drawing));
    }

    private static XElement Frame(WordShape shape, int id, string name, XElement graphic, XElement frameProperties)
    {
        var (cx, cy) = (ToEmu(shape.Width), ToEmu(shape.Height));
        var extent = new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy));
        var effect = new XElement(Wp + "effectExtent", new XAttribute("l", "0"), new XAttribute("t", "0"), new XAttribute("r", "0"), new XAttribute("b", "0"));
        var properties = new XElement(Wp + "docPr", new XAttribute("id", Format(id)), new XAttribute("name", name + " " + Format(id)));
        if (shape.Description is not null)
        {
            properties.Add(new XAttribute("descr", shape.Description));
        }

        XElement frame;
        if (shape.Floating is not { } floating)
        {
            frame = new XElement(Wp + "inline", Distances(), extent, effect, properties, frameProperties, graphic);
        }
        else
        {
            frame = new XElement(
                Wp + "anchor",
                Distances(),
                new XAttribute("simplePos", "0"),
                new XAttribute("relativeHeight", Format(id * 1024)),
                new XAttribute("behindDoc", floating.BehindText ? "1" : "0"),
                new XAttribute("locked", "0"),
                new XAttribute("layoutInCell", "1"),
                new XAttribute("allowOverlap", "1"),
                new XElement(Wp + "simplePos", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(Wp + "positionH", new XAttribute("relativeFrom", floating.HorizontalRelativeTo), new XElement(Wp + "posOffset", ToEmu(floating.HorizontalOffset))),
                new XElement(Wp + "positionV", new XAttribute("relativeFrom", floating.VerticalRelativeTo), new XElement(Wp + "posOffset", ToEmu(floating.VerticalOffset))),
                extent,
                effect,
                Wrap(floating.Wrap),
                properties,
                frameProperties,
                graphic);
        }

        return new XElement(W + "drawing", frame);
    }

    private static XAttribute[] Distances() => [new("distT", "0"), new("distB", "0"), new("distL", "0"), new("distR", "0")];

    private static XElement Wrap(WordWrap wrap) => wrap switch
    {
        WordWrap.None => new XElement(Wp + "wrapNone"),
        WordWrap.TopAndBottom => new XElement(Wp + "wrapTopAndBottom"),
        WordWrap.Tight => new XElement(Wp + "wrapTight", new XAttribute("wrapText", "bothSides"), Polygon()),
        WordWrap.Through => new XElement(Wp + "wrapThrough", new XAttribute("wrapText", "bothSides"), Polygon()),
        _ => new XElement(Wp + "wrapSquare", new XAttribute("wrapText", "bothSides")),
    };

    // Tight and through wrapping need an outline: the bounding square, in the 21600 unit space.
    private static XElement Polygon() => new(
        Wp + "wrapPolygon",
        new XAttribute("edited", "0"),
        new XElement(Wp + "start", new XAttribute("x", "0"), new XAttribute("y", "0")),
        new XElement(Wp + "lineTo", new XAttribute("x", "0"), new XAttribute("y", "21600")),
        new XElement(Wp + "lineTo", new XAttribute("x", "21600"), new XAttribute("y", "21600")),
        new XElement(Wp + "lineTo", new XAttribute("x", "21600"), new XAttribute("y", "0")),
        new XElement(Wp + "lineTo", new XAttribute("x", "0"), new XAttribute("y", "0")));

    private static XElement Transform(long cx, long cy) => new(
        A + "xfrm",
        new XElement(A + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
        new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)));

    private static XElement Rectangle() => new(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"));
}
