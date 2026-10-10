// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// Decodes one tile: its coding parameters (tile-part headers over the main header, component markers over the
/// default ones), tier-2 then tier-1 decoding, the inverse wavelet transform, the inverse multiple component
/// transform (RCT for the 5-3 filter, ICT for the 9-7, G.2 and G.3) and the DC level shift (G.1), each component's
/// samples written into its plane.
/// </summary>
internal static class J2kTileDecoder
{
    public static void Decode(J2kCodestream codestream, J2kTileParts parts, Jpeg2000Plane[] planes)
    {
        var size = codestream.Size;
        var main = codestream.Main;
        var local = parts.Parameters;
        var style = local.TileStyle ?? main.TileStyle ?? throw new InvalidDataException("The JPEG 2000 codestream has no COD marker.");
        var area = size.Tile(parts.Index);
        var components = new J2kTileComponent[size.Components.Length];
        for (var c = 0; c < components.Length; c++)
        {
            components[c] = Component(area, size.Components[c], main, local, c);
        }

        var changes = local.ProgressionChanges.Count > 0 ? local.ProgressionChanges : main.ProgressionChanges;
        var body = parts.Bodies.SelectMany(b => codestream.Data.AsSpan(b.Start, Math.Max(0, b.End - b.Start)).ToArray()).ToArray();
        new J2kPacketReader(components, body, PackedHeaders(parts)).Read(J2kPacketOrder.Packets(components, area, style, changes));
        var blocks = new J2kBlockDecoder();
        foreach (var component in components)
        {
            DecodeBlocks(component, blocks);
            J2kWavelet.Inverse(component);
        }

        if (style.MultipleComponentTransform && components.Length >= 3)
        {
            J2kColorTransform.Inverse(components);
        }

        for (var c = 0; c < components.Length; c++)
        {
            Store(components[c], planes[c]);
        }
    }

    private static J2kTileComponent Component((int X0, int Y0, int X1, int Y1) area, J2kComponentSize size, J2kCodingParameters main, J2kCodingParameters local, int c)
    {
        var componentStyle = local.Styles[c] ?? local.DefaultStyle ?? main.Styles[c] ?? main.DefaultStyle
            ?? throw new InvalidDataException("The JPEG 2000 codestream has no COD marker.");
        var quantization = local.Quantizations[c] ?? local.DefaultQuantization ?? main.Quantizations[c] ?? main.DefaultQuantization
            ?? throw new InvalidDataException("The JPEG 2000 codestream has no QCD marker.");
        var component = new J2kTileComponent(area, size, componentStyle, quantization, local.RoiShifts[c] ?? main.RoiShifts[c] ?? 0);
        foreach (var band in component.Resolutions.SelectMany(r => r.Bands))
        {
            var count = band.Width * band.Height;
            if (componentStyle.Reversible)
            {
                band.Integers = new int[count];
            }
            else
            {
                band.Reals = new float[count];
            }
        }

        return component;
    }

    // The packet headers of PPT markers (by Zppt), or else of the PPM marker for this tile's tile-parts; null when the
    // headers are inline.
    private static byte[]? PackedHeaders(J2kTileParts parts)
    {
        if (parts.TileHeaders.Count > 0)
        {
            return [.. parts.TileHeaders.Values.SelectMany(h => h)];
        }

        return parts.MainHeaders.Count > 0 ? [.. parts.MainHeaders.SelectMany(h => h)] : null;
    }

    private static void DecodeBlocks(J2kTileComponent component, J2kBlockDecoder decoder)
    {
        var style = component.Style;
        foreach (var precinct in component.Resolutions.SelectMany(r => r.Precincts))
        {
            foreach (var band in precinct ?? [])
            {
                foreach (var block in band.Blocks)
                {
                    decoder.Decode(block, band.Band, style.BlockStyle, style.Reversible, component.RoiShift);
                }
            }
        }
    }

    // The DC level shift (unsigned components get 2^(depth - 1) back), rounding and clipping to the sample depth.
    private static void Store(J2kTileComponent component, Jpeg2000Plane plane)
    {
        var precision = component.Size.Precision;
        var (min, max) = component.Size.Signed ? (-(1 << (precision - 1)), (1 << (precision - 1)) - 1) : (0, (1 << precision) - 1);
        var shift = component.Size.Signed ? 0 : 1 << (precision - 1);
        var width = component.X1 - component.X0;
        for (var y = 0; y < component.Y1 - component.Y0; y++)
        {
            var target = ((component.Y0 - plane.Y0 + y) * plane.Width) + component.X0 - plane.X0;
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var value = component.Integers is { } integers ? integers[i] : (int)MathF.Round(component.Reals![i], MidpointRounding.AwayFromZero);
                plane.Samples[target + x] = Math.Clamp(value + shift, min, max);
            }
        }
    }
}
