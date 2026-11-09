// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>Regressions of the 2026-11-09 audit: unreadable content streams, lost dates and inherited MaxLen,
/// a table header painted out of the margins and the resolution recorded in a rendered PNG.</summary>
public sealed class PdfAuditRegressionTests
{
    private static readonly DateTimeOffset Created = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void An_unreadable_content_stream_is_reported_as_a_gap_and_the_readable_one_still_draws()
    {
        var bad = RawPdf.Stream("/Filter /FlateDecode", RawPdf.Ascii("1 0 0 rg 0 0 200 200 re f"));
        var good = RawPdf.Stream(string.Empty, RawPdf.Ascii("0 0 1 rg 0 0 100 100 re f"));
        var onlyBad = RawPdf.Objects(
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>", bad);
        var both = RawPdf.Objects(
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents [4 0 R 5 0 R] >>", bad, good);
        var options = new PdfRenderOptions { Dpi = 72 };

        var empty = PdfRenderer.Render(PdfDocument.Open(onlyBad).GetPage(1), options);
        var partial = PdfRenderer.Render(PdfDocument.Open(both).GetPage(1), options);

        Assert.Contains(empty.GapDetails, g => g.Code == "RenderContentStreamUnreadable");
        Assert.Equal(empty.Gaps, empty.GapDetails.Select(g => g.Message));
        Assert.Contains(partial.GapDetails, g => g.Code == "RenderContentStreamUnreadable");
        var pixel = partial.Image.GetRgba(50, 150);
        Assert.Equal((0, 0, 255), (pixel.R, pixel.G, pixel.B));
    }

    [Fact]
    public void A_readable_content_stream_reports_no_unreadable_gap()
    {
        var pdf = RawPdf.Page("0 0 1 rg 0 0 100 100 re f");

        var rendering = PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        Assert.Empty(rendering.GapDetails);
    }

    [Fact]
    public void Page_operations_keep_the_creation_date_of_the_source()
    {
        var builder = new PdfDocumentBuilder { Title = "Dated", CreationDate = Created };
        builder.AddPage(100, 100).DrawText("A", 10, 50, PdfFont.Sans, 12);
        var source = PdfDocument.Open(builder.ToArray());
        Assert.Equal(Created, source.Information.CreationDate);

        var results = new Dictionary<string, byte[]>
        {
            ["rotate"] = PdfEditor.RotatePages(source, [1], 90),
            ["extract"] = PdfEditor.ExtractPages(source, [1]),
            ["merge"] = PdfEditor.Merge(source, source),
            ["encrypt"] = PdfEditor.Encrypt(source, new PdfEncryption(string.Empty, "owner")),
            ["compress"] = PdfCompressor.Compress(source),
        };

        foreach (var (name, bytes) in results)
        {
            var information = PdfDocument.Open(bytes).Information;
            Assert.True(Created == information.CreationDate, $"{name}: {information.CreationDate}");
            Assert.Equal("Dated", information.Title);
        }
    }

    [Fact]
    public void The_rebuilt_information_dictionary_keeps_both_dates()
    {
        var modified = new DateTimeOffset(2021, 6, 7, 8, 9, 10, TimeSpan.FromHours(2));
        var information = new PdfInformation("T", null, null, null, null, null, Created, modified);

        var info = PdfEditor.Information(information);

        Assert.Equal(Created, PdfDocument.ParseDate(Assert.IsType<PdfString>(info["CreationDate"]).ToText()));
        Assert.Equal(modified, PdfDocument.ParseDate(Assert.IsType<PdfString>(info["ModDate"]).ToText()));
    }

    [Fact]
    public void An_inherited_max_length_is_read_and_enforced_and_a_terminal_one_wins()
    {
        var inherited = Form("/MaxLen 2", string.Empty);
        var overridden = Form("/MaxLen 2", "/MaxLen 3");
        var direct = Form(string.Empty, "/MaxLen 2");

        Assert.Equal(2, Assert.Single(PdfForm.Read(PdfDocument.Open(inherited))).MaxLength);
        Assert.Equal(2, Assert.Single(PdfForm.Read(PdfDocument.Open(direct))).MaxLength);
        Assert.Equal(3, Assert.Single(PdfForm.Read(PdfDocument.Open(overridden))).MaxLength);
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(inherited, [new PdfFieldValue("p.c", "ABC")]));
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(direct, [new PdfFieldValue("p.c", "ABC")]));
        Assert.NotEmpty(PdfForm.Fill(inherited, [new PdfFieldValue("p.c", "AB")]));
        Assert.NotEmpty(PdfForm.Fill(overridden, [new PdfFieldValue("p.c", "ABC")]));
    }

    [Fact]
    public void A_comb_field_uses_the_inherited_max_length_for_its_cells()
    {
        var inherited = Form("/MaxLen 4 /Ff 16777216", string.Empty);
        var direct = Form(string.Empty, "/MaxLen 4 /Ff 16777216");

        var a = Rendered(PdfForm.Fill(inherited, [new PdfFieldValue("p.c", "AB")]));
        var b = Rendered(PdfForm.Fill(direct, [new PdfFieldValue("p.c", "AB")]));

        Assert.Equal(b.Pixels, a.Pixels);
        Assert.Contains(b.Pixels, p => p < 128);
    }

    [Fact]
    public void A_table_near_the_page_bottom_starts_with_its_header_and_first_row_on_the_next_page()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 100, PageHeight = 100, Margins = (10, 10, 10, 10) }) { Y = 89 };
        var table = new PdfTable(1);
        table.Header("HEAD");
        table.Row("BODY");

        flow.AddTable(table);
        flow.Finish();

        var pdf = PdfDocument.Open(document.ToArray());
        Assert.Equal(2, pdf.PageCount);
        Assert.Empty(pdf.GetPage(1).Letters);
        Assert.Contains("HEAD", pdf.GetPage(2).Text);
        Assert.Contains("BODY", pdf.GetPage(2).Text);
        Assert.All(pdf.GetPage(2).Letters, l => Assert.InRange(l.BoundingBox.Bottom, 10, 90));
    }

    [Fact]
    public void Empty_and_taller_than_page_tables_still_finish()
    {
        var document = new PdfDocumentBuilder();
        var flow = new PdfFlowLayout(document, new PdfFlowOptions { PageWidth = 100, PageHeight = 100, Margins = (10, 10, 10, 10) });
        var empty = new PdfTable(1);
        empty.Header("ONLY");
        var tall = new PdfTable(1);
        tall.Header("H");
        tall.Row(string.Join(' ', Enumerable.Repeat("word", 200)));

        flow.AddTable(empty);
        flow.AddTable(tall);
        flow.Finish();

        Assert.True(PdfDocument.Open(document.ToArray()).PageCount >= 1);
    }

    [Theory]
    [InlineData(2400.0, 1200, 1200.0)]
    [InlineData(0.5, 1, 1.0)]
    [InlineData(96.0, 96, 96.0)]
    public void The_png_records_the_resolution_the_page_was_rendered_at(double requested, int expectedPixels, double expectedDpi)
    {
        var page = PdfDocument.Open(RawPdf.Page("0 g 0 0 10 10 re f", pageExtra: "/CropBox [0 0 72 72]")).GetPage(1);

        var png = PdfRenderer.Render(page, new PdfRenderOptions { Dpi = requested }).ToPng();

        var decoded = PngCodec.Decode(png);
        Assert.Equal(expectedPixels, decoded.Width);
        Assert.Equal(expectedDpi, decoded.DpiX, 1);
        var reimported = PdfDocument.Open(ImagesToPdf.Convert([png])).GetPage(1);
        Assert.Equal(72, reimported.Width, 1);
    }

    private static RasterImage Rendered(byte[] pdf) =>
        PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image.ConvertTo(ImageColorType.Gray);

    // A form with a parent field (p) holding parentExtra and a widget child (c) holding childExtra.
    private static byte[] Form(string parentExtra, string childExtra) => RawPdf.Objects(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> /Contents 4 0 R /Annots [7 0 R] >>",
        RawPdf.Stream(string.Empty, RawPdf.Ascii("0 g")),
        "<< /Fields [6 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 8 0 R >> >> >>",
        $"<< /FT /Tx /T (p) {parentExtra} /Kids [7 0 R] >>",
        $"<< /Type /Annot /Subtype /Widget /Parent 6 0 R /T (c) {childExtra} /Rect [10 100 190 130] /F 4 /P 3 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
}
