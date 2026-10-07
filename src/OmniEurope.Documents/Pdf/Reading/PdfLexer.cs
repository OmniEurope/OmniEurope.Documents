// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>Kinds of lexical token.</summary>
internal enum PdfTokenKind
{
    End,
    Number,
    String,
    Name,
    Keyword,
    ArrayStart,
    ArrayEnd,
    DictionaryStart,
    DictionaryEnd,
}

/// <summary>A token: its kind, its value (number, string, name or keyword text) and where it started.</summary>
internal readonly record struct PdfToken(PdfTokenKind Kind, PdfObject? Value, string Text, long Position);

/// <summary>
/// Splits PDF syntax into tokens (ISO 32000-1 §7.2): white space and comments skipped, numbers, literal
/// strings with escapes and nested parentheses, hexadecimal strings, names with #xx escapes, delimiters
/// and keywords. Never throws on malformed input: unknown bytes become keywords.
/// </summary>
internal sealed class PdfLexer(byte[] data, long position = 0)
{
    // The single-character escapes of a literal string (ISO 32000-1 table 3).
    private static readonly Dictionary<byte, byte> Escapes = new()
    {
        [(byte)'n'] = (byte)'\n',
        [(byte)'r'] = (byte)'\r',
        [(byte)'t'] = (byte)'\t',
        [(byte)'b'] = 8,
        [(byte)'f'] = 12,
    };

    public long Position { get; set; } = position;

    public byte[] Data => data;

    public static bool IsWhiteSpace(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;

    public static bool IsDelimiter(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    public PdfToken Next()
    {
        SkipWhiteSpaceAndComments();
        var start = Position;
        if (Position >= data.Length)
        {
            return new PdfToken(PdfTokenKind.End, null, string.Empty, start);
        }

        var b = data[Position];
        return Delimited(b, start) ?? (IsNumberStart(b) ? NumberOrKeyword(start) : Keyword(b, start));
    }

    private static bool IsNumberStart(byte b) => b is (byte)'+' or (byte)'-' or (byte)'.' or (>= (byte)'0' and <= (byte)'9');

    // Arrays, dictionaries, strings and names: the tokens a delimiter starts. Null for anything else.
    private PdfToken? Delimited(byte b, long start)
    {
        switch (b)
        {
            case (byte)'[':
                Position++;
                return new PdfToken(PdfTokenKind.ArrayStart, null, "[", start);
            case (byte)']':
                Position++;
                return new PdfToken(PdfTokenKind.ArrayEnd, null, "]", start);
            case (byte)'<' when Peek(1) == '<':
                Position += 2;
                return new PdfToken(PdfTokenKind.DictionaryStart, null, "<<", start);
            case (byte)'>' when Peek(1) == '>':
                Position += 2;
                return new PdfToken(PdfTokenKind.DictionaryEnd, null, ">>", start);
            case (byte)'<':
                return new PdfToken(PdfTokenKind.String, ReadHexString(), string.Empty, start);
            case (byte)'(':
                return new PdfToken(PdfTokenKind.String, ReadLiteralString(), string.Empty, start);
            case (byte)'/':
                return new PdfToken(PdfTokenKind.Name, ReadName(), string.Empty, start);
            default:
                return null;
        }
    }

    // A number, or a keyword when the run does not parse as one (such as "1.2.3").
    private PdfToken NumberOrKeyword(long start)
    {
        var number = ReadRegular();
        return TryParseNumber(number, out var value)
            ? new PdfToken(PdfTokenKind.Number, value, number, start)
            : new PdfToken(PdfTokenKind.Keyword, null, number, start);
    }

    private PdfToken Keyword(byte b, long start)
    {
        var keyword = ReadRegular();
        if (keyword.Length == 0)
        {
            // A lone delimiter such as ')' or '}': consume it so parsing always advances.
            Position++;
            keyword = ((char)b).ToString();
        }

        return new PdfToken(PdfTokenKind.Keyword, null, keyword, start);
    }
    public void SkipWhiteSpaceAndComments()
    {
        while (Position < data.Length)
        {
            var b = data[Position];
            if (IsWhiteSpace(b))
            {
                Position++;
            }
            else if (b == '%')
            {
                while (Position < data.Length && data[Position] is not ((byte)'\r' or (byte)'\n'))
                {
                    Position++;
                }
            }
            else
            {
                break;
            }
        }
    }

    private int Peek(int ahead) => Position + ahead < data.Length ? data[Position + ahead] : -1;

    private string ReadRegular()
    {
        var start = Position;
        while (Position < data.Length && !IsWhiteSpace(data[Position]) && !IsDelimiter(data[Position]))
        {
            Position++;
        }

        return Encoding.Latin1.GetString(data, (int)start, (int)(Position - start));
    }

    private static bool TryParseNumber(string text, out PdfNumber number)
    {
        number = PdfNumber.Of(0);
        if (!text.Contains('.') && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            number = PdfNumber.Of(integer);
            return true;
        }

        // Tolerate producer quirks such as "--5" or "5." or "-.5".
        var cleaned = text.TrimStart('+');
        while (cleaned.StartsWith("--", StringComparison.Ordinal))
        {
            cleaned = cleaned[1..];
        }

        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            number = PdfNumber.Of(real);
            return true;
        }

        return false;
    }

    private PdfName ReadName()
    {
        Position++;
        var bytes = new List<byte>();
        while (Position < data.Length && !IsWhiteSpace(data[Position]) && !IsDelimiter(data[Position]))
        {
            var b = data[Position++];
            if (b == '#' && Position + 1 < data.Length && IsHex(data[Position]) && IsHex(data[Position + 1]))
            {
                bytes.Add((byte)((HexValue(data[Position]) << 4) | HexValue(data[Position + 1])));
                Position += 2;
            }
            else
            {
                bytes.Add(b);
            }
        }

        var array = bytes.ToArray();
        var text = Documents.Text.TextEncodingDetector.IsValidUtf8(array) ? Encoding.UTF8.GetString(array) : Encoding.Latin1.GetString(array);
        return PdfName.Of(text);
    }

    private PdfString ReadHexString()
    {
        Position++;
        var bytes = new List<byte>();
        var high = -1;
        while (Position < data.Length && data[Position] != '>')
        {
            var b = data[Position++];
            if (!IsHex(b))
            {
                continue;
            }

            if (high < 0)
            {
                high = HexValue(b);
            }
            else
            {
                bytes.Add((byte)((high << 4) | HexValue(b)));
                high = -1;
            }
        }

        if (high >= 0)
        {
            bytes.Add((byte)(high << 4));
        }

        Position++;
        return new PdfString(bytes.ToArray(), hex: true);
    }

    private PdfString ReadLiteralString()
    {
        Position++;
        var bytes = new List<byte>();
        var depth = 1;
        while (Position < data.Length)
        {
            var b = data[Position++];
            if (b == '\\')
            {
                ReadEscape(bytes);
                continue;
            }

            if (b == '(')
            {
                depth++;
            }
            else if (b == ')' && --depth == 0)
            {
                break;
            }

            if (b == '\r')
            {
                // An end of line inside a string is a single line feed.
                if (Position < data.Length && data[Position] == '\n')
                {
                    Position++;
                }

                b = (byte)'\n';
            }

            bytes.Add(b);
        }

        return new PdfString(bytes.ToArray());
    }

    private void ReadEscape(List<byte> bytes)
    {
        if (Position >= data.Length)
        {
            return;
        }

        var c = data[Position++];
        if (Escapes.TryGetValue(c, out var escaped))
        {
            bytes.Add(escaped);
        }
        else if (c == '\r')
        {
            // A backslash before an end of line continues the string on the next line.
            SkipLineFeed();
        }
        else if (IsOctal(c))
        {
            bytes.Add(ReadOctal(c));
        }
        else if (c != '\n')
        {
            bytes.Add(c);
        }
    }

    private void SkipLineFeed()
    {
        if (Position < data.Length && data[Position] == '\n')
        {
            Position++;
        }
    }

    // One to three octal digits; the value wraps to a byte as the specification allows.
    private byte ReadOctal(byte first)
    {
        var value = first - '0';
        for (var i = 0; i < 2 && Position < data.Length && IsOctal(data[Position]); i++)
        {
            value = (value * 8) + (data[Position++] - '0');
        }

        return (byte)value;
    }

    private static bool IsOctal(byte b) => b is >= (byte)'0' and <= (byte)'7';
    public static bool IsHex(byte b) => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';

    public static int HexValue(byte b) => b <= '9' ? b - '0' : (b | 0x20) - 'a' + 10;
}
