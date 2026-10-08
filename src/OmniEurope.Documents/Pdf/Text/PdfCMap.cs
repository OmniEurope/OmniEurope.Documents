// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// A CMap: how a byte string splits into codes (code space ranges) and what each code means, either a CID
/// (encoding CMaps) or Unicode text (ToUnicode CMaps, bfchar and bfrange). The predefined Identity-H/V
/// CMaps are two-byte identities.
/// </summary>
internal sealed class PdfCMap
{
    private readonly List<(int Length, uint Low, uint High)> _codeSpaces = [];
    private readonly Dictionary<uint, string> _unicode = [];
    private readonly List<(uint Low, uint High, int Cid)> _cidRanges = [];

    public static PdfCMap Identity { get; } = CreateIdentity();

    public static PdfCMap Parse(byte[] data)
    {
        var cmap = new PdfCMap();
        var lexer = new PdfLexer(data);
        var operands = new List<PdfToken>();
        var guard = 0;
        while (guard++ < 5_000_000)
        {
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.End)
            {
                break;
            }

            if (token.Kind == PdfTokenKind.Keyword)
            {
                cmap.Apply(token.Text, lexer, operands);
                operands.Clear();
                continue;
            }

            operands.Add(token);
            if (operands.Count > 8)
            {
                operands.RemoveAt(0);
            }
        }

        return cmap;
    }

    /// <summary>Splits <paramref name="bytes"/> into codes using the code space ranges (one byte when none).</summary>
    public IEnumerable<(uint Code, int Length)> Codes(byte[] bytes)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            var length = MatchLength(bytes, i);
            uint code = 0;
            for (var k = 0; k < length && i + k < bytes.Length; k++)
            {
                code = (code << 8) | bytes[i + k];
            }

            yield return (code, length);
            i += length;
        }
    }

    public string? ToUnicode(uint code) => _unicode.TryGetValue(code, out var text) ? text : null;

    public int ToCid(uint code)
    {
        foreach (var (low, high, cid) in _cidRanges)
        {
            if (code >= low && code <= high)
            {
                return cid + (int)(code - low);
            }
        }

        return (int)code;
    }

    private int MatchLength(byte[] bytes, int position)
    {
        if (_codeSpaces.Count == 0)
        {
            return 1;
        }

        for (var length = 1; length <= 4; length++)
        {
            if (position + length > bytes.Length)
            {
                break;
            }

            uint code = 0;
            for (var k = 0; k < length; k++)
            {
                code = (code << 8) | bytes[position + k];
            }

            if (_codeSpaces.Exists(s => s.Length == length && code >= s.Low && code <= s.High))
            {
                return length;
            }
        }

        return _codeSpaces.Min(s => s.Length);
    }

    private void Apply(string keyword, PdfLexer lexer, List<PdfToken> operands)
    {
        switch (keyword)
        {
            case "begincodespacerange":
                // A code is one to four bytes (ISO 32000-1 §9.7.6.2): an empty range would never advance along the text.
                ReadPairs(lexer, "endcodespacerange", (low, high, _) =>
                {
                    if (low.Length is >= 1 and <= 4)
                    {
                        _codeSpaces.Add((low.Length, Code(low), Code(high)));
                    }
                }, 2);
                break;
            case "beginbfchar":
                ReadPairs(lexer, "endbfchar", (source, target, _) => _unicode[Code(source)] = Utf16(target), 2);
                break;
            case "beginbfrange":
                ReadRanges(lexer);
                break;
            case "begincidchar":
                ReadPairs(lexer, "endcidchar", (source, _, cid) => _cidRanges.Add((Code(source), Code(source), cid)), 2);
                break;
            case "begincidrange":
                ReadPairs(lexer, "endcidrange", (low, high, cid) => _cidRanges.Add((Code(low), Code(high), cid)), 3);
                break;
            case "usecmap" when operands.Count > 0 && operands[^1].Value is PdfName { Value: var name } && name.StartsWith("Identity", StringComparison.Ordinal):
                _codeSpaces.Add((2, 0, 0xFFFF));
                break;
        }
    }

    // Reads groups of 2 (code, value) or 3 (low, high, cid) operands until the end keyword.
    private static void ReadPairs(PdfLexer lexer, string end, Action<byte[], byte[], int> add, int arity)
    {
        var group = new List<PdfToken>(arity);
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.End || token.Text == end)
            {
                return;
            }

            group.Add(token);
            if (group.Count < arity)
            {
                continue;
            }

            var first = Bytes(group[0]);
            var second = Bytes(group[1]);
            var number = group[^1].Value is PdfNumber n ? n.IntValue : 0;
            if (first is not null && (second is not null || arity == 2 && group[1].Value is PdfNumber))
            {
                add(first, second ?? [], number);
            }

            group.Clear();
        }
    }

    private void ReadRanges(PdfLexer lexer)
    {
        while (true)
        {
            var low = lexer.Next();
            if (low.Kind == PdfTokenKind.End || low.Text == "endbfrange")
            {
                return;
            }

            var high = lexer.Next();
            var target = lexer.Next();
            if (Bytes(low) is not { } lowBytes || Bytes(high) is not { } highBytes)
            {
                continue;
            }

            var from = Code(lowBytes);
            var to = Math.Min(Code(highBytes), from + 0xFFFF);
            if (target.Kind == PdfTokenKind.ArrayStart)
            {
                ReadRangeArray(lexer, from, to);
            }
            else if (Bytes(target) is { Length: > 0 } start)
            {
                for (var code = from; code <= to; code++)
                {
                    _unicode[code] = Utf16(Increment(start, code - from));
                }
            }
        }
    }

    // <low> <high> [<text> <text> ...]: one destination string per code.
    private void ReadRangeArray(PdfLexer lexer, uint from, uint to)
    {
        for (var code = from; ; code++)
        {
            var item = lexer.Next();
            if (item.Kind is PdfTokenKind.ArrayEnd or PdfTokenKind.End)
            {
                return;
            }

            if (Bytes(item) is { } text && code <= to)
            {
                _unicode[code] = Utf16(text);
            }
        }
    }

    // <low> <high> <start>: the destination bytes plus the code's offset in the range, carried big-endian.
    private static byte[] Increment(byte[] start, uint add)
    {
        var value = (byte[])start.Clone();
        for (var k = value.Length - 1; k >= 0 && add > 0; k--)
        {
            var sum = value[k] + (add & 0xFF);
            value[k] = (byte)sum;
            add = (add >> 8) + (sum >> 8);
        }

        return value;
    }

    private static byte[]? Bytes(PdfToken token) => token.Value is PdfString s ? s.Bytes : null;

    private static uint Code(byte[] bytes)
    {
        uint code = 0;
        foreach (var b in bytes.Take(4))
        {
            code = (code << 8) | b;
        }

        return code;
    }

    private static string Utf16(byte[] bytes) =>
        bytes.Length == 1 ? ((char)bytes[0]).ToString() : Encoding.BigEndianUnicode.GetString(bytes, 0, bytes.Length & ~1);

    private static PdfCMap CreateIdentity()
    {
        var cmap = new PdfCMap();
        cmap._codeSpaces.Add((2, 0, 0xFFFF));
        return cmap;
    }
}
