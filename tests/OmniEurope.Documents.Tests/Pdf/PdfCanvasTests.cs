// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Drawing on a page (polygons, polylines, paths with holes, alignment, unbalanced states), flowing content
/// (rules, alignment, page breaks), images of every colour type, and the PDF object model. Shapes are checked
/// by rendering the page at 72 dpi (one pixel per point, origin at the top left).
/// </summary>
public sealed class PdfCanvasTests
{
    private static readonly PdfColor Red = PdfColor.Red;

    [Fact]
    public void Polygons_fill_their_inside_with_the_opacity_asked()
    {
        var image = Render(page =>
        {
            page.FillPolygon([(20, 20), (180, 20), (100, 180)], Red);
            page.FillPolygon([(150, 150), (190, 150), (190, 190), (150, 190)], Red, evenOdd: true, opacity: 0.5);
            page.FillPolygon([(0, 0), (10, 10)], Red);
        });

        Assert.Equal((255, 0, 0), Pixel(image, 100, 60));
        Assert.Equal((255, 255, 255), Pixel(image, 20, 150));
        Assert.Equal((255, 128, 128), Pixel(image, 185, 185), new NearColour(2));
    }

    [Fact]
    public void Polylines_stroke_open_or_closed()
    {
        var image = Render(page =>
        {
            page.StrokePolyline([(10, 50), (190, 50)], Red, lineWidth: 4);
            page.StrokePolyline([(20, 100), (180, 100), (180, 180)], Red, lineWidth: 4, closed: true);
            page.StrokePolyline([(5, 5)], Red);
        });

        Assert.Equal((255, 0, 0), Pixel(image, 100, 50));
        Assert.Equal((255, 255, 255), Pixel(image, 100, 60));

        // The closing segment runs from (180, 180) back to (20, 100); (100, 140) lies on it.
        Assert.Equal((255, 0, 0), Pixel(image, 100, 140));
    }

    [Theory]
    [InlineData(true, 255)]
    [InlineData(false, 0)]
    public void Paths_with_a_hole_follow_the_fill_rule(bool evenOdd, int centre)
    {
        // Two squares drawn the same way round: even-odd leaves the inner one empty, non-zero fills it.
        IReadOnlyList<(double, double)> outer = [(20, 20), (180, 20), (180, 180), (20, 180)];
        IReadOnlyList<(double, double)> inner = [(60, 60), (140, 60), (140, 140), (60, 140)];

        var image = Render(page =>
        {
            page.DrawPath([outer, inner], PdfColor.Black, PdfColor.Black, lineWidth: 2, evenOdd: evenOdd);
            page.DrawPath([outer], null, null);
            page.DrawPath([[(1, 1)]], PdfColor.Black, null);
        });

        Assert.Equal((0, 0, 0), Pixel(image, 40, 100));
        Assert.Equal((centre, centre, centre), Pixel(image, 100, 100));
    }

    [Fact]
    public void Stroke_only_paths_leave_their_inside_empty()
    {
        var image = Render(page => page.DrawPath([[(20, 20), (180, 20), (180, 180), (20, 180)]], null, PdfColor.Black, lineWidth: 4));

        Assert.Equal((0, 0, 0), Pixel(image, 100, 20));
        Assert.Equal((255, 255, 255), Pixel(image, 100, 100));
    }

    [Fact]
    public void Pages_know_their_number_and_text_can_be_aligned_right()
    {
        var document = new PdfDocumentBuilder();
        var first = document.AddPage(200, 200);
        var second = document.AddPage(200, 200);
        second.DrawTextInBox("AB", 10, 10, 100, 20, PdfFont.Sans, 10, alignment: PdfTextAlignment.Right);

        var letters = PdfDocument.Open(document.ToArray()).GetPage(2).Letters;

        Assert.Equal((1, 2), (first.Number, second.Number));
        Assert.Equal(110, Math.Round(letters[^1].X + letters[^1].Width, 1));
    }

    [Fact]
    public void Rotated_text_runs_along_its_angle_from_the_start_of_its_baseline()
    {
        var document = new PdfDocumentBuilder();
        var page = document.AddPage(200, 200);
        var upwards = page.DrawRotatedText("AB", 100, 150, 90, PdfFont.Sans, 10);
        var downwards = page.DrawRotatedText("CD", 150, 50, 270, PdfFont.Sans, 10, Red, characterSpacing: 1);

        var pdf = PdfDocument.Open(document.ToArray());
        var letters = pdf.GetPage(1).Letters;
        var image = PdfRenderer.Render(pdf.GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

        Assert.Equal(page.MeasureText("AB", PdfFont.Sans, 10), upwards, 3);
        Assert.Equal(page.MeasureText("CD", PdfFont.Sans, 10, 1), downwards, 3);
        Assert.Equal([90, 90, -90, -90], letters.Select(l => l.Rotation));
        Assert.Equal((100, 50), (Math.Round(letters[0].X, 2), Math.Round(letters[0].Y, 2)));
        Assert.Equal(Math.Round(50 + page.MeasureText("A", PdfFont.Sans, 10), 2), Math.Round(letters[1].Y, 2));
        Assert.Equal(Math.Round(150 - page.MeasureText("C", PdfFont.Sans, 10) - 1, 2), Math.Round(letters[3].Y, 2));
        Assert.Contains(Enumerable.Range(150, 8), x => Pixel(image, x, 51) is ( > 200, < 100, < 100));
        Assert.Equal((255, 255, 255), Pixel(image, 160, 46));
        Assert.Equal("AB\nCD", pdf.GetPage(1).Text);
    }

    [Fact]
    public void Control_characters_are_not_drawn_and_unbalanced_states_are_closed()
    {
        var document = new PdfDocumentBuilder();
        var page = document.AddPage(200, 200);
        page.SaveState();
        page.DrawText("A\u0001B", 10, 100, PdfFont.Sans, 10);

        var pdf = PdfDocument.Open(document.ToArray());

        Assert.Equal("AB", pdf.GetPage(1).Text);
        var content = Encoding.Latin1.GetString(pdf.GetPage(1).ContentBytes());
        Assert.Equal(content.Split(' ', '\n').Count(t => t == "q"), content.Split(' ', '\n').Count(t => t == "Q"));
    }

    [Fact]
    public void Flow_rules_alignment_and_page_breaks()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 200, PageHeight = 200, Margins = (20, 20, 20, 20) });
        flow.AddRule(Red, thickness: 4, spaceAround: 0);
        flow.AddParagraph("AB", PdfFont.Sans, 10, alignment: PdfTextAlignment.Center);
        flow.AddParagraph("CD", PdfFont.Sans, 10, alignment: PdfTextAlignment.Right);
        var remaining = flow.Remaining;
        flow.EnsureSpace(remaining + 1);
        flow.AddParagraph("EF", PdfFont.Sans, 10);
        flow.Finish();
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());
        var image = PdfRenderer.Render(pdf.GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
        var first = pdf.GetPage(1).Letters;

        Assert.Equal(2, pdf.PageCount);
        Assert.Equal((255, 0, 0), Pixel(image, 100, 22));
        Assert.Equal(100, Math.Round((first[0].X + first[1].X + first[1].Width) / 2, 1));
        Assert.Equal(180, Math.Round(first[3].X + first[3].Width, 1));
        Assert.Equal("EF", pdf.GetPage(2).Text);
    }

    [Theory]
    [InlineData(ImageColorType.Cmyk, new byte[] { 255, 0, 0, 0 }, "/DeviceCMYK", false)]
    [InlineData(ImageColorType.GrayAlpha, new byte[] { 100, 50 }, "/DeviceGray", true)]
    [InlineData(ImageColorType.GrayAlpha, new byte[] { 100, 255 }, "/DeviceGray", false)]
    public void Images_keep_their_colour_space_and_a_soft_mask_only_when_transparent(ImageColorType type, byte[] pixels, string space, bool mask)
    {
        var document = new PdfDocumentBuilder();
        document.AddPage(100, 100).DrawImage(document.AddImage(new RasterImage(1, 1, type, pixels)), 0, 0, 10, 10);

        var image = Assert.Single(PdfDocument.Open(document.ToArray()).GetPage(1).Images);
        var text = Encoding.Latin1.GetString(document.ToArray());

        Assert.Contains("/ColorSpace " + space, text, StringComparison.Ordinal);
        Assert.Equal(mask, text.Contains("/SMask", StringComparison.Ordinal));
        Assert.NotNull(image.Decode());
    }

    [Fact]
    public void Tables_need_positive_column_weights_and_ignore_cells_beyond_the_last_column()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document);
        flow.AddTable(new PdfTable(1).Row(new PdfTableCell("kept"), new PdfTableCell("dropped")));

        Assert.Throws<ArgumentException>(() => new PdfTable());
        Assert.Throws<ArgumentException>(() => new PdfTable(1, 0));
        var text = PdfDocument.Open(document.ToArray()).GetPage(1).Text;
        Assert.Contains("kept", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_references_and_booleans_compare_by_value_and_print_as_pdf_syntax()
    {
        var dictionary = new PdfDictionary().SetName("A", "x y").SetNumber("B", -0.00001);
        dictionary.Set("C", PdfBoolean.Of(true)).Set("C", null).Set("D", null);

        Assert.Equal(PdfName.Of("A"), PdfName.Of("A"));
        Assert.Equal(PdfName.Of("A").GetHashCode(), PdfName.Of("A").GetHashCode());
        Assert.Equal("/A", PdfName.Of("A").ToString());
        Assert.Equal(new PdfReference(3, 0), new PdfReference(3, 0));
        Assert.NotEqual(new PdfReference(3, 0), new PdfReference(3, 1));
        Assert.Equal(new PdfReference(3, 0).GetHashCode(), new PdfReference(3, 0).GetHashCode());
        Assert.Equal("3 0 R", new PdfReference(3, 0).ToString());
        Assert.Same(PdfBoolean.False, PdfBoolean.Of(false));
        Assert.True(dictionary.Remove("B"));
        Assert.False(dictionary.Remove("B"));
        Assert.Equal("<</A /x#20y>>", Encoding.Latin1.GetString(PdfSerializer.ToBytes(dictionary)));
        Assert.Equal("0", PdfFormat.Real(double.NaN));
        Assert.Equal("0", PdfFormat.Real(-0.00001));
    }

    [Fact]
    public void Strings_escape_line_ends_and_text_strings_may_be_utf8()
    {
        var bytes = PdfSerializer.ToBytes(new PdfString(Encoding.ASCII.GetBytes("a\rb\nc")));

        Assert.Equal("(a\\rb\\nc)", Encoding.Latin1.GetString(bytes));
        Assert.Equal("é", PdfTextEncoding.Decode([0xEF, 0xBB, 0xBF, 0xC3, 0xA9]));
    }

    private static RasterImage Render(Action<PdfCanvas> draw)
    {
        var document = new PdfDocumentBuilder();
        draw(document.AddPage(200, 200));
        return PdfRenderer.Render(PdfDocument.Open(document.ToArray()).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
    }

    private static (int R, int G, int B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }

    private sealed class NearColour(int tolerance) : IEqualityComparer<(int R, int G, int B)>
    {
        public bool Equals((int R, int G, int B) x, (int R, int G, int B) y) =>
            Math.Abs(x.R - y.R) <= tolerance && Math.Abs(x.G - y.G) <= tolerance && Math.Abs(x.B - y.B) <= tolerance;

        public int GetHashCode((int R, int G, int B) obj) => 0;
    }
}
