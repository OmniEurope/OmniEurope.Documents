// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using OmniEurope.Documents.Word.Validation;

namespace OmniEurope.Documents.Tests.Word;

/// <summary>
/// Drawings of a paragraph edited in place: listed, inserted (ink and PNG), replaced and removed in the package XML,
/// then checked as the package (parts, relationships, content types, ids), as the model reads the saved file and as
/// the page the PDF converter draws.
/// </summary>
public sealed class WordDrawingEditingTests
{
    private static readonly XNamespace W = DocxFactory.W;
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string Letter = """<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>""";

    [Fact]
    public void Lists_the_drawing_of_the_reference_template_with_its_kind_size_and_placement()
    {
        var editor = Template();

        var paragraph = editor.Paragraphs().Single(p => p.Drawings().Count > 0);
        var drawing = Assert.Single(paragraph.Drawings());

        Assert.Equal((0, WordDrawingKind.Ink), (drawing.Index, drawing.Kind));
        Assert.Equal((433.9, 244.9), (Math.Round(drawing.Width, 1), Math.Round(drawing.Height, 1)));
        Assert.True(drawing.IsFloating);
        Assert.Equal((-7.5, -75.7), (Math.Round(drawing.OffsetX, 1), Math.Round(drawing.OffsetY, 1)));
        Assert.Equal(("column", "paragraph", WordWrap.None, false), (drawing.HorizontalRelativeTo, drawing.VerticalRelativeTo, drawing.Wrap, drawing.BehindText));
        Assert.Equal("Encre 1", drawing.Name);
        Assert.Equal(editor.Paragraphs().Count - 1, editor.Paragraphs().Count(p => p.Drawings().Count == 0));
    }

    [Fact]
    public void Lists_the_content_controls_of_one_paragraph_only()
    {
        var editor = WordEditor.Open(ContentControlTemplate.Build());
        var paragraphs = editor.Paragraphs();

        var dates = paragraphs.Single(p => p.Text.StartsWith("Du ", StringComparison.Ordinal)).ContentControls();
        var notes = paragraphs.Single(p => p.Text == "Première note");

        Assert.Equal(["debut", "fin"], dates.Select(c => c.Tag));
        Assert.All(dates, c => Assert.Equal(WordContentControlLevel.Inline, c.Level));
        Assert.Empty(notes.ContentControls());
        Assert.Equal(["mois", "annee", "paiement", "signature"], paragraphs.Single(p => p.Text.Contains("Loyer", StringComparison.Ordinal)).ContentControls().Select(c => c.Tag));
        Assert.Equal(editor.ContentControls().Count(c => c.Level == WordContentControlLevel.Inline && c.PartName == "word/document.xml"), paragraphs.Where(p => p.PartName == "word/document.xml").Sum(p => p.ContentControls().Count));
    }

    [Fact]
    public void Inserts_ink_as_an_anchor_without_wrapping_with_its_own_part_relationship_and_content_type()
    {
        var editor = Blank();

        var info = editor.Paragraphs()[0].InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 80, 40)]), new WordDrawingPlacement { OffsetX = 100, OffsetY = 30, Width = 90, Height = 50 });
        var bytes = editor.ToArray();

        Assert.Equal((0, WordDrawingKind.Ink, 90.0, 50.0, true, 100.0, 30.0, WordWrap.None, false), (info.Index, info.Kind, info.Width, info.Height, info.IsFloating, info.OffsetX, info.OffsetY, info.Wrap, info.BehindText));
        var entries = DocxFactory.Entries(bytes);
        Assert.Contains("word/ink/ink1.xml", entries.Keys);
        Assert.Contains("""<Override PartName="/word/ink/ink1.xml" ContentType="application/inkml+xml" />""", DocxFactory.Part(bytes, "[Content_Types].xml").Replace("\"/>", "\" />", StringComparison.Ordinal), StringComparison.Ordinal);
        var relationship = Relationships(bytes).Single(r => (string?)r.Attribute("Target") == "ink/ink1.xml");
        var document = XDocument.Parse(DocxFactory.Part(bytes, "word/document.xml"));
        var anchor = document.Descendants(Wp + "anchor").Single();
        Assert.Equal("0", (string?)anchor.Attribute("behindDoc"));
        Assert.NotNull(anchor.Element(Wp + "wrapNone"));
        Assert.Equal((100 * 12700).ToString(CultureInfo.InvariantCulture), anchor.Element(Wp + "positionH")!.Element(Wp + "posOffset")!.Value);
        Assert.Equal((string?)relationship.Attribute("Id"), document.Descendants().Attributes().Single(a => a.Name.LocalName == "id" && a.Name.Namespace.NamespaceName.EndsWith("/relationships", StringComparison.Ordinal)).Value);
        var ink = Assert.IsType<WordInk>(WordDocument.Load(bytes).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
        Assert.Equal((90.0, 50.0, 1), (ink.Width, ink.Height, ink.Strokes.Count));
        Assert.Equal(80, ink.Strokes[0].Points[^1].X, 1);
        var reopened = Assert.Single(WordEditor.Open(bytes).Paragraphs()[0].Drawings());
        Assert.Equal(info, reopened);
    }

    [Fact]
    public void Inserts_a_png_with_its_transparency_at_its_pixel_size_at_96_dpi_behind_the_text_when_asked()
    {
        var png = TransparentSquare();
        var editor = Blank();

        var info = editor.Paragraphs()[0].InsertDrawing(WordDrawingContent.Png(png), new WordDrawingPlacement { OffsetX = 10, OffsetY = 20, BehindText = true });
        var bytes = editor.ToArray();

        Assert.Equal((WordDrawingKind.Picture, 15.0, 15.0, true, true), (info.Kind, info.Width, info.Height, info.IsFloating, info.BehindText));
        var entries = DocxFactory.Entries(bytes);
        Assert.Equal(png, entries["word/media/image1.png"]);
        Assert.Contains("image/png", DocxFactory.Part(bytes, "[Content_Types].xml"), StringComparison.Ordinal);
        Assert.Contains(Relationships(bytes), r => (string?)r.Attribute("Target") == "media/image1.png" && ((string?)r.Attribute("Type") ?? string.Empty).EndsWith("/image", StringComparison.Ordinal));
        Assert.Equal("1", (string?)XDocument.Parse(DocxFactory.Part(bytes, "word/document.xml")).Descendants(Wp + "anchor").Single().Attribute("behindDoc"));
        var picture = Assert.IsType<WordPicture>(WordDocument.Load(bytes).Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordShape>().Single());
        Assert.Equal(png, picture.Image.Data);
        Assert.True(picture.Floating!.BehindText);
    }

    [Fact]
    public void Refuses_data_that_is_not_a_decodable_png()
    {
        var good = TransparentSquare();

        Assert.Throws<ArgumentException>(() => WordDrawingContent.Png([]));
        Assert.Throws<ArgumentException>(() => WordDrawingContent.Png("not an image"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => WordDrawingContent.Png(good[..(good.Length / 2)]));
        var corrupted = (byte[])good.Clone();
        corrupted[corrupted.Length - 20] ^= 0xFF;
        Assert.Throws<ArgumentException>(() => WordDrawingContent.Png(corrupted));
        Assert.Throws<ArgumentException>(() => WordDrawingContent.Ink([]));
        Assert.Throws<ArgumentException>(() => WordDrawingContent.Ink([new WordInkStroke([])]));
        Assert.Throws<ArgumentNullException>(() => WordDrawingContent.Png(null!));
    }

    [Fact]
    public void Refuses_an_empty_box_and_an_unknown_drawing_without_touching_the_package()
    {
        var editor = Blank();
        var paragraph = editor.Paragraphs()[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { Width = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.ReplaceDrawing(0, WordDrawingContent.Png(TransparentSquare())));
        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.RemoveDrawing(-1));

        Assert.Empty(editor.ChangedParts);
    }

    [Fact]
    public void Replaces_the_ink_of_the_template_keeping_its_placement_and_drops_its_parts()
    {
        var editor = Template();
        var paragraph = editor.Paragraphs().Single(p => p.Drawings().Count > 0);
        var before = paragraph.Drawings()[0];
        var oldAnchor = XDocument.Parse(DocxFactory.Part(editor.ToArray(), "word/document.xml")).Descendants(Wp + "anchor").Single();

        var after = paragraph.ReplaceDrawing(0, WordDrawingContent.Ink([Stroke(10, 10, 200, 100), Stroke(10, 100, 200, 10)]));
        var bytes = editor.ToArray();

        Assert.Equal(before with { Name = after.Name }, after);
        Assert.NotEqual(before.Name, after.Name);
        var entries = DocxFactory.Entries(bytes);
        Assert.DoesNotContain("word/ink/ink1.xml", entries.Keys);
        Assert.DoesNotContain("word/media/image1.png", entries.Keys);
        Assert.Contains("word/ink/ink2.xml", entries.Keys);
        Assert.DoesNotContain("ink1.xml", DocxFactory.Part(bytes, "[Content_Types].xml"), StringComparison.Ordinal);
        Assert.Equal(["ink/ink2.xml"], Relationships(bytes).Select(r => (string?)r.Attribute("Target")).Where(t => t is not null && (t.StartsWith("ink/", StringComparison.Ordinal) || t.StartsWith("media/", StringComparison.Ordinal))));
        var document = XDocument.Parse(DocxFactory.Part(bytes, "word/document.xml"));
        var anchor = document.Descendants(Wp + "anchor").Single();
        Assert.Equal((string?)oldAnchor.Attribute("relativeHeight"), (string?)anchor.Attribute("relativeHeight"));
        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "Fallback");
        var model = WordDocument.Load(bytes);
        var ink = model.Blocks.OfType<WordParagraph>().SelectMany(p => p.Inlines).OfType<WordInk>().Single();
        Assert.Equal(2, ink.Strokes.Count);
        Assert.Equal(before.Width, ink.Width, 1);
        Assert.Equal(before with { Name = after.Name }, Assert.Single(WordEditor.Open(bytes).Paragraphs().Single(p => p.Drawings().Count > 0).Drawings()));
    }

    [Fact]
    public void Replaces_with_a_new_placement_and_a_png_for_ink_and_the_reverse()
    {
        var editor = Blank();
        var paragraph = editor.Paragraphs()[0];
        paragraph.InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 50, 50)]), new WordDrawingPlacement { OffsetX = 5, OffsetY = 6, Width = 60, Height = 60 });

        var asPicture = paragraph.ReplaceDrawing(0, WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { OffsetX = 70, OffsetY = 80, BehindText = true });
        var inkAgain = paragraph.ReplaceDrawing(0, WordDrawingContent.Ink([Stroke(0, 0, 10, 10)]), new WordDrawingPlacement { Height = 33 });
        var entries = DocxFactory.Entries(editor.ToArray());

        Assert.Equal((WordDrawingKind.Picture, 60.0, 60.0, 70.0, 80.0, true), (asPicture.Kind, asPicture.Width, asPicture.Height, asPicture.OffsetX, asPicture.OffsetY, asPicture.BehindText));
        Assert.Equal((WordDrawingKind.Ink, 60.0, 33.0, 0.0, 0.0, false), (inkAgain.Kind, inkAgain.Width, inkAgain.Height, inkAgain.OffsetX, inkAgain.OffsetY, inkAgain.BehindText));
        Assert.Equal(["word/ink/ink1.xml"], entries.Keys.Where(k => k.StartsWith("word/ink/", StringComparison.Ordinal)));
        Assert.DoesNotContain(entries.Keys, k => k.StartsWith("word/media/", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeps_the_position_of_a_replaced_vml_picture_and_an_inline_one_stays_inline()
    {
        var png = TransparentSquare();
        var vml = $"""<w:p><w:r><w:pict xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w10="urn:schemas-microsoft-com:office:word"><v:shape id="s1" style="position:absolute;margin-left:30pt;margin-top:20pt;width:50pt;height:40pt;z-index:-5;mso-position-horizontal-relative:margin;mso-position-vertical-relative:page"><v:imagedata r:id="rId9"/></v:shape></w:pict></w:r></w:p>""";
        var inline = """<w:p><w:r><w:drawing><wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><wp:extent cx="635000" cy="254000"/><wp:docPr id="7" name="Image 7"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:blipFill><a:blip r:embed="rId9"/></pic:blipFill></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>""";
        var package = DocxFactory.Build(vml + inline, documentRelationships: """<Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image9.png"/>""", binaryParts: new Dictionary<string, byte[]> { ["word/media/image9.png"] = png });
        var editor = WordEditor.Open(package);
        var (first, second) = (editor.Paragraphs()[0], editor.Paragraphs()[1]);

        var listed = Assert.Single(first.Drawings());
        var replaced = first.ReplaceDrawing(0, WordDrawingContent.Png(png));
        var inlineReplaced = second.ReplaceDrawing(0, WordDrawingContent.Ink([Stroke(0, 0, 20, 20)]));

        Assert.Equal((WordDrawingKind.Picture, 50.0, 40.0, 30.0, 20.0, "margin", "page", true), (listed.Kind, listed.Width, listed.Height, listed.OffsetX, listed.OffsetY, listed.HorizontalRelativeTo, listed.VerticalRelativeTo, listed.BehindText));
        Assert.Equal((50.0, 40.0, 30.0, 20.0, "margin", "page", true), (replaced.Width, replaced.Height, replaced.OffsetX, replaced.OffsetY, replaced.HorizontalRelativeTo, replaced.VerticalRelativeTo, replaced.BehindText));
        Assert.Equal((WordDrawingKind.Ink, false, 50.0, 20.0), (inlineReplaced.Kind, inlineReplaced.IsFloating, inlineReplaced.Width, inlineReplaced.Height));
        Assert.DoesNotContain("word/media/image9.png", DocxFactory.Entries(editor.ToArray()).Keys);
    }

    [Fact]
    public void Removes_a_drawing_with_its_parts_and_leaves_the_text_and_the_other_drawings()
    {
        var editor = Blank();
        var paragraph = editor.Paragraphs()[0];
        paragraph.InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 50, 50)]), new WordDrawingPlacement());
        paragraph.InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { OffsetX = 40 });
        paragraph.InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 5, 5)]), new WordDrawingPlacement { OffsetX = 80 });

        paragraph.RemoveDrawing(1);
        var bytes = editor.ToArray();

        Assert.Equal([WordDrawingKind.Ink, WordDrawingKind.Ink], paragraph.Drawings().Select(d => d.Kind));
        Assert.Equal([0.0, 80.0], paragraph.Drawings().Select(d => d.OffsetX));
        var entries = DocxFactory.Entries(bytes);
        Assert.DoesNotContain(entries.Keys, k => k.StartsWith("word/media/", StringComparison.Ordinal));
        Assert.Equal(["word/ink/ink1.xml", "word/ink/ink2.xml"], entries.Keys.Where(k => k.StartsWith("word/ink/", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Equal("Bonjour", paragraph.Text);
        paragraph.RemoveDrawing(1);
        paragraph.RemoveDrawing(0);
        Assert.Empty(paragraph.Drawings());
        Assert.Equal(["Bonjour"], XDocument.Parse(DocxFactory.Part(editor.ToArray(), "word/document.xml")).Descendants(W + "r").Select(r => r.Value));
        Assert.DoesNotContain(DocxFactory.Entries(editor.ToArray()).Keys, k => k.StartsWith("word/ink/", StringComparison.Ordinal));
    }

    [Fact]
    public void A_shared_image_stays_while_another_drawing_names_it()
    {
        var png = TransparentSquare();
        var shared = """<w:p><w:r><w:drawing><wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><wp:extent cx="635000" cy="254000"/><wp:docPr id="{0}" name="Image"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:blipFill><a:blip r:embed="rId9"/></pic:blipFill></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>""";
        var package = DocxFactory.Build(string.Format(CultureInfo.InvariantCulture, shared, 1) + string.Format(CultureInfo.InvariantCulture, shared, 2), documentRelationships: """<Relationship Id="rId9" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image9.png"/>""", binaryParts: new Dictionary<string, byte[]> { ["word/media/image9.png"] = png });
        var editor = WordEditor.Open(package);

        editor.Paragraphs()[0].RemoveDrawing(0);
        Assert.Contains("word/media/image9.png", DocxFactory.Entries(editor.ToArray()).Keys);

        editor.Paragraphs()[1].RemoveDrawing(0);
        Assert.DoesNotContain("word/media/image9.png", DocxFactory.Entries(editor.ToArray()).Keys);
    }

    [Fact]
    public void Gives_every_new_drawing_a_unique_id_and_a_layer_above_the_existing_shapes()
    {
        var editor = Template();
        var paragraphs = editor.Paragraphs();
        var existing = XDocument.Parse(DocxFactory.Part(editor.ToArray(), "word/document.xml")).Descendants(Wp + "anchor").Single();
        var (existingId, existingLayer) = (long.Parse((string)existing.Element(Wp + "docPr")!.Attribute("id")!, CultureInfo.InvariantCulture), long.Parse((string)existing.Attribute("relativeHeight")!, CultureInfo.InvariantCulture));

        paragraphs[0].InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 10, 10)]), new WordDrawingPlacement());
        paragraphs[0].InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement());
        paragraphs[1].InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { BehindText = true });
        var anchors = XDocument.Parse(DocxFactory.Part(editor.ToArray(), "word/document.xml")).Descendants(Wp + "anchor").ToList();

        var ids = anchors.Select(a => (string)a.Element(Wp + "docPr")!.Attribute("id")!).ToList();
        Assert.Equal(4, ids.Distinct(StringComparer.Ordinal).Count());
        var added = anchors.Where(a => (string)a.Element(Wp + "docPr")!.Attribute("id")! != existingId.ToString(CultureInfo.InvariantCulture)).ToList();
        Assert.All(added, a => Assert.True(long.Parse((string)a.Element(Wp + "docPr")!.Attribute("id")!, CultureInfo.InvariantCulture) > existingId));
        var layers = added.Select(a => long.Parse((string)a.Attribute("relativeHeight")!, CultureInfo.InvariantCulture)).ToList();
        Assert.All(layers, layer => Assert.True(layer > existingLayer));
        Assert.Equal(layers.Order().ToList(), layers);
        Assert.Equal(3, layers.Distinct().Count());
    }

    [Fact]
    public void An_edited_document_is_valid_against_the_schemas()
    {
        var editor = Template();
        var paragraphs = editor.Paragraphs();
        paragraphs[0].InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 10, 10)]), new WordDrawingPlacement { OffsetX = 3, OffsetY = 4 });
        paragraphs[0].InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { BehindText = true });
        var original = WordSchemaValidator.Validate(Template().ToArray());

        var report = WordSchemaValidator.Validate(editor.ToArray());

        Assert.Equal(original.Errors.Count, report.Errors.Count);
    }

    [Fact]
    public void A_document_edited_with_the_editor_renders_like_the_model_read_from_it_and_saved_again()
    {
        var bytesA = EditedTemplate(withNewDrawings: true);
        var bytesB = WordDocument.Load(bytesA).ToArray();
        var pdfA = WordToPdf.Convert(bytesA);
        var pdfB = WordToPdf.Convert(bytesB);
        var comparison = PdfPageComparison.Compare(PdfDocument.Open(pdfA.Pdf), PdfDocument.Open(pdfB.Pdf), 1);
        var without = PdfPageComparison.Compare(PdfDocument.Open(pdfA.Pdf), PdfDocument.Open(WordToPdf.Convert(EditedTemplate(withNewDrawings: false)).Pdf), 1);

        Assert.Equal(pdfA.Gaps, pdfB.Gaps);
        Assert.DoesNotContain(pdfA.Gaps, gap => gap.Contains("skipped", StringComparison.Ordinal));
        Assert.False(comparison.SizeMismatch);
        Assert.Equal(0, comparison.DifferingPixels);
        Assert.True(comparison.InkPixelsA > 1000, "the page is blank");
        // The two inserted drawings are really on the page: without them the page differs.
        Assert.True(without.DifferingPixels > 500, $"the inserted drawings do not show ({without.DifferingPixels} pixels)");
        var listed = WordEditor.Open(bytesA).Paragraphs().Where(p => p.Drawings().Count > 0).ToList();
        Assert.Equal(2, listed.Count);
        Assert.Equal([WordDrawingKind.Ink], listed.Single(p => p.Drawings().Count == 1).Drawings().Select(d => d.Kind));
        var added = listed.Single(p => p.Drawings().Count == 2).Drawings();
        Assert.Equal([WordDrawingKind.Ink, WordDrawingKind.Picture], added.Select(d => d.Kind));
        Assert.Equal([(300.0, 5.0, 70.0, 40.0), (400.0, 5.0, 40.0, 40.0)], added.Select(d => (d.OffsetX, d.OffsetY, d.Width, d.Height)));
    }

    // The template with its controls filled, a paragraph rewritten and its ink replaced; the new ink and PNG on request.
    private static byte[] EditedTemplate(bool withNewDrawings)
    {
        var editor = Template();
        var controls = editor.ContentControls();
        var values = controls.Select(c => c.Tag!).Distinct().ToDictionary(t => t, t => controls.First(c => c.Tag == t).Kind == WordContentControlKind.Date ? "2026-03-01" : controls.First(c => c.Tag == t).ListItems[2].Value);
        Assert.Empty(editor.FillContentControls(values).Gaps);
        var paragraphs = editor.Paragraphs();
        var text = paragraphs.First(p => p.EditableText.Length > 20);
        text.SetText("Texte remplacé par l'éditeur, sur une ligne assez longue pour compter.");
        paragraphs.Single(p => p.Drawings().Count > 0).ReplaceDrawing(0, WordDrawingContent.Ink([Stroke(0, 0, 150, 80), Stroke(0, 80, 150, 0)]));
        if (withNewDrawings)
        {
            var target = paragraphs.First(p => p.EditableText.Length > 0 && p != text);
            target.InsertDrawing(WordDrawingContent.Ink([Stroke(0, 0, 60, 30), Stroke(0, 30, 60, 0)]), new WordDrawingPlacement { OffsetX = 300, OffsetY = 5, Width = 70, Height = 40 });
            target.InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { OffsetX = 400, OffsetY = 5, Width = 40, Height = 40 });
        }

        return editor.ToDocumentArray();
    }

    [Fact]
    public void The_pdf_shows_the_inserted_ink_and_the_png_where_they_were_placed_and_nothing_else()
    {
        var editor = Blank();
        var paragraph = editor.Paragraphs()[0];
        paragraph.InsertDrawing(WordDrawingContent.Ink([new WordInkStroke([(0, 0), (60, 30)], 4, "FF0000")]), new WordDrawingPlacement { OffsetX = 100, OffsetY = 100, Width = 60, Height = 30 });
        paragraph.InsertDrawing(WordDrawingContent.Png(TransparentSquare()), new WordDrawingPlacement { OffsetX = 300, OffsetY = 100, Width = 40, Height = 40 });

        var page = Render(WordToPdf.Convert(editor.ToArray()).Pdf);

        // The page is Letter with 72 pt margins, the paragraph starts at (72, 72): the ink box is (172, 172) to (232, 202).
        Assert.True(Painted(page, 195, 180, 210, 195) > 20, "the red diagonal is not at the centre of its box");
        var (r, g, b, _) = page.GetRgba(202, 187);
        Assert.True(r > 200 && g < 90 && b < 90, $"the stroke is not red ({r},{g},{b})");
        // The PNG is (372, 172) to (412, 212): its left half is opaque green, its right half is transparent.
        var (pr, pg, pb, _) = page.GetRgba(380, 190);
        Assert.True(pg > 150 && pr < 80 && pb < 80, $"the opaque half is not green ({pr},{pg},{pb})");
        var (tr, tg, tb, _) = page.GetRgba(404, 190);
        Assert.True(tr > 245 && tg > 245 && tb > 245, $"the transparent half is not white ({tr},{tg},{tb})");
        Assert.Equal(0, Painted(page, 0, 150, 160, 230) + Painted(page, 240, 150, 365, 230) + Painted(page, 420, 150, 612, 230) + Painted(page, 0, 215, 612, 792));
    }

    private static WordEditor Template() => WordEditor.Open(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "rent-receipt-template.docx")));

    private static WordEditor Blank() => WordEditor.Open(DocxFactory.Build($"<w:p><w:r><w:t>Bonjour</w:t></w:r></w:p>{Letter}"));

    private static WordInkStroke Stroke(double x1, double y1, double x2, double y2) => new([(x1, y1), (x2, y2)], 2);

    private static List<XElement> Relationships(byte[] package) =>
        XDocument.Parse(DocxFactory.Part(package, "word/_rels/document.xml.rels")).Root!.Elements(Rel + "Relationship").ToList();

    // 20 x 20 pixels at 96 dpi (15 pt): the left half opaque green, the right half fully transparent (its colour is red).
    private static byte[] TransparentSquare()
    {
        var image = new RasterImage(20, 20, ImageColorType.Rgba);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var offset = ((y * 20) + x) * 4;
                var opaque = x < 10;
                (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2], image.Pixels[offset + 3]) = opaque ? ((byte)0, (byte)200, (byte)0, (byte)255) : ((byte)255, (byte)0, (byte)0, (byte)0);
            }
        }

        return PngCodec.Encode(image);
    }

    private static RasterImage Render(byte[] pdf) => PdfRenderer.Render(PdfDocument.Open(pdf).GetPage(1), new PdfRenderOptions { Dpi = 72 }).Image;

    private static int Painted(RasterImage image, int left, int top, int right, int bottom)
    {
        var count = 0;
        for (var y = top; y < Math.Min(bottom, image.Height); y++)
        {
            for (var x = left; x < Math.Min(right, image.Width); x++)
            {
                var (r, g, b, _) = image.GetRgba(x, y);
                count += r < 245 || g < 245 || b < 245 ? 1 : 0;
            }
        }

        return count;
    }
}
