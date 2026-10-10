// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>
/// What text wrapping needs from an anchored drawing (ECMA-376 part 1, §20.4.2.3 anchor, §20.4.3 wrapping) and from
/// unequal columns (§17.6.3 col): distances, alignments, simple positions, wrap sides and the space after each column,
/// read and written back.
/// </summary>
public sealed class WordFloatingTests
{
    private const string Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    [Fact]
    public void Anchor_distances_alignments_and_wrap_sides_are_read_and_written_back()
    {
        var document = WordDocument.Load(DocxFactory.Build(Paragraph(Anchor(
            """distT="12700" distB="25400" distL="114300" distR="127000" simplePos="0" """,
            """<wp:positionH relativeFrom="margin"><wp:align>right</wp:align></wp:positionH><wp:positionV relativeFrom="page"><wp:align>bottom</wp:align></wp:positionV>""",
            """<wp:wrapSquare wrapText="largest"/>"""))));

        var floating = Shape(document).Floating!;

        Assert.Equal(("margin", "right", "page", "bottom"), (floating.HorizontalRelativeTo, floating.HorizontalAlignment, floating.VerticalRelativeTo, floating.VerticalAlignment));
        Assert.Equal((1.0, 2.0, 9.0, 10.0), (floating.DistanceTop, floating.DistanceBottom, floating.DistanceLeft, floating.DistanceRight));
        Assert.Equal((WordWrap.Square, WordWrapSide.Largest), (floating.Wrap, floating.WrapSide));
        var reread = Shape(WordDocument.Load(document.ToArray())).Floating!;
        Assert.Equal(floating, reread);
    }

    [Theory]
    [InlineData("""<wp:wrapTight wrapText="left"><wp:wrapPolygon edited="0"><wp:start x="0" y="0"/><wp:lineTo x="0" y="21600"/></wp:wrapPolygon></wp:wrapTight>""", WordWrap.Tight, WordWrapSide.Left)]
    [InlineData("""<wp:wrapThrough wrapText="right"><wp:wrapPolygon edited="0"><wp:start x="0" y="0"/><wp:lineTo x="0" y="21600"/></wp:wrapPolygon></wp:wrapThrough>""", WordWrap.Through, WordWrapSide.Right)]
    [InlineData("""<wp:wrapTopAndBottom/>""", WordWrap.TopAndBottom, WordWrapSide.BothSides)]
    [InlineData("""<wp:wrapNone/>""", WordWrap.None, WordWrapSide.BothSides)]
    public void Every_wrap_kind_and_side_round_trips(string wrap, WordWrap kind, WordWrapSide side)
    {
        var document = WordDocument.Load(DocxFactory.Build(Paragraph(Anchor(
            """distT="0" distB="0" distL="0" distR="0" simplePos="0" """,
            """<wp:positionH relativeFrom="column"><wp:posOffset>12700</wp:posOffset></wp:positionH><wp:positionV relativeFrom="paragraph"><wp:posOffset>25400</wp:posOffset></wp:positionV>""",
            wrap))));

        var floating = Shape(document).Floating!;

        Assert.Equal((kind, side, 1.0, 2.0), (floating.Wrap, floating.WrapSide, floating.HorizontalOffset, floating.VerticalOffset));
        Assert.Equal(floating, Shape(WordDocument.Load(document.ToArray())).Floating);
    }

    [Fact]
    public void A_simple_position_places_the_shape_from_the_page_corner()
    {
        var document = WordDocument.Load(DocxFactory.Build(Paragraph(Anchor(
            """distT="0" distB="0" distL="0" distR="0" simplePos="1" """,
            """<wp:positionH relativeFrom="margin"><wp:align>center</wp:align></wp:positionH><wp:positionV relativeFrom="paragraph"><wp:posOffset>0</wp:posOffset></wp:positionV>""",
            """<wp:wrapSquare wrapText="bothSides"/>""",
            """<wp:simplePos x="1270000" y="2540000"/>"""))));

        var floating = Shape(document).Floating!;

        Assert.Equal((100.0, "page", 200.0, "page"), (floating.HorizontalOffset, floating.HorizontalRelativeTo, floating.VerticalOffset, floating.VerticalRelativeTo));
        Assert.Null(floating.HorizontalAlignment);
    }

    [Fact]
    public void A_vml_wrap_side_and_wrap_distances_are_read()
    {
        const string Namespaces = "xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:w10=\"urn:schemas-microsoft-com:office:word\"";
        var document = WordDocument.Load(DocxFactory.Build(
            "<w:p><w:r><w:pict " + Namespaces + "><v:shape style=\"position:absolute;width:100pt;height:50pt;mso-wrap-distance-left:9pt;"
            + "mso-wrap-distance-right:8pt;mso-wrap-distance-top:2pt;mso-wrap-distance-bottom:3pt\"><w10:wrap type=\"square\" side=\"left\"/>"
            + "<v:textbox><w:txbxContent><w:p><w:r><w:t>x</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></w:r></w:p>"));

        var floating = Shape(document).Floating!;

        Assert.Equal((9.0, 8.0, 2.0, 3.0, WordWrapSide.Left), (floating.DistanceLeft, floating.DistanceRight, floating.DistanceTop, floating.DistanceBottom, floating.WrapSide));
    }

    [Fact]
    public void The_space_after_each_unequal_column_is_read_and_written_back()
    {
        const string Body = """<w:p/><w:sectPr><w:cols w:num="3" w:space="720" w:equalWidth="0"><w:col w:w="2000" w:space="400"/><w:col w:w="3000" w:space="200"/><w:col w:w="1000"/></w:cols></w:sectPr>""";

        var page = WordDocument.Load(DocxFactory.Build(Body)).Sections.Single().Page;

        Assert.Equal([100, 150, 50], page.ColumnWidths!);
        Assert.Equal([20, 10, 36], page.ColumnSpacings!);
        var document = new WordDocument();
        document.Sections[0].Page = page;
        Assert.Equal([20, 10, 36], WordDocument.Load(document.ToArray()).Sections.Single().Page.ColumnSpacings!);
        var equal = WordDocument.Load(DocxFactory.Build("""<w:p/><w:sectPr><w:cols w:num="2" w:equalWidth="0"><w:col w:w="2000"/><w:col w:w="3000"/></w:cols></w:sectPr>"""));
        Assert.Null(equal.Sections.Single().Page.ColumnSpacings);
    }

    private static WordShape Shape(WordDocument document) => document.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single();

    private static string Paragraph(string drawing) => $"<w:p><w:r><w:drawing>{drawing}</w:drawing></w:r></w:p>";

    private static string Anchor(string attributes, string position, string wrap, string simple = """<wp:simplePos x="0" y="0"/>""") =>
        $"""<wp:anchor xmlns:wp="{Wp}" {attributes} relativeHeight="1" behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1">{simple}{position}<wp:extent cx="1270000" cy="635000"/>{wrap}<wp:docPr id="1" name="Box"/><a:graphic xmlns:a="{A}"><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"><wps:wsp xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"><wps:txbx><w:txbxContent><w:p><w:r><w:t>Box</w:t></w:r></w:p></w:txbxContent></wps:txbx></wps:wsp></a:graphicData></a:graphic></wp:anchor>""";
}
