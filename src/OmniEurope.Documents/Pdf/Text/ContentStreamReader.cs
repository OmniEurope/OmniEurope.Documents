// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>An operator of a content stream with its operands; inline images carry their dictionary and data.</summary>
internal readonly record struct ContentOperation(string Operator, IReadOnlyList<PdfObject> Operands, byte[]? InlineData = null);

/// <summary>An affine matrix [a b c d e f] (ISO 32000-1 §8.3.3).</summary>
internal readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
{
    public static Matrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>This matrix then <paramref name="other"/> (row-vector convention: p × this × other).</summary>
    public Matrix Multiply(Matrix other) => new(
        (A * other.A) + (B * other.C),
        (A * other.B) + (B * other.D),
        (C * other.A) + (D * other.C),
        (C * other.B) + (D * other.D),
        (E * other.A) + (F * other.C) + other.E,
        (E * other.B) + (F * other.D) + other.F);

    public (double X, double Y) Transform(double x, double y) => ((x * A) + (y * C) + E, (x * B) + (y * D) + F);

    public static Matrix FromOperands(IReadOnlyList<PdfObject> operands, int start = 0) =>
        operands.Count >= start + 6
            ? new Matrix(Value(operands[start]), Value(operands[start + 1]), Value(operands[start + 2]), Value(operands[start + 3]), Value(operands[start + 4]), Value(operands[start + 5]))
            : Identity;

    public static Matrix FromArray(PdfArray? array) => array is { Count: 6 } ? FromOperands(array.Items) : Identity;

    private static double Value(PdfObject value) => value is PdfNumber n ? n.Value : 0;
}

/// <summary>Reads the operations of a content stream; never throws on malformed content.</summary>
internal static class ContentStreamReader
{
    public static IEnumerable<ContentOperation> Read(byte[] content)
    {
        var lexer = new PdfLexer(content);
        var parser = new PdfParser(lexer);
        var operands = new List<PdfObject>();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.End)
            {
                yield break;
            }

            if (token.Kind == PdfTokenKind.Keyword && token.Text is not ("true" or "false" or "null"))
            {
                if (token.Text == "BI")
                {
                    yield return ReadInlineImage(lexer, parser);
                }
                else
                {
                    yield return new ContentOperation(token.Text, operands.ToArray());
                }

                operands.Clear();
                continue;
            }

            if (token.Kind is PdfTokenKind.ArrayEnd or PdfTokenKind.DictionaryEnd)
            {
                continue;
            }

            if (parser.ReadObjectFrom(token) is { } operand)
            {
                operands.Add(operand);
                if (operands.Count > 64)
                {
                    operands.RemoveAt(0);
                }
            }
        }
    }

    private static ContentOperation ReadInlineImage(PdfLexer lexer, PdfParser parser)
    {
        var dictionary = new PdfDictionary();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.End || token.Text == "ID")
            {
                break;
            }

            if (token.Kind == PdfTokenKind.Name && parser.ReadObject() is { } value)
            {
                dictionary.Set(((PdfName)token.Value!).Value, value);
            }
        }

        var data = lexer.Data;
        var start = (int)Math.Min(lexer.Position + 1, data.Length);
        var end = start;
        while (end + 2 <= data.Length)
        {
            if (data[end] == 'E' && data[end + 1] == 'I' && end > start && PdfLexer.IsWhiteSpace(data[end - 1])
                && (end + 2 == data.Length || PdfLexer.IsWhiteSpace(data[end + 2]) || PdfLexer.IsDelimiter(data[end + 2])))
            {
                break;
            }

            end++;
        }

        var bytes = data.AsSpan(start, Math.Max(0, end - start - 1)).ToArray();
        lexer.Position = Math.Min(end + 2, data.Length);
        return new ContentOperation("BI", [dictionary], bytes);
    }
}
