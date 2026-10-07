// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Pdf;
using OmniEurope.Documents.Pdf.Writing;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Tests.Conversion;

public sealed class LineBreakerTests
{
    private static readonly LayoutContext Context = new(new WordDocument(), new PdfDocumentBuilder());
    private static readonly TextStyle Style = new(new PdfFont("Liberation Sans"), 10, PdfColor.Black);

    [Fact]
    public void Breaks_at_the_last_opportunity_before_an_unbreakable_token()
    {
        var lines = Break(Geometry(), Word("aaaa", 40, false), Space(), Word("bbbb", 40), Word("cc", 30, false));

        Assert.Equal(["aaaa ", "bbbbcc"], Texts(lines));
    }

    [Fact]
    public void Cuts_a_word_wider_than_the_line()
    {
        var word = new TextToken("abcdefghijklmnopqrstuvwxyz", Style) { Width = Context.Measure("abcdefghijklmnopqrstuvwxyz", Style) };

        var lines = Break(Geometry(width: 40), word);

        Assert.True(lines.Count > 2);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", string.Concat(Texts(lines)));
        Assert.All(lines.SkipLast(1), l => Assert.True(l.Items.Sum(i => i.Width) <= 40.01));
    }

    [Fact]
    public void Right_centre_and_decimal_tabs_align_on_their_stops()
    {
        var tabs = new WordTabStop[] { new(30, WordTabAlignment.Center), new(60, WordTabAlignment.Decimal), new(90, WordTabAlignment.Right) };

        var line = Break(Geometry(tabs: tabs), Word("a", 5), Tab(), Word("bb", 10, false), Tab(), Word("1.5", 12, false), Tab(), Word("z", 10, false)).Single();

        Assert.Equal(25, Item(line, "bb").X, 2);
        Assert.Equal(56, Item(line, "1.5").X, 2);
        Assert.Equal(80, Item(line, "z").X, 2);
    }

    [Fact]
    public void Default_stops_hanging_indents_and_positional_tabs()
    {
        var hanging = Break(Geometry(left: 36, first: -36), Word("1.", 10), Tab(), Word("x", 10, false)).Single();
        var grid = Break(Geometry(), Word("a", 10), Tab(), Word("b", 50, false), Tab(), Word("c", 10, false));
        var positional = Break(Geometry(), Word("a", 10), new TabToken(Style, new WordTab { Alignment = WordTabAlignment.Right }), Word("z", 10, false)).Single();

        Assert.Equal(36, Item(hanging, "x").X, 2);
        Assert.Equal(2, grid.Count);
        Assert.Equal(36, Item(grid[0], "b").X, 2);
        Assert.Equal(36, Item(grid[1], "c").X, 2);
        Assert.Equal(90, Item(positional, "z").X, 2);
    }

    [Fact]
    public void Justified_lines_end_on_the_right_edge_except_the_last()
    {
        Token[] tokens = [Word("aa", 20), Space(), Word("bb", 20), Space(), Word("cc", 20), Space(), Word("dd", 20), Space(), Word("ee", 20)];

        var lines = Break(Geometry(alignment: WordAlignment.Justify), tokens);

        var dd = Item(lines[0], "dd");
        Assert.Equal(100, dd.X + dd.Width, 2);
        Assert.Equal(0, Item(lines[1], "ee").X, 2);
    }

    [Fact]
    public void Line_spacing_rules_and_trailing_breaks()
    {
        var exact = Break(Geometry(spacing: 30, rule: WordLineSpacingRule.Exact), Word("a", 10)).Single();
        var atLeast = Break(Geometry(spacing: 2, rule: WordLineSpacingRule.AtLeast), Word("a", 10)).Single();
        var broken = Break(Geometry(), Word("a", 10), new BreakToken(Style, WordBreakKind.Line));

        Assert.Equal(30, exact.Height);
        Assert.Equal(Context.Metrics(Style).LineHeight, atLeast.Height, 3);
        Assert.Equal(2, broken.Count);
        Assert.Empty(broken[1].Items);
    }

    private static LineGeometry Geometry(double width = 100, IReadOnlyList<WordTabStop>? tabs = null, double left = 0, double first = 0,
        WordAlignment alignment = WordAlignment.Left, double? spacing = null, WordLineSpacingRule rule = WordLineSpacingRule.Multiple) =>
        new(width, left, width, first, tabs ?? [], 36, alignment, spacing, rule, Style);

    private static TextToken Word(string text, double width, bool breakBefore = true) => new(text, Style) { Width = width, BreakBefore = breakBefore };

    private static TextToken Space() => new(" ", Style) { Width = 5 };

    private static TabToken Tab() => new(Style, null) { BreakBefore = true };

    private static List<Line> Break(LineGeometry geometry, params Token[] tokens) => new LineBreaker(Context, geometry).Break([.. tokens]);

    private static string[] Texts(List<Line> lines) =>
        lines.Select(l => string.Concat(l.Items.Select(i => i.Token is TextToken t ? t.Text : string.Empty))).ToArray();

    private static Placed Item(Line line, string text) => line.Items.First(i => i.Token is TextToken t && t.Text == text);
}
