// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering.Fonts;

/// <summary>
/// A Type 1 font program: the clear-text part (font matrix, encoding) and the eexec-encrypted private part
/// (subroutines and charstrings, each encrypted again), decrypted and read into glyph programs by name.
/// </summary>
internal sealed class Type1Font
{
    private const ushort EexecKey = 55665;
    private const ushort CharStringKey = 4330;
    private const int MaxSubroutine = 0xFFFF;
    private static readonly Regex MatrixPattern = new(@"/FontMatrix\s*\[([^\]]*)\]", RegexOptions.CultureInvariant);
    private static readonly Regex EncodingPattern = new(@"dup\s+(\d+)\s*/([^\s/\[\]{}()<>%]+)\s+put", RegexOptions.CultureInvariant);

    private Type1Font(Matrix matrix, Dictionary<int, string> encoding, List<byte[]> subrs, Dictionary<string, byte[]> charStrings)
    {
        FontMatrix = matrix;
        Encoding = encoding;
        Subrs = subrs;
        CharStrings = charStrings;
    }

    public Matrix FontMatrix { get; }

    /// <summary>The font's own encoding (code to glyph name), empty for StandardEncoding.</summary>
    public Dictionary<int, string> Encoding { get; }

    public List<byte[]> Subrs { get; }

    public Dictionary<string, byte[]> CharStrings { get; }

    public static Type1Font Parse(byte[] program, int length1, int length2)
    {
        var (clear, encrypted) = Split(program, length1, length2);
        var header = System.Text.Encoding.Latin1.GetString(clear);
        var matrix = MatrixPattern.Match(header) is { Success: true } m && Numbers(m.Groups[1].Value) is { Length: 6 } v
            ? new Matrix(v[0], v[1], v[2], v[3], v[4], v[5])
            : new Matrix(0.001, 0, 0, 0.001, 0, 0);
        var encoding = new Dictionary<int, string>();
        foreach (Match match in EncodingPattern.Matches(header))
        {
            encoding[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)] = match.Groups[2].Value;
        }

        var plain = Decrypt(IsHex(encrypted) ? FromHex(encrypted) : encrypted, EexecKey, 4);
        var lenIV = LenIV(plain);
        var (subrs, charStrings) = ReadPrivate(plain, lenIV);
        return new Type1Font(matrix, encoding, subrs, charStrings);
    }

    // The clear part ends with "eexec"; segment lengths come from the stream (or a PFB header).
    private static (byte[] Clear, byte[] Encrypted) Split(byte[] program, int length1, int length2)
    {
        if (program.Length > 6 && program[0] == 0x80 && program[1] == 1)
        {
            var first = BitConverter.ToInt32(program, 2);
            var second = program.Length > first + 12 ? BitConverter.ToInt32(program, first + 8) : program.Length - first - 12;
            return (program[6..(6 + first)], program[(first + 12)..Math.Min(program.Length, first + 12 + second)]);
        }

        if (length1 <= 0 || length1 >= program.Length)
        {
            var text = System.Text.Encoding.Latin1.GetString(program);
            var at = text.IndexOf("eexec", StringComparison.Ordinal);
            length1 = at < 0 ? program.Length : at + 5;
            while (length1 < program.Length && program[length1] is (byte)'\r' or (byte)'\n' or (byte)' ' or (byte)'\t')
            {
                length1++;
            }
        }

        var end = length2 > 0 ? Math.Min(program.Length, length1 + length2) : program.Length;
        return (program[..length1], program[length1..end]);
    }

    private static bool IsHex(byte[] data) => data.Length >= 4 && data.Take(4).All(b => char.IsAsciiHexDigit((char)b));

    private static byte[] FromHex(byte[] data)
    {
        var digits = data.Where(b => char.IsAsciiHexDigit((char)b)).ToArray();
        var result = new byte[digits.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = byte.Parse(System.Text.Encoding.ASCII.GetString(digits, 2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return result;
    }

    /// <summary>Type 1 decryption (eexec and charstring encryption), dropping the first <paramref name="skip"/> bytes.</summary>
    public static byte[] Decrypt(ReadOnlySpan<byte> cipher, ushort key, int skip)
    {
        var r = key;
        var plain = new byte[cipher.Length];
        for (var i = 0; i < cipher.Length; i++)
        {
            plain[i] = (byte)(cipher[i] ^ (r >> 8));
            r = (ushort)(((cipher[i] + r) * 52845) + 22719);
        }

        return skip >= plain.Length ? [] : plain[skip..];
    }

    private static int LenIV(byte[] plain)
    {
        var text = System.Text.Encoding.Latin1.GetString(plain, 0, Math.Min(plain.Length, 4096));
        var at = text.IndexOf("/lenIV", StringComparison.Ordinal);
        return at >= 0 && int.TryParse(new string(text[(at + 6)..].SkipWhile(char.IsWhiteSpace).TakeWhile(c => char.IsAsciiDigit(c) || c == '-').ToArray()), out var value) ? value : 4;
    }

    // "dup i n RD <n bytes> NP" subroutines and "/name n RD <n bytes> ND" charstrings (RD may be "-|").
    private static (List<byte[]> Subrs, Dictionary<string, byte[]> CharStrings) ReadPrivate(byte[] plain, int lenIV)
    {
        var subrs = new List<byte[]>();
        var charStrings = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var text = System.Text.Encoding.Latin1.GetString(plain);
        var charStart = text.IndexOf("/CharStrings", StringComparison.Ordinal);
        var subrStart = text.IndexOf("/Subrs", StringComparison.Ordinal);
        if (subrStart >= 0)
        {
            ReadBinaries(plain, text, subrStart, charStart < 0 ? text.Length : charStart, @"dup\s+(\d+)\s+(\d+)\s+(RD|-\|)\s", (key, data) =>
            {
                // Subroutine numbers past 65,535 are not real programs: such an entry would grow the table by
                // billions of slots, so it is skipped.
                if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index > MaxSubroutine)
                {
                    return;
                }

                while (subrs.Count <= index)
                {
                    subrs.Add([]);
                }

                subrs[index] = Charstring(data, lenIV);
            });
        }

        if (charStart >= 0)
        {
            ReadBinaries(plain, text, charStart, text.Length, @"/([^\s/\[\]{}()<>%]+)\s+(\d+)\s+(RD|-\|)\s", (key, data) => charStrings[key] = Charstring(data, lenIV));
        }

        return (subrs, charStrings);
    }

    private static void ReadBinaries(byte[] plain, string text, int start, int end, string pattern, Action<string, byte[]> add)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        var position = start;
        while (position < end && regex.Match(text, position) is { Success: true } match && match.Index < end)
        {
            var length = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var dataStart = match.Index + match.Length;
            if (length < 0 || dataStart + length > plain.Length)
            {
                break;
            }

            add(match.Groups[1].Value, plain[dataStart..(dataStart + length)]);
            position = dataStart + length;
        }
    }

    private static byte[] Charstring(byte[] data, int lenIV) => lenIV < 0 ? data : Decrypt(data, CharStringKey, lenIV);

    private static double[] Numbers(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).Where(double.IsFinite).ToArray();

    /// <summary>The outline of a glyph by name, in font units.</summary>
    public GlyphShape? Outline(string name)
    {
        if (!CharStrings.TryGetValue(name, out var program))
        {
            return null;
        }

        var commands = new Type1CharString(this).Run(program);
        return new GlyphShape(commands).Transform(FontMatrix);
    }
}
