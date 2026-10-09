// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Imaging.Jbig2;

/// <summary>
/// Decodes embedded JBIG2 data (ITU-T T.88), as the PDF JBIG2Decode filter holds it: the global segments then the
/// page's segments, giving the first page's bitmap. Every region type is decoded: generic (arithmetic with typical
/// prediction, or MMR), generic refinement, text (arithmetic or Huffman, with refinements), halftone; with symbol
/// and pattern dictionaries, table segments, page information, end of stripe and end of page. Extended templates,
/// colour extensions and profiles are not.
/// </summary>
internal sealed class Jbig2Decoder
{
    private readonly Dictionary<int, object> _results = [];
    private readonly byte[] _data;
    private Jbig2Page? _page;

    private Jbig2Decoder(byte[] data) => _data = data;

    /// <summary>The page bitmap (1 is black) of <paramref name="data"/>, after the segments of <paramref name="globals"/>.</summary>
    public static Jbig2Bitmap Decode(byte[] data, byte[]? globals = null)
    {
        Jbig2Decoder? global = null;
        if (globals is { Length: > 0 })
        {
            global = new Jbig2Decoder(globals);
            global.Run();
        }

        var decoder = new Jbig2Decoder(data);
        if (global is not null)
        {
            foreach (var (number, result) in global._results)
            {
                decoder._results[number] = result;
            }
        }

        decoder.Run();
        return decoder._page?.Finish() ?? throw new InvalidDataException("JBIG2 data holds no page.");
    }

    private void Run()
    {
        foreach (var segment in Jbig2Segments.Read(_data))
        {
            if (!Apply(segment))
            {
                break;
            }
        }
    }

    // Decodes one segment; false once the page or the data ends.
    private bool Apply(Jbig2Segment segment)
    {
        switch (segment.Type)
        {
            case Jbig2Segments.PageInformation:
                if (_page is not null)
                {
                    return false;
                }

                _page = Jbig2Page.Parse(_data, segment.DataStart);
                break;
            case Jbig2Segments.EndOfPage or Jbig2Segments.EndOfFile:
                return _page is null;
            case Jbig2Segments.EndOfStripe:
                Page.EndStripe(Jbig2Bytes.Int32(_data, segment.DataStart));
                break;
            case Jbig2Segments.Tables:
                _results[segment.Number] = Jbig2HuffmanTable.Parse(_data, segment.DataStart, segment.DataEnd);
                break;
            case Jbig2Segments.SymbolDictionary:
                _results[segment.Number] = SymbolDictionary(segment);
                break;
            case Jbig2Segments.PatternDictionary:
                _results[segment.Number] = Jbig2Halftone.Patterns(_data, segment.DataStart, segment.DataEnd);
                break;
            default:
                Region(segment);
                break;
        }

        return true;
    }

    private Jbig2Page Page => _page ?? throw new InvalidDataException("A JBIG2 region comes before the page information.");

    private Jbig2SymbolDictionaryResult SymbolDictionary(Jbig2Segment segment)
    {
        var dictionaries = Referred<Jbig2SymbolDictionaryResult>(segment);
        return Jbig2SymbolDictionary.Decode(_data, segment.DataStart, segment.DataEnd,
            [.. dictionaries.SelectMany(d => d.Exported)], Referred<Jbig2HuffmanTable>(segment), dictionaries.LastOrDefault()?.Retained);
    }

    private List<T> Referred<T>(Jbig2Segment segment) =>
        [.. segment.ReferredTo.Select(n => _results.GetValueOrDefault(n)).OfType<T>()];

    private void Region(Jbig2Segment segment)
    {
        var kind = segment.Type & ~3;
        if (kind is not (Jbig2Segments.IntermediateText or Jbig2Segments.IntermediateHalftone or Jbig2Segments.IntermediateGeneric
            or Jbig2Segments.IntermediateRefinement) || segment.Type % 4 == 1)
        {
            return;
        }

        var info = Jbig2RegionInfo.Parse(_data, segment.DataStart);
        var start = segment.DataStart + Jbig2RegionInfo.Size;
        var bitmap = kind switch
        {
            Jbig2Segments.IntermediateText => Jbig2TextSegment.Decode(_data, start, segment.DataEnd, info,
                [.. Referred<Jbig2SymbolDictionaryResult>(segment).SelectMany(d => d.Exported)], Referred<Jbig2HuffmanTable>(segment)),
            Jbig2Segments.IntermediateHalftone => Jbig2Halftone.Region(_data, start, segment.DataEnd, info.Width, info.Height,
                Referred<Jbig2Bitmap[]>(segment).FirstOrDefault() ?? []),
            Jbig2Segments.IntermediateGeneric => Generic(start, segment.DataEnd, ref info),
            _ => Refinement(segment, start, info),
        };
        if (segment.Type == kind)
        {
            _results[segment.Number] = new Jbig2RegionResult(bitmap, info);
        }
        else
        {
            Page.Place(bitmap, info.X, info.Y, info.Combination);
        }
    }

    // A generic region segment (T.88 7.4.6); an unknown height comes from the row count ending the data.
    private Jbig2Bitmap Generic(int start, int end, ref Jbig2RegionInfo info)
    {
        var flags = Jbig2Bytes.Byte(_data, start);
        var mmr = (flags & 1) != 0;
        var template = (flags >> 1) & 3;
        if ((flags & 0x10) != 0)
        {
            throw new NotSupportedException("JBIG2 extended generic templates are not supported.");
        }

        if (info.Height == -1)
        {
            info = info with { Height = Jbig2Bytes.Int32(_data, end - 4) };
            end -= 6;
        }

        start++;
        if (mmr)
        {
            return Jbig2GenericDecoder.DecodeMmr(_data, start, end, info.Width, info.Height, out _);
        }

        var adaptive = Jbig2Segments.AdaptivePixels(_data, start, Jbig2GenericDecoder.AdaptivePixelCount(template));
        start += adaptive.Length * 2;
        return new Jbig2GenericDecoder(template).Decode(new MqDecoder(_data, start, end), info.Width, info.Height, adaptive, (flags & 8) != 0);
    }

    // A generic refinement region segment (T.88 7.4.7): it refines the region it refers to, or the page under it.
    private Jbig2Bitmap Refinement(Jbig2Segment segment, int start, Jbig2RegionInfo info)
    {
        var flags = Jbig2Bytes.Byte(_data, start);
        var template = flags & 1;
        start++;
        var adaptive = Jbig2RefinementDecoder.DefaultAdaptivePixels;
        if (template == 0)
        {
            adaptive = Jbig2Segments.AdaptivePixels(_data, start, 2);
            start += 4;
        }

        var reference = Referred<Jbig2RegionResult>(segment).FirstOrDefault()?.Bitmap
            ?? Page.Bitmap.Extract(info.X, info.Y, info.Width, info.Height);
        return new Jbig2RefinementDecoder(template).Decode(new MqDecoder(_data, start, segment.DataEnd), info.Width, info.Height,
            reference, 0, 0, adaptive, (flags & 2) != 0);
    }
}

/// <summary>An intermediate region's bitmap, kept for a refinement that refers to it.</summary>
internal sealed record Jbig2RegionResult(Jbig2Bitmap Bitmap, Jbig2RegionInfo Info);

/// <summary>The page buffer (T.88 7.4.8): its size, default pixel, and the stripes a page of unknown height grows by.</summary>
internal sealed class Jbig2Page
{
    private readonly bool _unknownHeight;
    private readonly byte _default;
    private int _stripeEnd;

    private Jbig2Page(int width, int height, byte fill)
    {
        _unknownHeight = height == -1;
        _default = fill;
        Bitmap = new Jbig2Bitmap(width, _unknownHeight ? 0 : height, fill);
    }

    public Jbig2Bitmap Bitmap { get; private set; }

    public static Jbig2Page Parse(byte[] data, int at) =>
        new(Jbig2Bytes.Int32(data, at), Jbig2Bytes.Int32(data, at + 4), (byte)((Jbig2Bytes.Byte(data, at + 16) >> 2) & 1));

    public void Place(Jbig2Bitmap bitmap, int x, int y, Jbig2Combination combination)
    {
        if (_unknownHeight && (long)y + bitmap.Height > Bitmap.Height)
        {
            Bitmap = Bitmap.Grow((int)Math.Min(int.MaxValue, (long)y + bitmap.Height), _default);
        }

        Bitmap.Combine(bitmap, x, y, combination);
    }

    public void EndStripe(int lastRow)
    {
        _stripeEnd = Math.Max(_stripeEnd, lastRow + 1);
        if (_unknownHeight && _stripeEnd > Bitmap.Height)
        {
            Bitmap = Bitmap.Grow(_stripeEnd, _default);
        }
    }

    /// <summary>The finished page: a page of unknown height ends with its last stripe.</summary>
    public Jbig2Bitmap Finish() => _unknownHeight && _stripeEnd > 0 && _stripeEnd != Bitmap.Height ? Bitmap.Grow(_stripeEnd, _default) : Bitmap;
}
