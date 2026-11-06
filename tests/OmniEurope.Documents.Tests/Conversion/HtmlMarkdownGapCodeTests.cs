// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// The gaps of Word to HTML, Word to Markdown and HTML to Word carry a stable code, keep their English text in
/// <c>Gaps</c> and read in French under a French culture.
/// </summary>
public sealed class HtmlMarkdownGapCodeTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [Fact]
    public void Word_to_html_names_the_format_of_a_picture_it_leaves_out()
    {
        var document = new WordDocument();
        document.AddParagraph().Add(new WordPicture(new WordImage(Encoding.ASCII.GetBytes("not an image"), "image/x-unknown"), 40, 20));

        var result = WordToHtml.Convert(document);

        var gap = Assert.Single(result.GapDetails, g => g.Code == "HtmlPictureFormatLeftOut");
        Assert.Equal(["image/x-unknown"], gap.Arguments);
        Assert.Equal("picture format image/x-unknown left out", gap.Message);
        Assert.Contains(gap.Message, result.Gaps);
        Assert.Equal("format d'image image/x-unknown omis", gap.Describe(French));
    }

    [Fact]
    public void Word_to_markdown_reports_the_comments_it_leaves_out_with_a_code()
    {
        var document = new WordDocument();
        document.AddParagraph("texte");
        document.Comments.Add(new WordComment(1));

        var result = WordToMarkdown.Convert(document);

        var gap = Assert.Single(result.GapDetails, g => g.Code == "MarkdownCommentsLeftOut");
        Assert.Equal("comments are left out", gap.Message);
        Assert.Contains(gap.Message, result.Gaps);
        Assert.Equal("les commentaires sont omis", gap.Describe(French));
    }

    [Fact]
    public void Html_to_word_reports_a_linked_style_sheet_with_a_code()
    {
        var document = HtmlToWord.Convert("<html><head><link rel=\"stylesheet\" href=\"a.css\"></head><body><p>x</p></body></html>");

        var gap = Assert.Single(document.GapDetails, g => g.Code == "HtmlToWordLinkedStyleSheetsNotRead");
        Assert.Equal("linked style sheets are not read (nothing is fetched)", gap.Message);
        Assert.Contains(gap.Message, document.Gaps);
        Assert.Equal("les feuilles de style liées ne sont pas lues (rien n'est téléchargé)", gap.Describe(French));
    }

    [Fact]
    public void Html_to_word_reports_an_image_that_is_not_a_data_uri_once_with_a_code()
    {
        var document = HtmlToWord.Convert("<p><img src=\"http://example.test/a.png\"><img src=\"http://example.test/b.png\"></p>");

        var gap = Assert.Single(document.GapDetails, g => g.Code == "HtmlToWordImagesNotDataUris");
        Assert.Equal("images that are not data: URIs are not loaded", gap.Message);
        Assert.Equal("les images qui ne sont pas des URI data: ne sont pas chargées", gap.Describe(French));
    }
}
