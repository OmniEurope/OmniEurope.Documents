// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OmniEurope.Documents.Internal;

/// <summary>
/// Writes an Open Packaging Conventions ZIP (DOCX, XLSX): parts written through <see cref="XmlWriter"/>,
/// fixed entry timestamps so equal documents give equal bytes.
/// </summary>
internal sealed class OpenXmlPackageWriter : IDisposable
{
    private static readonly DateTimeOffset FixedTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        NewLineHandling = NewLineHandling.Entitize,
        Indent = false,
        CheckCharacters = true,
    };

    private readonly ZipArchive _archive;

    public OpenXmlPackageWriter(Stream stream, bool leaveOpen = true)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen, Encoding.UTF8);
    }

    public void WriteXml(string path, Action<XmlWriter> write)
    {
        var entry = _archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedTime;
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, Settings);
        writer.WriteStartDocument(standalone: true);
        write(writer);
        writer.WriteEndDocument();
    }

    public void WriteBytes(string path, ReadOnlySpan<byte> bytes, bool compress = true)
    {
        var entry = _archive.CreateEntry(path, compress ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
        entry.LastWriteTime = FixedTime;
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    public void Dispose() => _archive.Dispose();

    /// <summary>
    /// Makes text safe for an XML element: characters XML forbids become Office escapes <c>_xHHHH_</c>, a
    /// literal escape-like sequence is protected with <c>_x005F_</c>, lone surrogates become U+FFFD.
    /// </summary>
    public static string EscapeOfficeText(string text)
    {
        if (!NeedsOfficeEscape(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(c).Append(text[++i]);
            }
            else if (Escaped(text, i) is { } escaped)
            {
                builder.Append(escaped);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // The escaped form of text[i] (not part of a surrogate pair), or null when it is stored as is: a '_' that
    // would read as an escape, a lone surrogate, or a character XML 1.0 cannot carry.
    private static string? Escaped(string text, int i)
    {
        var c = text[i];
        if (c == '_')
        {
            return IsEscapeSequence(text, i) ? "_x005F_" : null;
        }

        if (char.IsSurrogate(c))
        {
            return "�";
        }

        return (c < 0x20 && c is not ('\t' or '\n' or '\r')) || c >= 0xFFFE
            ? "_x" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + "_"
            : null;
    }

    /// <summary>Reverses <see cref="EscapeOfficeText"/>.</summary>
    public static string UnescapeOfficeText(string text)
    {
        if (!text.Contains("_x", StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '_' && IsEscapeSequence(text, i))
            {
                builder.Append((char)Convert.ToInt32(text.Substring(i + 2, 4), 16));
                i += 6;
            }
            else
            {
                builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }

    private static bool NeedsOfficeEscape(string text)
    {
        foreach (var c in text)
        {
            if (c < 0x20 || c == '_' || char.IsSurrogate(c) || c >= '￾')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEscapeSequence(string text, int i) =>
        i + 6 < text.Length && text[i + 1] == 'x' && text[i + 6] == '_'
        && char.IsAsciiHexDigit(text[i + 2]) && char.IsAsciiHexDigit(text[i + 3])
        && char.IsAsciiHexDigit(text[i + 4]) && char.IsAsciiHexDigit(text[i + 5]);
}
