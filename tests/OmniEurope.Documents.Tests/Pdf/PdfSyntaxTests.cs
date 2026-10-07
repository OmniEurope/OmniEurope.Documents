// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// PDF syntax (ISO 32000-1 §7.2 and §7.3) read token by token and object by object, including the malformed
/// input real files contain: stray delimiters, doubled signs, odd hexadecimal digits, missing keywords.
/// </summary>
public sealed class PdfSyntaxTests
{
    [Theory]
    [InlineData("--5", -5.0)]
    [InlineData("+.5", 0.5)]
    [InlineData("5.", 5.0)]
    public void Numbers_tolerate_producer_quirks(string text, double value)
    {
        var token = Lexer(text).Next();

        Assert.Equal((PdfTokenKind.Number, value), (token.Kind, ((PdfNumber)token.Value!).Value));
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData(") x", ")")]
    [InlineData("} x", "}")]
    public void Runs_that_are_not_numbers_and_lone_delimiters_are_keywords(string text, string keyword)
    {
        var lexer = Lexer(text);

        var token = lexer.Next();

        Assert.Equal((PdfTokenKind.Keyword, keyword), (token.Kind, token.Text));
        Assert.True(lexer.Position > 0);
    }

    [Theory]
    [InlineData("/A#20B", "A B")]
    [InlineData("/A#2", "A#2")]
    [InlineData("/caf#C3#A9", "café")]
    public void Names_decode_hexadecimal_escapes(string text, string name)
    {
        Assert.Equal(name, ((PdfName)Lexer(text).Next().Value!).Value);
    }

    [Theory]
    [InlineData("<48 65x6C6C6F7>", "48656C6C6F70")]
    [InlineData("(ab\\", "6162")]
    [InlineData("(a\r\nb)", "610A62")]
    public void Strings_skip_bad_digits_pad_odd_ones_and_stop_at_the_end(string text, string hex)
    {
        Assert.Equal(Convert.FromHexString(hex), ((PdfString)Lexer(text).Next().Value!).Bytes);
    }

    [Fact]
    public void Arrays_and_dictionaries_drop_what_is_not_an_object()
    {
        // A stray ">>" in the array, a number in key position, a keyword as a value, then a key with no value.
        var array = (PdfArray)Parse("[1 >> null foo 2]")!;
        var dictionary = (PdfDictionary)Parse("<< 5 /A foo /B 1 /C null /D >>")!;

        Assert.Equal(["1", "null", "2"], array.Items.Select(i => i is PdfNumber n ? n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null"));
        Assert.Equal(["B"], dictionary.Entries.Select(e => e.Key));
        Assert.Null(Parse(string.Empty));
    }

    [Fact]
    public void Nesting_is_bounded()
    {
        Assert.Throws<InvalidDataException>(() => Parse(new string('[', 250)));
    }

    [Fact]
    public void An_object_without_endobj_leaves_the_next_token_unread()
    {
        var parser = new PdfParser(Lexer("7 0 obj 42 8 0 obj"));

        var (number, _, value) = parser.ReadIndirect()!.Value;

        Assert.Equal((7, 42.0), (number, ((PdfNumber)value).Value));
        Assert.Equal(8, parser.ReadIndirect()!.Value.Number);
    }

    [Theory]
    [InlineData("<< /Length 5 >> stream\r\nHello\nendstream\nendobj", "Hello")]
    [InlineData("<< /Length 3 0 R >> stream\nHello\nendstream 9 0 obj", "Hello")]
    [InlineData("<< /Length /Five >> stream\nHello\r\nendstream", "Hello")]
    [InlineData("<< /Length 99 >> stream\nHello", "Hello")]
    public void Stream_lengths_are_checked_against_endstream(string body, string data)
    {
        // Length 3 0 R resolves to 5; a wrong, unresolvable or missing Length falls back to endstream or the end.
        var parser = new PdfParser(Lexer("1 0 obj " + body), reference => reference.Number == 3 ? 5 : null);

        var stream = (PdfStream)parser.ReadIndirect()!.Value.Value;

        Assert.Equal(Encoding.ASCII.GetBytes(data), stream.Data);
    }

    private static PdfLexer Lexer(string text) => new(Encoding.Latin1.GetBytes(text));

    private static PdfObject? Parse(string text) => new PdfParser(Lexer(text)).ReadObject();
}
