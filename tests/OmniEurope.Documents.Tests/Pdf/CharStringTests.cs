// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Rendering;
using OmniEurope.Documents.Pdf.Rendering.Fonts;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Runs every Type 1 and Type 2 charstring operator on programs assembled from the specifications; the expected
/// points are the running sums of the operands, worked out by hand.
/// </summary>
public sealed class CharStringTests
{
    private static GlyphCommand M(double x, double y) => new('M', x, y);

    private static GlyphCommand L(double x, double y) => new('L', x, y);

    private static GlyphCommand C(double x1, double y1, double x2, double y2, double x3, double y3) => new('C', x1, y1, x2, y2, x3, y3);

    private static readonly GlyphCommand Z = new('Z');

    [Fact]
    public void Type2_operators_draw_lines_curves_and_flexes_and_call_subroutines()
    {
        var main = CharStringAssembler.Type2(
            "50 10 20 rmoveto 30 40 rlineto 300 -300 rlineto -300 300 rlineto 2000 0 rlineto -2000 0 rlineto 1.5 0 rlineto -1.5 0 rlineto "
            + "10 20 30 hlineto 5 6 vlineto 1 2 3 4 5 6 rrcurveto 1 1 1 1 1 1 10 0 rcurveline 5 0 1 1 1 1 1 1 rlinecurve "
            + "7 1 2 3 4 vvcurveto 1 2 3 4 hhcurveto 1 2 3 4 5 6 7 8 9 vhcurveto 1 2 3 4 hvcurveto "
            + "1 1 1 1 1 1 1 1 1 1 1 1 50 flex 1 2 3 4 5 6 7 hflex 1 2 3 4 5 6 7 8 9 hflex1 "
            + "10 0 10 0 10 0 10 0 10 1 10 flex1 0 10 0 10 0 10 0 10 1 10 10 flex1 1 flex "
            + "0 10 20 10 hstem 0 5 vstemhm 0 5 hstemhm 0 5 hintmask #80 cntrmask #80 "
            + "callsubr 99 callsubr -107 callsubr -107 callgsubr 5 hmoveto 5 vmoveto endchar");
        var local = CharStringAssembler.Type2("10 0 rlineto return");
        var global = CharStringAssembler.Type2("0 10 rlineto return");
        byte[] data = [.. main, .. local, .. global];
        var locals = new List<(int Start, int End)> { (main.Length, main.Length + local.Length) };
        var globals = new List<(int Start, int End)> { (main.Length + local.Length, data.Length) };

        var commands = new Type2CharString(data, globals, locals).Run(0, main.Length, 0);

        GlyphCommand[] expected =
        [
            M(10, 20), L(40, 60), L(340, -240), L(40, 60), L(2040, 60), L(40, 60), L(41.5, 60), L(40, 60),
            L(50, 60), L(50, 80), L(80, 80), L(80, 85), L(86, 85), C(87, 87, 90, 91, 95, 97),
            C(96, 98, 97, 99, 98, 100), L(108, 100), L(113, 100), C(114, 101, 115, 102, 116, 103),
            C(123, 104, 125, 107, 125, 111), C(126, 111, 128, 114, 132, 114),
            C(132, 115, 134, 118, 138, 118), C(143, 118, 149, 125, 158, 133), C(159, 133, 161, 136, 161, 140),
            C(162, 141, 163, 142, 164, 143), C(165, 144, 166, 145, 167, 146),
            C(168, 146, 170, 149, 174, 149), C(179, 149, 185, 146, 192, 146),
            C(193, 148, 196, 152, 201, 152), C(207, 152, 214, 160, 223, 146),
            C(233, 146, 243, 146, 253, 146), C(263, 146, 273, 147, 283, 146),
            C(283, 156, 283, 166, 283, 176), C(283, 186, 284, 196, 283, 206),
            L(293, 206), L(293, 216), Z, M(298, 216), Z, M(298, 221), Z,
        ];
        Assert.Equal(expected, commands);
    }

    [Theory]
    [InlineData("100 endchar", "")]
    [InlineData("100 0 10 hstem 10 20 rmoveto endchar", "M10,20 Z")]
    [InlineData("100 10 hmoveto -10 20 rmoveto endchar", "M10,0 Z M0,20 Z")]
    [InlineData("100 20 vmoveto 10 0 rmoveto endchar", "M0,20 Z M10,20 Z")]
    public void Type2_odd_first_operands_carry_the_advance_width(string program, string expected)
    {
        var data = CharStringAssembler.Type2(program);

        var commands = new Type2CharString(data, [], []).Run(0, data.Length, 0);

        Assert.Equal(expected, string.Join(" ", commands.Select(c => c.Op == 'Z' ? "Z" : $"{c.Op}{c.X1},{c.Y1}")));
    }

    [Fact]
    public void Type1_operators_draw_the_outline_with_subroutines_flex_and_other_subroutines()
    {
        var glyph = CharStringAssembler.Type1(
            "50 600 hsbw 0 10 hstem 0 10 vstem 0 1 2 3 4 5 hstem3 0 1 2 3 4 5 vstem3 10 20 rmoveto 30 hlineto 30 vlineto -30 0 rlineto "
            + "0 10 10 0 10 0 rrcurveto 10 10 10 10 vhcurveto 10 10 10 10 hvcurveto closepath 5 hmoveto 5 vmoveto "
            + "1 callsubr callsubr 99 callsubr 100 2 div hlineto -300 0 rlineto 100000 0 rlineto -100000 0 rlineto "
            + "200 300 setcurrentpoint 10 0 rlineto 0 1 callothersubr "
            + "10 0 rmoveto 0 2 callothersubr 10 10 rmoveto 0 2 callothersubr 10 0 rmoveto 0 2 callothersubr "
            + "10 -10 rmoveto 0 2 callothersubr 10 -10 rmoveto 0 2 callothersubr 10 0 rmoveto 0 2 callothersubr "
            + "10 10 rmoveto 0 2 callothersubr 50 280 300 3 0 callothersubr pop pop setcurrentpoint "
            + "2 1 3 callothersubr pop callsubr 7 8 2 12 callothersubr pop pop rlineto 5 callothersubr dotsection "
            + "10 0 div 0 rlineto pop 5 rlineto endchar 1000 0 rlineto");

        var commands = new Type1CharString(Font(glyph)).Run(glyph);

        GlyphCommand[] expected =
        [
            M(60, 20), L(90, 20), L(90, 50), L(60, 50), C(60, 60, 70, 60, 80, 60), C(80, 70, 90, 80, 100, 80),
            C(110, 80, 120, 90, 120, 100), Z, M(125, 100), Z, M(125, 105), L(425, 105), L(475, 105), L(175, 105),
            L(100175, 105), L(175, 105), L(210, 300), C(230, 310, 240, 310, 250, 300), C(260, 290, 270, 290, 280, 300),
            L(288, 307), L(298, 307), L(298, 312), Z,
        ];
        Assert.Equal(expected, commands);
    }

    [Fact]
    public void Type1_seac_composes_the_base_and_the_accent_from_the_standard_encoding()
    {
        var aacute = CharStringAssembler.Type1("0 600 hsbw 0 100 200 65 194 seac 1000 0 rlineto");
        var font = Font(aacute);

        var commands = new Type1CharString(font).Run(aacute);

        GlyphCommand[] expected = [M(100, 0), L(600, 0), L(600, 600), L(100, 600), Z, M(100, 200), L(150, 250), Z];
        Assert.Equal(expected, commands);
    }

    [Fact]
    public void Type1_seac_without_enough_operands_or_known_codes_draws_nothing_more()
    {
        var program = CharStringAssembler.Type1("0 500 hsbw 1 seac 10 20 rmoveto 0 0 0 300 999 seac 1000 0 rlineto");

        var commands = new Type1CharString(Font(program)).Run(program);

        Assert.Equal([M(10, 20), Z], commands);
    }

    [Theory]
    [InlineData(4, "binary")]
    [InlineData(4, "hex")]
    [InlineData(4, "pfb")]
    [InlineData(-1, "binary")]
    public void Type1_fonts_are_read_from_every_packaging(int lenIV, string packaging)
    {
        var square = CharStringAssembler.Type1("0 700 hsbw 100 0 rmoveto 500 0 rlineto 0 600 rlineto closepath 1 callsubr endchar");
        var (program, length1, length2) = FontPrograms.Type1(
            [CharStringAssembler.Type1("return"), CharStringAssembler.Type1("return")],
            [(".notdef", CharStringAssembler.Type1("0 500 hsbw endchar")), ("A", square)],
            lenIV,
            packaging);

        var font = Type1Font.Parse(program, length1, length2);

        Assert.Equal(2, font.Subrs.Count);
        Assert.Equal(square, font.CharStrings["A"]);
        Assert.Equal([M(100, 0), L(600, 0), L(600, 600), Z], new Type1CharString(font).Run(font.CharStrings["A"]));
    }

    [Fact]
    public void Subroutine_numbers_past_the_supported_range_are_skipped()
    {
        // "dup 2147483647 1 RD" would grow the table to two billion entries before anything is drawn.
        var ret = CharStringAssembler.Type1("return");
        var (program, length1, length2) = FontPrograms.Type1(
            [ret, ret, ret, ret],
            [(".notdef", CharStringAssembler.Type1("0 500 hsbw endchar"))],
            lenIV: -1,
            numbers: [0, 2147483647, 99999999999, 65535]);

        var font = Type1Font.Parse(program, length1, length2);

        Assert.Equal(65536, font.Subrs.Count);
        Assert.Equal(ret, font.Subrs[0]);
        Assert.Equal(ret, font.Subrs[65535]);
    }

    // A font holding the glyph under test, the square "A", an "acute" stroke, and subroutines 0 to 3 (1 draws a line).
    private static Type1Font Font(byte[] glyph)
    {
        var square = CharStringAssembler.Type1("0 700 hsbw 100 0 rmoveto 500 0 rlineto 0 600 rlineto -500 0 rlineto closepath 0 callsubr endchar");
        var acute = CharStringAssembler.Type1("0 0 300 0 sbw 0 0 rmoveto 50 50 rlineto closepath endchar");
        var ret = CharStringAssembler.Type1("return");
        var (program, length1, length2) = FontPrograms.Type1(
            [ret, CharStringAssembler.Type1("300 0 rlineto return"), ret, ret],
            [(".notdef", CharStringAssembler.Type1("0 500 hsbw endchar")), ("A", square), ("acute", acute), ("test", glyph)]);
        return Type1Font.Parse(program, length1, length2);
    }
}
