// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Tests.Text;

/// <summary>
/// The Unicode Bidirectional Algorithm (UAX #9). Expected levels and orders are worked out by hand from the rules of
/// the specification. In the cases, capital letters stand for Hebrew letters (R, from alef), "~" for an Arabic letter
/// (AL), lower-case letters and digits for themselves.
/// </summary>
public sealed class BidiTests
{
    private const string Rlo = "\u202E";
    private const string Lro = "\u202D";
    private const string Rle = "\u202B";
    private const string Lre = "\u202A";
    private const string Pdf = "\u202C";
    private const string Lri = "\u2066";
    private const string Rli = "\u2067";
    private const string Fsi = "\u2068";
    private const string Pdi = "\u2069";
    private const string HebrewPoint = "\u05B4";

    [Theory]
    // Hebrew in left-to-right text: an R run at level 1.
    [InlineData("abc DEF", 0, "0000111")]
    // Latin in right-to-left text (I2: L at odd level goes up one); the space between R and L takes the embedding level.
    [InlineData("ABC def", 1, "1111222")]
    // W7 not applying: numbers after R stay EN, raised to 2 at level 1 (I2).
    [InlineData("AB 123", 1, "111222")]
    // W2: a European number after an Arabic letter is an Arabic number, raised by two at level 0 (I1).
    [InlineData("~ 12", 0, "1122")]
    // W4: a common separator between two numbers joins them.
    [InlineData("A 1,2", 1, "11222")]
    // W5: a terminator after a number is a number.
    [InlineData("A 10%", 1, "11222")]
    // W4 does not join across two separators; W6 makes them neutral and N1 gives them R, the numbers around counting as R.
    [InlineData("A 1,,2", 1, "112112")]
    // W7: numbers after Latin text are L.
    [InlineData("ab 12 CD", 0, "00000011")]
    // N1: neutrals between two R runs are R.
    [InlineData("ab AB - CD", 0, "0001111111")]
    // N2: neutrals between R and L take the embedding direction.
    [InlineData("AB - cd", 0, "1100000")]
    public void Levels_follow_the_weak_neutral_and_implicit_rules(string text, byte paragraph, string levels)
    {
        Assert.Equal(levels, Levels(text, paragraph));
    }

    [Theory]
    // The example of rule N0 in UAX #9: "AB(CD[&ef]!)gh" in a right-to-left paragraph. The parentheses hold R (the
    // embedding direction) and are R; the brackets hold only L, the text before them is R: they take the embedding
    // direction too.
    [InlineData("AB(CD[&ef]!)gh", 1, "11111112211122")]
    // Parentheses holding only R in left-to-right text after Latin keep the embedding direction (L)...
    [InlineData("ab (AB) cd", 0, "0000110000")]
    // ...and after R text they take the opposite direction, R.
    [InlineData("AB (CD) ab", 0, "1111111000")]
    // Unpaired brackets are plain neutrals.
    [InlineData("ab (AB cd", 0, "000011000")]
    public void Paired_brackets_resolve_as_a_unit(string text, byte paragraph, string levels)
    {
        Assert.Equal(levels, Levels(text, paragraph));
    }

    [Theory]
    // Mixed text in a right-to-left paragraph read from the left: the Latin words keep their order, the Hebrew runs
    // are reversed and come right to left.
    [InlineData("ABC def", 1, "def CBA")]
    [InlineData("AB 123 CD", 1, "DC 123 BA")]
    [InlineData("abc DEF ghi", 0, "abc FED ghi")]
    [InlineData("AB abc 12 CD", 1, "DC abc 12 BA")]
    // An Arabic letter with a European number after it: the number stays left to right, left of the letter.
    [InlineData("~ 12", 0, "12 ~")]
    // The bracket pair after R text in left-to-right text is reversed with it (drawn mirrored).
    [InlineData("AB (CD) ab", 0, ")DC( BA ab")]
    public void Lines_are_reordered_by_rule_L2(string text, byte paragraph, string visual)
    {
        Assert.Equal(visual, Visual(text, paragraph));
    }

    [Fact]
    public void Explicit_overrides_embeddings_and_isolates_set_the_levels()
    {
        // RLO forces Latin letters right to left; the removed controls take the level of what precedes them.
        Assert.Equal("001111", Levels("x" + Rlo + "abc" + Pdf, 0));
        Assert.Equal(Rlo + "cba" + Pdf, Visual(Rlo + "abc" + Pdf, 0));
        // RLE embeds Latin text at level 1 (resolved to 2) inside level 0 text.
        Assert.Equal("0022", Levels("x" + Rle + "ab" + Pdf, 0)[..4]);
        // LRO in a right-to-left paragraph forces Hebrew left to right (level 2).
        Assert.Equal("1122", Levels("A" + Lro + "BC" + Pdf, 1)[..4]);
        // An FSI holding Hebrew first is a right-to-left isolate; the initiator, its PDI and the neutrals around stay at
        // level 0: they do not see the R inside.
        Assert.Equal("00011000", Levels("x " + Fsi + "AB" + Pdi + " y", 0));
        // An FSI holding Latin first is a left-to-right isolate: inside a right-to-left paragraph the Latin is at 2.
        Assert.Equal("11221", Levels("A" + Fsi + "ab" + Pdi, 1));
        // LRI and RLI, and a PDI without an initiator, which is then a plain neutral.
        Assert.Equal("0110", Levels(Rli + "AB" + Pdi, 0));
        Assert.Equal("1221", Levels(Lri + "ab" + Pdi, 1));
        Assert.Equal("000", Levels("a" + Pdi + "b", 0));
        // A PDF closes the embedding; an unmatched PDF is ignored.
        Assert.Equal("12222", Levels(Lre + "ab" + Pdf + "c", 1));
        Assert.Equal("00", Levels("a" + Pdf, 0));
    }

    [Fact]
    public void An_override_applies_to_isolate_initiators_and_the_embedding_stack_overflows_quietly()
    {
        // Under RLO, the LRI itself is R (level 1) and its content L at level 2.
        Assert.Equal("0122", Levels(Rlo + Lri + "ab", 0));
        // 130 nested embeddings: those past depth 125 are ignored, the text keeps the deepest valid level (125), and
        // I2 raises its Latin letter to 126.
        var deep = string.Concat(Enumerable.Repeat(Rle + Lre, 65)) + "a";
        Assert.Equal(126, BidiAlgorithm.Levels(deep, 0)[^1]);
        var isolates = string.Concat(Enumerable.Repeat(Rli + Lri, 65)) + "a" + string.Concat(Enumerable.Repeat(Pdi, 130));
        var isolated = BidiAlgorithm.Levels(isolates, 0);
        Assert.Equal(126, isolated.Max());
        Assert.Equal(0, isolated[^1]);
    }

    [Fact]
    public void Non_spacing_marks_take_the_type_before_them()
    {
        // W1: a Hebrew point after a Hebrew letter is R, at the start of a sequence the sos, after a PDI ON.
        Assert.Equal("011", Levels("aA" + HebrewPoint, 0));
        Assert.Equal("1", Levels(HebrewPoint, 1));
        Assert.Equal("0200", Levels(Lri + HebrewPoint + Pdi + "b", 0));
        Assert.Equal("00200", Levels("a" + Lri + "b" + Pdi + HebrewPoint, 0));
        // A bracket that N0 resolves carries the marks after it (a combining acute accent here).
        Assert.Equal("111111", Levels("AB(C)\u0301", 0));
    }

    [Fact]
    public void Trailing_white_space_and_separators_return_to_the_paragraph_level()
    {
        // The spaces between two Hebrew words are R; ending the first line, they go back to level 0.
        var text = Hebrew("AB  CD");
        var levels = BidiAlgorithm.Levels(text, 0);

        Assert.Equal([1, 1, 1, 1, 1, 1], levels);
        Assert.Equal([1, 1, 0, 0], BidiAlgorithm.LineLevels(text, levels, 0, 4, 0));
        // A tab and the spaces before it reset too (segment separator).
        var tabbed = Hebrew("AB \tCD");
        var line = BidiAlgorithm.LineLevels(tabbed, BidiAlgorithm.Levels(tabbed, 0), 0, tabbed.Length, 0);
        Assert.Equal([1, 1, 0, 0, 1, 1], line);
    }

    [Theory]
    [InlineData(0x05D0, BidiClass.R)]
    [InlineData(0x05BE, BidiClass.R)]
    [InlineData(0x05B4, BidiClass.NSM)]
    [InlineData(0x0628, BidiClass.AL)]
    [InlineData(0x061F, BidiClass.AL)]
    [InlineData(0x0661, BidiClass.AN)]
    [InlineData(0x06F1, BidiClass.EN)]
    [InlineData(0x066A, BidiClass.ET)]
    [InlineData(0x060C, BidiClass.CS)]
    [InlineData(0x0041, BidiClass.L)]
    [InlineData(0x0031, BidiClass.EN)]
    [InlineData(0x002B, BidiClass.ES)]
    [InlineData(0x0024, BidiClass.ET)]
    [InlineData(0x20AC, BidiClass.ET)]
    [InlineData(0x002C, BidiClass.CS)]
    [InlineData(0x00A0, BidiClass.CS)]
    [InlineData(0x0020, BidiClass.WS)]
    [InlineData(0x2003, BidiClass.WS)]
    [InlineData(0x0009, BidiClass.S)]
    [InlineData(0x2029, BidiClass.B)]
    [InlineData(0x000A, BidiClass.B)]
    [InlineData(0x200D, BidiClass.BN)]
    [InlineData(0x00AD, BidiClass.BN)]
    [InlineData(0x0021, BidiClass.ON)]
    [InlineData(0x0028, BidiClass.ON)]
    [InlineData(0x00B2, BidiClass.EN)]
    [InlineData(0x00BD, BidiClass.ON)]
    [InlineData(0x0966, BidiClass.L)]
    [InlineData(0x07C1, BidiClass.R)]
    [InlineData(0x0301, BidiClass.NSM)]
    [InlineData(0x200F, BidiClass.R)]
    [InlineData(0x061C, BidiClass.AL)]
    [InlineData(0x05FF, BidiClass.R)]
    [InlineData(0x08FF, BidiClass.NSM)]
    [InlineData(0x20CF, BidiClass.ET)]
    [InlineData(0xFFFF, BidiClass.BN)]
    [InlineData(0xFDD0, BidiClass.BN)]
    [InlineData(0x1E900, BidiClass.R)]
    [InlineData(0x1EE01, BidiClass.AL)]
    [InlineData(0xE000, BidiClass.L)]
    [InlineData(0x3400, BidiClass.L)]
    internal void Character_classes_match_the_unicode_bidi_class(int codePoint, BidiClass expected)
    {
        Assert.Equal(expected, BidiClasses.Of(codePoint));
    }

    [Fact]
    public void Brackets_and_mirrors_pair_up()
    {
        Assert.Equal(')', BidiBrackets.Mirror('('));
        Assert.Equal(0x00AB, BidiBrackets.Mirror(0x00BB));
        Assert.Equal(0x2265, BidiBrackets.Mirror(0x2264));
        Assert.Equal('a', BidiBrackets.Mirror('a'));
        Assert.Equal(0x3009, BidiBrackets.ClosingOf(0x2329));
        Assert.True(BidiBrackets.IsClosing(0x232A));
        Assert.Null(BidiBrackets.ClosingOf(')'));
        // Canonically equivalent angle brackets pair with each other (BD16): paired, the closing one is R like the
        // opening one; unpaired, it would be left to the embedding direction.
        Assert.Equal("1111111000", Levels("AB \u2329CD\u3009 ab", 0));
    }

    [Fact]
    public void A_surrogate_pair_takes_the_class_of_its_code_point_and_reordering_is_detected()
    {
        var text = char.ConvertFromUtf32(0x10800) + "a";

        Assert.Equal([BidiClass.R, BidiClass.R, BidiClass.L], BidiAlgorithm.Classify(text));
        Assert.True(BidiAlgorithm.NeedsReordering(Hebrew("ab C")));
        Assert.True(BidiAlgorithm.NeedsReordering("x" + Rli + "y"));
        Assert.False(BidiAlgorithm.NeedsReordering("plain text, 12 %"));
        Assert.Empty(BidiAlgorithm.VisualOrder([]));
    }

    [Fact]
    public void Right_to_left_runs_make_their_neutrals_strong()
    {
        // "(" in a right-to-left run is taken as R: between Latin letters it no longer resolves to L.
        const string Text = "a(b";
        Assert.Equal([0, 0, 0], BidiAlgorithm.Levels(Text, 0));
        Assert.Equal([0, 1, 0], BidiAlgorithm.Levels(Text, 0, i => i == 1));
    }

    private static string Hebrew(string pseudo) => string.Concat(pseudo.Select(c => c switch
    {
        >= 'A' and <= 'Z' => ((char)(0x05D0 + (c - 'A'))).ToString(),
        '~' => "\u0628",
        _ => c.ToString(),
    }));

    private static string Levels(string pseudo, byte paragraph) =>
        string.Concat(BidiAlgorithm.Levels(Hebrew(pseudo), paragraph).Select(l => l.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string Visual(string pseudo, byte paragraph)
    {
        var text = Hebrew(pseudo);
        var levels = BidiAlgorithm.LineLevels(text, BidiAlgorithm.Levels(text, paragraph), 0, text.Length, paragraph);
        return string.Concat(BidiAlgorithm.VisualOrder(levels).Select(i => pseudo[i]));
    }
}
