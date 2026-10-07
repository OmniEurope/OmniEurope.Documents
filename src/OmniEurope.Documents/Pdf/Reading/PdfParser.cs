// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>
/// Builds objects from tokens: arrays, dictionaries, indirect references (<c>n g R</c>), indirect object
/// definitions (<c>n g obj ... endobj</c>) and streams. Nesting is bounded so hostile files cannot exhaust
/// the stack.
/// </summary>
internal sealed class PdfParser(PdfLexer lexer, Func<PdfReference, int?>? resolveLength = null)
{
    private const int MaxDepth = 200;

    /// <summary>Reads the object that starts with an already read token.</summary>
    public PdfObject? ReadObjectFrom(PdfToken token) => ReadObject(token, 0);

    /// <summary>Reads one object; returns null at the end of input or at an unexpected token.</summary>
    public PdfObject? ReadObject() => ReadObject(lexer.Next(), 0);

    /// <summary>Reads <c>n g obj</c> at the current position and the object it defines.</summary>
    public (int Number, int Generation, PdfObject Value)? ReadIndirect()
    {
        var number = lexer.Next();
        var generation = lexer.Next();
        var keyword = lexer.Next();
        if (number.Kind != PdfTokenKind.Number || generation.Kind != PdfTokenKind.Number || keyword.Text != "obj")
        {
            return null;
        }

        var value = ReadObject() ?? PdfNull.Instance;
        var save = lexer.Position;
        var next = lexer.Next();
        if (next.Text == "stream" && value is PdfDictionary dictionary)
        {
            value = ReadStream(dictionary);
        }
        else if (next.Text != "endobj")
        {
            lexer.Position = save;
        }

        return (((PdfNumber)number.Value!).IntValue, ((PdfNumber)generation.Value!).IntValue, value);
    }

    private PdfObject? ReadObject(PdfToken token, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidDataException("PDF objects are nested too deeply.");
        }

        switch (token.Kind)
        {
            case PdfTokenKind.Number:
                return ReadNumberOrReference((PdfNumber)token.Value!);
            case PdfTokenKind.String:
            case PdfTokenKind.Name:
                return token.Value;
            case PdfTokenKind.ArrayStart:
                return ReadArray(depth);
            case PdfTokenKind.DictionaryStart:
                return ReadDictionary(depth);
            case PdfTokenKind.Keyword:
                return token.Text switch
                {
                    "true" => PdfBoolean.True,
                    "false" => PdfBoolean.False,
                    "null" => PdfNull.Instance,
                    _ => null,
                };
            default:
                return null;
        }
    }

    // "12 0 R" is a reference; anything else leaves the lexer just after the first number.
    private PdfObject ReadNumberOrReference(PdfNumber first)
    {
        if (!first.IsInteger || first.Value < 0)
        {
            return first;
        }

        var save = lexer.Position;
        var second = lexer.Next();
        if (second.Kind == PdfTokenKind.Number && ((PdfNumber)second.Value!).IsInteger)
        {
            var third = lexer.Next();
            if (third.Kind == PdfTokenKind.Keyword && third.Text == "R")
            {
                return new PdfReference(first.IntValue, ((PdfNumber)second.Value!).IntValue);
            }
        }

        lexer.Position = save;
        return first;
    }

    private PdfArray ReadArray(int depth)
    {
        var array = new PdfArray();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind is PdfTokenKind.ArrayEnd or PdfTokenKind.End)
            {
                return array;
            }

            if (token.Kind == PdfTokenKind.DictionaryEnd)
            {
                continue;
            }

            if (ReadObject(token, depth + 1) is { } item)
            {
                array.Items.Add(item);
            }
        }
    }

    private PdfDictionary ReadDictionary(int depth)
    {
        var dictionary = new PdfDictionary();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind is PdfTokenKind.DictionaryEnd or PdfTokenKind.End)
            {
                return dictionary;
            }

            if (token.Kind != PdfTokenKind.Name)
            {
                // Malformed key: skip it.
                continue;
            }

            var key = ((PdfName)token.Value!).Value;
            var valueToken = lexer.Next();
            if (valueToken.Kind == PdfTokenKind.DictionaryEnd)
            {
                return dictionary;
            }

            // A value that is not an object (a stray keyword) drops its key.
            var value = ReadObject(valueToken, depth + 1);
            if (value is not null and not PdfNull)
            {
                dictionary.Set(key, value);
            }
        }
    }

    private PdfStream ReadStream(PdfDictionary dictionary)
    {
        var data = lexer.Data;
        var start = AfterEndOfLine(data, lexer.Position);
        if (DeclaredLength(dictionary) is not { } declared || declared < 0 || start + declared > data.Length || !EndsAt(data, start + declared))
        {
            declared = FindEndStream(data, start) - (int)start;
        }

        var stream = new PdfStream(data.AsSpan((int)start, declared).ToArray());
        foreach (var (key, value) in dictionary.Entries)
        {
            stream.Set(key, value);
        }

        lexer.Position = start + declared;
        var after = lexer.Next();
        if (after.Text == "endstream")
        {
            var end = lexer.Position;
            if (lexer.Next().Text != "endobj")
            {
                lexer.Position = end;
            }
        }

        return stream;
    }

    // The data starts after the end of line (CR LF or LF) that follows the stream keyword.
    private static long AfterEndOfLine(byte[] data, long start)
    {
        if (start < data.Length && data[start] == 13)
        {
            start++;
        }

        return start < data.Length && data[start] == 10 ? start + 1 : start;
    }

    private int? DeclaredLength(PdfDictionary dictionary) => dictionary["Length"] switch
    {
        PdfNumber n => n.IntValue,
        PdfReference r => resolveLength?.Invoke(r),
        _ => null,
    };

    private static bool EndsAt(byte[] data, long position)
    {
        var lexer = new PdfLexer(data, position);
        lexer.SkipWhiteSpaceAndComments();
        return lexer.Position + 9 <= data.Length && data.AsSpan((int)lexer.Position, 9).SequenceEqual("endstream"u8);
    }

    private static int FindEndStream(byte[] data, long start)
    {
        var index = data.AsSpan((int)start).IndexOf("endstream"u8);
        if (index < 0)
        {
            return data.Length;
        }

        var end = (int)start + index;
        if (end > start && data[end - 1] == '\n')
        {
            end--;
        }

        if (end > start && data[end - 1] == '\r')
        {
            end--;
        }

        return end;
    }
}
