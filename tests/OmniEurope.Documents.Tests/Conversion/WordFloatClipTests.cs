// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// A floating shape anchored inside a text box is drawn within the box, clipped to it like the box's own text, while the
/// shapes in front of the text at page level still go over the whole page body. The page is rendered at 72 dpi (one
/// pixel per point) and the pixels are read.
/// </summary>
public sealed class WordFloatClipTests
{
    // The text box stands at (200, 200) from the page's top-left corner and measures 100 x 50 pt.
    private const double BoxLeft = 200;
    private const double BoxTop = 200;
    private const double BoxWidth = 100;
    private const double BoxHeight = 50;

    [Fact]
    public void A_front_shape_anchored_in_a_text_box_stays_within_the_clip_of_the_box()
    {
        // A 100 x 100 pt picture, placed from the page corner at (150, 150): it overlaps the box only at its lower-right quarter.
        var inner = new WordParagraph().Add(Picture(100, 100, new WordFloatingPosition(150, "page", 150, "page", WordWrap.None)));
        // The box itself is behind the text, so it is painted with the body (a box in front of the text is painted once the body is).
        var box = new WordTextBox(BoxWidth, BoxHeight) { Floating = new WordFloatingPosition(BoxLeft, "page", BoxTop, "page", WordWrap.None, BehindText: true) };
        box.Blocks.Add(inner);
        var image = Render(WordFloatWrapTests.Document(new WordParagraph().Add(box)));

        Assert.True(Painted(image, BoxLeft + 1, BoxTop + 1, BoxLeft + BoxWidth - 1, BoxTop + BoxHeight - 1) > 500, "nothing of the picture shows in the box");
        Assert.Equal(0, Painted(image, 140, 140, BoxLeft - 2, 260));
        Assert.Equal(0, Painted(image, 140, 140, 310, BoxTop - 2));
    }

    [Fact]
    public void A_front_shape_anchored_in_the_body_is_drawn_over_the_text()
    {
        var text = new WordParagraph().Add(Picture(60, 60, new WordFloatingPosition(100, "page", 100, "page", WordWrap.None))).AddText("Texte sous l'image");
        var image = Render(WordFloatWrapTests.Document(text));

        Assert.True(Painted(image, 105, 105, 155, 155) > 2000, "the picture is not drawn");
    }

    private static RasterImage Render(WordDocument document)
    {
        var page = PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);
        return PdfRenderer.Render(page, new PdfRenderOptions { Dpi = 72 }).Image;
    }

    // The number of non-white pixels in the rectangle (from the page's top-left corner).
    private static int Painted(RasterImage image, double left, double top, double right, double bottom)
    {
        var count = 0;
        for (var y = (int)top; y < (int)bottom; y++)
        {
            for (var x = (int)left; x < (int)right; x++)
            {
                var p = image.GetRgba(x, y);
                count += p.R < 245 || p.G < 245 || p.B < 245 ? 1 : 0;
            }
        }

        return count;
    }

    private static WordPicture Picture(double width, double height, WordFloatingPosition position) =>
        new(WordImage.FromBytes(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"))), width, height) { Floating = position };
}
