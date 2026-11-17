// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class WordExactSpacingImageTests
{
    private const double Exact = 12;
    private const double PictureHeight = 60;

    [Fact]
    public void An_inline_picture_taller_than_an_exact_line_grows_its_line_instead_of_covering_the_previous_one()
    {
        var page = Convert(withPicture: true);
        var first = page.Letters.First(l => l.Value == "F").Y;
        var last = page.Letters.First(l => l.Value == "L").Y;
        var bounds = Assert.Single(page.Images).Bounds;

        Assert.Equal(PictureHeight, bounds.Top - bounds.Bottom, 1);
        // The picture starts below the text of the line above it (PDF y grows upwards)...
        Assert.True(bounds.Top < first, $"picture top {bounds.Top} overlaps the previous line (baseline {first})");
        // ...and the text after it starts below the picture.
        Assert.True(last < bounds.Bottom, $"text baseline {last} is not below the picture bottom {bounds.Bottom}");
        Assert.True(first - last >= PictureHeight + Exact, $"lines pushed down by only {first - last}");
    }

    [Fact]
    public void Exact_spacing_without_a_picture_keeps_its_line_pitch()
    {
        var page = Convert(withPicture: false);
        var first = page.Letters.First(l => l.Value == "F").Y;
        var second = page.Letters.First(l => l.Value == "S").Y;
        var last = page.Letters.First(l => l.Value == "L").Y;

        Assert.Equal(Exact, first - second, 2);
        Assert.Equal(Exact, second - last, 2);
    }

    private static PdfPage Convert(bool withPicture)
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "rgb.png"));
        var document = new WordDocument(language: null);
        var paragraph = new WordParagraph
        {
            Properties = new WordParagraphProperties { LineSpacing = Exact, LineSpacingRule = WordLineSpacingRule.Exact, SpacingAfter = 0 },
        };
        var run = new WordRunProperties { Font = "Liberation Serif", FontSize = 10 };
        paragraph.AddText("First", run).Add(new WordBreak(WordBreakKind.Line));
        if (withPicture)
        {
            paragraph.Add(new WordPicture(WordImage.FromBytes(png), 60, PictureHeight));
        }
        else
        {
            paragraph.AddText("Second", run);
        }

        paragraph.Add(new WordBreak(WordBreakKind.Line)).AddText("Last", run);
        document.Body.Add(paragraph);
        return PdfDocument.Open(WordToPdf.Convert(document).Pdf).GetPage(1);
    }
}
