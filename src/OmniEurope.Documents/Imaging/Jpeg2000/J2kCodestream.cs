// SPDX-License-Identifier: EUPL-1.2
using System.Buffers.Binary;

namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>The coding parameters one header (main or tile-part) sets; null where it sets nothing.</summary>
internal sealed class J2kCodingParameters(int components)
{
    public J2kTileStyle? TileStyle { get; set; }

    public J2kComponentStyle? DefaultStyle { get; set; }

    public J2kComponentStyle?[] Styles { get; } = new J2kComponentStyle?[components];

    public J2kQuantization? DefaultQuantization { get; set; }

    public J2kQuantization?[] Quantizations { get; } = new J2kQuantization?[components];

    public int?[] RoiShifts { get; } = new int?[components];

    public List<J2kProgressionChange> ProgressionChanges { get; } = [];
}

/// <summary>The tile-parts of one tile: where their data lies, their headers' parameters and packed packet headers.</summary>
internal sealed class J2kTileParts(int index, int components)
{
    public int Index { get; } = index;

    public J2kCodingParameters Parameters { get; } = new(components);

    public List<(int Start, int End)> Bodies { get; } = [];

    /// <summary>Packed packet headers of PPT markers, by their Zppt index.</summary>
    public SortedDictionary<int, byte[]> TileHeaders { get; } = [];

    /// <summary>The PPM headers of this tile's tile-parts, in codestream order.</summary>
    public List<byte[]> MainHeaders { get; } = [];
}

/// <summary>
/// Reads the markers of a JPEG 2000 codestream (ISO/IEC 15444-1 annex A): the main header (SIZ, COD, COC, QCD, QCC,
/// RGN, POC, PPM; TLM, PLM, CRG, COM and unknown markers skipped) and every tile-part (SOT, its header with COD, COC,
/// QCD, QCC, RGN, POC, PPT, and its data up to the next tile-part).
/// </summary>
internal sealed class J2kCodestream
{
    private const int MaxTiles = 1 << 16;

    // A decompression bomb guard: the samples an image may claim, at least 16 million, or 4096 per byte of
    // codestream (0.002 bit per sample, far below what coded content needs).
    private const long SamplesPerByte = 4096;
    private const long FreeSamples = 1 << 24;
    private readonly SortedDictionary<int, byte[]> _ppm = [];

    private J2kCodestream(byte[] data, J2kImageSize size)
    {
        Data = data;
        Size = size;
        Main = new J2kCodingParameters(size.Components.Length);
    }

    public byte[] Data { get; }

    public J2kImageSize Size { get; }

    public J2kCodingParameters Main { get; }

    public SortedDictionary<int, J2kTileParts> Tiles { get; } = [];

    public static J2kCodestream Read(byte[] data, int start, int end)
    {
        if (end - start < 4 || Marker(data, start) != 0xFF4F || Marker(data, start + 2) != 0xFF51)
        {
            throw new InvalidDataException("Not a JPEG 2000 codestream: no SOC then SIZ marker.");
        }

        var codestream = new J2kCodestream(data, ReadSize(data, start + 4));
        var samples = codestream.Size.Components.Sum(c => (long)(J2kTileComponent.Ceil(codestream.Size.X1, c.Dx) - J2kTileComponent.Ceil(codestream.Size.X0, c.Dx))
            * (J2kTileComponent.Ceil(codestream.Size.Y1, c.Dy) - J2kTileComponent.Ceil(codestream.Size.Y0, c.Dy)));
        if (samples > Math.Max(FreeSamples, (end - start) * SamplesPerByte))
        {
            throw new InvalidDataException("The JPEG 2000 codestream claims far more samples than its data can code.");
        }

        var at = codestream.ReadMainHeader(start + 6 + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(start + 4)) - 2, end);
        codestream.ReadTileParts(at, end);
        return codestream;
    }

    private static int Marker(byte[] data, int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));

    private static J2kImageSize ReadSize(byte[] data, int at)
    {
        var s = data.AsSpan(at);
        int Int(int offset) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at + offset)));
        var count = BinaryPrimitives.ReadUInt16BigEndian(s[36..]);
        if (count == 0 || BinaryPrimitives.ReadUInt16BigEndian(s) != 38 + (3 * count))
        {
            throw new InvalidDataException("The JPEG 2000 SIZ marker is malformed.");
        }

        var components = new J2kComponentSize[count];
        for (var c = 0; c < count; c++)
        {
            var bits = s[38 + (3 * c)];
            components[c] = new J2kComponentSize((bits & 0x7F) + 1, (bits & 0x80) != 0, s[39 + (3 * c)], s[40 + (3 * c)]);
            if (components[c].Dx == 0 || components[c].Dy == 0)
            {
                throw new InvalidDataException("A JPEG 2000 component has no subsampling factor.");
            }

            if (components[c].Precision > 30)
            {
                throw new NotSupportedException("JPEG 2000 components of more than 30 bits are not supported.");
            }
        }

        var size = new J2kImageSize(Int(12), Int(16), Int(4), Int(8), Int(20), Int(24), Int(28), Int(32), components);
        Validate(size);
        return size;
    }

    private static void Validate(J2kImageSize size)
    {
        if (!Fits(size.X0, size.X1, size.TileX0, size.TileWidth) || !Fits(size.Y0, size.Y1, size.TileY0, size.TileHeight))
        {
            throw new InvalidDataException("The JPEG 2000 image or tile geometry is inconsistent.");
        }

        if ((long)(size.X1 - size.X0) * (size.Y1 - size.Y0) > RasterImage.MaxPixels || (long)size.TilesAcross * size.TilesDown > MaxTiles)
        {
            throw new InvalidDataException("The JPEG 2000 image is larger than allowed.");
        }
    }

    // One axis of the geometry (B.3): a non-empty image whose first tile starts at or before it and reaches into it.
    private static bool Fits(int start, int end, int tileStart, int tileSize) =>
        end > start && tileSize >= 1 && tileStart <= start && (long)tileStart + tileSize > start;

    // The main header markers up to the first SOT; returns where it starts.
    private int ReadMainHeader(int at, int end)
    {
        while (at + 4 <= end)
        {
            var marker = Marker(Data, at);
            if (marker == 0xFF90)
            {
                return at;
            }

            var length = Length(at, end);
            if (marker == 0xFF60)
            {
                _ppm[Data[at + 4]] = Data[(at + 5)..(at + 2 + length)];
            }
            else
            {
                ReadParameter(marker, at + 4, at + 2 + length, Main);
            }

            at += 2 + length;
        }

        throw new InvalidDataException("The JPEG 2000 codestream has no tile.");
    }

    private int Length(int at, int end)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(at + 2));
        if (length < 2 || at + 2 + length > end)
        {
            throw new InvalidDataException("A JPEG 2000 marker segment runs past the data.");
        }

        return length;
    }

    private void ReadTileParts(int at, int end)
    {
        var ppm = PpmChunks();
        var partNumber = 0;
        while (at + 12 <= end && Marker(Data, at) == 0xFF90)
        {
            var index = BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(at + 4));
            var length = BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(at + 6));
            var partEnd = length == 0 ? end : (int)Math.Min(end, at + length);
            if (index >= (long)Size.TilesAcross * Size.TilesDown || length is > 0 and < 14)
            {
                throw new InvalidDataException("A JPEG 2000 tile-part is malformed.");
            }

            if (!Tiles.TryGetValue(index, out var tile))
            {
                Tiles[index] = tile = new J2kTileParts(index, Size.Components.Length);
            }

            var body = ReadTilePartHeader(at + 12, partEnd, tile);
            tile.Bodies.Add((body, partEnd));
            if (ppm is not null)
            {
                tile.MainHeaders.Add(partNumber < ppm.Count ? ppm[partNumber] : []);
            }

            partNumber++;
            at = partEnd;
        }
    }

    // The tile-part header markers up to SOD; returns where the tile-part data starts.
    private int ReadTilePartHeader(int at, int end, J2kTileParts tile)
    {
        while (at + 2 <= end)
        {
            var marker = Marker(Data, at);
            if (marker == 0xFF93)
            {
                return at + 2;
            }

            var length = Length(at, end);
            if (marker == 0xFF61)
            {
                tile.TileHeaders[Data[at + 4]] = Data[(at + 5)..(at + 2 + length)];
            }
            else
            {
                ReadParameter(marker, at + 4, at + 2 + length, tile.Parameters);
            }

            at += 2 + length;
        }

        throw new InvalidDataException("A JPEG 2000 tile-part header has no SOD marker.");
    }

    // The PPM data split by tile-part: each a 32-bit length then that many bytes of packet headers.
    private List<byte[]>? PpmChunks()
    {
        if (_ppm.Count == 0)
        {
            return null;
        }

        var all = _ppm.Values.SelectMany(b => b).ToArray();
        var chunks = new List<byte[]>();
        var at = 0;
        while (at + 4 <= all.Length)
        {
            var length = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(all.AsSpan(at)), all.Length - at - 4);
            chunks.Add(all[(at + 4)..(at + 4 + length)]);
            at += 4 + length;
        }

        return chunks;
    }

    private void ReadParameter(int marker, int start, int end, J2kCodingParameters parameters)
    {
        // The markers ISO/IEC 15444-2 adds (DC offset, arbitrary decompositions and wavelets, multiple component
        // transforms, non-linearity...) change how samples are reconstructed: refused rather than decoded wrongly.
        if (marker is >= 0xFF70 and <= 0xFF7F)
        {
            throw new NotSupportedException($"JPEG 2000 part 2 marker 0x{marker:X4} is not supported.");
        }

        var s = Data.AsSpan(start, end - start);
        switch (marker)
        {
            case 0xFF52:
                parameters.TileStyle = new J2kTileStyle((s[0] & 2) != 0, (s[0] & 4) != 0, Progression(s[1]), BinaryPrimitives.ReadUInt16BigEndian(s[2..]), s[4] != 0);
                parameters.DefaultStyle = ReadComponentStyle(s[5..], (s[0] & 1) != 0);
                break;
            case 0xFF53:
                var (c, at) = ComponentIndex(s);
                parameters.Styles[c] = ReadComponentStyle(s[(at + 1)..], (s[at] & 1) != 0);
                break;
            case 0xFF5C:
                parameters.DefaultQuantization = ReadQuantization(s);
                break;
            case 0xFF5D:
                var (qc, qat) = ComponentIndex(s);
                parameters.Quantizations[qc] = ReadQuantization(s[qat..]);
                break;
            case 0xFF5E:
                var (rc, rat) = ComponentIndex(s);
                parameters.RoiShifts[rc] = s[rat + 1];
                break;
            case 0xFF5F:
                ReadProgressionChanges(s, parameters.ProgressionChanges);
                break;
        }
    }

    private (int Component, int Next) ComponentIndex(ReadOnlySpan<byte> s)
    {
        var wide = Size.Components.Length > 256;
        var component = wide ? BinaryPrimitives.ReadUInt16BigEndian(s) : s[0];
        if (component >= Size.Components.Length)
        {
            throw new InvalidDataException("A JPEG 2000 marker names a component the image does not have.");
        }

        return (component, wide ? 2 : 1);
    }

    private static J2kProgression Progression(byte value) => value <= 4
        ? (J2kProgression)value
        : throw new InvalidDataException("Unknown JPEG 2000 progression order.");

    private static J2kComponentStyle ReadComponentStyle(ReadOnlySpan<byte> s, bool precincts)
    {
        var levels = s[0];
        var (width, height) = (s[1] + 2, s[2] + 2);
        if (levels > 32 || width > 10 || height > 10 || width + height > 12)
        {
            throw new InvalidDataException("The JPEG 2000 coding style is out of range.");
        }

        // Bit 6 of the code-block style selects the high-throughput block coder (ISO/IEC 15444-15), and a wavelet
        // other than 9-7 (0) and 5-3 (1) is an arbitrary one of part 2.
        if ((s[3] & 0xC0) != 0 || s[4] > 1)
        {
            throw new NotSupportedException("The JPEG 2000 high-throughput block coder and part 2 wavelets are not supported.");
        }

        var precinctWidths = new int[levels + 1];
        var precinctHeights = new int[levels + 1];
        for (var r = 0; r <= levels; r++)
        {
            var value = precincts ? s[5 + r] : 0xFF;
            precinctWidths[r] = precincts ? value & 0xF : 15;
            precinctHeights[r] = precincts ? value >> 4 : 15;
            if (r > 0 && (precinctWidths[r] == 0 || precinctHeights[r] == 0))
            {
                throw new InvalidDataException("A JPEG 2000 precinct of a high resolution has a zero size exponent.");
            }
        }

        return new J2kComponentStyle(levels, width, height, (J2kBlockStyle)(s[3] & 0x3F), s[4] == 1, precinctWidths, precinctHeights);
    }

    private static J2kQuantization ReadQuantization(ReadOnlySpan<byte> s)
    {
        var style = s[0] & 0x1F;
        var guard = s[0] >> 5;
        if (style == J2kQuantization.None)
        {
            var bytes = s[1..];
            return new J2kQuantization(style, guard, [.. bytes.ToArray().Select(b => b >> 3)], new int[bytes.Length]);
        }

        if (style > 2)
        {
            throw new InvalidDataException("Unknown JPEG 2000 quantization style.");
        }

        var count = (s.Length - 1) / 2;
        var exponents = new int[count];
        var mantissas = new int[count];
        for (var i = 0; i < count; i++)
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(s[(1 + (2 * i))..]);
            exponents[i] = value >> 11;
            mantissas[i] = value & 0x7FF;
        }

        return count == 0 ? throw new InvalidDataException("A JPEG 2000 quantization marker has no step size.") : new J2kQuantization(style, guard, exponents, mantissas);
    }

    private void ReadProgressionChanges(ReadOnlySpan<byte> s, List<J2kProgressionChange> changes)
    {
        var wide = Size.Components.Length > 256;
        var size = wide ? 9 : 7;
        for (var at = 0; at + size <= s.Length; at += size)
        {
            var e = s[at..];
            var componentStart = wide ? BinaryPrimitives.ReadUInt16BigEndian(e[1..]) : e[1];
            var next = wide ? 3 : 2;
            var layerEnd = BinaryPrimitives.ReadUInt16BigEndian(e[next..]);
            var resolutionEnd = e[next + 2];
            var componentEnd = wide ? BinaryPrimitives.ReadUInt16BigEndian(e[(next + 3)..]) : e[next + 3];
            changes.Add(new J2kProgressionChange(e[0], componentStart, layerEnd, resolutionEnd, componentEnd == 0 ? 256 : componentEnd,
                Progression(e[size - 1])));
        }
    }
}
