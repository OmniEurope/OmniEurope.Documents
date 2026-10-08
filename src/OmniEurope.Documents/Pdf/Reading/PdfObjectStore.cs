// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Reading;

/// <summary>
/// The objects of a parsed PDF: resolves references (directly, through object streams, or by repair when an
/// offset is wrong), decrypts strings and streams, and decodes stream data.
/// </summary>
internal sealed class PdfObjectStore
{
    private const int MaxResolveDepth = 32;
    private readonly byte[] _data;
    private readonly Dictionary<int, XrefEntry> _entries;
    private readonly Dictionary<int, PdfObject> _cache = [];
    private readonly Dictionary<int, (int[] Numbers, int[] Offsets, byte[] Data)> _objectStreams = [];
    private readonly HashSet<int> _resolving = [];
    private PdfSecurity? _security;
    private PdfReference? _encryptReference;

    public PdfObjectStore(byte[] data, string? password)
    {
        _data = data;
        (_entries, Trailer, Repaired, StartXref, StreamXref) = PdfXref.Load(data);
        if (Trailer["Encrypt"] is { } encrypt)
        {
            _encryptReference = encrypt as PdfReference;
            var dictionary = Resolve(encrypt) as PdfDictionary ?? throw new InvalidDataException("Invalid Encrypt dictionary.");
            var id = (Trailer["ID"] as PdfArray)?.Items.FirstOrDefault() is PdfString s ? s.Bytes : [];
            _security = PdfSecurity.Create(dictionary, id, password);
            _cache.Clear();
        }

        if (Repaired)
        {
            IndexObjectStreams();
        }

        Catalog = Resolve(Trailer["Root"]) as PdfDictionary ?? FindCatalog() ?? throw new InvalidDataException("The PDF has no document catalog.");
    }

    public PdfDictionary Trailer { get; }

    public PdfDictionary Catalog { get; }

    public bool Repaired { get; }

    /// <summary>Offset of the last cross-reference section (0 after a repair).</summary>
    public long StartXref { get; }

    /// <summary>True when the last cross-reference section is a stream.</summary>
    public bool StreamXref { get; }

    /// <summary>One more than the highest object number in use.</summary>
    public int NextObjectNumber => Math.Max(_entries.Count == 0 ? 1 : _entries.Keys.Max() + 1, Trailer["Size"] is PdfNumber size ? size.IntValue : 0);

    /// <summary>The generation of an object, 0 when unknown.</summary>
    public int GenerationOf(int number) => _entries.TryGetValue(number, out var entry) ? entry.Generation : 0;

    public bool IsEncrypted => _security is not null;

    public IEnumerable<int> ObjectNumbers => _entries.Where(e => e.Value.Offset >= 0 || e.Value.InStream).Select(e => e.Key).Order();

    /// <summary>Follows references until a direct object (null for missing objects and cycles).</summary>
    public PdfObject? Resolve(PdfObject? value)
    {
        for (var depth = 0; value is PdfReference reference && depth < MaxResolveDepth; depth++)
        {
            value = Load(reference);
        }

        return value is PdfReference ? null : value;
    }

    /// <summary>A dictionary entry resolved.</summary>
    public PdfObject? Get(PdfDictionary? dictionary, string key) => dictionary is null ? null : Resolve(dictionary[key]);

    public T? Get<T>(PdfDictionary? dictionary, string key)
        where T : PdfObject => Get(dictionary, key) as T;

    public double Number(PdfDictionary? dictionary, string key, double fallback = 0) =>
        Get(dictionary, key) is PdfNumber n ? n.Value : fallback;

    /// <summary>The stream data with every general filter applied (image filters are left in place and reported).</summary>
    public (byte[] Data, string? ImageFilter, PdfDictionary? ImageParameters) Decode(PdfStream stream)
    {
        var parameters = PdfStreams.FilterParameters(stream).Select(p => p).ToList();
        var resolvedParameters = Get(stream, "DecodeParms") switch
        {
            PdfDictionary single => [single],
            PdfArray array => array.Items.Select(i => Resolve(i) as PdfDictionary).ToList(),
            _ => parameters,
        };
        var filters = Get(stream, "Filter") switch
        {
            PdfName name => [name.Value],
            PdfArray array => array.Items.Select(Resolve).OfType<PdfName>().Select(n => n.Value).ToList(),
            _ => new List<string>(),
        };
        return PdfFilters.Decode(stream.Data, filters, resolvedParameters);
    }

    /// <summary>Fully decoded bytes of a stream that has no image filter (content streams, CMaps, fonts).</summary>
    public byte[] DecodeBytes(PdfStream stream) => Decode(stream).Data;

    private PdfObject? Load(PdfReference reference)
    {
        var number = reference.Number;
        if (_cache.TryGetValue(number, out var cached))
        {
            return cached;
        }

        if (!_resolving.Add(number))
        {
            return null;
        }

        try
        {
            var value = LoadUncached(number);
            if (value is not null)
            {
                _cache[number] = value;
            }

            return value;
        }
        finally
        {
            _resolving.Remove(number);
        }
    }

    private PdfObject? LoadUncached(int number)
    {
        if (!_entries.TryGetValue(number, out var entry) || entry.Offset < 0 && !entry.InStream)
        {
            return null;
        }

        if (entry.InStream)
        {
            return FromObjectStream(entry.StreamNumber, number);
        }

        var parsed = ParseAt(entry.Offset, number) ?? ParseAt(FindHeader(number), number);
        if (parsed is not { } found)
        {
            return null;
        }

        var value = found.Value;
        if (_security is not null && (_encryptReference is null || _encryptReference.Number != number))
        {
            value = Decrypt(value, number, found.Generation);
        }

        return value;
    }

    private (int Number, int Generation, PdfObject Value)? ParseAt(long offset, int number)
    {
        if (offset < 0 || offset >= _data.Length)
        {
            return null;
        }

        var parser = new PdfParser(new PdfLexer(_data, offset), ResolveLength);
        var parsed = parser.ReadIndirect();
        return parsed is { } p && p.Number == number ? p : null;
    }

    private int? ResolveLength(PdfReference reference) => Resolve(reference) is PdfNumber n ? n.IntValue : null;

    // A wrong offset: look for the object header anywhere in the file.
    private long FindHeader(int number)
    {
        var marker = System.Text.Encoding.ASCII.GetBytes($"{number} 0 obj");
        var index = _data.AsSpan().LastIndexOf(marker);
        while (index > 0 && _data[index - 1] is >= (byte)'0' and <= (byte)'9')
        {
            index = _data.AsSpan(0, index).LastIndexOf(marker);
        }

        return index;
    }

    private PdfObject? FromObjectStream(int streamNumber, int number)
    {
        if (!_objectStreams.TryGetValue(streamNumber, out var content))
        {
            if (Load(new PdfReference(streamNumber, 0)) is not PdfStream stream)
            {
                return null;
            }

            var data = DecodeBytes(stream);
            var count = PairCount(stream, data);
            var first = (int)Number(stream, "First");
            var lexer = new PdfLexer(data);
            var numbers = new int[count];
            var offsets = new int[count];
            for (var i = 0; i < count; i++)
            {
                numbers[i] = lexer.Next().Value is PdfNumber n ? n.IntValue : -1;
                offsets[i] = lexer.Next().Value is PdfNumber o ? first + o.IntValue : -1;
            }

            content = (numbers, offsets, data);
            _objectStreams[streamNumber] = content;
        }

        var index = Array.IndexOf(content.Numbers, number);
        if (index < 0 || content.Offsets[index] < 0 || content.Offsets[index] >= content.Data.Length)
        {
            return null;
        }

        return new PdfParser(new PdfLexer(content.Data, content.Offsets[index])).ReadObject();
    }

    private PdfObject Decrypt(PdfObject value, int number, int generation)
    {
        switch (value)
        {
            case PdfString s:
                return new PdfString(_security!.DecryptString(s.Bytes, number, generation), s.IsHex);
            case PdfStream stream:
                DecryptEntries(stream, number, generation);
                var xref = stream["Type"] is PdfName { Value: "XRef" };
                var metadata = stream["Type"] is PdfName { Value: "Metadata" } && !_security!.EncryptMetadata;
                if (!xref && !metadata && !IsIdentityCrypt(stream))
                {
                    stream.Data = _security!.DecryptStream(stream.Data, number, generation);
                }

                return stream;
            case PdfDictionary dictionary:
                DecryptEntries(dictionary, number, generation);
                return dictionary;
            case PdfArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    array.Items[i] = Decrypt(array.Items[i], number, generation);
                }

                return array;
            default:
                return value;
        }
    }

    private void DecryptEntries(PdfDictionary dictionary, int number, int generation)
    {
        foreach (var (key, item) in dictionary.Entries.ToList())
        {
            dictionary.Set(key, Decrypt(item, number, generation));
        }
    }

    private static bool IsIdentityCrypt(PdfStream stream) =>
        PdfStreams.FilterNames(stream).Contains("Crypt") && PdfStreams.FilterParameters(stream).Any(p => p?["Name"] is PdfName { Value: "Identity" } || p?["Name"] is null);

    // After a repair scan, objects stored in object streams have no entry yet: list them from each stream.
    private void IndexObjectStreams()
    {
        foreach (var number in ObjectNumbers.ToList())
        {
            if (Load(new PdfReference(number, 0)) is PdfStream stream && stream["Type"] is PdfName { Value: "ObjStm" })
            {
                IndexObjectStream(number, stream);
            }
        }
    }

    // The header of an object stream: N pairs of object number and offset; each object is entry i of the stream.
    private void IndexObjectStream(int number, PdfStream stream)
    {
        var data = DecodeBytes(stream);
        var lexer = new PdfLexer(data);
        var count = PairCount(stream, data);
        for (var i = 0; i < count; i++)
        {
            if (lexer.Next().Value is PdfNumber inner && lexer.Next().Value is PdfNumber)
            {
                _entries.TryAdd(inner.IntValue, new XrefEntry(0, 0, number, i));
            }
        }
    }

    // /N of an object stream, bounded by the pairs its header can hold: each pair is two numbers and their
    // separators, at least three bytes, so a forged count allocates and loops no further than the data.
    private int PairCount(PdfStream stream, byte[] data) => (int)Math.Clamp(Number(stream, "N"), 0, data.Length / 3);

    private PdfDictionary? FindCatalog()
    {
        foreach (var number in ObjectNumbers)
        {
            if (Load(new PdfReference(number, 0)) is PdfDictionary { } dictionary && dictionary["Type"] is PdfName { Value: "Catalog" })
            {
                return dictionary;
            }
        }

        return null;
    }
}
