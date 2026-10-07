// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Pdf.Objects;

/// <summary>A PDF object (ISO 32000-1 §7.3).</summary>
public abstract class PdfObject
{
    internal PdfObject()
    {
    }
}

/// <summary>The null object.</summary>
public sealed class PdfNull : PdfObject
{
    private PdfNull()
    {
    }

    /// <summary>The only instance.</summary>
    public static PdfNull Instance { get; } = new();
}

/// <summary>A boolean.</summary>
public sealed class PdfBoolean : PdfObject
{
    private PdfBoolean(bool value) => Value = value;

    /// <summary>True.</summary>
    public static PdfBoolean True { get; } = new(true);

    /// <summary>False.</summary>
    public static PdfBoolean False { get; } = new(false);

    /// <summary>The value.</summary>
    public bool Value { get; }

    /// <summary>The instance for a value.</summary>
    public static PdfBoolean Of(bool value) => value ? True : False;
}

/// <summary>An integer or real number.</summary>
public sealed class PdfNumber(double value, bool isInteger) : PdfObject
{
    /// <summary>The value.</summary>
    public double Value { get; } = value;

    /// <summary>True when written without a decimal point.</summary>
    public bool IsInteger { get; } = isInteger;

    /// <summary>The value as an integer (rounded toward zero).</summary>
    public int IntValue => (int)Math.Clamp(Value, int.MinValue, int.MaxValue);

    /// <summary>An integer.</summary>
    public static PdfNumber Of(long value) => new(value, true);

    /// <summary>A real number.</summary>
    public static PdfNumber Of(double value) => new(value, false);

    /// <inheritdoc />
    public override string ToString() => IsInteger ? ((long)Value).ToString(CultureInfo.InvariantCulture) : PdfFormat.Real(Value);
}

/// <summary>A string: raw bytes, written literal or hexadecimal.</summary>
public sealed class PdfString(byte[] bytes, bool hex = false) : PdfObject
{
    /// <summary>The bytes.</summary>
    public byte[] Bytes { get; } = bytes;

    /// <summary>True when written as &lt;hex&gt;.</summary>
    public bool IsHex { get; } = hex;

    /// <summary>A text string: PDFDocEncoding-compatible Latin-1 when possible, else UTF-16BE with a byte
    /// order mark.</summary>
    public static PdfString FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.All(c => c is >= ' ' and <= '~' or '\n' or '\r' or '\t'))
        {
            return new PdfString(Encoding.ASCII.GetBytes(text));
        }

        return new PdfString([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)], hex: true);
    }

    /// <summary>Decodes a text string (UTF-16BE with BOM, UTF-8 with BOM, else PDFDocEncoding).</summary>
    public string ToText() => PdfTextEncoding.Decode(Bytes);

    /// <inheritdoc />
    public override string ToString() => ToText();
}

/// <summary>A name (without its leading slash).</summary>
public sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private static readonly Dictionary<string, PdfName> Common = [];

    private PdfName(string value) => Value = value;

    /// <summary>The name.</summary>
    public string Value { get; }

    /// <summary>The name object for <paramref name="value"/>.</summary>
    public static PdfName Of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (Common)
        {
            if (!Common.TryGetValue(value, out var name))
            {
                name = new PdfName(value);
                if (Common.Count < 4096)
                {
                    Common[value] = name;
                }
            }

            return name;
        }
    }

    /// <inheritdoc />
    public bool Equals(PdfName? other) => other is not null && other.Value == Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfName);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => "/" + Value;
}

/// <summary>An array.</summary>
public sealed class PdfArray : PdfObject
{
    /// <summary>Creates an array.</summary>
    public PdfArray(params IEnumerable<PdfObject> items) => Items = [.. items];

    /// <summary>The elements.</summary>
    public List<PdfObject> Items { get; }

    /// <summary>The number of elements.</summary>
    public int Count => Items.Count;

    /// <summary>The element at <paramref name="index"/>.</summary>
    public PdfObject this[int index] => Items[index];

    /// <summary>An array of numbers.</summary>
    public static PdfArray OfNumbers(params IEnumerable<double> values) => new(values.Select(v => (PdfObject)(v == Math.Floor(v) && Math.Abs(v) < 1e15 ? PdfNumber.Of((long)v) : PdfNumber.Of(v))));
}

/// <summary>A dictionary with name keys, in insertion order.</summary>
public class PdfDictionary : PdfObject
{
    private readonly List<KeyValuePair<string, PdfObject>> _entries = [];

    /// <summary>The entries, keys without slash.</summary>
    public IReadOnlyList<KeyValuePair<string, PdfObject>> Entries => _entries;

    /// <summary>The value of a key, or null.</summary>
    public PdfObject? this[string key]
    {
        get => _entries.Find(e => e.Key == key).Value;
        set => Set(key, value);
    }

    /// <summary>True when the key exists.</summary>
    public bool ContainsKey(string key) => _entries.Exists(e => e.Key == key);

    /// <summary>Sets or (with null) removes a key; returns this dictionary.</summary>
    public PdfDictionary Set(string key, PdfObject? value)
    {
        var index = _entries.FindIndex(e => e.Key == key);
        if (value is null)
        {
            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }

            return this;
        }

        if (index >= 0)
        {
            _entries[index] = new KeyValuePair<string, PdfObject>(key, value);
        }
        else
        {
            _entries.Add(new KeyValuePair<string, PdfObject>(key, value));
        }

        return this;
    }

    /// <summary>Sets a name value.</summary>
    public PdfDictionary SetName(string key, string name) => Set(key, PdfName.Of(name));

    /// <summary>Sets a number value.</summary>
    public PdfDictionary SetNumber(string key, double value) => Set(key, value == Math.Floor(value) && Math.Abs(value) < 1e15 ? PdfNumber.Of((long)value) : PdfNumber.Of(value));

    /// <summary>Removes a key.</summary>
    public bool Remove(string key) => _entries.RemoveAll(e => e.Key == key) > 0;
}

/// <summary>A stream: a dictionary and its (encoded) data.</summary>
public sealed class PdfStream : PdfDictionary
{
    /// <summary>Creates a stream holding <paramref name="data"/> as stored (already encoded by its filters).</summary>
    public PdfStream(byte[] data) => Data = data;

    /// <summary>The stored bytes (encoded by the filters named in the dictionary).</summary>
    public byte[] Data { get; set; }
}

/// <summary>An indirect reference (<c>12 0 R</c>).</summary>
public sealed class PdfReference(int number, int generation) : PdfObject, IEquatable<PdfReference>
{
    /// <summary>The object number.</summary>
    public int Number { get; } = number;

    /// <summary>The generation number.</summary>
    public int Generation { get; } = generation;

    /// <inheritdoc />
    public bool Equals(PdfReference? other) => other is not null && other.Number == Number && other.Generation == Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfReference);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Number, Generation);

    /// <inheritdoc />
    public override string ToString() => $"{Number} {Generation} R";
}

/// <summary>Number formatting for PDF syntax.</summary>
internal static class PdfFormat
{
    /// <summary>A real with at most 4 decimals, no exponent, no trailing zeros.</summary>
    public static string Real(double value)
    {
        if (!double.IsFinite(value))
        {
            return "0";
        }

        var text = Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }
}
