// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;

namespace OmniEurope.Documents.Pdf;

/// <summary>A rectangle in PDF user space (points, origin at the bottom-left of the page).</summary>
public readonly record struct PdfRectangle(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Width.</summary>
    public double Width => Right - Left;

    /// <summary>Height.</summary>
    public double Height => Top - Bottom;

    /// <summary>The rectangle with its corners ordered.</summary>
    public PdfRectangle Normalize() => new(Math.Min(Left, Right), Math.Min(Bottom, Top), Math.Max(Left, Right), Math.Max(Bottom, Top));

    /// <summary>The smallest rectangle containing both.</summary>
    public PdfRectangle Union(PdfRectangle other) =>
        new(Math.Min(Left, other.Left), Math.Min(Bottom, other.Bottom), Math.Max(Right, other.Right), Math.Max(Top, other.Top));
}

/// <summary>The document information dictionary.</summary>
public sealed record PdfInformation(string? Title, string? Author, string? Subject, string? Keywords, string? Creator, string? Producer,
    DateTimeOffset? CreationDate, DateTimeOffset? ModificationDate);

/// <summary>
/// An existing PDF opened for reading: pages, their text (letters with positions) and images, and the
/// document metadata. Damaged cross-reference tables are rebuilt; encrypted files open with the user or
/// owner password (the empty password by default).
/// </summary>
public sealed class PdfDocument
{
    private readonly List<PdfPage> _pages;

    private PdfDocument(byte[] data, string? password)
    {
        if (data.Length < 8 || data.AsSpan(0, Math.Min(data.Length, 1024)).IndexOf("%PDF-"u8) < 0)
        {
            throw new InvalidDataException("Not a PDF file.");
        }

        Store = new PdfObjectStore(data, password);
        _pages = PdfPageTree.Collect(this);
        Information = ReadInformation();
        Version = ReadVersion(data);
    }

    /// <summary>The number of pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>The pages in order.</summary>
    public IReadOnlyList<PdfPage> Pages => _pages;

    /// <summary>The metadata.</summary>
    public PdfInformation Information { get; }

    /// <summary>The PDF version (header, or the catalog's when later).</summary>
    public string Version { get; }

    /// <summary>True when the file is encrypted.</summary>
    public bool IsEncrypted => Store.IsEncrypted;

    /// <summary>
    /// The permissions the file grants to the user password (its P entry); every permission for a file that is not
    /// encrypted. Opened with the owner password, the reader may do everything whatever this says.
    /// </summary>
    public PdfPermissions Permissions => Store.Security is { } security ? (PdfPermissions)security.Permissions & PdfPermissions.All : PdfPermissions.All;

    /// <summary>True when the file is not encrypted or was opened with its owner password.</summary>
    public bool OpenedAsOwner => Store.Security?.OpenedAsOwner ?? true;

    /// <summary>True when the cross-reference information was damaged and rebuilt from a scan.</summary>
    public bool WasRepaired => Store.Repaired;

    internal PdfObjectStore Store { get; }

    /// <summary>Opens a PDF.</summary>
    /// <exception cref="InvalidDataException">The data is not a readable PDF.</exception>
    /// <exception cref="PdfPasswordException">The password does not open the encrypted file.</exception>
    public static PdfDocument Open(byte[] data, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new PdfDocument(data, password);
    }

    /// <summary>Opens a PDF from a stream (read to its end).</summary>
    public static PdfDocument Open(Stream stream, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return new PdfDocument(copy.ToArray(), password);
    }

    /// <summary>The page with a 1-based number.</summary>
    public PdfPage GetPage(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, PageCount);
        return _pages[number - 1];
    }

    private PdfInformation ReadInformation()
    {
        var info = Store.Resolve(Store.Trailer["Info"]) as PdfDictionary;
        string? Text(string key) => Store.Get(info, key) is PdfString s ? s.ToText().TrimEnd('\0') : null;
        return new PdfInformation(Text("Title"), Text("Author"), Text("Subject"), Text("Keywords"), Text("Creator"), Text("Producer"),
            ParseDate(Text("CreationDate")), ParseDate(Text("ModDate")));
    }

    private string ReadVersion(byte[] data)
    {
        var start = data.AsSpan(0, Math.Min(data.Length, 1024)).IndexOf("%PDF-"u8);
        var header = Encoding.ASCII.GetString(data, start + 5, Math.Min(3, data.Length - start - 5));
        var catalog = Store.Get(Store.Catalog, "Version") is PdfName name ? name.Value : null;
        return catalog is not null && string.CompareOrdinal(catalog, header) > 0 ? catalog : header;
    }

    /// <summary>Parses a PDF date (<c>D:YYYYMMDDHHmmSSOHH'mm'</c>, every part after the year optional).</summary>
    internal static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith("D:", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        int Part(int start, int length, int fallback) =>
            value.Length >= start + length && int.TryParse(value.AsSpan(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        var year = Part(0, 4, 0);
        if (year < 1)
        {
            return null;
        }

        var offset = TimeSpan.Zero;
        if (value.Length > 14 && value[14] is '+' or '-')
        {
            var sign = value[14] == '-' ? -1 : 1;
            offset = new TimeSpan(sign * Part(15, 2, 0), sign * Part(18, 2, 0), 0);
        }

        try
        {
            return new DateTimeOffset(year, Math.Clamp(Part(4, 2, 1), 1, 12), Math.Clamp(Part(6, 2, 1), 1, 31), Math.Min(Part(8, 2, 0), 23),
                Math.Min(Part(10, 2, 0), 59), Math.Min(Part(12, 2, 0), 59), offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
