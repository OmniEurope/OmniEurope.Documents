// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Forms;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfFormTests
{
    // Rectangles of the hand-written form below (PDF user space of the 200 x 200 page).
    private static readonly PdfRectangle NameBox = new(10, 170, 110, 190);
    private static readonly PdfRectangle AgreeBox = new(10, 115, 24, 129);
    private static readonly PdfRectangle NewsBox = new(40, 115, 54, 129);

    private static byte[] Source() => RawPdf.Objects(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> /Contents 4 0 R /Annots [6 0 R 7 0 R 12 0 R 9 0 R 10 0 R 11 0 R 13 0 R 14 0 R 19 0 R 20 0 R 21 0 R] >>",
        RawPdf.Stream(string.Empty, RawPdf.Ascii("0 g")),
        "<< /Fields [6 0 R 8 0 R 9 0 R 10 0 R 11 0 R 15 0 R 14 0 R 19 0 R 20 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 16 0 R >> >> /XFA 22 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /Rect [10 170 110 190] /DA (/Helv 12 Tf 0 0 1 rg) /MK << /BC [0] /BG [1 1 0.8] >> /F 4 /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /Parent 8 0 R /Rect [10 140 24 154] /AP << /N << /A 17 0 R /Off 18 0 R >> >> /AS /Off /F 4 >>",
        "<< /FT /Btn /Ff 49152 /T (choice) /Kids [7 0 R 12 0 R] /V /Off >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /T (agree) /Rect [10 115 24 129] /AP << /N << /Yes 17 0 R /Off 18 0 R >> >> /AS /Off /V /Off /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 393216 /T (country) /Opt [[(be) (Belgique)] [(fr) (France)]] /Rect [10 90 110 106] /DA (/Helv 10 Tf 0 g) /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 2097152 /T (colours) /Opt [(Red) (Green) (Blue)] /Rect [120 60 190 106] /DA (/Helv 10 Tf 0 g) /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /Parent 8 0 R /Rect [30 140 44 154] /AP << /N << /B 17 0 R /Off 18 0 R >> >> /AS /Off /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /Parent 15 0 R /FT /Tx /Ff 4096 /T (city) /Rect [10 10 110 50] /DA (/Helv 0 Tf 0 g) /Q 1 /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /Ff 65536 /T (send) /Rect [150 10 190 30] /F 4 >>",
        "<< /T (address) /Kids [13 0 R] >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 14 14]", RawPdf.Ascii("0 g 3 3 8 8 re f")),
        RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 14 14]", []),
        "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 16777216 /MaxLen 4 /T (code) /Rect [120 170 190 190] /DA (/Cour 12 Tf 0 g) /F 4 >>",
        "<< /Type /Annot /Subtype /Widget /FT /Btn /T (news) /Rect [40 115 54 129] /F 4 >>",
        "<< /Type /Annot /Subtype /Text /Rect [180 180 190 190] /Contents (kept note) >>",
        RawPdf.Stream(string.Empty, RawPdf.Ascii("<xdp:xdp/>")));

    private static PdfFieldValue[] Values() =>
    [
        new("name", "Zoë Ünïcode €"),
        new("choice", "B"),
        PdfFieldValue.Check("agree", true),
        new("country", "France"),
        new("colours", ["Blue", "Red"]),
        new("address.city", "Rue de la Loi 200, 1049 Bruxelles, Belgique"),
        new("code", "AB12"),
        PdfFieldValue.Check("news", true),
    ];

    private static PdfFormField Field(byte[] pdf, string name) => PdfForm.Read(PdfDocument.Open(pdf)).Single(f => f.Name == name);

    [Fact]
    public void Fields_are_read_with_their_kinds_names_options_and_states()
    {
        var fields = PdfForm.Read(PdfDocument.Open(Source()));

        Assert.Equal(["name", "choice", "agree", "country", "colours", "address.city", "send", "code", "news"], fields.Select(f => f.Name));
        Assert.Equal([PdfFieldKind.Text, PdfFieldKind.RadioButton, PdfFieldKind.CheckBox, PdfFieldKind.ComboBox, PdfFieldKind.ListBox,
            PdfFieldKind.Text, PdfFieldKind.PushButton, PdfFieldKind.Text, PdfFieldKind.CheckBox], fields.Select(f => f.Kind));
        var choice = fields[1];
        Assert.Equal("Off", choice.Value);
        Assert.Equal(["A", "B"], choice.States);
        Assert.Equal([(1, new PdfRectangle(10, 140, 24, 154)), (1, new PdfRectangle(30, 140, 44, 154))], choice.Widgets.Select(w => (w.PageNumber, w.Rectangle)));
        Assert.Equal([new PdfChoiceOption("be", "Belgique"), new PdfChoiceOption("fr", "France")], fields[3].Options);
        Assert.Equal(["Red", "Green", "Blue"], fields[4].Options.Select(o => o.DisplayText));
        Assert.True(fields[4].IsMultiSelect);
        Assert.True(fields[5].IsMultiline);
        Assert.Equal(4, fields[7].MaxLength);
        Assert.Null(fields[0].Value);
        Assert.Null(fields[6].Value);
        Assert.False(fields[0].IsReadOnly || fields[0].IsRequired);
    }

    [Fact]
    public void Filled_values_read_back_and_the_original_bytes_are_kept()
    {
        var source = Source();
        var filled = PdfForm.Fill(source, Values());

        Assert.Equal(source, filled[..source.Length]);
        var fields = PdfForm.Read(PdfDocument.Open(filled)).ToDictionary(f => f.Name);
        Assert.Equal("Zoë Ünïcode €", fields["name"].Value);
        Assert.Equal("B", fields["choice"].Value);
        Assert.Equal("Yes", fields["agree"].Value);
        Assert.Equal("fr", fields["country"].Value);
        Assert.Equal(["Blue", "Red"], fields["colours"].Values);
        Assert.Equal("Rue de la Loi 200, 1049 Bruxelles, Belgique", fields["address.city"].Value);
        Assert.Equal("AB12", fields["code"].Value);
        Assert.Equal("Yes", fields["news"].Value);
        var store = PdfDocument.Open(filled).Store;
        var list = (PdfDictionary)store.Resolve(new PdfReference(11, 0))!;
        Assert.Equal([0.0, 2.0], store.Get<PdfArray>(list, "I")!.Items.Cast<PdfNumber>().Select(n => n.Value));
        Assert.Equal(["Off", "B"], new[] { 7, 12 }.Select(n => ((PdfName)((PdfDictionary)store.Resolve(new PdfReference(n, 0))!)["AS"]!).Value));
        Assert.False(FormTree.AcroForm(store)!.ContainsKey("XFA"));
    }

    [Fact]
    public void Appearance_streams_hold_the_value_text()
    {
        var fields = PdfForm.Read(PdfDocument.Open(PdfForm.Fill(Source(), Values()))).ToDictionary(f => f.Name);

        Assert.Equal("Zoë Ünïcode €", fields["name"].Widgets[0].AppearanceText);
        Assert.Equal("France", fields["country"].Widgets[0].AppearanceText);
        Assert.Equal("Red Green Blue", fields["colours"].Widgets[0].AppearanceText.ReplaceLineEndings(" "));
        Assert.Equal("Rue de la Loi 200, 1049 Bruxelles, Belgique", fields["address.city"].Widgets[0].AppearanceText.ReplaceLineEndings(" "));
        Assert.True(fields["address.city"].Widgets[0].AppearanceText.Split('\n').Length > 1);
        Assert.Equal("AB12", fields["code"].Widgets[0].AppearanceText.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Equal(string.Empty, PdfForm.Read(PdfDocument.Open(Source()))[0].Widgets[0].AppearanceText);
    }

    [Fact]
    public void The_renderer_shows_the_filled_values()
    {
        var before = PdfRenderer.Render(PdfDocument.Open(Source()).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;
        var after = PdfRenderer.Render(PdfDocument.Open(PdfForm.Fill(Source(), Values())).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

        Assert.Equal(0, Count(before, NameBox, Blue));
        Assert.True(Count(after, NameBox, Blue) > 40, "blue text drawn in the text field");
        Assert.True(Count(after, NameBox, (r, g, b) => r == 255 && g == 255 && b is > 190 and < 220) > 500, "background drawn");
        Assert.Equal(0, Count(before, AgreeBox, Dark));
        Assert.True(Count(after, AgreeBox, Dark) >= 49, "the check box's Yes appearance is shown");
        Assert.True(Count(after, NewsBox, Dark) > 5, "a check mark is drawn for the widget that had no appearance");
    }

    [Fact]
    public void Flattening_puts_the_values_on_the_page_and_removes_the_form()
    {
        var flat = PdfForm.Fill(Source(), Values(), new PdfFormFillOptions { Flatten = true });
        var document = PdfDocument.Open(flat);

        Assert.Empty(PdfForm.Read(document));
        Assert.Null(FormTree.AcroForm(document.Store));
        var annotations = document.Store.Get<PdfArray>(document.GetPage(1).Dictionary, "Annots")!.Items.Select(document.Store.Resolve).Cast<PdfDictionary>().ToList();
        Assert.Equal(["Text"], annotations.Select(a => ((PdfName)a["Subtype"]!).Value));
        var text = document.GetPage(1).Text;
        Assert.Contains("Zoë Ünïcode €", text, StringComparison.Ordinal);
        Assert.Contains("France", text, StringComparison.Ordinal);
        Assert.Contains("Bruxelles", text, StringComparison.Ordinal);
        var image = PdfRenderer.Render(document.GetPage(1), new PdfRenderOptions { Dpi = 72, Annotations = false }).Image;
        Assert.True(Count(image, NameBox, Blue) > 40);
        Assert.True(Count(image, AgreeBox, Dark) >= 49);
    }

    [Fact]
    public void Flattening_draws_appearances_without_a_subtype_and_leaves_hidden_widgets_out()
    {
        var source = RawPdf.Objects(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 6 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R /Annots [5 0 R 6 0 R] >>",
            RawPdf.Stream(string.Empty, []),
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (shown) /Rect [10 10 50 50] /AP << /N 7 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (hidden) /F 2 /Rect [100 100 140 140] /AP << /N 7 0 R >> >>",
            RawPdf.Stream("/BBox [0 0 10 10]", RawPdf.Ascii("0 g 0 0 10 10 re f")));

        var flat = PdfForm.Flatten(source);
        var document = PdfDocument.Open(flat);
        var image = PdfRenderer.Render(document.GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

        Assert.Null(document.Store.Get(document.GetPage(1).Dictionary, "Annots"));
        Assert.Equal("Form", ((PdfName)((PdfStream)document.Store.Resolve(new PdfReference(7, 0))!)["Subtype"]!).Value);
        Assert.Equal(38 * 38, Count(image, new PdfRectangle(10, 10, 50, 50), Dark));
        Assert.Equal(0, Count(image, new PdfRectangle(100, 100, 140, 140), Dark));
        Assert.Throws<NotSupportedException>(() => PdfForm.Fill(DirectField(), [new PdfFieldValue("direct", "x")]));
    }

    // A field written inside the catalog rather than as an object of its own.
    private static byte[] DirectField() => RawPdf.Objects(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [<< /FT /Tx /T (direct) /Rect [0 0 10 10] /Subtype /Widget >>] >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");

    [Fact]
    public void Flattening_a_file_without_widgets_returns_it_unchanged()
    {
        var plain = RawPdf.Page("0 g");

        Assert.Equal(plain, PdfForm.Flatten(plain));
        Assert.Empty(PdfForm.Read(PdfDocument.Open(plain)));
    }

    [Fact]
    public void Unchecking_and_free_text_in_an_editable_combo_box_are_written()
    {
        var once = PdfForm.Fill(Source(), [PdfFieldValue.Check("agree", true), new("country", "Luxembourg"), new("choice", "A")]);
        var twice = PdfForm.Fill(once, [new PdfFieldValue("agree", "Off"), new PdfFieldValue("colours", Array.Empty<string>())]);

        Assert.Equal("Off", Field(twice, "agree").Value);
        Assert.Equal("Luxembourg", Field(twice, "country").Value);
        Assert.Equal("A", Field(twice, "choice").Value);
        Assert.Empty(Field(twice, "colours").Values);
    }

    [Theory]
    [InlineData("missing", "x")]
    [InlineData("send", "x")]
    [InlineData("choice", "C")]
    [InlineData("agree", "Maybe")]
    [InlineData("colours", "Purple")]
    [InlineData("code", "ABCDE")]
    public void Values_the_field_cannot_hold_are_refused(string name, string value) =>
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(Source(), [new PdfFieldValue(name, value)]));

    [Fact]
    public void Wrong_value_shapes_are_refused()
    {
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(Source(), [new PdfFieldValue("country", ["be", "fr"])]));
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(Source(), [PdfFieldValue.Check("choice", true)]));
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(Source(), [PdfFieldValue.Check("country", true)]));
        Assert.Throws<ArgumentException>(() => PdfForm.Fill(Source(), [new PdfFieldValue("name", ["a", "b"])]));
    }

    [Fact]
    public void Encrypted_and_damaged_files_are_refused()
    {
        var builder = new PdfDocumentBuilder { Encryption = new PdfEncryption(string.Empty, "owner") };
        builder.AddPage();
        var encrypted = builder.ToArray();
        var source = Source();
        var damaged = source[..^30];

        Assert.Throws<NotSupportedException>(() => PdfForm.Fill(encrypted, [new PdfFieldValue("name", "x")]));
        Assert.Throws<NotSupportedException>(() => PdfForm.Flatten(damaged));
    }

    private static bool Blue(byte r, byte g, byte b) => b > 150 && r < 120 && g < 120;

    private static bool Dark(byte r, byte g, byte b) => r < 100 && g < 100 && b < 100;

    // Pixels inside a rectangle of the page (72 dpi: one pixel per point, y from the top).
    private static int Count(RasterImage image, PdfRectangle box, Func<byte, byte, byte, bool> match)
    {
        var count = 0;
        for (var y = (int)(200 - box.Top) + 1; y < (int)(200 - box.Bottom) - 1; y++)
        {
            for (var x = (int)box.Left + 1; x < (int)box.Right - 1; x++)
            {
                var (r, g, b) = Pixel(image, x, y);
                count += match(r, g, b) ? 1 : 0;
            }
        }

        return count;
    }

    private static (byte R, byte G, byte B) Pixel(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }
}
