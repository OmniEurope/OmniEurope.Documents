// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Security;
using System.Text;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>The document properties an XMP packet mirrors from the information dictionary.</summary>
internal sealed record XmpProperties(string? Title, string? Author, string? Subject, string? Keywords, string? Creator, string Producer, DateTimeOffset? CreationDate);

/// <summary>
/// The XMP metadata packet of a PDF/A file (ISO 19005-2 §6.6): the PDF/A identification (<c>pdfaid:part</c>,
/// <c>pdfaid:conformance</c>) and the information dictionary entries in their XMP equivalents (§6.6.3, table 4),
/// only predefined schemas (Dublin Core, XMP basic, Adobe PDF, PDF/A identification), in UTF-8 with an
/// <c>xpacket</c> header that has neither <c>bytes</c> nor <c>encoding</c> attribute.
/// </summary>
internal static class XmpPacket
{
    public static byte[] Create(XmpProperties properties, int part, string conformance)
    {
        var xmp = new StringBuilder();
        xmp.Append("<?xpacket begin=\"").Append((char)0xFEFF).Append("\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        xmp.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        xmp.Append("<rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"");
        xmp.Append(" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
        xmp.Append(CultureInfo.InvariantCulture, $"<pdfaid:part>{part}</pdfaid:part>\n<pdfaid:conformance>{conformance}</pdfaid:conformance>\n");
        xmp.Append("<dc:format>application/pdf</dc:format>\n");
        Alternative(xmp, "dc:title", properties.Title);
        if (!string.IsNullOrEmpty(properties.Author))
        {
            xmp.Append("<dc:creator><rdf:Seq><rdf:li>").Append(Escape(properties.Author)).Append("</rdf:li></rdf:Seq></dc:creator>\n");
        }

        Alternative(xmp, "dc:description", properties.Subject);
        Simple(xmp, "pdf:Keywords", properties.Keywords);
        Simple(xmp, "pdf:Producer", properties.Producer);
        Simple(xmp, "xmp:CreatorTool", properties.Creator);
        if (properties.CreationDate is { } date)
        {
            Simple(xmp, "xmp:CreateDate", date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
        }

        xmp.Append("</rdf:Description>\n</rdf:RDF>\n</x:xmpmeta>\n<?xpacket end=\"w\"?>");
        return Encoding.UTF8.GetBytes(xmp.ToString());
    }

    private static void Alternative(StringBuilder xmp, string element, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            xmp.Append('<').Append(element).Append("><rdf:Alt><rdf:li xml:lang=\"x-default\">").Append(Escape(value)).Append("</rdf:li></rdf:Alt></").Append(element).Append(">\n");
        }
    }

    private static void Simple(StringBuilder xmp, string element, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            xmp.Append('<').Append(element).Append('>').Append(Escape(value)).Append("</").Append(element).Append(">\n");
        }
    }

    // XML text: markup characters escaped.
    private static string Escape(string value) => SecurityElement.Escape(value)!;

    /// <summary>The text with the characters XML 1.0 cannot carry left out (control characters, U+FFFE, U+FFFF).</summary>
    public static string Clean(string value) => new(value.Where(c => c is (char)9 or (char)10 or (char)13 or (>= (char)32 and not ((char)0xFFFE or (char)0xFFFF))).ToArray());
}
