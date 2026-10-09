// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// The arithmetic integer decoding procedures of T.88 annex A: an integer with its own 512 contexts (IADH, IADW,
/// IAEX, IAAI, IADT, IAFS, IADS, IAIT, IARI, IARDW, IARDH, IARDX, IARDY), and the symbol ID procedure IAID.
/// </summary>
internal sealed class Jbig2IntegerDecoder
{
    private readonly byte[] _contexts = new byte[512];

    /// <summary>The next integer, or null for the out-of-band value.</summary>
    public int? Decode(MqDecoder decoder)
    {
        var previous = 1;
        int Bit()
        {
            var bit = decoder.Decode(_contexts, previous);
            previous = previous < 256 ? (previous << 1) | bit : ((((previous << 1) | bit) & 511) | 256);
            return bit;
        }

        int Bits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = (value << 1) | Bit();
            }

            return value;
        }

        var sign = Bit();
        var value = Bit() == 0 ? Bits(2)
            : Bit() == 0 ? Bits(4) + 4
            : Bit() == 0 ? Bits(6) + 20
            : Bit() == 0 ? Bits(8) + 84
            : Bit() == 0 ? Bits(12) + 340
            : Bits(32) + 4436;
        if (sign == 1 && value == 0)
        {
            return null;
        }

        return sign == 1 ? -value : value;
    }
}
