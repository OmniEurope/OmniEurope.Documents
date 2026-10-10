// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// Tier-2 decoding (ISO/IEC 15444-1 B.9, B.10): each packet's header (empty flag; per code-block inclusion by tag tree
/// or bit, zero bit-planes, number of passes, length indicator and codeword segment lengths), its optional SOP and
/// EPH markers, then its body split among the code-blocks. Headers are read inline, or from the packed headers of PPM
/// or PPT markers. Data cut short ends the tile's packets: what was read is decoded.
/// </summary>
internal sealed class J2kPacketReader
{
    private readonly J2kTileComponent[] _components;
    private readonly byte[] _body;
    private readonly J2kPacketBits? _packed;
    private readonly List<(J2kCodeBlock Block, J2kSegment Segment, int Passes, int Length)> _contributions = [];
    private int _position;

    public J2kPacketReader(J2kTileComponent[] components, byte[] body, byte[]? packedHeaders)
    {
        _components = components;
        _body = body;
        _packed = packedHeaders is null ? null : new J2kPacketBits(packedHeaders, 0, packedHeaders.Length);
    }

    /// <summary>Reads the packets in order until the data ends.</summary>
    public void Read(IEnumerable<J2kPacket> packets)
    {
        foreach (var packet in packets)
        {
            if (_position >= _body.Length && _packed is null || !Read(packet))
            {
                return;
            }
        }
    }

    // One packet; false when its data was cut short.
    private bool Read(J2kPacket packet)
    {
        var component = _components[packet.Component];
        var bands = component.Precinct(packet.Resolution, packet.Precinct);
        var bodyBits = new J2kPacketBits(_body, _position, _body.Length);
        bodyBits.Skip(0xFF91, 4);
        var bits = _packed ?? bodyBits;
        _contributions.Clear();
        if (bits.Bit() == 1)
        {
            foreach (var band in bands)
            {
                ReadBand(bits, band, packet.Layer, component.Style.BlockStyle);
            }
        }

        bits.Align();
        bits.Skip(0xFF92, 0);
        if (bits.Overrun)
        {
            return false;
        }

        _position = bodyBits.Position;
        return ReadBody();
    }

    private void ReadBand(J2kPacketBits bits, J2kPrecinctBand band, int layer, J2kBlockStyle style)
    {
        for (var i = 0; i < band.Blocks.Length; i++)
        {
            var block = band.Blocks[i];
            var (x, y) = (i % band.Across, i / band.Across);
            var included = block.Included ? bits.Bit() == 1 : band.Inclusion.Decode(bits, x, y, layer + 1) <= layer;
            if (!included)
            {
                continue;
            }

            if (!block.Included)
            {
                block.ZeroPlanes = band.ZeroPlanes.Decode(bits, x, y, int.MaxValue);
                block.Included = true;
            }

            var passes = Passes(bits);
            while (bits.Bit() == 1 && !bits.Overrun)
            {
                block.LengthBits++;
            }

            AddSegments(bits, block, passes, style);
        }
    }

    // Table B.4: the number of new coding passes.
    private static int Passes(J2kPacketBits bits)
    {
        if (bits.Bit() == 0)
        {
            return 1;
        }

        if (bits.Bit() == 0)
        {
            return 2;
        }

        var two = bits.Bits(2);
        if (two < 3)
        {
            return 3 + two;
        }

        var five = bits.Bits(5);
        return five < 31 ? 6 + five : 37 + bits.Bits(7);
    }

    // The new passes split among codeword segments (B.10.7), each with its length: Lblock + floor(log2(passes)) bits.
    private void AddSegments(J2kPacketBits bits, J2kCodeBlock block, int passes, J2kBlockStyle style)
    {
        while (passes > 0)
        {
            var segment = block.Segments.Count > 0 && block.Segments[^1].Passes < block.Segments[^1].MaxPasses ? block.Segments[^1] : null;
            if (segment is null)
            {
                var first = block.Passes;
                segment = new J2kSegment(first, MaxPasses(first, style), -1);
                block.Segments.Add(segment);
            }

            var take = Math.Min(passes, segment.MaxPasses - segment.Passes);
            segment.Passes += take;
            passes -= take;
            var length = bits.Bits(block.LengthBits + (31 - int.LeadingZeroCount(take)));
            _contributions.Add((block, segment, take, length));
        }
    }

    // The passes a codeword segment holds (D.4.1): one when every pass is terminated; with bypass, the first ten
    // passes, then a raw significance and refinement pair, then a cleanup pass, in turn; otherwise all.
    private static int MaxPasses(int first, J2kBlockStyle style)
    {
        if ((style & J2kBlockStyle.TerminateAll) != 0)
        {
            return 1;
        }

        if ((style & J2kBlockStyle.Bypass) == 0)
        {
            return int.MaxValue;
        }

        return first < 10 ? 10 - first : first % 3 == 1 ? 2 : 1;
    }

    // The packet body: each contribution's bytes appended to its code-block, in header order.
    private bool ReadBody()
    {
        foreach (var (block, segment, _, length) in _contributions)
        {
            var available = Math.Min(length, _body.Length - _position);
            if (segment.Start < 0)
            {
                segment.Start = block.DataLength;
            }

            block.Append(_body, _position, available);
            segment.Length += available;
            _position += available;
            if (available < length)
            {
                return false;
            }
        }

        return true;
    }
}
