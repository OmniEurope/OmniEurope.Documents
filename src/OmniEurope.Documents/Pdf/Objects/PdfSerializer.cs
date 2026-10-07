// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text;

namespace OmniEurope.Documents.Pdf.Objects;

/// <summary>Writes PDF objects in PDF syntax.</summary>
internal static class PdfSerializer
{
    public static void Write(PdfObject value, Stream output)
    {
        switch (value)
        {
            case PdfNull:
                Ascii(output, "null");
                break;
            case PdfBoolean b:
                Ascii(output, b.Value ? "true" : "false");
                break;
            case PdfNumber n:
                Ascii(output, n.ToString());
                break;
            case PdfName name:
                WriteName(name.Value, output);
                break;
            case PdfString s:
                WriteString(s, output);
                break;
            case PdfReference r:
                Ascii(output, r.Number.ToString(CultureInfo.InvariantCulture) + " " + r.Generation.ToString(CultureInfo.InvariantCulture) + " R");
                break;
            case PdfArray array:
                output.WriteByte((byte)'[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        output.WriteByte((byte)' ');
                    }

                    Write(array[i], output);
                }

                output.WriteByte((byte)']');
                break;
            case PdfStream stream:
                stream.SetNumber("Length", stream.Data.Length);
                WriteDictionary(stream, output);
                Ascii(output, "\nstream\n");
                output.Write(stream.Data);
                Ascii(output, "\nendstream");
                break;
            case PdfDictionary dictionary:
                WriteDictionary(dictionary, output);
                break;
        }
    }

    public static byte[] ToBytes(PdfObject value)
    {
        using var output = new MemoryStream();
        Write(value, output);
        return output.ToArray();
    }

    private static void WriteDictionary(PdfDictionary dictionary, Stream output)
    {
        Ascii(output, "<<");
        foreach (var (key, value) in dictionary.Entries)
        {
            WriteName(key, output);
            output.WriteByte((byte)' ');
            Write(value, output);
        }

        Ascii(output, ">>");
    }

    // '#' and the PDF delimiters are written as #XX inside a name.
    private static readonly System.Buffers.SearchValues<byte> NameDelimiters = System.Buffers.SearchValues.Create("#/()<>[]{}%"u8);

    public static void WriteName(string name, Stream output)
    {
        output.WriteByte((byte)'/');
        foreach (var b in Encoding.UTF8.GetBytes(name))
        {
            if (b is < 0x21 or > 0x7E || NameDelimiters.Contains(b))
            {
                Ascii(output, "#" + b.ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                output.WriteByte(b);
            }
        }
    }

    private static void WriteString(PdfString value, Stream output)
    {
        if (value.IsHex)
        {
            output.WriteByte((byte)'<');
            Ascii(output, Convert.ToHexString(value.Bytes));
            output.WriteByte((byte)'>');
            return;
        }

        output.WriteByte((byte)'(');
        foreach (var b in value.Bytes)
        {
            switch (b)
            {
                case (byte)'(' or (byte)')' or (byte)'\\':
                    output.WriteByte((byte)'\\');
                    output.WriteByte(b);
                    break;
                case (byte)'\r':
                    Ascii(output, "\\r");
                    break;
                case (byte)'\n':
                    Ascii(output, "\\n");
                    break;
                default:
                    output.WriteByte(b);
                    break;
            }
        }

        output.WriteByte((byte)')');
    }

    public static void Ascii(Stream output, string text)
    {
        Span<byte> buffer = stackalloc byte[Math.Min(text.Length, 256)];
        var position = 0;
        while (position < text.Length)
        {
            var count = Math.Min(buffer.Length, text.Length - position);
            for (var i = 0; i < count; i++)
            {
                buffer[i] = (byte)text[position + i];
            }

            output.Write(buffer[..count]);
            position += count;
        }
    }
}
