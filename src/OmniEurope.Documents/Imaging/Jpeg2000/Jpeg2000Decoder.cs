// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jpeg2000;

/// <summary>
/// Decodes JPEG 2000 images (ITU-T T.800 | ISO/IEC 15444-1), a bare codestream or a JP2 (or JPX) file as the PDF
/// JPXDecode filter holds them: every tile and tile-part, the five progression orders and progression order changes,
/// precincts, layers, packed packet headers (PPM, PPT), SOP and EPH markers, every code-block style, scalar
/// quantization (derived or expounded) or none, the region of interest shift, the reversible 5-3 and irreversible 9-7
/// wavelets, the RCT and ICT component transforms, subsampled and signed components of up to 30 bits; in a JP2 file
/// the palette, component mapping, channel definitions (colours reordered, opacity) and colour specification.
/// </summary>
internal static class Jpeg2000Decoder
{
    public static Jpeg2000Image Decode(byte[] data)
    {
        try
        {
            var header = Jp2File.IsJp2(data) ? Jp2File.Read(data) : null;
            var codestream = J2kCodestream.Read(data, header?.CodestreamStart ?? 0, header?.CodestreamEnd ?? data.Length);
            var size = codestream.Size;
            var planes = size.Components.Select(c => Jpeg2000Plane.Of(size, c)).ToArray();
            foreach (var tile in codestream.Tiles.Values)
            {
                J2kTileDecoder.Decode(codestream, tile, planes);
            }

            return Jpeg2000Channels.Image(size, planes, header);
        }
        catch (Exception exception) when (DamagedData.IsOverrun(exception))
        {
            throw DamagedData.Error("JPEG 2000", exception);
        }
    }
}
