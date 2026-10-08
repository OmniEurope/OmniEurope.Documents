// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OmniEurope.Documents.Internal;

/// <summary>
/// An Open Packaging Conventions package held in memory for reading and editing. Parts are kept as their
/// original bytes and parsed to <see cref="XDocument"/> on demand; saving writes untouched parts byte for
/// byte and re-serializes only the parts that changed.
/// </summary>
internal sealed class OpcPackage
{
    public const string ContentTypesPart = "[Content_Types].xml";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string RelationshipsContentType = "application/vnd.openxmlformats-package.relationships+xml";

    // Deepest element nesting accepted in a part. Readers walk the XML recursively (wrappers, tables in
    // tables), so a part nesting thousands of elements is refused instead of overflowing the stack.
    private const int MaxXmlDepth = 256;

    private readonly List<string> _order = [];
    private readonly Dictionary<string, byte[]> _raw = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XDocument> _xml = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _changed = new(StringComparer.OrdinalIgnoreCase);

    private OpcPackage()
    {
    }

    /// <summary>Reads every part through the zip-bomb limits.</summary>
    public static OpcPackage Open(Stream stream, PackageLimits? limits)
    {
        using var zip = SafeZip.Open(stream, limits, leaveOpen: true);
        var package = new OpcPackage();
        foreach (var name in zip.EntryNames.Where(n => !n.EndsWith('/')).ToList())
        {
            if (package._raw.ContainsKey(name))
            {
                throw new DocumentFormatException($"The package holds part '{name}' twice.");
            }

            package._order.Add(name);
            package._raw[name] = zip.ReadBytes(name)!;
        }

        if (!package.Contains(ContentTypesPart))
        {
            throw new DocumentFormatException("The package has no content types part.");
        }

        return package;
    }

    /// <summary>The part names in package order.</summary>
    public IReadOnlyList<string> PartNames => _order;

    /// <summary>Parts added or modified since the package was opened.</summary>
    public IReadOnlyCollection<string> ChangedParts => _changed;

    public bool Contains(string name) => _raw.ContainsKey(Normalize(name));

    /// <summary>The current bytes of a part (a parsed part that changed is serialized).</summary>
    public byte[]? GetBytes(string name)
    {
        name = Normalize(name);
        if (_xml.TryGetValue(name, out var document) && _changed.Contains(name))
        {
            return Serialize(document);
        }

        return _raw.GetValueOrDefault(name);
    }

    /// <summary>The parsed part (DTDs prohibited); edits made to it mark the part as changed.</summary>
    public XDocument? GetXml(string name)
    {
        name = Normalize(name);
        if (_xml.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (!_raw.TryGetValue(name, out var bytes))
        {
            return null;
        }

        XDocument document;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            // Building the tree costs more with each level, so the depth is checked by a plain read first.
            using (var scan = XmlReader.Create(new MemoryStream(bytes), settings))
            {
                while (scan.Read())
                {
                    if (scan.NodeType == XmlNodeType.Element && scan.Depth >= MaxXmlDepth)
                    {
                        throw new DocumentFormatException($"Part '{name}' nests elements deeper than {MaxXmlDepth} levels.");
                    }
                }
            }

            using var reader = XmlReader.Create(new MemoryStream(bytes), settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new DocumentFormatException($"Part '{name}' is not well-formed XML.", exception);
        }

        var key = name;
        document.Changed += (_, _) => _changed.Add(key);
        _xml[name] = document;
        return document;
    }

    /// <summary>Adds or replaces an XML part.</summary>
    public void SetXml(string name, XDocument document, string? contentType = null)
    {
        name = Normalize(name);
        Register(name);
        _raw[name] = [];
        _xml[name] = document;
        _changed.Add(name);
        var key = name;
        document.Changed += (_, _) => _changed.Add(key);
        if (contentType is not null)
        {
            SetContentType(name, contentType);
        }
    }

    /// <summary>Adds or replaces a binary part.</summary>
    public void SetBytes(string name, byte[] bytes, string? contentType = null)
    {
        name = Normalize(name);
        Register(name);
        _xml.Remove(name);
        _raw[name] = bytes;
        _changed.Add(name);
        if (contentType is not null)
        {
            SetContentType(name, contentType);
        }
    }

    /// <summary>
    /// Removes a part with its own relationships, its content type override and every relationship of the package
    /// that targets it.
    /// </summary>
    public void RemovePart(string name)
    {
        name = Normalize(name);
        if (!_raw.ContainsKey(name))
        {
            return;
        }

        foreach (var part in new[] { name, OpcRelationships.RelationshipsPath(name) })
        {
            _order.Remove(part);
            _raw.Remove(part);
            _xml.Remove(part);
            _changed.Remove(part);
        }

        GetXml(ContentTypesPart)!.Root!.Elements(Ct + "Override")
            .Where(e => string.Equals(Normalize((string?)e.Attribute("PartName") ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
            .Remove();
        foreach (var source in _order.Where(p => p.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var owner = OpcRelationships.OwnerOf(source);
            var targets = Relationships(owner).Where(r => !r.External && string.Equals(r.Target, name, StringComparison.OrdinalIgnoreCase)).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            if (targets.Count > 0)
            {
                GetXml(source)!.Root!.Elements(Rel + "Relationship").Where(e => targets.Contains((string?)e.Attribute("Id") ?? string.Empty)).Remove();
            }
        }
    }

    /// <summary>The content type of a part: its override, else the default of its extension.</summary>
    public string? ContentTypeOf(string name)
    {
        name = Normalize(name);
        var types = GetXml(ContentTypesPart)!.Root!;
        var match = types.Elements(Ct + "Override").FirstOrDefault(e => string.Equals(Normalize((string?)e.Attribute("PartName") ?? string.Empty), name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return (string?)match.Attribute("ContentType");
        }

        var extension = Path.GetExtension(name).TrimStart('.');
        return (string?)types.Elements(Ct + "Default").FirstOrDefault(e => string.Equals((string?)e.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase))?.Attribute("ContentType");
    }

    /// <summary>All content types declared (defaults and overrides).</summary>
    public IEnumerable<string> DeclaredContentTypes =>
        GetXml(ContentTypesPart)!.Root!.Elements().Select(e => (string?)e.Attribute("ContentType")).OfType<string>();

    /// <summary>Gives a part its own content type, or a default for its extension when that is enough.</summary>
    public void SetContentType(string name, string contentType)
    {
        name = Normalize(name);
        if (string.Equals(ContentTypeOf(name), contentType, StringComparison.Ordinal))
        {
            return;
        }

        var types = GetXml(ContentTypesPart)!.Root!;
        var extension = Path.GetExtension(name).TrimStart('.');
        var hasDefault = types.Elements(Ct + "Default").Any(e => string.Equals((string?)e.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase));
        if (!hasDefault && !name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && extension.Length > 0)
        {
            types.AddFirst(new XElement(Ct + "Default", new XAttribute("Extension", extension.ToLowerInvariant()), new XAttribute("ContentType", contentType)));
            return;
        }

        types.Elements(Ct + "Override").Where(e => string.Equals(Normalize((string?)e.Attribute("PartName") ?? string.Empty), name, StringComparison.OrdinalIgnoreCase)).Remove();
        types.Add(new XElement(Ct + "Override", new XAttribute("PartName", "/" + name), new XAttribute("ContentType", contentType)));
    }

    /// <summary>The relationships of a part (<c>""</c> for the package), targets resolved to part names.</summary>
    public List<OpcRelationship> Relationships(string partName)
    {
        var result = new List<OpcRelationship>();
        var document = GetXml(OpcRelationships.RelationshipsPath(Normalize(partName)));
        if (document?.Root is null)
        {
            return result;
        }

        var directory = OpcRelationships.DirectoryOf(Normalize(partName));
        foreach (var element in document.Root.Elements(Rel + "Relationship"))
        {
            var target = (string?)element.Attribute("Target") ?? string.Empty;
            var external = string.Equals((string?)element.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
            result.Add(new OpcRelationship((string?)element.Attribute("Id") ?? string.Empty, (string?)element.Attribute("Type") ?? string.Empty, external ? target : OpcRelationships.Resolve(directory, target), external));
        }

        return result;
    }

    /// <summary>Adds a relationship from <paramref name="partName"/> and returns its new id.</summary>
    public string AddRelationship(string partName, string type, string target, bool external = false)
    {
        partName = Normalize(partName);
        var path = OpcRelationships.RelationshipsPath(partName);
        var document = GetXml(path);
        if (document is null)
        {
            document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(Rel + "Relationships"));
            SetXml(path, document, RelationshipsContentType);
        }

        var used = document.Root!.Elements(Rel + "Relationship").Select(e => (string?)e.Attribute("Id")).ToHashSet(StringComparer.Ordinal);
        var number = used.Count + 1;
        while (used.Contains("rId" + number))
        {
            number++;
        }

        var id = "rId" + number;
        var element = new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", external ? target : RelativeTarget(partName, target)));
        if (external)
        {
            element.Add(new XAttribute("TargetMode", "External"));
        }

        document.Root.Add(element);
        return id;
    }

    /// <summary>A part name not yet used: <paramref name="stem"/> followed by the first free number and <paramref name="extension"/>.</summary>
    public string UniqueName(string stem, string extension)
    {
        for (var n = 1; ; n++)
        {
            var name = stem + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + extension;
            if (!Contains(name))
            {
                return name;
            }
        }
    }

    public void Save(Stream stream)
    {
        using var writer = new OpenXmlPackageWriter(stream);
        foreach (var name in _order)
        {
            writer.WriteBytes(name, GetBytes(name)!);
        }
    }

    public static byte[] Serialize(XDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.None }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    private static string RelativeTarget(string source, string target)
    {
        var directory = OpcRelationships.DirectoryOf(source);
        if (directory.Length == 0)
        {
            return target;
        }

        return target.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase) ? target[(directory.Length + 1)..] : "/" + target;
    }

    private void Register(string name)
    {
        if (!_raw.ContainsKey(name))
        {
            _order.Add(name);
        }
    }

    private static string Normalize(string name) => name.TrimStart('/');
}
