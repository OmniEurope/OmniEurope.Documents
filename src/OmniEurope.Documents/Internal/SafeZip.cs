// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Xml;

namespace OmniEurope.Documents.Internal;

/// <summary>
/// Read access to the parts of a ZIP package with zip-bomb protection: entry count, claimed and actual
/// inflated sizes and compression ratio are all checked, and XML is read with DTDs prohibited.
/// </summary>
internal sealed class SafeZip : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly PackageLimits _limits;
    private long _totalRead;

    private SafeZip(ZipArchive archive, PackageLimits limits)
    {
        _archive = archive;
        _limits = limits;
    }

    public static SafeZip Open(Stream stream, PackageLimits? limits = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        limits ??= PackageLimits.Default;
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        }
        catch (InvalidDataException exception)
        {
            throw new DocumentFormatException("The file is not a valid ZIP package.", exception);
        }

        if (archive.Entries.Count > limits.MaxEntries)
        {
            archive.Dispose();
            throw new DocumentFormatException($"The package holds more than {limits.MaxEntries} entries.");
        }

        return new SafeZip(archive, limits);
    }

    public IEnumerable<string> EntryNames => _archive.Entries.Select(e => e.FullName);

    public bool Contains(string name) => Find(name) is not null;

    public byte[]? ReadBytes(string name)
    {
        var entry = Find(name);
        if (entry is null)
        {
            return null;
        }

        CheckClaims(entry);
        using var input = entry.Open();
        using var output = new MemoryStream((int)Math.Min(entry.Length, 1 << 20));
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            read += n;
            _totalRead += n;
            if (read > _limits.MaxPartSize || _totalRead > _limits.MaxTotalSize)
            {
                throw new DocumentFormatException($"Part '{name}' inflates beyond the allowed size.");
            }

            output.Write(buffer, 0, n);
        }

        return output.ToArray();
    }

    public XmlReader? ReadXml(string name)
    {
        var bytes = ReadBytes(name);
        return bytes is null ? null : CreateXmlReader(new MemoryStream(bytes));
    }

    public static XmlReader CreateXmlReader(Stream stream) => XmlReader.Create(stream, new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
    });

    public void Dispose() => _archive.Dispose();

    private ZipArchiveEntry? Find(string name)
    {
        var normalized = name.TrimStart('/');
        return _archive.Entries.FirstOrDefault(e => e.FullName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private void CheckClaims(ZipArchiveEntry entry)
    {
        if (entry.Length > _limits.MaxPartSize)
        {
            throw new DocumentFormatException($"Part '{entry.FullName}' is larger than allowed.");
        }

        if (entry.Length > (1 << 20) && entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > _limits.MaxCompressionRatio)
        {
            throw new DocumentFormatException($"Part '{entry.FullName}' has a suspicious compression ratio.");
        }
    }
}
