// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// A tag tree (ISO/IEC 15444-1 B.10.2): a value per leaf of a grid, coded through a pyramid of minima, each node's
/// value refined with as many bits as a threshold asks and remembered across packets.
/// </summary>
internal sealed class J2kTagTree
{
    private readonly int[] _widths;
    private readonly int[] _offsets;
    private readonly int[] _lower;
    private readonly int[] _values;

    public J2kTagTree(int width, int height)
    {
        var widths = new List<int>();
        var offsets = new List<int>();
        var total = 0;
        var (w, h) = (width, height);
        while (true)
        {
            widths.Add(w);
            offsets.Add(total);
            total += w * h;
            if (w <= 1 && h <= 1)
            {
                break;
            }

            (w, h) = ((w + 1) / 2, (h + 1) / 2);
        }

        _widths = [.. widths];
        _offsets = [.. offsets];
        _lower = new int[total];
        _values = new int[total];
        Array.Fill(_values, int.MaxValue);
    }

    /// <summary>
    /// The value of leaf (<paramref name="x"/>, <paramref name="y"/>) if it is below <paramref name="threshold"/>,
    /// reading the bits needed to know; otherwise <see cref="int.MaxValue"/>.
    /// </summary>
    public int Decode(J2kPacketBits bits, int x, int y, int threshold)
    {
        var low = 0;
        var node = 0;
        for (var level = _widths.Length - 1; level >= 0; level--)
        {
            node = _offsets[level] + ((y >> level) * _widths[level]) + (x >> level);
            _lower[node] = Math.Max(_lower[node], low);
            while (_lower[node] < threshold && _lower[node] < _values[node])
            {
                if (bits.Bit() == 1)
                {
                    _values[node] = _lower[node];
                }
                else
                {
                    _lower[node]++;
                }
            }

            low = _lower[node];
        }

        return _values[node];
    }
}
