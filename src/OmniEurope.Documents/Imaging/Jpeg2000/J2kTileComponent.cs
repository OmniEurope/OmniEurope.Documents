// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>A codeword segment of a code-block: its passes and where its bytes lie in the block's data.</summary>
internal sealed class J2kSegment(int firstPass, int maxPasses, int start)
{
    public int FirstPass { get; } = firstPass;

    public int MaxPasses { get; } = maxPasses;

    public int Start { get; set; } = start;

    public int Passes { get; set; }

    public int Length { get; set; }
}

/// <summary>A code-block (B.7): its area in sub-band coordinates and the coded data packets brought to it.</summary>
internal sealed class J2kCodeBlock(int x0, int y0, int x1, int y1)
{
    private byte[] _data = [];

    public int X0 { get; } = x0;

    public int Y0 { get; } = y0;

    public int X1 { get; } = x1;

    public int Y1 { get; } = y1;

    public bool Included { get; set; }

    public int ZeroPlanes { get; set; }

    public int LengthBits { get; set; } = 3;

    public int Passes => Segments.Count == 0 ? 0 : Segments[^1].FirstPass + Segments[^1].Passes;

    public List<J2kSegment> Segments { get; } = [];

    public byte[] Data => _data;

    public int DataLength { get; private set; }

    public void Append(byte[] source, int start, int length)
    {
        if (DataLength + length > _data.Length)
        {
            Array.Resize(ref _data, Math.Max(DataLength + length, _data.Length * 2));
        }

        Buffer.BlockCopy(source, start, _data, DataLength, length);
        DataLength += length;
    }
}

/// <summary>The code-blocks of one sub-band inside one precinct, with their two tag trees.</summary>
internal sealed class J2kPrecinctBand(J2kBand band, int across, int down, J2kCodeBlock[] blocks)
{
    public J2kBand Band { get; } = band;

    public int Across { get; } = across;

    public J2kCodeBlock[] Blocks { get; } = blocks;

    public J2kTagTree Inclusion { get; } = new(across, down);

    public J2kTagTree ZeroPlanes { get; } = new(across, down);
}

/// <summary>A sub-band (B.5): its orientation, area, decomposition level, magnitude bit-planes and step size.</summary>
internal sealed class J2kBand(int orientation, int x0, int y0, int x1, int y1)
{
    /// <summary>0 for LL, 1 for HL, 2 for LH, 3 for HH.</summary>
    public int Orientation { get; } = orientation;

    public int X0 { get; } = x0;

    public int Y0 { get; } = y0;

    public int X1 { get; } = x1;

    public int Y1 { get; } = y1;

    public int Width => X1 - X0;

    public int Height => Y1 - Y0;

    /// <summary>The magnitude bit-planes Mb (E.1), the region of interest shift included.</summary>
    public int MagnitudePlanes { get; set; }

    public float StepSize { get; set; } = 1;

    /// <summary>The dequantized coefficients, row by row (reversible decoding).</summary>
    public int[]? Integers { get; set; }

    /// <summary>The dequantized coefficients, row by row (irreversible decoding).</summary>
    public float[]? Reals { get; set; }
}

/// <summary>A resolution level of a tile-component (B.5, B.6): its area, sub-bands and precincts.</summary>
internal sealed class J2kResolution(int x0, int y0, int x1, int y1, int precinctWidth, int precinctHeight)
{
    public int X0 { get; } = x0;

    public int Y0 { get; } = y0;

    public int X1 { get; } = x1;

    public int Y1 { get; } = y1;

    public int PrecinctWidthExponent { get; } = precinctWidth;

    public int PrecinctHeightExponent { get; } = precinctHeight;

    public int PrecinctsAcross => X1 > X0 ? CeilDiv(X1, PrecinctWidthExponent) - (X0 >> PrecinctWidthExponent) : 0;

    public int PrecinctsDown => Y1 > Y0 ? CeilDiv(Y1, PrecinctHeightExponent) - (Y0 >> PrecinctHeightExponent) : 0;

    public J2kBand[] Bands { get; set; } = [];

    /// <summary>Each precinct's sub-band pieces, created when its first packet is read.</summary>
    public J2kPrecinctBand[]?[] Precincts { get; set; } = [];

    private static int CeilDiv(int value, int exponent) => (int)(((long)value + (1L << exponent) - 1) >> exponent);
}

/// <summary>
/// A tile-component: its area on the component's grid, coding style, quantization and resolution levels, each with
/// its sub-bands (B.5) and its precinct partition (B.6).
/// </summary>
internal sealed class J2kTileComponent
{
    // Most precincts one resolution level may have (a guard against absurd precinct sizes on large tiles).
    private const long MaxPrecincts = 1 << 20;

    public J2kTileComponent((int X0, int Y0, int X1, int Y1) tile, J2kComponentSize size, J2kComponentStyle style, J2kQuantization quantization, int roiShift)
    {
        X0 = Ceil(tile.X0, size.Dx);
        Y0 = Ceil(tile.Y0, size.Dy);
        X1 = Ceil(tile.X1, size.Dx);
        Y1 = Ceil(tile.Y1, size.Dy);
        Size = size;
        Style = style;
        RoiShift = roiShift;
        var levels = style.Levels;
        Resolutions = new J2kResolution[levels + 1];
        for (var r = 0; r <= levels; r++)
        {
            var scale = levels - r;
            var resolution = new J2kResolution(Ceil(X0, 1L << scale), Ceil(Y0, 1L << scale), Ceil(X1, 1L << scale), Ceil(Y1, 1L << scale),
                style.PrecinctWidthExponents[r], style.PrecinctHeightExponents[r]);
            resolution.Bands = r == 0 ? [Band(0, levels)] : [Band(1, scale + 1), Band(2, scale + 1), Band(3, scale + 1)];
            var precincts = (long)resolution.PrecinctsAcross * resolution.PrecinctsDown;
            resolution.Precincts = precincts <= MaxPrecincts
                ? new J2kPrecinctBand[]?[precincts]
                : throw new InvalidDataException("A JPEG 2000 resolution level has more precincts than allowed.");
            foreach (var band in resolution.Bands)
            {
                Quantize(band, r == 0 ? 0 : (3 * (r - 1)) + band.Orientation, scale + (r == 0 ? 0 : 1), quantization);
            }

            Resolutions[r] = resolution;
        }
    }

    public int X0 { get; }

    public int Y0 { get; }

    public int X1 { get; }

    public int Y1 { get; }

    public J2kComponentSize Size { get; }

    public J2kComponentStyle Style { get; }

    public int RoiShift { get; }

    public J2kResolution[] Resolutions { get; }

    /// <summary>The samples after the inverse transform (reversible decoding), row by row over the tile-component.</summary>
    public int[]? Integers { get; set; }

    /// <summary>The samples after the inverse transform (irreversible decoding).</summary>
    public float[]? Reals { get; set; }

    public static int Ceil(long value, long divisor) => (int)((value + divisor - 1) / divisor);

    /// <summary>The sub-band pieces of precinct <paramref name="index"/> of resolution <paramref name="r"/>, created on first use.</summary>
    public J2kPrecinctBand[] Precinct(int r, int index)
    {
        var resolution = Resolutions[r];
        return resolution.Precincts[index] ??= [.. resolution.Bands.Select(b => PrecinctBand(resolution, r, index, b))];
    }

    // The sub-band of orientation o and decomposition level n (B-15): LL keeps the resolution's own area.
    private J2kBand Band(int orientation, int level)
    {
        var xo = orientation is 1 or 3 ? 1L << (level - 1) : 0;
        var yo = orientation is 2 or 3 ? 1L << (level - 1) : 0;
        var d = 1L << level;
        return new J2kBand(orientation, Ceil(X0 - xo, d), Ceil(Y0 - yo, d), Ceil(X1 - xo, d), Ceil(Y1 - yo, d));
    }

    private void Quantize(J2kBand band, int index, int level, J2kQuantization quantization)
    {
        var (exponent, mantissa) = quantization.Band(index, level, Style.Levels);
        band.MagnitudePlanes = quantization.GuardBits + exponent - 1 + RoiShift;
        var gain = band.Orientation switch { 0 => 0, 3 => 2, _ => 1 };
        band.StepSize = Style.Reversible ? 1 : (float)(Math.Pow(2, Size.Precision + gain - exponent) * (1 + (mantissa / 2048.0)));
        if (band.MagnitudePlanes > 30)
        {
            throw new NotSupportedException("A JPEG 2000 sub-band has more than 30 magnitude bit-planes.");
        }
    }

    // The code-blocks of band b inside a precinct (B.7): the precinct's area halved on the sub-band for r > 0, cut
    // by the code-block grid anchored at 0.
    private J2kPrecinctBand PrecinctBand(J2kResolution resolution, int r, int index, J2kBand band)
    {
        var shift = r == 0 ? 0 : 1;
        var px = resolution.PrecinctWidthExponent - shift;
        var py = resolution.PrecinctHeightExponent - shift;
        var cbw = Math.Min(Style.BlockWidthExponent, px);
        var cbh = Math.Min(Style.BlockHeightExponent, py);
        var i = (index % resolution.PrecinctsAcross) + (resolution.X0 >> resolution.PrecinctWidthExponent);
        var j = (index / resolution.PrecinctsAcross) + (resolution.Y0 >> resolution.PrecinctHeightExponent);
        var x0 = (int)Math.Max(band.X0, (long)i << px);
        var y0 = (int)Math.Max(band.Y0, (long)j << py);
        var x1 = (int)Math.Min(band.X1, (long)(i + 1) << px);
        var y1 = (int)Math.Min(band.Y1, (long)(j + 1) << py);
        if (x1 <= x0 || y1 <= y0)
        {
            return new J2kPrecinctBand(band, 0, 0, []);
        }

        var bx0 = x0 >> cbw;
        var by0 = y0 >> cbh;
        var across = Ceil(x1, 1L << cbw) - bx0;
        var down = Ceil(y1, 1L << cbh) - by0;
        var blocks = new J2kCodeBlock[across * down];
        for (var y = 0; y < down; y++)
        {
            for (var x = 0; x < across; x++)
            {
                var cx = (bx0 + x) << cbw;
                var cy = (by0 + y) << cbh;
                blocks[(y * across) + x] = new J2kCodeBlock(Math.Max(cx, x0), Math.Max(cy, y0), Math.Min(cx + (1 << cbw), x1), Math.Min(cy + (1 << cbh), y1));
            }
        }

        return new J2kPrecinctBand(band, across, down, blocks);
    }
}
