// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Rendering.Fonts;

/// <summary>
/// A Compact Font Format program (the first font of the set): charstrings, global and local subroutines,
/// charset (glyph names or CIDs), built-in encoding, font matrix, and for CID-keyed fonts the font dictionary
/// array and selector that give each glyph its own private subroutines.
/// </summary>
internal sealed class CffFont
{
    private readonly byte[] _data;
    private readonly List<(int Start, int End)> _charStrings;
    private readonly List<(int Start, int End)> _globalSubrs;
    private readonly List<PrivateData> _privates = [];
    private readonly byte[]? _fdSelect;
    private readonly string[] _strings;

    public CffFont(byte[] data)
    {
        _data = data;
        var position = data.Length > 2 ? data[2] : 4;
        Index(ref position);
        var top = Index(ref position);
        _strings = Index(ref position).Select(s => Encoding.Latin1.GetString(data, s.Start, s.End - s.Start)).ToArray();
        _globalSubrs = Index(ref position);
        if (top.Count == 0)
        {
            throw new InvalidDataException("The CFF font has no top dictionary.");
        }

        var topDict = Dict(top[0].Start, top[0].End);
        _charStrings = IndexAt((int)Operand(topDict, 17));
        IsCid = topDict.ContainsKey(1230);
        FontMatrix = topDict.TryGetValue(1207, out var m) && m.Count == 6 ? new Matrix(m[0], m[1], m[2], m[3], m[4], m[5]) : new Matrix(0.001, 0, 0, 0.001, 0, 0);
        GlyphNames = Charset((int)Operand(topDict, 15));
        BuiltInEncoding = IsCid ? null : EncodingTable((int)Operand(topDict, 16));
        _fdSelect = ReadPrivates(topDict);
    }

    // A CID-keyed font has one private dictionary per font dictionary of its array, chosen by FDSelect.
    private byte[]? ReadPrivates(Dictionary<int, List<double>> topDict)
    {
        if (!IsCid || !topDict.TryGetValue(1236, out var fdArray))
        {
            _privates.Add(Private(topDict));
            return null;
        }

        foreach (var (start, end) in IndexAt((int)fdArray[0]))
        {
            _privates.Add(Private(Dict(start, end)));
        }

        return topDict.ContainsKey(1237) ? FdSelect((int)Operand(topDict, 1237)) : null;
    }

    public bool IsCid { get; }

    public Matrix FontMatrix { get; }

    public int GlyphCount => _charStrings.Count;

    /// <summary>Glyph names by glyph index (for a CID-keyed font: the CIDs as decimal strings).</summary>
    public string[] GlyphNames { get; }

    /// <summary>Glyph names by code, when the font carries its own encoding.</summary>
    public Dictionary<int, string>? BuiltInEncoding { get; }

    public GlyphShape? Outline(int glyph)
    {
        if (glyph < 0 || glyph >= _charStrings.Count)
        {
            return null;
        }

        var local = _privates.Count == 0 ? new PrivateData([], 0, 0) : _privates[_fdSelect is not null && glyph < _fdSelect.Length ? Math.Min(_fdSelect[glyph], _privates.Count - 1) : 0];
        var (start, end) = _charStrings[glyph];
        var commands = new Type2CharString(_data, _globalSubrs, local.Subrs).Run(start, end, local.NominalWidth);
        return new GlyphShape(commands).Transform(FontMatrix);
    }

    private PrivateData Private(Dictionary<int, List<double>> dict)
    {
        if (!dict.TryGetValue(18, out var p) || p.Count < 2)
        {
            return new PrivateData([], 0, 0);
        }

        var (size, offset) = ((int)p[0], (int)p[1]);
        var privateDict = Dict(offset, Math.Min(_data.Length, offset + size));
        var subrs = privateDict.TryGetValue(19, out var s) ? IndexAt(offset + (int)s[0]) : [];
        return new PrivateData(subrs, Operand(privateDict, 20), Operand(privateDict, 21));
    }

    private string[] Charset(int offset)
    {
        var names = new string[_charStrings.Count];
        if (names.Length == 0)
        {
            return names;
        }

        names[0] = IsCid ? "0" : ".notdef";
        if (offset <= 2)
        {
            // Predefined charsets: glyph i has SID i (ISOAdobe); the expert sets are not kept.
            for (var g = 1; g < names.Length; g++)
            {
                names[g] = Identifier(g);
            }

            return names;
        }

        var position = offset + 1;
        var glyph = 1;
        var format = _data[offset];
        while (glyph < names.Length && position < _data.Length - 1)
        {
            var (first, count) = CharsetRange(format, ref position);
            for (var i = 0; i < count && glyph < names.Length; i++, glyph++)
            {
                names[glyph] = Identifier(first + i);
            }
        }

        return names;
    }

    // Format 0 lists one identifier per glyph; formats 1 and 2 give ranges with an 8- or 16-bit count.
    private (int First, int Count) CharsetRange(byte format, ref int position)
    {
        var first = Card16(position);
        position += 2;
        if (format == 0)
        {
            return (first, 1);
        }

        var left = format == 1 ? _data[position] : Card16(position);
        position += format == 1 ? 1 : 2;
        return (first, left + 1);
    }

    private string Identifier(int value) => IsCid ? value.ToString(CultureInfo.InvariantCulture) : Name(value);

    private Dictionary<int, string>? EncodingTable(int offset)
    {
        if (offset <= 1 || offset >= _data.Length)
        {
            return null;
        }

        var result = new Dictionary<int, string>();
        var format = _data[offset] & 0x7F;
        var position = offset + 1;
        if (format == 0)
        {
            var count = _data[position++];
            for (var g = 1; g <= count && g < GlyphNames.Length; g++)
            {
                result[_data[position++]] = GlyphNames[g];
            }
        }
        else
        {
            var ranges = _data[position++];
            var glyph = 1;
            for (var r = 0; r < ranges; r++)
            {
                var (first, left) = (_data[position], _data[position + 1]);
                position += 2;
                for (var i = 0; i <= left && glyph < GlyphNames.Length; i++)
                {
                    result[first + i] = GlyphNames[glyph++];
                }
            }
        }

        return result;
    }

    private byte[] FdSelect(int offset)
    {
        var result = new byte[_charStrings.Count];
        if (_data[offset] == 0)
        {
            Array.Copy(_data, offset + 1, result, 0, Math.Min(result.Length, _data.Length - offset - 1));
            return result;
        }

        var ranges = Card16(offset + 1);
        var position = offset + 3;
        for (var r = 0; r < ranges; r++)
        {
            var (first, fd, next) = (Card16(position), _data[position + 2], Card16(position + 3));
            for (var g = first; g < next && g < result.Length; g++)
            {
                result[g] = fd;
            }

            position += 3;
        }

        return result;
    }

    private string Name(int sid) => CffStandardStrings.Get(sid) ?? (sid - CffStandardStrings.Count < _strings.Length ? _strings[sid - CffStandardStrings.Count] : ".notdef");

    private int Card16(int at) => (_data[at] << 8) | _data[at + 1];

    private List<(int Start, int End)> IndexAt(int offset)
    {
        var position = offset;
        return Index(ref position);
    }

    // An INDEX: count, offset size, count + 1 offsets (from 1), then the data.
    private List<(int Start, int End)> Index(ref int position)
    {
        var result = new List<(int, int)>();
        if (position + 2 > _data.Length)
        {
            return result;
        }

        var count = Card16(position);
        position += 2;
        if (count == 0)
        {
            return result;
        }

        var size = _data[position++];
        var offsets = new int[count + 1];
        for (var i = 0; i <= count; i++)
        {
            var value = 0;
            for (var b = 0; b < size; b++)
            {
                value = (value << 8) | _data[position++];
            }

            offsets[i] = value;
        }

        var dataStart = position - 1;
        for (var i = 0; i < count; i++)
        {
            result.Add((dataStart + offsets[i], dataStart + offsets[i + 1]));
        }

        position = dataStart + offsets[count];
        return result;
    }

    // A DICT: operands before each operator (escaped operators are stored as 1200 + second byte).
    private Dictionary<int, List<double>> Dict(int start, int end)
    {
        var result = new Dictionary<int, List<double>>();
        var operands = new List<double>();
        var position = start;
        while (position < end)
        {
            var b0 = _data[position];
            if (b0 <= 21)
            {
                var op = b0 == 12 ? 1200 + _data[position + 1] : b0;
                position += b0 == 12 ? 2 : 1;
                result[op] = operands;
                operands = [];
                continue;
            }

            operands.Add(DictNumber(ref position));
        }

        return result;
    }

    private double DictNumber(ref int position)
    {
        var b0 = _data[position++];
        return b0 switch
        {
            28 => (short)((_data[position++] << 8) | _data[position++]),
            29 => (_data[position++] << 24) | (_data[position++] << 16) | (_data[position++] << 8) | _data[position++],
            30 => Real(ref position),
            >= 32 and <= 246 => b0 - 139,
            >= 247 and <= 250 => ((b0 - 247) * 256) + _data[position++] + 108,
            >= 251 and <= 254 => -((b0 - 251) * 256) - _data[position++] - 108,
            _ => 0,
        };
    }

    private double Real(ref int position)
    {
        var text = new StringBuilder();
        while (position < _data.Length)
        {
            var b = _data[position++];
            if (Nibble(text, b >> 4) || Nibble(text, b & 0xF))
            {
                break;
            }
        }

        return double.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static bool Nibble(StringBuilder text, int nibble)
    {
        switch (nibble)
        {
            case <= 9:
                text.Append((char)('0' + nibble));
                return false;
            case 0xA:
                text.Append('.');
                return false;
            case 0xB:
                text.Append('E');
                return false;
            case 0xC:
                text.Append("E-");
                return false;
            case 0xE:
                text.Append('-');
                return false;
            default:
                return nibble == 0xF;
        }
    }

    private static double Operand(Dictionary<int, List<double>> dict, int op) => dict.TryGetValue(op, out var values) && values.Count > 0 ? values[0] : 0;

    private sealed record PrivateData(List<(int Start, int End)> Subrs, double DefaultWidth, double NominalWidth);
}
