// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Minimal font programs written from the CFF and Type 1 specifications: the glyph is a square from (100, 0)
/// to (600, 600) in a 1000-unit em, named "A" (or CID 5 in a CID-keyed font).
/// </summary>
internal static class FontPrograms
{
    // Type 2 charstring: 100 0 rmoveto, 500 0 rlineto, 0 600 rlineto, -500 0 rlineto, endchar.
    private static readonly byte[] Type2Square = [239, 139, 21, 248, 136, 139, 5, 139, 248, 236, 5, 252, 136, 139, 5, 14];

    // Type 1 charstring: 0 700 hsbw, 100 0 rmoveto, 500 0 rlineto, 0 600 rlineto, -500 0 rlineto, closepath, 0 callsubr, endchar.
    private static readonly byte[] Type1Square = [139, 249, 80, 13, 239, 139, 21, 248, 136, 139, 5, 139, 248, 236, 5, 252, 136, 139, 5, 9, 139, 10, 14];

    // 0.001 as a CFF real number: nibbles 0 . 0 0 1 end.
    private static readonly byte[] Thousandth = [30, 0x0A, 0x00, 0x1F];

    /// <summary>A bare CFF font with ".notdef" and the square.</summary>
    /// <param name="charsetFormat">0, 1 or 2.</param>
    /// <param name="encodingFormat">-1 for none, else 0 or 1 (code 66 draws the square).</param>
    /// <param name="realMatrix">Writes the font matrix with real numbers.</param>
    /// <param name="fdSelectFormat">-1 for a name-keyed font, else 0 or 3 for a CID-keyed one (the square is CID 5).</param>
    public static byte[] Cff(int charsetFormat = 0, int encodingFormat = -1, bool realMatrix = false, int fdSelectFormat = -1)
    {
        var cid = fdSelectFormat >= 0;
        var identifier = cid ? 5 : 34;
        byte[] header = [1, 0, 4, 1];
        var name = Index(Encoding.ASCII.GetBytes("Test"));
        byte[] empty = [0, 0];
        var charset = charsetFormat switch
        {
            1 => new byte[] { 1, 0, (byte)identifier, 0 },
            2 => [2, 0, (byte)identifier, 0, 0],
            _ => [0, 0, (byte)identifier],
        };
        var encoding = encodingFormat switch
        {
            0 => new byte[] { 0, 1, 66 },
            1 => [1, 1, 66, 0],
            _ => [],
        };
        var charStrings = Index([14], Type2Square);
        var fontDicts = cid ? Index(Private(0, 0)) : [];
        byte[] fdSelect = fdSelectFormat == 3 ? [3, 0, 1, 0, 0, 0, 0, 2] : cid ? [0, 0, 0] : [];
        var topLength = Top(0, 0, 0, 0, 0, 0, encodingFormat, realMatrix, cid).Length;
        var charsetAt = header.Length + name.Length + 2 + 1 + 2 + topLength + empty.Length + empty.Length;
        var encodingAt = charsetAt + charset.Length;
        var charStringsAt = encodingAt + encoding.Length;
        var fontDictsAt = charStringsAt + charStrings.Length;
        var fdSelectAt = fontDictsAt + fontDicts.Length;
        var privateAt = fdSelectAt + fdSelect.Length;
        var top = Top(charsetAt, encodingAt, charStringsAt, privateAt, fontDictsAt, fdSelectAt, encodingFormat, realMatrix, cid);
        if (cid)
        {
            fontDicts = Index(Private(0, privateAt));
        }

        return [.. header, .. name, .. Index(top), .. empty, .. empty, .. charset, .. encoding, .. charStrings, .. fontDicts, .. fdSelect];
    }

    private static byte[] Top(int charsetAt, int encodingAt, int charStringsAt, int privateAt, int fontDictsAt, int fdSelectAt, int encodingFormat, bool realMatrix, bool cid)
    {
        var top = new List<byte>();
        if (cid)
        {
            top.AddRange([139, 139, 139, 12, 30]);
        }

        if (realMatrix)
        {
            top.AddRange([.. Thousandth, 139, 139, .. Thousandth, 139, 139, 12, 7]);
        }

        top.AddRange([.. Int32(charsetAt), 15]);
        if (encodingFormat >= 0)
        {
            top.AddRange([.. Int32(encodingAt), 16]);
        }

        top.AddRange([.. Int32(charStringsAt), 17]);
        if (cid)
        {
            top.AddRange([.. Int32(fontDictsAt), 12, 36, .. Int32(fdSelectAt), 12, 37]);
        }
        else
        {
            top.AddRange(Private(0, privateAt));
        }

        return [.. top];
    }

    private static byte[] Private(int size, int offset) => [.. Int32(size), .. Int32(offset), 18];

    /// <summary>The CFF program inside an OpenType wrapper (a single 'CFF ' table).</summary>
    public static byte[] OpenType(byte[] cff)
    {
        var header = new byte[28];
        Encoding.ASCII.GetBytes("OTTO").CopyTo(header, 0);
        header[5] = 1;
        Encoding.ASCII.GetBytes("CFF ").CopyTo(header, 12);
        header[23] = 28;
        header[24] = (byte)(cff.Length >> 24);
        header[25] = (byte)(cff.Length >> 16);
        header[26] = (byte)(cff.Length >> 8);
        header[27] = (byte)cff.Length;
        return [.. header, .. cff];
    }

    /// <summary>A Type 1 font program and its clear and encrypted lengths.</summary>
    public static (byte[] Program, int Length1, int Length2) Type1() =>
        Type1([[11]], [(".notdef", [139, 248, 136, 13, 14]), ("A", Type1Square)]);

    /// <summary>A Type 1 font program with the given subroutines and glyphs.</summary>
    /// <param name="subrs">The subroutines, by index.</param>
    /// <param name="glyphs">The charstrings, by glyph name.</param>
    /// <param name="lenIV">4, or -1 for charstrings stored without encryption.</param>
    /// <param name="packaging">"binary" (lengths given), "hex" (hexadecimal eexec part, no lengths) or "pfb" (segment headers).</param>
    /// <param name="numbers">The number written before each subroutine, its index when null.</param>
    public static (byte[] Program, int Length1, int Length2) Type1(byte[][] subrs, (string Name, byte[] Program)[] glyphs, int lenIV = 4, string packaging = "binary", IReadOnlyList<long>? numbers = null)
    {
        var clear = Encoding.ASCII.GetBytes("%!PS-AdobeFont-1.0: Test 001\n/FontMatrix [0.001 0 0 0.001 0 0] readonly def\n/Encoding StandardEncoding def\ncurrentfile eexec\n");
        byte[] Protect(byte[] program) => lenIV < 0 ? program : Encrypt(program, 4330);
        var text = new List<byte>();
        text.AddRange(Encoding.ASCII.GetBytes("dup /Private 8 dict dup begin\n/lenIV " + lenIV + " def\n/Subrs " + subrs.Length + " array\n"));
        for (var i = 0; i < subrs.Length; i++)
        {
            var subr = Protect(subrs[i]);
            text.AddRange(Encoding.ASCII.GetBytes("dup " + (numbers?[i] ?? i) + " " + subr.Length + " RD "));
            text.AddRange(subr);
            text.AddRange(Encoding.ASCII.GetBytes(" NP\n"));
        }

        text.AddRange(Encoding.ASCII.GetBytes("noaccess def\n2 index /CharStrings " + glyphs.Length + " dict dup begin\n"));
        foreach (var (name, program) in glyphs)
        {
            var glyph = Protect(program);
            text.AddRange(Encoding.ASCII.GetBytes("/" + name + " " + glyph.Length + " RD "));
            text.AddRange(glyph);
            text.AddRange(Encoding.ASCII.GetBytes(" ND\n"));
        }

        text.AddRange(Encoding.ASCII.GetBytes("end\nend\n"));
        var encrypted = Encrypt([.. text], 55665);
        return packaging switch
        {
            "hex" => ([.. clear, .. Encoding.ASCII.GetBytes(string.Join("\n", Convert.ToHexString(encrypted).Chunk(64).Select(c => new string(c))))], 0, 0),
            "pfb" => ([.. Segment(1, clear), .. Segment(2, encrypted), 0x80, 3], 0, 0),
            _ => ([.. clear, .. encrypted], clear.Length, encrypted.Length),
        };
    }

    // A PFB segment: 0x80, its type, its length (little-endian), then its bytes.
    private static byte[] Segment(byte type, byte[] data) =>
        [0x80, type, (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16), (byte)(data.Length >> 24), .. data];

    // Type 1 encryption: four leading bytes, each cipher byte feeding the key (Adobe Type 1 Font Format, chapter 7).
    private static byte[] Encrypt(byte[] plain, ushort key)
    {
        var r = key;
        var input = new byte[] { 1, 2, 3, 4 }.Concat(plain).ToArray();
        var cipher = new byte[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            cipher[i] = (byte)(input[i] ^ (r >> 8));
            r = (ushort)(((cipher[i] + r) * 52845) + 22719);
        }

        return cipher;
    }

    private static byte[] Index(params byte[][] items)
    {
        var result = new List<byte> { 0, (byte)items.Length, 1, 1 };
        var offset = 1;
        foreach (var item in items)
        {
            offset += item.Length;
            result.Add((byte)offset);
        }

        foreach (var item in items)
        {
            result.AddRange(item);
        }

        return [.. result];
    }

    private static byte[] Int32(int value) => [29, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
