// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>One packet: a quality layer of one precinct of a resolution level of a tile-component.</summary>
internal readonly record struct J2kPacket(int Layer, int Resolution, int Component, int Precinct);

/// <summary>
/// The order of the packets of a tile (ISO/IEC 15444-1 B.12): one of the five progressions, or the progression
/// order changes of POC markers in turn, each packet given once, the layers of a precinct in order. Position-driven
/// progressions walk the reference grid of the tile and take a precinct where it starts (B.12.1.3 to B.12.1.5).
/// </summary>
internal sealed class J2kPacketOrder
{
    private readonly J2kTileComponent[] _components;
    private readonly (int X0, int Y0, int X1, int Y1) _tile;
    private readonly int[][][] _nextLayer;

    private J2kPacketOrder(J2kTileComponent[] components, (int X0, int Y0, int X1, int Y1) tile)
    {
        _components = components;
        _tile = tile;
        _nextLayer = [.. components.Select(c => c.Resolutions.Select(r => new int[r.Precincts.Length]).ToArray())];
    }

    public static IEnumerable<J2kPacket> Packets(J2kTileComponent[] components, (int X0, int Y0, int X1, int Y1) tile, J2kTileStyle style,
        IReadOnlyList<J2kProgressionChange> changes)
    {
        var order = new J2kPacketOrder(components, tile);
        var resolutions = components.Max(c => c.Resolutions.Length);
        IReadOnlyList<J2kProgressionChange> progressions = changes.Count > 0
            ? changes
            : [new J2kProgressionChange(0, 0, style.Layers, resolutions, components.Length, style.Progression)];
        foreach (var change in progressions)
        {
            var bounded = change with
            {
                LayerEnd = Math.Min(change.LayerEnd, style.Layers),
                ResolutionEnd = Math.Min(change.ResolutionEnd, resolutions),
                ComponentEnd = Math.Min(change.ComponentEnd, components.Length),
            };
            foreach (var packet in order.Sequence(bounded).Where(order.Take))
            {
                yield return packet;
            }
        }
    }

    // A packet not given yet whose layer is the next of its precinct.
    private bool Take(J2kPacket packet)
    {
        ref var next = ref _nextLayer[packet.Component][packet.Resolution][packet.Precinct];
        if (packet.Layer != next)
        {
            return false;
        }

        next++;
        return true;
    }

    private IEnumerable<J2kPacket> Sequence(J2kProgressionChange c) => c.Order switch
    {
        J2kProgression.LayerResolutionComponentPosition =>
            from l in Range(0, c.LayerEnd)
            from r in Range(c.ResolutionStart, c.ResolutionEnd)
            from k in Range(c.ComponentStart, c.ComponentEnd)
            from p in Range(0, Precincts(k, r))
            select new J2kPacket(l, r, k, p),
        J2kProgression.ResolutionLayerComponentPosition =>
            from r in Range(c.ResolutionStart, c.ResolutionEnd)
            from l in Range(0, c.LayerEnd)
            from k in Range(c.ComponentStart, c.ComponentEnd)
            from p in Range(0, Precincts(k, r))
            select new J2kPacket(l, r, k, p),
        J2kProgression.ResolutionPositionComponentLayer =>
            from r in Range(c.ResolutionStart, c.ResolutionEnd)
            from position in Positions(c)
            from k in Range(c.ComponentStart, c.ComponentEnd)
            let p = PrecinctAt(k, r, position)
            where p >= 0
            from l in Range(0, c.LayerEnd)
            select new J2kPacket(l, r, k, p),
        J2kProgression.PositionComponentResolutionLayer =>
            from position in Positions(c)
            from k in Range(c.ComponentStart, c.ComponentEnd)
            from r in Range(c.ResolutionStart, c.ResolutionEnd)
            let p = PrecinctAt(k, r, position)
            where p >= 0
            from l in Range(0, c.LayerEnd)
            select new J2kPacket(l, r, k, p),
        _ =>
            from k in Range(c.ComponentStart, c.ComponentEnd)
            from position in Positions(c)
            from r in Range(c.ResolutionStart, c.ResolutionEnd)
            let p = PrecinctAt(k, r, position)
            where p >= 0
            from l in Range(0, c.LayerEnd)
            select new J2kPacket(l, r, k, p),
    };

    private static IEnumerable<int> Range(int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            yield return i;
        }
    }

    private int Precincts(int component, int resolution)
    {
        var resolutions = _components[component].Resolutions;
        return resolution < resolutions.Length ? resolutions[resolution].Precincts.Length : 0;
    }

    // The reference grid positions to visit: the tile's origin, then every multiple of the smallest precinct step
    // of the components and resolutions in play.
    private IEnumerable<(int X, int Y)> Positions(J2kProgressionChange c)
    {
        var (xStep, yStep) = (long.MaxValue, long.MaxValue);
        for (var k = c.ComponentStart; k < c.ComponentEnd; k++)
        {
            var component = _components[k];
            for (var r = c.ResolutionStart; r < Math.Min(c.ResolutionEnd, component.Resolutions.Length); r++)
            {
                var (dx, dy) = Step(component, r);
                (xStep, yStep) = (Math.Min(xStep, dx), Math.Min(yStep, dy));
            }
        }

        if (xStep == long.MaxValue)
        {
            yield break;
        }

        for (long y = _tile.Y0; y < _tile.Y1; y += yStep - (y % yStep))
        {
            for (long x = _tile.X0; x < _tile.X1; x += xStep - (x % xStep))
            {
                yield return ((int)x, (int)y);
            }
        }
    }

    // The size of a precinct of resolution r on the reference grid: XRsiz * 2^(PPx + NL - r).
    private static (long X, long Y) Step(J2kTileComponent component, int r)
    {
        var resolution = component.Resolutions[r];
        var scale = component.Style.Levels - r;
        return ((long)component.Size.Dx << Math.Min(resolution.PrecinctWidthExponent + scale, 40),
            (long)component.Size.Dy << Math.Min(resolution.PrecinctHeightExponent + scale, 40));
    }

    // The precinct of resolution r of component k that starts at this position (B.12.1.3), or -1.
    private int PrecinctAt(int k, int r, (int X, int Y) position)
    {
        var component = _components[k];
        if (r >= component.Resolutions.Length)
        {
            return -1;
        }

        var resolution = component.Resolutions[r];
        if (resolution.Precincts.Length == 0)
        {
            return -1;
        }

        var scale = component.Style.Levels - r;
        var (stepX, stepY) = Step(component, r);
        var startsX = position.X % stepX == 0 || (position.X == _tile.X0 && (resolution.X0 & ((1 << resolution.PrecinctWidthExponent) - 1)) != 0);
        var startsY = position.Y % stepY == 0 || (position.Y == _tile.Y0 && (resolution.Y0 & ((1 << resolution.PrecinctHeightExponent) - 1)) != 0);
        if (!startsX || !startsY)
        {
            return -1;
        }

        var i = (J2kTileComponent.Ceil(position.X, (long)component.Size.Dx << scale) >> resolution.PrecinctWidthExponent) - (resolution.X0 >> resolution.PrecinctWidthExponent);
        var j = (J2kTileComponent.Ceil(position.Y, (long)component.Size.Dy << scale) >> resolution.PrecinctHeightExponent) - (resolution.Y0 >> resolution.PrecinctHeightExponent);
        return i < resolution.PrecinctsAcross && j < resolution.PrecinctsDown ? i + (j * resolution.PrecinctsAcross) : -1;
    }
}
