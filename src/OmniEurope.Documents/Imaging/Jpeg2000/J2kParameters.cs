// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>One image component of the SIZ marker: sample depth, signedness and subsampling on the reference grid.</summary>
internal sealed record J2kComponentSize(int Precision, bool Signed, int Dx, int Dy);

/// <summary>The image and tile geometry of the SIZ marker (ISO/IEC 15444-1 A.5.1), on the reference grid.</summary>
internal sealed record J2kImageSize(int X0, int Y0, int X1, int Y1, int TileWidth, int TileHeight, int TileX0, int TileY0, J2kComponentSize[] Components)
{
    public int TilesAcross => (int)(((long)X1 - TileX0 + TileWidth - 1) / TileWidth);

    public int TilesDown => (int)(((long)Y1 - TileY0 + TileHeight - 1) / TileHeight);

    /// <summary>The area of tile <paramref name="index"/> on the reference grid (B.3).</summary>
    public (int X0, int Y0, int X1, int Y1) Tile(int index)
    {
        var p = index % TilesAcross;
        var q = index / TilesAcross;
        return (
            Math.Max(TileX0 + (p * TileWidth), X0),
            Math.Max(TileY0 + (q * TileHeight), Y0),
            (int)Math.Min((long)TileX0 + ((p + 1L) * TileWidth), X1),
            (int)Math.Min((long)TileY0 + ((q + 1L) * TileHeight), Y1));
    }
}

/// <summary>The progression orders of SGcod and Ppoc (table A.16).</summary>
internal enum J2kProgression
{
    LayerResolutionComponentPosition = 0,
    ResolutionLayerComponentPosition = 1,
    ResolutionPositionComponentLayer = 2,
    PositionComponentResolutionLayer = 3,
    ComponentPositionResolutionLayer = 4,
}

/// <summary>The code-block coding style flags of SPcod (table A.19).</summary>
[Flags]
internal enum J2kBlockStyle
{
    None = 0,

    /// <summary>Selective arithmetic coding bypass: raw significance and refinement passes after the fourth bit-plane.</summary>
    Bypass = 1,

    /// <summary>Contexts reset at the end of each coding pass.</summary>
    Reset = 2,

    /// <summary>Each coding pass terminated, its data a codeword segment of its own.</summary>
    TerminateAll = 4,

    /// <summary>Vertically causal contexts: the next stripe counts as insignificant.</summary>
    VerticallyCausal = 8,

    /// <summary>Predictable termination (nothing to do when decoding).</summary>
    Predictable = 16,

    /// <summary>A segmentation symbol closes each cleanup pass.</summary>
    Segmentation = 32,
}

/// <summary>The coding style of one tile-component (COD/COC SPcod, A.6.1 and A.6.2).</summary>
internal sealed record J2kComponentStyle(int Levels, int BlockWidthExponent, int BlockHeightExponent, J2kBlockStyle BlockStyle, bool Reversible, int[] PrecinctWidthExponents, int[] PrecinctHeightExponents);

/// <summary>The tile-wide part of a COD marker (Scod and SGcod).</summary>
internal sealed record J2kTileStyle(bool StartOfPacket, bool EndOfPacketHeader, J2kProgression Progression, int Layers, bool MultipleComponentTransform);

/// <summary>Quantization of one tile-component (QCD/QCC, A.6.4 and A.6.5): guard bits and each sub-band's exponent and mantissa.</summary>
internal sealed record J2kQuantization(int Style, int GuardBits, int[] Exponents, int[] Mantissas)
{
    /// <summary>No quantization (reversible): one exponent per sub-band.</summary>
    public const int None = 0;

    /// <summary>Scalar derived: one exponent and mantissa for the lowest LL band, the others derived from it.</summary>
    public const int Derived = 1;

    /// <summary>The exponent and mantissa of sub-band <paramref name="band"/> (0 is LL, then HL, LH, HH by resolution) of decomposition level <paramref name="level"/> out of <paramref name="levels"/>.</summary>
    public (int Exponent, int Mantissa) Band(int band, int level, int levels)
    {
        if (Style == Derived)
        {
            return (Exponents[0] - levels + level, Mantissas[0]);
        }

        if (band >= Exponents.Length)
        {
            throw new InvalidDataException("The JPEG 2000 quantization lists fewer sub-bands than the image has.");
        }

        return (Exponents[band], Mantissas[band]);
    }
}

/// <summary>One progression order change of a POC marker (A.6.6), its ends exclusive.</summary>
internal sealed record J2kProgressionChange(int ResolutionStart, int ComponentStart, int LayerEnd, int ResolutionEnd, int ComponentEnd, J2kProgression Order);
