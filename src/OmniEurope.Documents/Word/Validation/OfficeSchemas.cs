// SPDX-License-Identifier: EUPL-1.2
using System.Xml;
using System.Xml.Schema;

namespace OmniEurope.Documents.Word.Validation;

/// <summary>
/// The Office Open XML schemas shipped in the assembly (ECMA-376 5th edition: Part 4 transitional, Part 1 strict,
/// Part 2 packaging conventions, unmodified, see <c>Word/Schemas/NOTICE-Ecma.txt</c>), compiled once into one set.
/// The transitional and strict schemas have disjoint target namespaces, so one set validates both kinds of
/// document. Validating with a set adds names to its name table, which is not safe across threads: callers hold
/// <see cref="Gate"/> while they validate.
/// </summary>
internal static class OfficeSchemas
{
    private const string Prefix = "OmniEurope.Documents.Word.Schemas.";

    private static readonly Lazy<XmlSchemaSet> Compiled = new(Compile, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Held by every validation that uses <see cref="Set"/>.</summary>
    public static Lock Gate { get; } = new();

    /// <summary>The compiled set.</summary>
    public static XmlSchemaSet Set => Compiled.Value;

    /// <summary>The target namespaces of the shipped schemas: the namespaces a validation understands.</summary>
    public static IReadOnlySet<string> Namespaces => NamespaceSet.Value;

    private static readonly Lazy<HashSet<string>> NamespaceSet = new(
        () => Set.Schemas().Cast<XmlSchema>().Select(s => s.TargetNamespace ?? string.Empty).ToHashSet(StringComparer.Ordinal),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The resource names of the shipped schemas.</summary>
    public static IEnumerable<string> Resources =>
        typeof(OfficeSchemas).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".xsd", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

    /// <summary>The Ecma notice shipped with the schemas.</summary>
    public static string Notice()
    {
        using var stream = Open(Prefix + "NOTICE-Ecma.txt");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static XmlSchemaSet Compile()
    {
        var errors = new List<string>();
        // Imports are not followed (no resolver): every schema they name is added to the set directly.
        var set = new XmlSchemaSet { XmlResolver = null };
        set.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                errors.Add(e.Message);
            }
        };
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        foreach (var name in Resources)
        {
            using var stream = Open(name);
            using var reader = XmlReader.Create(stream, settings);
            var schema = XmlSchema.Read(reader, null)!;
            StrictDefaultsCorrected += CorrectStrictDefaults(schema);
            set.Add(schema);
        }

        set.Add(XmlNamespaceSchema());
        set.Compile();
        return errors.Count == 0 ? set : throw new InvalidOperationException("The bundled Office Open XML schemas do not compile: " + string.Join(" | ", errors));
    }

    /// <summary>How many attribute defaults <see cref="CorrectStrictDefaults"/> corrected while compiling.</summary>
    internal static int StrictDefaultsCorrected { get; private set; }

    // The strict WordprocessingML schema gives three ST_OnOff attributes (beforeAutospacing and afterAutospacing of
    // CT_Spacing, nlCheck of CT_WritingStyle) the default "off", which its own ST_OnOff (xsd:boolean in strict) does not
    // allow, so the schema does not compile as published. The shipped file stays unmodified; in memory those defaults
    // are read as the booleans they mean ("off" is false, "on" is true).
    private static int CorrectStrictDefaults(XmlSchema schema)
    {
        var onOff = new XmlQualifiedName("ST_OnOff", "http://purl.oclc.org/ooxml/officeDocument/sharedTypes");
        var corrected = 0;
        foreach (var attribute in schema.Items.OfType<XmlSchemaComplexType>().SelectMany(t => t.Attributes.OfType<XmlSchemaAttribute>())
            .Where(a => a.SchemaTypeName == onOff && a.DefaultValue is "off" or "on"))
        {
            attribute.DefaultValue = attribute.DefaultValue == "on" ? "true" : "false";
            corrected++;
        }

        return corrected;
    }

    // The xml namespace, of which the schemas use only xml:space (XML 1.0, section 2.10: "default" or "preserve").
    private static XmlSchema XmlNamespaceSchema()
    {
        var values = new XmlSchemaSimpleTypeRestriction { BaseTypeName = new XmlQualifiedName("NCName", XmlSchema.Namespace) };
        values.Facets.Add(new XmlSchemaEnumerationFacet { Value = "default" });
        values.Facets.Add(new XmlSchemaEnumerationFacet { Value = "preserve" });
        var schema = new XmlSchema { TargetNamespace = "http://www.w3.org/XML/1998/namespace" };
        schema.Items.Add(new XmlSchemaAttribute { Name = "space", SchemaType = new XmlSchemaSimpleType { Content = values } });
        return schema;
    }

    private static Stream Open(string name) =>
        typeof(OfficeSchemas).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Bundled schema resource '{name}' is missing from the assembly.");
}
