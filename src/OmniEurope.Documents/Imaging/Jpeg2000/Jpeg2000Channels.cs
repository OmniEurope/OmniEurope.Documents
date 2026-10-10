// SPDX-License-Identifier: EUPL-1.2
using System.Text;

namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// Turns the decoded components into the image's channels (ISO/IEC 15444-1 I.5.3): through the component mapping
/// and palette, then the channel definitions that order the colours and name the opacity channel; an ICC profile
/// stands for the device space of its data colour space.
/// </summary>
internal static class Jpeg2000Channels
{
    public static Jpeg2000Image Image(J2kImageSize size, Jpeg2000Plane[] components, Jp2Header? header)
    {
        var channels = Mapped(components, header);
        var colors = channels;
        Jpeg2000Plane? alpha = null;
        var premultiplied = false;
        if (header is { Definitions.Count: > 0 })
        {
            colors = [.. header.Definitions.Where(d => d.Type == Jpeg2000ChannelType.Color).OrderBy(d => d.Association).Select(d => Channel(channels, d.Channel))];
            var opacity = header.Definitions.FirstOrDefault(d => d.Type is Jpeg2000ChannelType.Opacity or Jpeg2000ChannelType.PremultipliedOpacity);
            if (opacity is not null)
            {
                alpha = Channel(channels, opacity.Channel);
                premultiplied = opacity.Type == Jpeg2000ChannelType.PremultipliedOpacity;
            }
        }

        if (colors.Length == 0)
        {
            throw new InvalidDataException("The JPEG 2000 image defines no colour channel.");
        }

        var space = ColorSpace(header, colors);
        var lab = space == Jpeg2000ColorSpace.Lab && colors.Length >= 3 ? Jpeg2000Lab.From(header!.LabParameters, colors, header.LabD65) : null;
        return new Jpeg2000Image(size, components, colors) { Alpha = alpha, Premultiplied = premultiplied, ColorSpace = space, Lab = lab };
    }

    private static Jpeg2000Plane Channel(Jpeg2000Plane[] channels, int index) =>
        index < channels.Length ? channels[index] : throw new InvalidDataException("A JPEG 2000 channel definition names a channel the image does not have.");

    // The channels after the component mapping box (each a component, directly or through a palette column); a
    // palette without mapping maps component 0 through every column.
    private static Jpeg2000Plane[] Mapped(Jpeg2000Plane[] components, Jp2Header? header)
    {
        var palette = header?.Palette;
        var mapping = header?.Channels ?? (palette is null ? null : [.. Enumerable.Range(0, palette.Columns.Length).Select(c => new Jp2Channel(0, c))]);
        if (mapping is null)
        {
            return components;
        }

        return [.. mapping.Select(m => m.Component >= components.Length
            ? throw new InvalidDataException("A JPEG 2000 component mapping names a component the image does not have.")
            : m.PaletteColumn < 0 ? components[m.Component] : ThroughPalette(components[m.Component], palette, m.PaletteColumn))];
    }

    private static Jpeg2000Plane ThroughPalette(Jpeg2000Plane plane, Jp2Palette? palette, int column)
    {
        if (palette is null || column >= palette.Columns.Length)
        {
            throw new InvalidDataException("A JPEG 2000 component mapping names a palette column the image does not have.");
        }

        var entries = palette.Columns[column];
        var samples = plane.Samples.Select(i => entries[Math.Clamp(i, 0, entries.Length - 1)]).ToArray();
        return new Jpeg2000Plane(plane.X0, plane.Y0, plane.Width, plane.Height, plane.Dx, plane.Dy, palette.Depths[column], palette.Signed[column], samples);
    }

    // The declared colour space; a bare codestream of three components whose second and third are subsampled and
    // the first not is luma and chroma.
    private static Jpeg2000ColorSpace ColorSpace(Jp2Header? header, Jpeg2000Plane[] colors)
    {
        if (header is null)
        {
            return colors.Length == 3 && colors[0].Dx == 1 && colors[0].Dy == 1 && colors.Skip(1).All(c => c.Dx > 1 || c.Dy > 1)
                ? Jpeg2000ColorSpace.Ycc
                : Jpeg2000ColorSpace.Unknown;
        }

        if (header.ColorSpace != Jpeg2000ColorSpace.Icc)
        {
            return header.ColorSpace;
        }

        // The data colour space field of the profile header (ICC.1, 7.2.6).
        var profile = header.IccProfile!;
        return (profile.Length >= 20 ? Encoding.ASCII.GetString(profile, 16, 4) : string.Empty) switch
        {
            "GRAY" => Jpeg2000ColorSpace.Gray,
            "RGB " => Jpeg2000ColorSpace.Rgb,
            "CMYK" => Jpeg2000ColorSpace.Cmyk,
            _ => Jpeg2000ColorSpace.Unknown,
        };
    }
}
