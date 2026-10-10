// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Imaging;
using OmniEurope.Documents.Imaging.Jbig2;
using OmniEurope.Documents.Imaging.Jpeg2000;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf.Text;

/// <summary>
/// Decodes PDF image XObjects and inline images to pixels: 1 to 16 bits per component, DeviceGray, RGB and
/// CMYK, ICC-based (by component count), Indexed, CalGray/CalRGB, Lab (approximate), Separation and DeviceN
/// (as ink coverage), image masks, Decode arrays, DCT, CCITT, JBIG2 and JPEG 2000 data, and the soft mask as alpha
/// (for JPEG 2000 with SMaskInData, the opacity channel of the data instead).
/// </summary>
internal static class PdfImageDecoder
{
    public static RasterImage? TryDecode(PdfObjectStore store, PdfDictionary image, byte[]? inlineData, PdfDictionary? resources)
    {
        try
        {
            var decoded = Decode(store, image, inlineData, resources);
            if (decoded is null || inlineData is not null || store.Number(image, "SMaskInData") != 0 || store.Get(image, "SMask") is not PdfStream mask)
            {
                return decoded;
            }

            var alpha = Decode(store, mask, null, resources);
            return alpha is null ? decoded : WithAlpha(decoded, alpha);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
        {
            return null;
        }
    }

    private static RasterImage? Decode(PdfObjectStore store, PdfDictionary image, byte[]? inlineData, PdfDictionary? resources)
    {
        var width = (int)Value(store, image, "Width", "W");
        var height = (int)Value(store, image, "Height", "H");
        if (width < 1 || height < 1 || (long)width * height > RasterImage.MaxPixels)
        {
            return null;
        }

        var (data, imageFilter, parameters) = Filtered(store, image, inlineData);
        return imageFilter switch
        {
            "DCTDecode" or "DCT" => JpegDecoder.Decode(data),

            "JPXDecode" => Jpx(store, image, resources, data),
            "JBIG2Decode" => Samples(store, image, resources, Jbig2(store, data, parameters, width, height), width, height, ccitt: true),
            "CCITTFaxDecode" or "CCF" => Samples(store, image, resources, Ccitt(store, data, parameters, width, height), width, height, ccitt: true),
            _ => Samples(store, image, resources, data, width, height, ccitt: false),
        };
    }

    // Samples of the image's colour space (one bit for stencil masks and fax data), mapped through its Decode array.
    private static RasterImage Samples(PdfObjectStore store, PdfDictionary image, PdfDictionary? resources, byte[] data, int width, int height, bool ccitt)
    {
        var isMask = IsTrue(store, image, "ImageMask") || IsTrue(store, image, "IM");
        var bits = isMask || ccitt ? 1 : (int)Value(store, image, "BitsPerComponent", "BPC", 8);
        var space = isMask ? PdfColorSpace.Gray : PdfColorSpace.Resolve(store, store.Get(image, "ColorSpace") ?? store.Get(image, "CS"), resources);
        return Unpack(data, width, height, bits, space, DecodeArray(store, image, isMask));
    }

    private static bool IsTrue(PdfObjectStore store, PdfDictionary image, string key) => store.Get(image, key) is PdfBoolean { Value: true };

    // The Decode (or D) array; a stencil mask without one paints where the sample is 0, shown black.
    private static double[]? DecodeArray(PdfObjectStore store, PdfDictionary image, bool isMask)
    {
        var array = store.Get<PdfArray>(image, "Decode") ?? store.Get<PdfArray>(image, "D");
        var decode = array?.Items.Select(i => store.Resolve(i) is PdfNumber n ? n.Value : 0).ToArray();
        return decode ?? (isMask ? [0, 1] : null);
    }

    private static (byte[] Data, string? ImageFilter, PdfDictionary? Parameters) Filtered(PdfObjectStore store, PdfDictionary image, byte[]? inlineData)
    {
        if (inlineData is null)
        {
            return store.Decode((PdfStream)image);
        }

        var filters = (store.Get(image, "Filter") ?? store.Get(image, "F")) switch
        {
            PdfName name => [name.Value],
            PdfArray array => array.Items.OfType<PdfName>().Select(n => n.Value).ToList(),
            _ => new List<string>(),
        };
        var parameters = (store.Get(image, "DecodeParms") ?? store.Get(image, "DP")) switch
        {
            PdfDictionary single => [single],
            PdfArray array => array.Items.Select(i => store.Resolve(i) as PdfDictionary).ToList(),
            _ => new List<PdfDictionary?>(),
        };
        return PdfFilters.Decode(inlineData, filters, parameters);
    }

    private static byte[] Ccitt(PdfObjectStore store, byte[] data, PdfDictionary? parameters, int width, int height)
    {
        var options = new CcittOptions
        {
            K = (int)store.Number(parameters, "K"),
            Columns = (int)store.Number(parameters, "Columns", 1728),
            Rows = (int)store.Number(parameters, "Rows", height),
            EncodedByteAlign = store.Get(parameters, "EncodedByteAlign") is PdfBoolean { Value: true },
            BlackIs1 = store.Get(parameters, "BlackIs1") is PdfBoolean { Value: true },
        };
        return CcittFaxDecoder.Decode(data, options, out _, out _);
    }

    // JBIG2 data: the page bitmap, read after the JBIG2Globals segments, as 1-bit rows where 0 is black (1 is black
    // in JBIG2), cut or padded to the image size.
    private static byte[] Jbig2(PdfObjectStore store, byte[] data, PdfDictionary? parameters, int width, int height)
    {
        var globals = store.Get(parameters, "JBIG2Globals") is PdfStream stream ? store.Decode(stream).Data : null;
        var bitmap = Jbig2Decoder.Decode(data, globals);
        var stride = (width + 7) / 8;
        var rows = new byte[(long)stride * height];
        Array.Fill(rows, (byte)0xFF);
        for (var y = 0; y < Math.Min(height, bitmap.Height); y++)
        {
            for (var x = 0; x < Math.Min(width, bitmap.Width); x++)
            {
                if (bitmap.Pixels[(y * bitmap.Width) + x] != 0)
                {
                    rows[(y * stride) + (x >> 3)] &= (byte)~(0x80 >> (x & 7));
                }
            }
        }

        return rows;
    }

    // JPEG 2000 data: in its own colour space unless the image names one (BitsPerComponent and Decode are then
    // ignored; an Indexed space takes the samples as indices); its opacity channel becomes alpha when SMaskInData is
    // 1, or 2 for colours premultiplied by it.
    private static RasterImage Jpx(PdfObjectStore store, PdfDictionary image, PdfDictionary? resources, byte[] data)
    {
        var jpx = Jpeg2000Decoder.Decode(data);
        var named = IsTrue(store, image, "ImageMask") || IsTrue(store, image, "IM") ? PdfName.Of("DeviceGray") : store.Get(image, "ColorSpace") ?? store.Get(image, "CS");
        RasterImage colors;
        if (named is null)
        {
            colors = jpx.ToRaster(withAlpha: false);
        }
        else
        {
            var space = PdfColorSpace.Resolve(store, named, resources);
            var indices = space.DefaultDecode(0) is null;
            colors = Unpack(indices ? jpx.Indices(space.Components) : jpx.Interleaved(space.Components), jpx.Width, jpx.Height, 8, space, null);
        }

        var inData = (int)store.Number(image, "SMaskInData");
        return inData != 0 && jpx.AlphaImage() is { } alpha ? Jpeg2000Colors.WithAlpha(colors, alpha, inData == 2 || jpx.Premultiplied) : colors;
    }

    private static RasterImage Unpack(byte[] data, int width, int height, int bits, PdfColorSpace space, double[]? decode)
    {
        var components = space.Components;
        var image = new RasterImage(width, height, space.Output);
        var rowBits = (long)width * components * bits;
        var rowBytes = (int)((rowBits + 7) / 8);
        var max = (1 << Math.Min(bits, 16)) - 1;
        var samples = new double[components];
        var output = image.Pixels;
        var o = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                for (var c = 0; c < components; c++)
                {
                    var raw = Sample(data, (long)y * rowBytes, ((long)x * components) + c, bits);
                    var (low, high) = decode is { } d && d.Length > (2 * c) + 1 ? (d[2 * c], d[(2 * c) + 1]) : space.DefaultDecode(c) ?? (0, max);
                    samples[c] = low + (raw * (high - low) / max);
                }

                o = space.Write(samples, output, o);
            }
        }

        return image;
    }

    private static int Sample(byte[] data, long rowStart, long index, int bits)
    {
        switch (bits)
        {
            case 8:
                var i8 = rowStart + index;
                return i8 < data.Length ? data[i8] : 0;
            case 16:
                var i16 = rowStart + (index * 2);
                return i16 + 1 < data.Length ? (data[i16] << 8) | data[i16 + 1] : 0;
            default:
                var bit = index * bits;
                var at = rowStart + (bit >> 3);
                return at < data.Length ? (data[at] >> (8 - bits - (int)(bit & 7))) & ((1 << bits) - 1) : 0;
        }
    }

    private static RasterImage WithAlpha(RasterImage image, RasterImage alpha)
    {
        var rgb = image.ColorType == ImageColorType.Rgb ? image : image.ConvertTo(ImageColorType.Rgb);
        var result = new RasterImage(rgb.Width, rgb.Height, ImageColorType.Rgba);
        for (var y = 0; y < rgb.Height; y++)
        {
            for (var x = 0; x < rgb.Width; x++)
            {
                var i = (y * rgb.Width) + x;
                result.Pixels[i * 4] = rgb.Pixels[i * 3];
                result.Pixels[(i * 4) + 1] = rgb.Pixels[(i * 3) + 1];
                result.Pixels[(i * 4) + 2] = rgb.Pixels[(i * 3) + 2];
                var ax = Math.Min(x * alpha.Width / rgb.Width, alpha.Width - 1);
                var ay = Math.Min(y * alpha.Height / rgb.Height, alpha.Height - 1);
                result.Pixels[(i * 4) + 3] = alpha.GetRgba(ax, ay).R;
            }
        }

        return result;
    }

    private static double Value(PdfObjectStore store, PdfDictionary dictionary, string key, string shortKey, double fallback = 0) =>
        store.Get(dictionary, key) is PdfNumber n ? n.Value : store.Number(dictionary, shortKey, fallback);
}
