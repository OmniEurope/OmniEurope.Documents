// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// Tier-1 decoding of one code-block (ISO/IEC 15444-1 annex D): bit-plane by bit-plane, the significance propagation,
/// magnitude refinement and cleanup passes over stripes of four rows, with the MQ decoder and its 19 contexts, or raw
/// bits for bypassed passes. Every code-block style is honoured: bypass, context reset, termination of each pass,
/// vertically causal contexts, predictable termination and segmentation symbols. The coefficients are then
/// dequantized into the sub-band (E.1), mid-point reconstructed below the last bit-plane decoded.
/// </summary>
internal sealed class J2kBlockDecoder
{
    private const byte Significant = 1;
    private const byte Negative = 2;
    private const byte Visited = 4;
    private const byte Refined = 8;
    private const int RunLengthContext = 17;
    private const int UniformContext = 18;

    // Table D.1: the zero coding context for (horizontal, vertical, diagonal) significant neighbour counts, by
    // orientation (LL and LH, HL with horizontal and vertical swapped, HH).
    private static readonly byte[][] ZeroTables = [ZeroTable(0), ZeroTable(1), ZeroTable(0), ZeroTable(3)];

    // Table D.3: the sign context and the bit it is exclusive-ored with, by (horizontal + 1) * 3 + (vertical + 1).
    private static readonly (byte Context, int Xor)[] SignTable =
        [(13, 1), (12, 1), (11, 1), (10, 1), (9, 0), (10, 0), (11, 0), (12, 0), (13, 0)];

    private readonly byte[] _contexts = new byte[19];
    private byte[] _flags = [];
    private int[] _magnitudes = [];
    private byte[] _planes = [];
    private int _width;
    private int _height;
    private int _stride;
    private byte[] _zero = ZeroTables[0];
    private bool _causal;
    private MqDecoder? _mq;
    private J2kRawBits? _raw;

    public void Decode(J2kCodeBlock block, J2kBand band, J2kBlockStyle style, bool reversible, int roiShift)
    {
        var planes = band.MagnitudePlanes - block.ZeroPlanes;
        if (planes <= 0 || block.Segments.Count == 0)
        {
            return;
        }

        Prepare(block, band, style);
        var lastPass = (3 * planes) - 2;
        foreach (var segment in block.Segments)
        {
            Start(block, segment, style);
            for (var pass = segment.FirstPass; pass < segment.FirstPass + segment.Passes && pass < lastPass; pass++)
            {
                Pass(pass, planes - 1 - ((pass + 2) / 3), style);
            }
        }

        Store(block, band, reversible, roiShift);
    }

    // The zero coding table of an orientation, indexed by h * 15 + v * 5 + d.
    private static byte[] ZeroTable(int orientation)
    {
        var table = new byte[45];
        for (var h = 0; h <= 2; h++)
        {
            for (var v = 0; v <= 2; v++)
            {
                for (var d = 0; d <= 4; d++)
                {
                    table[(h * 15) + (v * 5) + d] = orientation == 3 ? DiagonalContext(h + v, d) : orientation == 1 ? Context(v, h, d) : Context(h, v, d);
                }
            }
        }

        return table;
    }

    private static byte Context(int h, int v, int d) => h switch
    {
        2 => 8,
        1 => (byte)(v >= 1 ? 7 : d >= 1 ? 6 : 5),
        _ => (byte)(v == 2 ? 4 : v == 1 ? 3 : d >= 2 ? 2 : d),
    };

    private static byte DiagonalContext(int hv, int d) => d switch
    {
        >= 3 => 8,
        2 => (byte)(hv >= 1 ? 7 : 6),
        1 => (byte)(hv >= 2 ? 5 : hv == 1 ? 4 : 3),
        _ => (byte)Math.Min(hv, 2),
    };

    private void Prepare(J2kCodeBlock block, J2kBand band, J2kBlockStyle style)
    {
        _width = block.X1 - block.X0;
        _height = block.Y1 - block.Y0;
        _stride = _width + 2;
        var size = _stride * (_height + 2);
        if (_flags.Length < size)
        {
            _flags = new byte[size];
            _magnitudes = new int[size];
            _planes = new byte[size];
        }
        else
        {
            Array.Clear(_flags, 0, size);
            Array.Clear(_magnitudes, 0, size);
        }

        _zero = ZeroTables[band.Orientation];
        _causal = (style & J2kBlockStyle.VerticallyCausal) != 0;
        ResetContexts();
    }

    // Table D.7: every context in state 0, but the uniform (46), run-length (3) and all-zero (4) ones.
    private void ResetContexts()
    {
        Array.Clear(_contexts);
        _contexts[0] = 4 << 1;
        _contexts[RunLengthContext] = 3 << 1;
        _contexts[UniformContext] = 46 << 1;
    }

    private void Start(J2kCodeBlock block, J2kSegment segment, J2kBlockStyle style)
    {
        var end = segment.Start + segment.Length;
        var raw = (style & J2kBlockStyle.Bypass) != 0 && segment.FirstPass >= 10 && segment.FirstPass % 3 != 0;
        _raw = raw ? new J2kRawBits(block.Data, segment.Start, end) : null;
        _mq = raw ? null : new MqDecoder(block.Data, segment.Start, end);
    }

    private void Pass(int pass, int plane, J2kBlockStyle style)
    {
        switch (pass == 0 ? 2 : (pass - 1) % 3)
        {
            case 0:
                SignificancePass(plane);
                break;
            case 1:
                RefinementPass(plane);
                break;
            default:
                CleanupPass(plane, (style & J2kBlockStyle.Segmentation) != 0);
                break;
        }

        if ((style & J2kBlockStyle.Reset) != 0)
        {
            ResetContexts();
        }
    }

    private int Bit(int context) => _raw?.Bit() ?? _mq!.Decode(_contexts, context);

    // Whether the coefficient at index i sits on the last row of its stripe, whose lower neighbours a vertically
    // causal block does not look at.
    private bool Causal(int row) => _causal && (row & 3) == 3;

    private int ZeroContext(int i, bool causal)
    {
        var f = _flags;
        var s = _stride;
        var h = (f[i - 1] & Significant) + (f[i + 1] & Significant);
        var v = (f[i - s] & Significant) + (causal ? 0 : f[i + s] & Significant);
        var d = (f[i - s - 1] & Significant) + (f[i - s + 1] & Significant)
            + (causal ? 0 : (f[i + s - 1] & Significant) + (f[i + s + 1] & Significant));
        return _zero[(h * 15) + (v * 5) + d];
    }

    private static int Sign(byte flags) => (flags & Significant) == 0 ? 0 : (flags & Negative) != 0 ? -1 : 1;

    private void DecodeSign(int i, bool causal, int plane)
    {
        int negative;
        if (_raw is not null)
        {
            negative = _raw.Bit();
        }
        else
        {
            var f = _flags;
            var h = Math.Clamp(Sign(f[i - 1]) + Sign(f[i + 1]), -1, 1);
            var v = Math.Clamp(Sign(f[i - _stride]) + (causal ? 0 : Sign(f[i + _stride])), -1, 1);
            var (context, xor) = SignTable[((h + 1) * 3) + v + 1];
            negative = _mq!.Decode(_contexts, context) ^ xor;
        }

        _flags[i] |= (byte)(Significant | (negative == 1 ? Negative : 0));
        _magnitudes[i] = 1 << plane;
        _planes[i] = (byte)plane;
    }

    private void SignificancePass(int plane)
    {
        for (var y0 = 0; y0 < _height; y0 += 4)
        {
            var rows = Math.Min(4, _height - y0);
            for (var x = 0; x < _width; x++)
            {
                for (var k = 0; k < rows; k++)
                {
                    var i = ((y0 + k + 1) * _stride) + x + 1;
                    if ((_flags[i] & Significant) != 0)
                    {
                        continue;
                    }

                    var causal = Causal(k);
                    var context = ZeroContext(i, causal);
                    if (context == 0)
                    {
                        continue;
                    }

                    _flags[i] |= Visited;
                    if (Bit(context) == 1)
                    {
                        DecodeSign(i, causal, plane);
                    }
                }
            }
        }
    }

    private void RefinementPass(int plane)
    {
        for (var y0 = 0; y0 < _height; y0 += 4)
        {
            var rows = Math.Min(4, _height - y0);
            for (var x = 0; x < _width; x++)
            {
                for (var k = 0; k < rows; k++)
                {
                    var i = ((y0 + k + 1) * _stride) + x + 1;
                    if ((_flags[i] & (Significant | Visited)) == Significant)
                    {
                        Refine(i, Causal(k), plane);
                    }
                }
            }
        }
    }

    // Table D.4: the first refinement of a coefficient looks at whether any neighbour is significant.
    private void Refine(int i, bool causal, int plane)
    {
        int context;
        if ((_flags[i] & Refined) != 0)
        {
            context = 16;
        }
        else
        {
            context = ZeroContext(i, causal) == 0 ? 14 : 15;
            _flags[i] |= Refined;
        }

        _magnitudes[i] |= Bit(context) << plane;
        _planes[i] = (byte)plane;
    }

    private void CleanupPass(int plane, bool segmentation)
    {
        for (var y0 = 0; y0 < _height; y0 += 4)
        {
            var rows = Math.Min(4, _height - y0);
            for (var x = 0; x < _width; x++)
            {
                var first = rows == 4 ? RunLength(y0, x, plane) : 0;
                for (var k = first; k < rows; k++)
                {
                    var i = ((y0 + k + 1) * _stride) + x + 1;
                    if ((_flags[i] & (Significant | Visited)) == 0)
                    {
                        var causal = Causal(k);
                        if (Bit(ZeroContext(i, causal)) == 1)
                        {
                            DecodeSign(i, causal, plane);
                        }
                    }

                    _flags[i] &= unchecked((byte)~Visited);
                }
            }
        }

        if (segmentation)
        {
            // The segmentation symbol 1010, which only a damaged stream would get wrong.
            for (var b = 0; b < 4; b++)
            {
                Bit(UniformContext);
            }
        }
    }

    // A column of four coefficients without any significant neighbour is coded with the run-length context
    // (D.3.4); returns the first row the ordinary cleanup still has to code (4 when the run covers the column).
    private int RunLength(int y0, int x, int plane)
    {
        for (var k = 0; k < 4; k++)
        {
            var i = ((y0 + k + 1) * _stride) + x + 1;
            if ((_flags[i] & (Significant | Visited)) != 0 || ZeroContext(i, Causal(k)) != 0)
            {
                return 0;
            }
        }

        if (_mq!.Decode(_contexts, RunLengthContext) == 0)
        {
            return 4;
        }

        var row = (_mq.Decode(_contexts, UniformContext) << 1) | _mq.Decode(_contexts, UniformContext);
        DecodeSign(((y0 + row + 1) * _stride) + x + 1, Causal(row), plane);
        return row + 1;
    }

    // The magnitudes, region of interest shift undone (H.1), dequantized into the sub-band.
    private void Store(J2kCodeBlock block, J2kBand band, bool reversible, int roiShift)
    {
        var bandWidth = band.Width;
        for (var y = 0; y < _height; y++)
        {
            var target = ((block.Y0 - band.Y0 + y) * bandWidth) + block.X0 - band.X0;
            for (var x = 0; x < _width; x++)
            {
                var i = ((y + 1) * _stride) + x + 1;
                var magnitude = _magnitudes[i];
                if (magnitude == 0)
                {
                    continue;
                }

                int plane = _planes[i];
                if (roiShift > 0 && magnitude >= 1 << roiShift)
                {
                    magnitude >>= roiShift;
                    plane = Math.Max(0, plane - roiShift);
                }

                var negative = (_flags[i] & Negative) != 0;
                if (reversible)
                {
                    var value = magnitude + ((1 << plane) >> 1);
                    band.Integers![target + x] = negative ? -value : value;
                }
                else
                {
                    var value = (magnitude + ((1 << plane) * 0.5f)) * band.StepSize;
                    band.Reals![target + x] = negative ? -value : value;
                }
            }
        }
    }
}
