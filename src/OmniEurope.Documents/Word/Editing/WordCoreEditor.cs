// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word.Writing;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>Updates the core properties part element by element, keeping the elements it does not model.</summary>
internal static class WordCoreEditor
{
    public static void Apply(OpcPackage package, string? part, WordInformation information)
    {
        if (part is null)
        {
            part = "docProps/core.xml";
            package.SetXml(part, new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), WordPartsWriter.Core(new WordInformation())), CoreContentType);
            package.AddRelationship(string.Empty, CorePropertiesType, part);
        }

        var root = package.GetXml(part)?.Root ?? throw new DocumentFormatException("The core properties part is empty.");
        Set(root, Dc + "title", information.Title);
        Set(root, Dc + "subject", information.Subject);
        Set(root, Dc + "creator", information.Author);
        Set(root, Cp + "keywords", information.Keywords);
        Set(root, Dc + "description", information.Description);
        Set(root, Cp + "category", information.Category);
        Set(root, Cp + "lastModifiedBy", information.LastModifiedBy);
        SetDate(root, "created", information.Created);
        SetDate(root, "modified", information.Modified);
    }

    private static void Set(XElement root, XName name, string? value)
    {
        var element = root.Element(name);
        if (value is null)
        {
            element?.Remove();
            return;
        }

        value = WordContentWriter.Clean(value);
        if (element is null)
        {
            root.Add(new XElement(name, value));
        }
        else if (element.Value != value)
        {
            element.Value = value;
        }
    }

    private static void SetDate(XElement root, string name, DateTimeOffset? value)
    {
        var text = value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var element = root.Element(DcTerms + name);
        if (text is null)
        {
            element?.Remove();
        }
        else if (element is null)
        {
            root.Add(new XElement(DcTerms + name, new XAttribute(Xsi + "type", "dcterms:W3CDTF"), text));
        }
        else if (ParseDate(element.Value) != value)
        {
            element.Value = text;
        }
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
}
