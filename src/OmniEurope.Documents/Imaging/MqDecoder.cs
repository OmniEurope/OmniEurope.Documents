// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging;

/// <summary>
/// The MQ adaptive binary arithmetic decoder shared by JBIG2 (ITU-T T.88, annex E) and JPEG 2000 (ISO/IEC 15444-1,
/// annex C). A context is one byte of an array the caller owns: its probability state times two plus its more
/// probable symbol. Past the end of the data the decoder reads 1 bits, as the standards ask; data that would have it
/// read far more of them than any coded data needs (a damaged or hostile length) is refused.
/// </summary>
internal sealed class MqDecoder
{
    // Probability estimation: Qe, next state after an MPS, next state after an LPS, and whether an LPS swaps the MPS.
    private static readonly ushort[] Qe =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    private static readonly byte[] NextMps =
    [
        1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
        32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46,
    ];

    private static readonly byte[] NextLps =
    [
        1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46,
    ];

    private static readonly bool[] Switch =
    [
        true, false, false, false, false, false, true, false, false, false, false, false, false, false, true, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
    ];

    private readonly byte[] _data;
    private readonly int _end;
    private int _position;
    private uint _c;
    private uint _a;
    private int _ct;
    private int _idle;

    /// <summary>Starts decoding <paramref name="data"/> from <paramref name="start"/> up to <paramref name="end"/>.</summary>
    public MqDecoder(byte[] data, int start, int end)
    {
        _data = data;
        _end = Math.Min(end, data.Length);
        _position = start;
        _c = (uint)Current << 16;
        ByteIn();
        _c <<= 7;
        _ct -= 7;
        _a = 0x8000;
    }

    // Bytes of 1 bits fed past a marker or the end of the data before the data is held for damaged.
    private const int IdleLimit = 1 << 16;

    /// <summary>The state a fresh context starts in (state 0, MPS 0).</summary>
    public const byte InitialContext = 0;

    private byte Current => _position < _end ? _data[_position] : (byte)0xFF;

    private byte Next => _position + 1 < _end ? _data[_position + 1] : (byte)0xFF;

    /// <summary>Decodes one bit with the context <paramref name="contexts"/>[<paramref name="index"/>], adapting it.</summary>
    public int Decode(byte[] contexts, int index)
    {
        var state = contexts[index] >> 1;
        var mps = contexts[index] & 1;
        uint qe = Qe[state];
        _a -= qe;
        int bit;
        if ((_c >> 16) < qe)
        {
            // The LPS sub-interval, unless the exchange makes it the MPS.
            if (_a < qe)
            {
                bit = mps;
                state = NextMps[state];
            }
            else
            {
                bit = 1 - mps;
                mps = Switch[state] ? 1 - mps : mps;
                state = NextLps[state];
            }

            _a = qe;
            Renormalize();
        }
        else
        {
            _c -= qe << 16;
            if ((_a & 0x8000) != 0)
            {
                return mps;
            }

            if (_a < qe)
            {
                bit = 1 - mps;
                mps = Switch[state] ? 1 - mps : mps;
                state = NextLps[state];
            }
            else
            {
                bit = mps;
                state = NextMps[state];
            }

            Renormalize();
        }

        contexts[index] = (byte)((state << 1) | mps);
        return bit;
    }

    private void Renormalize()
    {
        do
        {
            if (_ct == 0)
            {
                ByteIn();
            }

            _a <<= 1;
            _c <<= 1;
            _ct--;
        }
        while ((_a & 0x8000) == 0);
    }

    // A 0xFF followed by a byte above 0x8F is a marker: the decoder then feeds 1 bits without moving on.
    private void ByteIn()
    {
        if (Current == 0xFF)
        {
            if (Next > 0x8F)
            {
                // Past a marker or the end: 1 bits, which genuine data only needs for its last few symbols.
                if (++_idle > IdleLimit)
                {
                    throw new InvalidDataException("Arithmetic coded data ended long before what it codes.");
                }

                _c += 0xFF00;
                _ct = 8;
            }
            else
            {
                _position++;
                _c += (uint)Current << 9;
                _ct = 7;
            }
        }
        else
        {
            _position++;
            _c += (uint)Current << 8;
            _ct = 8;
        }
    }
}
