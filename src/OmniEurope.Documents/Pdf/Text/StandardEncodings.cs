// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Text;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>The base encodings of simple fonts (ISO 32000-1 Annex D) as code-to-text tables.</summary>
internal static class StandardEncodings
{
    public static string?[] WinAnsi { get; } = Build(code => code is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D ? "•" : Windows1252Encoding.DecodeByte((byte)code).ToString());

    public static string?[] MacRoman { get; } = Build(code => code < 0x80 ? ((char)code).ToString() : MacHigh[code - 0x80].ToString());

    public static string?[] Standard { get; } = BuildStandard();

    public static string?[] Symbol { get; } = BuildSymbol();

    public static string?[] Named(string name) => name switch
    {
        "WinAnsiEncoding" => WinAnsi,
        "MacRomanEncoding" => MacRoman,
        "MacExpertEncoding" => Standard,
        "StandardEncoding" => Standard,
        _ => Standard,
    };

    private const string MacHigh =
        "ÄÅÇÉÑÖÜáàâäãåçéèêëíìîïñóòôöõúùûü†°¢£§•¶ß®©™´¨≠ÆØ∞±≤≥¥µ∂∑∏π∫ªºΩæø¿¡¬√ƒ≈∆«»… ÀÃÕŒœ–—“”‘’÷◊ÿŸ⁄€‹›ﬁﬂ‡·‚„‰ÂÊÁËÈÍÎÏÌÓÔÒÚÛÙıˆ˜¯˘˙˚¸˝˛ˇ";

    private static string?[] Build(Func<int, string> map)
    {
        var table = new string?[256];
        for (var code = 32; code < 256; code++)
        {
            table[code] = code == 127 ? null : map(code);
        }

        return table;
    }

    private static string?[] BuildStandard()
    {
        var table = new string?[256];
        for (var code = 32; code < 127; code++)
        {
            table[code] = ((char)code).ToString();
        }

        table[0x27] = "’";
        table[0x60] = "‘";
        const string Upper = "¡¢£⁄¥ƒ§¤'“«‹›ﬁﬂ";
        for (var i = 0; i < Upper.Length; i++)
        {
            table[0xA1 + i] = Upper[i].ToString();
        }

        (int Code, string Text)[] rest =
        [
            (0xB1, "–"), (0xB2, "†"), (0xB3, "‡"), (0xB4, "·"), (0xB6, "¶"), (0xB7, "•"), (0xB8, "‚"), (0xB9, "„"), (0xBA, "”"),
            (0xBB, "»"), (0xBC, "…"), (0xBD, "‰"), (0xBF, "¿"), (0xC1, "`"), (0xC2, "´"), (0xC3, "ˆ"), (0xC4, "˜"), (0xC5, "¯"),
            (0xC6, "˘"), (0xC7, "˙"), (0xC8, "¨"), (0xCA, "˚"), (0xCB, "¸"), (0xCD, "˝"), (0xCE, "˛"), (0xCF, "ˇ"), (0xD0, "—"),
            (0xE1, "Æ"), (0xE3, "ª"), (0xE8, "Ł"), (0xE9, "Ø"), (0xEA, "Œ"), (0xEB, "º"), (0xF1, "æ"), (0xF5, "ı"), (0xF8, "ł"),
            (0xF9, "ø"), (0xFA, "œ"), (0xFB, "ß"),
        ];
        foreach (var (code, text) in rest)
        {
            table[code] = text;
        }

        return table;
    }

    // The Symbol font's own encoding for the letter positions: Latin letters stand for Greek ones.
    private static string?[] BuildSymbol()
    {
        var table = BuildStandard();
        const string Upper = "ΑΒΧΔΕΦΓΗΙϑΚΛΜΝΟΠΘΡΣΤΥςΩΞΨΖ";
        const string Lower = "αβχδεφγηιϕκλμνοπθρστυϖωξψζ";
        for (var i = 0; i < 26; i++)
        {
            table['A' + i] = Upper[i].ToString();
            table['a' + i] = Lower[i].ToString();
        }

        return table;
    }
}
