// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;

namespace OmniEurope.Documents.Tests.Pdf;

/// <summary>
/// Assembles Type 1 and Type 2 charstrings from text such as "10 20 rmoveto 30 hlineto endchar", following the
/// encodings of the Adobe Type 1 Font Format (§6) and of Technical Note 5177 (§4). A token "#80" is a raw byte
/// (a hint mask), a number with a decimal point a 16.16 fixed value (Type 2 only).
/// </summary>
internal static class CharStringAssembler
{
    private static readonly Dictionary<string, byte[]> Type1Operators = new(StringComparer.Ordinal)
    {
        ["hstem"] = [1], ["vstem"] = [3], ["vmoveto"] = [4], ["rlineto"] = [5], ["hlineto"] = [6], ["vlineto"] = [7],
        ["rrcurveto"] = [8], ["closepath"] = [9], ["callsubr"] = [10], ["return"] = [11], ["hsbw"] = [13],
        ["endchar"] = [14], ["rmoveto"] = [21], ["hmoveto"] = [22], ["vhcurveto"] = [30], ["hvcurveto"] = [31],
        ["dotsection"] = [12, 0], ["vstem3"] = [12, 1], ["hstem3"] = [12, 2], ["seac"] = [12, 6], ["sbw"] = [12, 7],
        ["div"] = [12, 12], ["callothersubr"] = [12, 16], ["pop"] = [12, 17], ["setcurrentpoint"] = [12, 33],
    };

    private static readonly Dictionary<string, byte[]> Type2Operators = new(StringComparer.Ordinal)
    {
        ["hstem"] = [1], ["vstem"] = [3], ["vmoveto"] = [4], ["rlineto"] = [5], ["hlineto"] = [6], ["vlineto"] = [7],
        ["rrcurveto"] = [8], ["callsubr"] = [10], ["return"] = [11], ["endchar"] = [14], ["hstemhm"] = [18],
        ["hintmask"] = [19], ["cntrmask"] = [20], ["rmoveto"] = [21], ["hmoveto"] = [22], ["vstemhm"] = [23],
        ["rcurveline"] = [24], ["rlinecurve"] = [25], ["vvcurveto"] = [26], ["hhcurveto"] = [27], ["callgsubr"] = [29],
        ["vhcurveto"] = [30], ["hvcurveto"] = [31], ["hflex"] = [12, 34], ["flex"] = [12, 35], ["hflex1"] = [12, 36],
        ["flex1"] = [12, 37],
    };

    public static byte[] Type1(string program) => Assemble(program, Type1Operators, type2: false);

    public static byte[] Type2(string program) => Assemble(program, Type2Operators, type2: true);

    private static byte[] Assemble(string program, Dictionary<string, byte[]> operators, bool type2)
    {
        var output = new List<byte>();
        foreach (var token in program.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (operators.TryGetValue(token, out var op))
            {
                output.AddRange(op);
            }
            else if (token.StartsWith('#'))
            {
                output.Add(byte.Parse(token[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            else if (token.Contains('.', StringComparison.Ordinal))
            {
                var fixedValue = (int)Math.Round(double.Parse(token, CultureInfo.InvariantCulture) * 65536);
                output.AddRange([255, (byte)(fixedValue >> 24), (byte)(fixedValue >> 16), (byte)(fixedValue >> 8), (byte)fixedValue]);
            }
            else
            {
                output.AddRange(Number(int.Parse(token, CultureInfo.InvariantCulture), type2));
            }
        }

        return [.. output];
    }

    // -107..107 in one byte, +-108..1131 in two, then a 16-bit (Type 2: 28) or 32-bit (Type 1: 255) integer.
    private static byte[] Number(int value, bool type2) => value switch
    {
        >= -107 and <= 107 => [(byte)(value + 139)],
        >= 108 and <= 1131 => [(byte)(247 + ((value - 108) >> 8)), (byte)((value - 108) & 0xFF)],
        >= -1131 and <= -108 => [(byte)(251 + ((-value - 108) >> 8)), (byte)((-value - 108) & 0xFF)],
        _ when type2 => [28, (byte)(value >> 8), (byte)value],
        _ => [255, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value],
    };
}
