// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Word.Validation;

/// <summary>
/// Validates the XML parts of a Word package against the Office Open XML schemas of ECMA-376 5th edition shipped in
/// the package (transitional WordprocessingML and the DrawingML, VML, math, shared types and document properties
/// schemas it uses; strict WordprocessingML; the content types and relationships schemas of the packaging
/// conventions). Markup compatibility (ECMA-376 Part 3) is processed first, as by a consumer that understands the
/// namespaces of those schemas: extensions declared ignorable (<c>w14</c>, <c>w15</c>...) are removed and
/// <c>mc:AlternateContent</c> resolves to the choice it can read or to its fallback.
/// </summary>
/// <remarks>
/// A part whose root namespace no shipped schema covers (core properties, custom XML data, application extensions) is
/// read as XML and listed in <see cref="WordSchemaReport.UncheckedParts"/>. The schemas are compiled once, on the
/// first validation (a few hundred milliseconds); validations then run one at a time, the compiled schemas not being
/// safe for concurrent use. Beyond the schemas, each attribute of the relationships namespace (<c>r:id</c>,
/// <c>r:embed</c>...) must name a relationship its part declares; the targets of relationships, the content type of
/// each part and the other rules of the standard that the schemas do not express are not checked here. The
/// validation follows the standard, not one application: markup some word processors write outside it without
/// declaring it ignorable (the <c>o:gfxdata</c> attribute of VML shapes, a <c>Version</c> attribute on bibliography
/// sources) is reported.
/// </remarks>
public static class WordSchemaValidator
{
    /// <summary>Validates a Word package held in memory.</summary>
    /// <exception cref="DocumentFormatException">The bytes are not a package, or break the limits.</exception>
    public static WordSchemaReport Validate(byte[] package, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        using var stream = new MemoryStream(package, writable: false);
        return Validate(stream, limits);
    }

    /// <summary>Validates a Word package read from a stream.</summary>
    /// <exception cref="DocumentFormatException">The stream is not a package, or breaks the limits.</exception>
    public static WordSchemaReport Validate(Stream package, PackageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        var opc = OpcPackage.Open(package, limits);
        var errors = new List<WordSchemaError>();
        var checkedParts = new List<string>();
        var uncheckedParts = new List<string>();
        foreach (var name in opc.PartNames.Where(n => IsXml(opc, n)))
        {
            if (Parse(opc, name, errors) is not { Root: { } root } document)
            {
                continue;
            }

            if (!OfficeSchemas.Namespaces.Contains(root.Name.NamespaceName))
            {
                uncheckedParts.Add(name);
                continue;
            }

            checkedParts.Add(name);
            ValidatePart(opc, name, document, errors);
        }

        return new WordSchemaReport(errors, checkedParts, uncheckedParts);
    }

    private static void ValidatePart(OpcPackage opc, string name, XDocument document, List<WordSchemaError> errors)
    {
        void Report(XObject node, string message) => errors.Add(Error(name, node, message));

        // The packaging conventions forbid markup compatibility in the content types and relationships parts.
        if (!IsPackagingPart(name))
        {
            MarkupCompatibility.Process(document.Root!, Understood, Report);
            CheckRelationshipIds(opc, name, document, Report);
        }

        lock (OfficeSchemas.Gate)
        {
            document.Validate(OfficeSchemas.Set, (sender, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    Report(sender as XObject ?? document, e.Message);
                }
            });
        }
    }

    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string StrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";

    // Every attribute of the relationships namespace (r:id, r:embed, r:link...) holds the id of a relationship of its
    // part (ST_RelationshipId): an id the part's relationships do not declare names nothing.
    private static void CheckRelationshipIds(OpcPackage opc, string name, XDocument document, Action<XObject, string> report)
    {
        var references = document.Descendants().Attributes().Where(a => a.Name.NamespaceName is RelationshipsNamespace or StrictRelationshipsNamespace).ToList();
        if (references.Count == 0)
        {
            return;
        }

        var declared = opc.Relationships(name).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var reference in references.Where(r => !declared.Contains(r.Value)))
        {
            report(reference, $"The relationship id '{reference.Value}' is not declared in {OpcRelationships.RelationshipsPath(name)}.");
        }
    }

    private static bool Understood(string ns) => ns.Length == 0 || ns == XNamespace.Xml.NamespaceName || OfficeSchemas.Namespaces.Contains(ns);

    private static bool IsPackagingPart(string name) =>
        name == OpcPackage.ContentTypesPart || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);

    // The XML parts: the content types part, relationships, and every part whose name or content type says XML, except
    // pictures (an SVG picture is XML, but no markup of the package).
    private static bool IsXml(OpcPackage opc, string name)
    {
        if (IsPackagingPart(name))
        {
            return true;
        }

        var type = opc.ContentTypeOf(name);
        if (type is not null && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || (type is not null && (type.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) || type is "application/xml" or "text/xml"));
    }

    private static XDocument? Parse(OpcPackage opc, string name, List<WordSchemaError> errors)
    {
        try
        {
            return OpcPackage.ParseXml(name, opc.GetBytes(name) ?? [], LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (DocumentFormatException exception)
        {
            var xml = exception.InnerException as XmlException;
            errors.Add(new WordSchemaError(name, xml?.LineNumber ?? 0, xml?.LinePosition ?? 0, string.Empty, xml?.Message ?? exception.Message));
            return null;
        }
    }

    private static WordSchemaError Error(string part, XObject node, string message)
    {
        var info = (IXmlLineInfo)node;
        return new WordSchemaError(part, info.LineNumber, info.LinePosition, PathOf(node), message);
    }

    // The path of an element or attribute: each step its qualified name and its index among the siblings of that name.
    internal static string PathOf(XObject node)
    {
        var element = node as XElement ?? node.Parent;
        var steps = new List<string>();
        for (var current = element; current is not null; current = current.Parent)
        {
            var index = current.ElementsBeforeSelf(current.Name).Count() + 1;
            var name = current.Name.Namespace == current.GetDefaultNamespace() ? current.Name.LocalName : QualifiedName(current, current.Name);
            steps.Add($"{name}[{index}]");
        }

        var path = new StringBuilder();
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            path.Append('/').Append(steps[i]);
        }

        if (node is XAttribute attribute && element is not null)
        {
            path.Append("/@").Append(QualifiedName(element, attribute.Name));
        }

        return path.ToString();
    }

    private static string QualifiedName(XElement scope, XName name)
    {
        if (name.Namespace == XNamespace.None)
        {
            return name.LocalName;
        }

        var prefix = scope.GetPrefixOfNamespace(name.Namespace);
        return prefix is null ? name.ToString() : prefix + ":" + name.LocalName;
    }
}
