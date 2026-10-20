// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Tests.Pdf;

public sealed class PdfRedactionTests
{
    // Helvetica 12 (Adobe AFM widths): "Name: " ends at 48.68, "SECRET42" runs to 110.70; "Tag" then -200 puts the
    // second "SECRET42" from 33.08 to 95.10.
    private static readonly PdfRectangle LineArea = new(49, 96, 110.5, 110);
    private static readonly PdfRectangle ArrayArea = new(32, 76, 96, 92);
    private static readonly PdfRectangle ImageCorner = new(140, 0, 200, 40);
    private static readonly PdfRectangle ImagesArea = new(5, 15, 72, 45);
    private static readonly PdfRectangle FormArea = new(5, 135, 100, 155);

    private static byte[] Source()
    {
        const string content = """
            BT /F1 12 Tf 10 170 Td (Public line) Tj ET
            0.5 g 150 150 20 20 re f 0 g
            BT /F1 12 Tf 10 100 Td (Name: ) Tj (SECRET42) Tj ( end) Tj ET
            /Span << /ActualText (SECRET42 spoken) >> BDC BT /F1 12 Tf 10 80 Td [(Tag) -200 (SECRET42)] TJ ET EMC
            q 40 0 0 40 120 20 cm /Im1 Do Q
            q 20 0 0 20 10 20 cm /Im2 Do Q
            /Fm1 Do
            q 10 0 0 10 60 20 cm BI /W 2 /H 2 /CS /G /BPC 8 ID ABCD
            EI Q
            """;
        var red = Enumerable.Repeat(new byte[] { 255, 0, 0 }, 16).SelectMany(b => b).ToArray();
        return RawPdf.Page(
            content,
            "/Font << /F1 5 0 R >> /XObject << /Im1 6 0 R /Im2 7 0 R /Fm1 8 0 R >>",
            "/Annots [9 0 R 10 0 R]",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 4 /Height 4 /ColorSpace /DeviceRGB /BitsPerComponent 8", red),
            RawPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8", [1, 2, 3, 4]),
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << /Font << /F1 5 0 R >> >>", RawPdf.Ascii("BT /F1 12 Tf 10 140 Td (FORMSECRET) Tj ET")),
            "<< /Type /Annot /Subtype /Text /Rect [50 130 60 140] /Contents (ANNOTSECRET) >>",
            "<< /Type /Annot /Subtype /Text /Rect [180 180 190 190] /Contents (kept note) >>");
    }

    private static PdfRedactionResult Redact(byte[] source, PdfRedactionOptions? options = null) =>
        PdfRedactor.Redact(PdfDocument.Open(source), new[] { LineArea, ArrayArea, ImageCorner, ImagesArea, FormArea }.Select(a => new PdfRedaction(1, a)), options);

    // Every string and every decoded stream of the file, as Latin-1 text.
    private static string Everything(byte[] pdf)
    {
        var store = PdfDocument.Open(pdf).Store;
        var text = new StringBuilder(Encoding.Latin1.GetString(pdf));
        foreach (var number in store.ObjectNumbers)
        {
            Append(store, store.Resolve(new PdfReference(number, 0)), text, 0);
        }

        return text.ToString();
    }

    private static void Append(Documents.Pdf.Reading.PdfObjectStore store, PdfObject? value, StringBuilder text, int depth)
    {
        switch (value)
        {
            case PdfString s:
                text.Append(Encoding.Latin1.GetString(s.Bytes)).Append('\n');
                break;
            case PdfStream stream:
                text.Append(Encoding.Latin1.GetString(store.DecodeBytes(stream))).Append('\n');
                foreach (var entry in stream.Entries)
                {
                    Append(store, entry.Value, text, depth + 1);
                }

                break;
            case PdfDictionary dictionary when depth < 8:
                foreach (var entry in dictionary.Entries)
                {
                    Append(store, entry.Value, text, depth + 1);
                }

                break;
            case PdfArray array when depth < 8:
                foreach (var item in array.Items)
                {
                    Append(store, item, text, depth + 1);
                }

                break;
        }
    }

    [Fact]
    public void Redacted_text_is_gone_from_the_extraction_and_from_every_decompressed_byte()
    {
        var source = Source();
        Assert.Contains("SECRET", Everything(source), StringComparison.Ordinal);

        var result = Redact(source);

        Assert.Equal("SECRET42SECRET42FORMSECRET", result.RemovedText);
        Assert.Equal(26, result.RemovedGlyphs);
        var page = PdfDocument.Open(result.Pdf).GetPage(1);
        Assert.DoesNotContain("SECRET", page.Text, StringComparison.Ordinal);
        Assert.Contains("Public line", page.Text, StringComparison.Ordinal);
        Assert.Contains("Name:", page.Text, StringComparison.Ordinal);
        Assert.Contains("Tag", page.Text, StringComparison.Ordinal);
        var all = Everything(result.Pdf);
        var at = all.IndexOf("SECRET", StringComparison.Ordinal);
        Assert.True(at < 0, at < 0 ? string.Empty : all.Substring(Math.Max(0, at - 300), 400));
        Assert.Equal(2, Encoding.Latin1.GetString(source).Split("/Subtype /Image").Length - 1);
        Assert.Equal(1, PdfDocument.Open(result.Pdf).Store.ObjectNumbers.Count(n => PdfDocument.Open(result.Pdf).Store.Resolve(new PdfReference(n, 0)) is PdfStream { } s && s["Subtype"] is PdfName { Value: "Image" }));
        Assert.DoesNotContain("/Width 2", all, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_after_a_removed_glyph_keeps_its_place()
    {
        var before = PdfDocument.Open(Source()).GetPage(1).Letters.Where(l => Math.Abs(l.Y - 100) < 0.01).ToList();

        var after = PdfDocument.Open(Redact(Source()).Pdf).GetPage(1).Letters.Where(l => Math.Abs(l.Y - 100) < 0.01).ToList();

        Assert.Equal("Name:  end", string.Concat(after.Select(l => l.Value)));
        Assert.Equal(before.Single(l => l.Value == "d").X, after.Single(l => l.Value == "d").X, 3);
        Assert.Equal(110.7, after.First(l => l.Value == " " && l.X > 100).X, 1);
    }

    [Fact]
    public void Images_under_an_area_are_removed_or_have_their_pixels_blanked()
    {
        var result = Redact(Source());

        Assert.Equal((2, 1), (result.RemovedImages, result.BlankedImages));
        var image = Assert.Single(PdfDocument.Open(result.Pdf).GetPage(1).Images);
        var pixels = image.Decode()!;
        Assert.Equal((255, 0, 0), Rgb(pixels, 0, 0));
        Assert.Equal((255, 0, 0), Rgb(pixels, 1, 3));
        Assert.Equal((0, 0, 0), Rgb(pixels, 2, 2));
        Assert.Equal((0, 0, 0), Rgb(pixels, 3, 3));
        Assert.Equal((255, 0, 0), Rgb(pixels, 3, 1));
    }

    private static (int R, int G, int B) Rgb(RasterImage image, int x, int y)
    {
        var (r, g, b, _) = image.GetRgba(x, y);
        return (r, g, b);
    }

    [Fact]
    public void The_areas_are_painted_and_the_rest_of_the_page_still_renders()
    {
        var rendering = PdfRenderer.Render(PdfDocument.Open(Redact(Source(), new PdfRedactionOptions { Fill = PdfColor.Blue }).Pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 });

        foreach (var area in new[] { LineArea, ArrayArea, FormArea })
        {
            var (x, y) = ((int)((area.Left + area.Right) / 2), 200 - (int)((area.Bottom + area.Top) / 2));
            Assert.Equal((0, 0, 255), Rgb(rendering.Image, x, y));
        }

        Assert.Equal((255, 0, 0), Rgb(rendering.Image, 125, 200 - 55));
        Assert.Equal((255, 255, 255), Rgb(rendering.Image, 190, 100));
        Assert.Equal((128, 128, 128), Rgb(rendering.Image, 160, 200 - 160));
    }

    [Fact]
    public void Annotations_in_an_area_marked_content_texts_and_metadata_on_request_are_removed()
    {
        var result = Redact(Source(), new PdfRedactionOptions { RemoveMetadata = true });

        Assert.Equal(1, result.RemovedAnnotations);
        var everything = Everything(result.Pdf);
        Assert.Contains("kept note", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("ANNOTSECRET", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("spoken", everything, StringComparison.Ordinal);
        Assert.Contains("vector graphics and shadings under an area are painted over, not removed", result.Gaps);
    }

    [Fact]
    public void Metadata_goes_only_on_request_and_other_pages_are_copied_unchanged()
    {
        var builder = new PdfDocumentBuilder { Title = "Board minutes", Author = "Clerk" };
        builder.AddPage(300, 200).DrawText("Merger with Globex approved", 20, 100, PdfFont.Sans, 14);
        builder.AddPage(300, 200).DrawText("Second page stays", 20, 100, PdfFont.Sans, 14);
        var document = PdfDocument.Open(builder.ToArray());
        var word = document.GetPage(1).Letters.Where(l => l.Value is "G" or "l" or "o" or "b" or "e" or "x").ToList();
        var globex = document.GetPage(1).Letters.SkipWhile(l => l.Value != "G").Take(6).Select(l => l.BoundingBox).Aggregate((a, b) => a.Union(b));
        Assert.NotEmpty(word);

        var kept = PdfRedactor.Redact(document, [new PdfRedaction(1, globex)]);
        var cleaned = PdfRedactor.Redact(document, [new PdfRedaction(1, globex)], new PdfRedactionOptions { RemoveMetadata = true });

        var redacted = PdfDocument.Open(kept.Pdf);
        Assert.Equal("Globex", kept.RemovedText);
        Assert.Equal("Merger with  approved", redacted.GetPage(1).Text.Trim());
        Assert.Equal("Second page stays", redacted.GetPage(2).Text.Trim());
        Assert.Equal(("Board minutes", "Clerk"), (redacted.Information.Title, redacted.Information.Author));
        var anonymous = PdfDocument.Open(cleaned.Pdf).Information;
        Assert.Equal((null, null, "OmniEurope.Documents"), (anonymous.Title, anonymous.Author, anonymous.Producer));
    }

    [Fact]
    public void Spacing_scaling_rise_and_next_line_operators_keep_the_remaining_letters_in_place()
    {
        const string content = "BT /F1 10 Tf 14 TL 2 Tc 1 Tw 90 Tz 1 Ts 1 0 0 1 10 160 Tm (Alpha HIDE1 tail) Tj T* (Beta) Tj 0 -14 TD (keep HIDE2 tail) ' 3 1 (gamma HIDE3 tail) \" ET";
        var source = RawPdf.Page(content, "/Font << /F1 5 0 R >>", string.Empty, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var letters = PdfDocument.Open(source).GetPage(1).Letters;
        var areas = new List<PdfRedaction>();
        for (var i = 0; i + 5 <= letters.Count; i++)
        {
            if (string.Concat(letters.Skip(i).Take(4).Select(l => l.Value)) == "HIDE")
            {
                var box = letters.Skip(i).Take(5).Select(l => l.BoundingBox).Aggregate((a, b) => a.Union(b));
                areas.Add(new PdfRedaction(1, new PdfRectangle(box.Left + 0.5, box.Bottom + 1, box.Right - 0.5, box.Top - 1)));
            }
        }

        var result = PdfRedactor.Redact(PdfDocument.Open(source), areas);

        Assert.Equal("HIDE1HIDE2HIDE3", result.RemovedText);
        var expected = letters.Where(l => !areas.Any(a => l.BoundingBox.Left < a.Area.Right && l.BoundingBox.Right > a.Area.Left && l.Y > a.Area.Bottom - 5 && l.Y < a.Area.Top)).ToList();
        var after = PdfDocument.Open(result.Pdf).GetPage(1).Letters;
        Assert.Equal(expected.Select(l => l.Value), after.Select(l => l.Value));
        Assert.All(expected.Zip(after), pair =>
        {
            Assert.Equal(pair.First.X, pair.Second.X, 6);
            Assert.Equal(pair.First.Y, pair.Second.Y, 6);
        });
    }
    [Fact]
    public void An_area_that_meets_nothing_changes_no_text()
    {
        var result = PdfRedactor.Redact(PdfDocument.Open(Source()), [new PdfRedaction(1, new PdfRectangle(180, 120, 199, 130))]);

        Assert.Equal((0, 0, 0), (result.RemovedGlyphs, result.RemovedImages, result.RemovedAnnotations));
        Assert.Contains("SECRET42", PdfDocument.Open(result.Pdf).GetPage(1).Text, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfRedactor.Redact(PdfDocument.Open(Source()), [new PdfRedaction(2, LineArea)]));
    }

    private static readonly PdfRectangle SecretArea = new(0, 0, 100, 40);

    [Theory]
    [InlineData("BT /Missing 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET", "/Font << /F1 5 0 R >>")]
    [InlineData("BT /Missing 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET", "")]
    [InlineData("BT 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET", "/Font << /F1 5 0 R >>")]
    [InlineData("BT /F1 12 Tf 1 0 0 1 10 10 Tm [(SECRET_AUDIT)] TJ ET", "/Font << /F9 5 0 R >>")]
    [InlineData("BT /Missing 12 Tf 14 TL 1 0 0 1 10 30 Tm (SECRET_AUDIT) ' ET", "/Font << /F1 5 0 R >>")]
    [InlineData("BT /Missing 12 Tf 14 TL 1 0 0 1 10 30 Tm 2 1 (SECRET_AUDIT) \" ET", "/Font << /F1 5 0 R >>")]
    public void Text_shown_with_an_unresolvable_font_is_refused_not_kept(string content, string fonts)
    {
        var source = RawPdf.Page(content, fonts, "", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var exception = Assert.Throws<PdfRedactionException>(() => PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]));

        Assert.DoesNotContain("SECRET_AUDIT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_in_a_form_whose_font_is_missing_is_refused_not_kept()
    {
        var source = RawPdf.Page(
            "/Fm1 Do",
            "/XObject << /Fm1 5 0 R >>",
            "",
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << >>", RawPdf.Ascii("BT /Missing 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET")));

        Assert.Throws<PdfRedactionException>(() => PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]));
    }

    private const string HelveticaFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    // ISO 32000-1 8.10.1: a form XObject runs in the graphics state in force at its Do, which includes the text font.
    [Fact]
    public void A_form_showing_text_without_its_own_tf_uses_the_font_set_before_its_do()
    {
        var source = RawPdf.Page(
            "/F1 12 Tf /Fm1 Do",
            "/Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >>",
            "",
            HelveticaFont,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << >>", RawPdf.Ascii("BT 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET")));

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.Equal("SECRET_AUDIT", result.RemovedText);
        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void A_form_showing_text_with_no_font_in_force_at_its_do_is_refused()
    {
        var source = RawPdf.Page(
            "/Fm1 Do",
            "/Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >>",
            "",
            HelveticaFont,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << >>", RawPdf.Ascii("BT 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET")));

        Assert.Throws<PdfRedactionException>(() => PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]));
    }

    // A form without /Resources (PDF 1.1 and earlier) takes the resources of the page that draws it.
    [Fact]
    public void A_form_without_resources_takes_the_fonts_of_the_page()
    {
        var source = RawPdf.Page(
            "/Fm1 Do",
            "/Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >>",
            "",
            HelveticaFont,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200]", RawPdf.Ascii("BT /F1 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET")));

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.Equal("SECRET_AUDIT", result.RemovedText);
        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
    }

    // A form with its own /Resources must be self-contained (ISO 32000-1 8.10.1, ISO 32000-2 8.10.2): the page's
    // fonts are not looked up for a name its own resources lack, so that text is refused rather than guessed at.
    [Fact]
    public void A_form_with_resources_but_no_font_entry_does_not_borrow_the_fonts_of_the_page()
    {
        var source = RawPdf.Page(
            "/Fm1 Do",
            "/Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >>",
            "",
            HelveticaFont,
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << >>", RawPdf.Ascii("BT /F1 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET")));

        Assert.Throws<PdfRedactionException>(() => PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]));
    }

    private const string PatternGap = "tiling patterns showing text or images under an area removed whole";

    private const string TilingHead = "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 100 20] /XStep 100 /YStep 20";

    // Page content that fills (or strokes) a rectangle with pattern /P1 whose cell shows SECRET_AUDIT; object 5 is
    // the font, 6 the pattern, 7 a form that fills with the pattern it inherits.
    private static byte[] PatternPage(string content, string cell, string xobjects = "")
    {
        return RawPdf.Page(
            content,
            "/Font << /F1 5 0 R >> /Pattern << /P1 6 0 R >> " + xobjects,
            "",
            HelveticaFont,
            RawPdf.Stream(TilingHead + " /Resources << /Font << /F1 5 0 R >> >>", RawPdf.Ascii(cell)),
            RawPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << >>", RawPdf.Ascii("0 0 100 100 re f")));
    }

    private const string TextCell = "BT /F1 12 Tf 2 2 Td (SECRET_AUDIT) Tj ET";

    [Theory]
    [InlineData("/Pattern cs /P1 scn 0 0 100 100 re f")]
    [InlineData("/Pattern CS /P1 SCN 8 w 0 0 100 100 re S")]
    [InlineData("/Pattern cs /P1 scn q 1 0 0 1 20 0 cm 0 0 50 30 re B Q")]
    public void A_tiling_pattern_showing_text_under_an_area_is_removed_whole(string content)
    {
        var result = PdfRedactor.Redact(PdfDocument.Open(PatternPage(content, TextCell)), [new PdfRedaction(1, SecretArea)]);

        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
        Assert.Contains(PatternGap, result.Gaps);
    }

    [Theory]
    [InlineData("/Pattern cs /P1 scn 0 0 m 100 0 l 50 50 80 80 100 100 c 10 10 20 20 v 5 5 30 30 y f", "BT /F1 12 Tf 2 2 Td (SECRET_AUDIT) Tj ET")]
    [InlineData("/Pattern cs /P1 scn 0 0 100 100 re f", "/Pattern cs /P1 scn 0 0 5 5 re f BT /F1 12 Tf (SECRET_AUDIT) Tj ET")]
    [InlineData("/Pattern cs /P1 scn 0 0 100 100 re f", "q 5 0 0 5 0 0 cm BI /W 1 /H 1 /CS /G /BPC 8 ID \u0001\nEI Q BT /F1 12 Tf (SECRET_AUDIT) Tj ET")]
    public void Path_shapes_and_cells_that_select_patterns_or_draw_images_are_followed(string content, string cell)
    {
        var result = PdfRedactor.Redact(PdfDocument.Open(PatternPage(content, cell)), [new PdfRedaction(1, SecretArea)]);

        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
        Assert.Contains(PatternGap, result.Gaps);
    }

    [Fact]
    public void A_pattern_whose_cell_cannot_be_read_is_removed_whole_when_painted_under_an_area()
    {
        var source = RawPdf.Page(
            "/Pattern cs /P1 scn 0 0 100 100 re f",
            "/Pattern << /P1 5 0 R >>",
            "",
            RawPdf.Stream(TilingHead + " /Filter /FlateDecode", [1, 2, 3, 4]));

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.Contains(PatternGap, result.Gaps);
    }

    // A Type 3 glyph is a drawing selected by its code in the shown string: removing the code from the string
    // removes what the viewer would draw there, so the glyph procedures need no rewriting.
    [Fact]
    public void A_glyph_of_a_type_3_font_under_an_area_is_removed_from_the_shown_string()
    {
        var source = RawPdf.Page(
            "BT /T3 10 Tf 10 10 Td (a) Tj ET BT /T3 10 Tf 10 100 Td (a) Tj ET",
            "/Font << /T3 5 0 R >>",
            "",
            "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a 6 0 R >> "
            + "/Encoding << /Type /Encoding /Differences [97 /a] >> /FirstChar 97 /LastChar 97 /Widths [1000] /Resources << >> >>",
            RawPdf.Stream(string.Empty, RawPdf.Ascii("1000 0 0 0 800 800 d1 0 0 800 800 re f")));

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.Equal(1, result.RemovedGlyphs);
        Assert.Equal("a", result.RemovedText);
        Assert.Equal("a", PdfDocument.Open(result.Pdf).GetPage(1).Text.Trim());
    }

    private static byte[] Type3Page(string fontBox) => RawPdf.Page(
        "BT /T3 10 Tf 10 10 Td (a) Tj ET",
        "/Font << /T3 5 0 R >>",
        "",
        "<< /Type /Font /Subtype /Type3 /FontBBox " + fontBox + " /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a 6 0 R >> "
        + "/Encoding << /Type /Encoding /Differences [97 /a] >> /FirstChar 97 /LastChar 97 /Widths [1000] /Resources << >> >>",
        RawPdf.Stream(string.Empty, RawPdf.Ascii("1000 0 0 0 800 5000 d1 0 0 800 5000 re f")));

    // The ink of this glyph reaches 5 em above the baseline (FontBBox through FontMatrix), far above the default box.
    [Fact]
    public void A_type_3_glyph_is_boxed_by_the_font_bbox_not_the_default_ascent()
    {
        var result = PdfRedactor.Redact(PdfDocument.Open(Type3Page("[0 0 1000 5000]")), [new PdfRedaction(1, new PdfRectangle(0, 30, 100, 40))]);

        Assert.Equal(1, result.RemovedGlyphs);
    }

    [Fact]
    public void A_type_3_glyph_outside_its_font_bbox_is_kept()
    {
        var result = PdfRedactor.Redact(PdfDocument.Open(Type3Page("[0 0 1000 5000]")), [new PdfRedaction(1, new PdfRectangle(0, 90, 100, 99))]);

        Assert.Equal(0, result.RemovedGlyphs);
    }

    // A FontBBox of zeros means unknown: the glyph counts as under any area within a font size of its origin.
    [Theory]
    [InlineData("[0 0 0 0]")]
    [InlineData("[0 0 0]")]
    public void A_type_3_font_with_an_unknown_bbox_is_removed_when_an_area_is_within_a_font_size_of_the_glyph(string fontBox)
    {
        var near = PdfRedactor.Redact(PdfDocument.Open(Type3Page(fontBox)), [new PdfRedaction(1, new PdfRectangle(0, 18, 100, 25))]);
        var far = PdfRedactor.Redact(PdfDocument.Open(Type3Page(fontBox)), [new PdfRedaction(1, new PdfRectangle(0, 60, 100, 70))]);

        Assert.Equal(1, near.RemovedGlyphs);
        Assert.Equal(0, far.RemovedGlyphs);
    }

    [Fact]
    public void A_tiling_pattern_inherited_by_a_form_drawn_under_an_area_is_removed_whole()
    {
        var source = PatternPage("/Pattern cs /P1 scn /Fm1 Do", TextCell, "/XObject << /Fm1 7 0 R >>");

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
        Assert.Contains(PatternGap, result.Gaps);
    }

    [Fact]
    public void A_tiling_pattern_painted_elsewhere_or_without_text_is_kept()
    {
        var elsewhere = PatternPage("/Pattern cs /P1 scn 0 100 100 50 re f", TextCell);
        var vectorOnly = PatternPage("/Pattern cs /P1 scn 0 0 100 100 re f", "0 0 10 10 re f");

        var kept = PdfRedactor.Redact(PdfDocument.Open(elsewhere), [new PdfRedaction(1, SecretArea)]);
        var plain = PdfRedactor.Redact(PdfDocument.Open(vectorOnly), [new PdfRedaction(1, SecretArea)]);

        Assert.Contains("SECRET_AUDIT", Everything(kept.Pdf), StringComparison.Ordinal);
        Assert.DoesNotContain(PatternGap, kept.Gaps);
        Assert.DoesNotContain(PatternGap, plain.Gaps);
    }

    [Fact]
    public void A_page_without_areas_never_raises_for_an_unresolvable_font()
    {
        var source = RawPdf.Page("BT /Missing 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET", "", "");

        var untouched = PdfRedactor.Redact(PdfDocument.Open(source), []);
        var metadataOnly = PdfRedactor.Redact(PdfDocument.Open(source), [], new PdfRedactionOptions { RemoveMetadata = true });

        Assert.Equal(0, untouched.RemovedGlyphs);
        Assert.Equal(0, metadataOnly.RemovedGlyphs);
    }

    [Fact]
    public void Text_with_a_resolvable_font_is_still_redacted()
    {
        var source = RawPdf.Page(
            "BT /F1 12 Tf 1 0 0 1 10 10 Tm (SECRET_AUDIT) Tj ET",
            "/Font << /F1 5 0 R >>",
            "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        var result = PdfRedactor.Redact(PdfDocument.Open(source), [new PdfRedaction(1, SecretArea)]);

        Assert.Equal("SECRET_AUDIT", result.RemovedText);
        Assert.DoesNotContain("SECRET_AUDIT", Everything(result.Pdf), StringComparison.Ordinal);
    }
}
