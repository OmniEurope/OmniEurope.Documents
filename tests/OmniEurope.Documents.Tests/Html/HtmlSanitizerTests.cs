// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Html;

namespace OmniEurope.Documents.Tests.Html;

public sealed class HtmlSanitizerTests
{
    private static readonly HtmlSanitizer Default = new();

    [Theory]
    [InlineData("<script>alert(1)</script>ok", "ok")]
    [InlineData("<img src=x onerror=alert(1)>", "<img src=\"x\">")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"java&#09;script:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\" JaVaScRiPt:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"jav&#x0A;ascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"vbscript:x\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"data:text/html,<script>\">x</a>", "<a>x</a>")]
    [InlineData("<p style=\"background:url(javascript:x)\">t</p>", "<p>t</p>")]
    [InlineData("<svg><script>alert(1)</script></svg>", "")]
    [InlineData("<iframe src=//evil></iframe>after", "after")]
    [InlineData("<style>*{}</style><b>b</b>", "<b>b</b>")]
    [InlineData("<!--<script>-->x", "x")]
    [InlineData("<form action=/x><input></form>", "")]
    [InlineData("<a href=\"https://ok.example/?a=1&amp;b=2\" title=t>ok</a>", "<a href=\"https://ok.example/?a=1&amp;b=2\" title=\"t\">ok</a>")]
    [InlineData("<a href=\"/relative\">r</a>", "<a href=\"/relative\">r</a>")]
    [InlineData("<a href=\"mailto:a@b.c\">m</a>", "<a href=\"mailto:a@b.c\">m</a>")]
    [InlineData("<div id=x class=c>t</div>", "<div class=\"c\">t</div>")]
    [InlineData("<scr<script>ipt>alert(1)</script>", "")]
    [InlineData("<b>unclosed", "<b>unclosed</b>")]
    [InlineData("<html lang=fr><head><title>T</title></head><body class=x><p>Corps</p></body></html>", "<p>Corps</p>")]
    public void Removes_what_the_default_policy_forbids(string html, string expected)
    {
        Assert.Equal(expected, Default.Sanitize(html));
    }

    [Fact]
    public void Custom_policy_with_data_attributes_and_image_data_urls()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = HtmlSanitizerOptions.Set("p", "img", "a", "span"),
            AllowedAttributes = HtmlSanitizerOptions.Set("src", "href", "data-eid", "data-akn-*"),
            AllowUrl = (element, attribute, url) => element.TagName == "img" && attribute == "src"
                && url.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase),
        });

        var html = sanitizer.Sanitize(
            "<p data-eid=1 data-akn-class=x data-other=y>t</p><img src=\"data:image/png;base64,AA==\"><a href=\"data:image/png;base64,AA==\">a</a>");

        Assert.Equal("<p data-eid=\"1\" data-akn-class=\"x\">t</p><img src=\"data:image/png;base64,AA==\"><a>a</a>", html);
    }

    [Fact]
    public void Keeping_children_of_removed_elements_keeps_their_text_only()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions { KeepChildrenOfRemovedElements = true });

        Assert.Equal("text <b>bold</b>", sanitizer.Sanitize("<font color=red>text <b>bold</b></font><script>x</script>"));
    }
}
