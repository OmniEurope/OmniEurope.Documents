// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// Glyph names to Unicode. Accented letters are composed from the base letter and the accent name
/// (<c>Aacute</c> is A + combining acute, normalised); <c>uniXXXX</c>, <c>uXXXX</c>, the AFII Cyrillic
/// codes and the names of ASCII, Latin, Greek and common symbols are handled by rule or by table.
/// </summary>
internal static class GlyphNames
{
    private static readonly Dictionary<string, char> Accents = new(StringComparer.Ordinal)
    {
        ["grave"] = '̀', ["acute"] = '́', ["circumflex"] = '̂', ["tilde"] = '̃', ["macron"] = '̄',
        ["breve"] = '̆', ["dotaccent"] = '̇', ["dieresis"] = '̈', ["ring"] = '̊',
        ["hungarumlaut"] = '̋', ["caron"] = '̌', ["commaaccent"] = '̦', ["cedilla"] = '̧', ["ogonek"] = '̨',
    };

    private static readonly Dictionary<string, string> Names = Build();

    /// <summary>The text of a glyph name, or null when unknown.</summary>
    public static string? ToUnicode(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var dot = name.IndexOf('.');
        var bare = dot > 0 ? name[..dot] : name;
        if (bare.Contains('_'))
        {
            var parts = bare.Split('_').Select(ToUnicode).ToList();
            return parts.All(p => p is not null) ? string.Concat(parts) : null;
        }

        if (Names.TryGetValue(bare, out var known))
        {
            return known;
        }

        return Algorithmic(bare);
    }

    private static string? Algorithmic(string name)
    {
        if (name.Length == 1 && char.IsAsciiLetter(name[0]))
        {
            return name;
        }

        if (UniSequence(name) is { } sequence)
        {
            return sequence;
        }

        if (UScalar(name) is { } scalar)
        {
            return scalar;
        }

        if (name.StartsWith("afii", StringComparison.Ordinal) && int.TryParse(name.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var afii))
        {
            return Cyrillic(afii);
        }

        return Composed(name);
    }

    // uniXXXX[YYYY...]: one or more BMP code units, four hexadecimal digits each.
    private static string? UniSequence(string name)
    {
        if (!name.StartsWith("uni", StringComparison.Ordinal) || name.Length < 7 || (name.Length - 3) % 4 != 0 || name.AsSpan(3).ContainsAnyExcept(HexDigits))
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 3; i < name.Length; i += 4)
        {
            builder.Append((char)int.Parse(name.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    // uXXXX to uXXXXXX: one Unicode scalar value.
    private static string? UScalar(string name) =>
        name.Length is >= 5 and <= 7 && name[0] == 'u' && !name.AsSpan(1).ContainsAnyExcept(HexDigits)
        && int.TryParse(name.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
        && code <= 0x10FFFF && code is < 0xD800 or > 0xDFFF
            ? char.ConvertFromUtf32(code)
            : null;

    private static readonly System.Buffers.SearchValues<char> HexDigits = System.Buffers.SearchValues.Create("0123456789ABCDEFabcdef");

    // Base letter + accent name (Aacute, scommaaccent, Ydieresis...).
    private static string? Composed(string name)
    {
        foreach (var (accent, mark) in Accents)
        {
            if (name.Length > accent.Length && name.EndsWith(accent, StringComparison.Ordinal))
            {
                var baseName = name[..^accent.Length];
                var baseText = baseName.Length == 1 ? baseName : Names.GetValueOrDefault(baseName);
                if (baseText is { Length: 1 })
                {
                    var composed = (baseText + mark).Normalize(NormalizationForm.FormC);
                    if (composed.Length != 1 && accent == "commaaccent")
                    {
                        // Gcommaaccent, Kcommaaccent... are precomposed with a cedilla in Unicode.
                        composed = (baseText + '̧').Normalize(NormalizationForm.FormC);
                    }

                    return composed.Length == 1 ? composed : null;
                }
            }
        }

        return null;
    }

    private static string? Cyrillic(int afii) => afii switch
    {
        >= 10017 and <= 10022 => ((char)(0x0410 + afii - 10017)).ToString(),
        10023 => "Ё",
        >= 10024 and <= 10049 => ((char)(0x0416 + afii - 10024)).ToString(),
        >= 10065 and <= 10070 => ((char)(0x0430 + afii - 10065)).ToString(),
        10071 => "ё",
        >= 10072 and <= 10097 => ((char)(0x0436 + afii - 10072)).ToString(),
        _ => null,
    };

    private static Dictionary<string, string> Build()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string list, string chars)
        {
            var keys = list.Split(' ');
            for (var i = 0; i < keys.Length; i++)
            {
                names[keys[i]] = chars[i].ToString();
            }
        }

        Add("space exclam quotedbl numbersign dollar percent ampersand quotesingle parenleft parenright asterisk plus comma hyphen period slash",
            " !\"#$%&'()*+,-./");
        Add("zero one two three four five six seven eight nine colon semicolon less equal greater question at",
            "0123456789:;<=>?@");
        Add("bracketleft backslash bracketright asciicircum underscore grave braceleft bar braceright asciitilde",
            "[\\]^_`{|}~");
        Add("exclamdown cent sterling currency yen brokenbar section dieresis copyright ordfeminine guillemotleft logicalnot registered macron degree plusminus",
            "¡¢£¤¥¦§¨©ª«¬®¯°±");
        Add("twosuperior threesuperior acute mu paragraph periodcentered cedilla onesuperior ordmasculine guillemotright onequarter onehalf threequarters questiondown",
            "²³´µ¶·¸¹º»¼½¾¿");
        Add("AE Eth multiply Oslash Thorn germandbls ae eth divide oslash thorn dotlessi Lslash lslash OE oe Dcroat dcroat Hbar hbar Tbar tbar Eng eng",
            "ÆÐ×ØÞßæð÷øþıŁłŒœĐđĦħŦŧŊŋ");
        Add("quoteleft quoteright quotesinglbase quotedblleft quotedblright quotedblbase dagger daggerdbl bullet ellipsis perthousand guilsinglleft guilsinglright",
            "‘’‚“”„†‡•…‰‹›");
        Add("endash emdash trademark fraction florin circumflex tilde breve dotaccent ring ogonek caron hungarumlaut minus Euro euro",
            "–—™⁄ƒˆ˜˘˙˚˛ˇ˝−€€");
        Add("fi fl ff ffi ffl nbspace sfthyphen periodcentered middot lozenge infinity notequal lessequal greaterequal partialdiff summation product radical integral approxequal",
            "ﬁﬂﬀﬃﬄ ­··◊∞≠≤≥∂∑∏√∫≈");
        Add("Alpha Beta Gamma Delta Epsilon Zeta Eta Theta Iota Kappa Lambda Mu Nu Xi Omicron Pi Rho Sigma Tau Upsilon Phi Chi Psi Omega",
            "ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ");
        Add("alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma1 sigma tau upsilon phi chi psi omega",
            "αβγδεζηθικλμνξοπρςστυφχψω");
        Add("Alphatonos Epsilontonos Etatonos Iotatonos Omicrontonos Upsilontonos Omegatonos alphatonos epsilontonos etatonos iotatonos omicrontonos upsilontonos omegatonos iotadieresis upsilondieresis Iotadieresis Upsilondieresis iotadieresistonos upsilondieresistonos tonos dieresistonos",
            "ΆΈΉΊΌΎΏάέήίόύώϊϋΪΫΐΰ΄΅");
        Add("arrowleft arrowup arrowright arrowdown checkmark check heart spade club diamond copyrightsans registersans trademarksans",
            "←↑→↓✓✓♥♠♣♦©®™");
        return names;
    }
}
