// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// The raw bits of a bypassed coding pass (ISO/IEC 15444-1 D.6): most significant first, a byte after 0xFF holding
/// only seven; past the end of the segment the bits read as 1.
/// </summary>
internal sealed class J2kRawBits(byte[] data, int start, int end)
{
    private int _position = start;
    private int _byte;
    private int _bits;
    private bool _afterFF;

    public int Bit()
    {
        if (_bits == 0)
        {
            _byte = _position < end ? data[_position++] : 0xFF;
            _bits = _afterFF ? 7 : 8;
            _afterFF = _byte == 0xFF;
        }

        _bits--;
        return (_byte >> _bits) & 1;
    }
}
