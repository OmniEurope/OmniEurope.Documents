// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OmniEurope.Documents.Internal;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Appends the body of one package to another at XML level: relationship ids are remapped and the parts
/// they point to copied (pictures, headers, footers, links), notes and lists get fresh ids, missing styles
/// are copied, bookmark and drawing ids are made unique. The target's last section is closed on its last
/// paragraph so each document keeps its own page setup.
/// </summary>
internal sealed class WordMerger(WordEditor target, WordEditor source)
{
    private readonly OpcPackage _target = target.Package;
    private readonly OpcPackage _source = source.Package;
    private readonly Dictionary<string, string> _copied = new(StringComparer.OrdinalIgnoreCase);

    public void Append(bool startOnNewPage)
    {
        var body = source.Body;
        var content = body.Elements().Where(e => e.Name != W + "sectPr").Select(e => new XElement(e)).ToList();
        var lastSection = body.Element(W + "sectPr") is { } sectPr ? new XElement(sectPr) : new XElement(W + "sectPr");
        var elements = content.Append(lastSection).ToList();

        StripComments(elements);
        Remap(elements, source.MainPart, target.MainPart);
        var notes = CopyNotes(elements, "footnote", FootnotesType, FootnotesContentType);
        notes.AddRange(CopyNotes(elements, "endnote", EndnotesType, EndnotesContentType));
        var styles = CopyStyles(elements.Concat(notes).ToList());
        RemapNumbering(elements.Concat(notes).Concat(styles).ToList());
        RenumberBookmarks(elements);
        SetStart(elements.SelectMany(e => e.DescendantsAndSelf(W + "sectPr")).First(), startOnNewPage);

        var targetBody = target.Body;
        CloseLastSection(targetBody);
        targetBody.Add(elements);
        RenumberDrawings(targetBody);
    }

    private static void StripComments(IEnumerable<XElement> elements)
    {
        foreach (var element in elements)
        {
            element.DescendantsAndSelf().Where(e => e.Name == W + "commentRangeStart" || e.Name == W + "commentRangeEnd").Remove();
            foreach (var reference in element.Descendants(W + "commentReference").ToList())
            {
                var run = reference.Parent;
                reference.Remove();
                if (run is not null && run.Name == W + "r" && !run.Elements().Any(e => e.Name != W + "rPr"))
                {
                    run.Remove();
                }
            }
        }
    }

    // Points every relationship id of the elements at a relationship of the target part.
    private void Remap(IEnumerable<XElement> elements, string sourcePart, string targetPart)
    {
        var relationships = _source.Relationships(sourcePart).GroupBy(r => r.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in elements.SelectMany(e => e.DescendantsAndSelf()).SelectMany(e => e.Attributes()).Where(a => a.Name.Namespace == R).ToList())
        {
            if (!relationships.TryGetValue(attribute.Value, out var relationship))
            {
                continue;
            }

            if (!map.TryGetValue(attribute.Value, out var id))
            {
                id = relationship.External
                    ? _target.AddRelationship(targetPart, relationship.Type, relationship.Target, external: true)
                    : _target.AddRelationship(targetPart, relationship.Type, CopyPart(relationship.Target));
                map[attribute.Value] = id;
            }

            attribute.Value = id;
        }
    }

    private string CopyPart(string sourcePart)
    {
        if (_copied.TryGetValue(sourcePart, out var known))
        {
            return known;
        }

        var directory = OpcRelationships.DirectoryOf(sourcePart);
        var file = Path.GetFileNameWithoutExtension(sourcePart);
        var stem = (directory.Length > 0 ? directory + "/" : string.Empty) + TrailingDigits.Replace(file, string.Empty);
        var name = _target.UniqueName(stem, Path.GetExtension(sourcePart).TrimStart('.'));
        _copied[sourcePart] = name;
        var contentType = _source.ContentTypeOf(sourcePart) ?? "application/octet-stream";
        var xml = contentType.EndsWith("+xml", StringComparison.Ordinal) ? _source.GetXml(sourcePart) : null;
        if (xml?.Root is null)
        {
            _target.SetBytes(name, _source.GetBytes(sourcePart) ?? [], contentType);
            return name;
        }

        var copy = new XDocument(xml);
        _target.SetXml(name, copy, contentType);
        Remap([copy.Root!], sourcePart, name);
        return name;
    }

    // Notes referenced by the elements are copied under new ids into the target notes part.
    private List<XElement> CopyNotes(List<XElement> elements, string kind, string type, string contentType)
    {
        var references = elements.SelectMany(e => e.Descendants(W + kind + "Reference")).ToList();
        var sourcePart = _source.Relationships(source.MainPart).Find(r => r.Type == type && !r.External)?.Target;
        var sourceRoot = sourcePart is null ? null : _source.GetXml(sourcePart)?.Root;
        if (references.Count == 0 || sourceRoot is null)
        {
            return [];
        }

        var (targetPart, targetRoot) = NotesPart(kind, type, contentType);
        var next = targetRoot.Elements(W + kind).Select(n => Int(Attr(n, "id")) ?? 0).DefaultIfEmpty(0).Max() + 1;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var copied = new List<XElement>();
        foreach (var reference in references)
        {
            var id = Attr(reference, "id") ?? string.Empty;
            if (!map.TryGetValue(id, out var newId))
            {
                var note = sourceRoot.Elements(W + kind).FirstOrDefault(n => Attr(n, "id") == id);
                if (note is null)
                {
                    continue;
                }

                newId = Format(next++);
                var copy = new XElement(note);
                copy.SetAttributeValue(W + "id", newId);
                StripComments([copy]);
                Remap([copy], sourcePart!, targetPart);
                targetRoot.Add(copy);
                copied.Add(copy);
                map[id] = newId;
            }

            reference.SetAttributeValue(W + "id", newId);
        }

        return copied;
    }

    private (string Part, XElement Root) NotesPart(string kind, string type, string contentType)
    {
        var part = _target.Relationships(target.MainPart).Find(r => r.Type == type && !r.External)?.Target;
        if (part is not null && _target.GetXml(part)?.Root is { } root)
        {
            return (part, root);
        }

        part = "word/" + kind + "s.xml";
        root = new XElement(
            W + kind + "s",
            new XAttribute(XNamespace.Xmlns + "w", W),
            new XAttribute(XNamespace.Xmlns + "r", R),
            Separator(kind, "-1", "separator"),
            Separator(kind, "0", "continuationSeparator"));
        _target.SetXml(part, new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root), contentType);
        _target.AddRelationship(target.MainPart, type, part);
        return (part, root);
    }

    private static XElement Separator(string kind, string id, string type) =>
        new(W + kind, new XAttribute(W + "type", type), new XAttribute(W + "id", id), new XElement(W + "p", new XElement(W + "r", new XElement(W + type))));

    // Styles used by the elements (and their bases, links and next styles) that the target lacks.
    private List<XElement> CopyStyles(List<XElement> elements)
    {
        var sourceRoot = StylesRoot(_source, source.MainPart);
        var targetRoot = StylesRoot(_target, target.MainPart);
        var copied = new List<XElement>();
        if (sourceRoot is null || targetRoot is null)
        {
            return copied;
        }

        var existing = targetRoot.Elements(W + "style").Select(s => Attr(s, "styleId")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>(StyleReferences(elements));
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!existing.Add(id) || sourceRoot.Elements(W + "style").FirstOrDefault(s => Attr(s, "styleId") == id) is not { } style)
            {
                continue;
            }

            var copy = new XElement(style);
            targetRoot.Add(copy);
            copied.Add(copy);
            foreach (var reference in StyleReferences([copy]).Concat(new[] { "basedOn", "next", "link" }.Select(n => Val(copy, n)).OfType<string>()))
            {
                pending.Enqueue(reference);
            }
        }

        return copied;
    }

    private static IEnumerable<string> StyleReferences(IEnumerable<XElement> elements) => elements
        .SelectMany(e => e.DescendantsAndSelf())
        .Where(e => e.Name == W + "pStyle" || e.Name == W + "rStyle" || e.Name == W + "tblStyle")
        .Select(e => Attr(e, "val"))
        .OfType<string>()
        .Distinct(StringComparer.Ordinal);

    private static XElement? StylesRoot(OpcPackage package, string main)
    {
        var part = package.Relationships(main).Find(r => r.Type == StylesType && !r.External)?.Target;
        return part is null ? null : package.GetXml(part)?.Root;
    }

    // Lists of the source get their own instances and definitions in the target.
    private void RemapNumbering(List<XElement> elements)
    {
        var references = elements.SelectMany(e => e.Descendants(W + "numId")).Where(n => Attr(n, "val") is { } v && v != "0").ToList();
        var sourcePart = _source.Relationships(source.MainPart).Find(r => r.Type == NumberingType && !r.External)?.Target;
        var sourceRoot = sourcePart is null ? null : _source.GetXml(sourcePart)?.Root;
        if (references.Count == 0 || sourceRoot is null)
        {
            return;
        }

        var targetRoot = NumberingRoot();
        var nextNum = targetRoot.Elements(W + "num").Select(n => Int(Attr(n, "numId")) ?? 0).DefaultIfEmpty(0).Max() + 1;
        var nextAbstract = targetRoot.Elements(W + "abstractNum").Select(n => Int(Attr(n, "abstractNumId")) ?? 0).DefaultIfEmpty(-1).Max() + 1;
        var numbers = new Dictionary<string, string>(StringComparer.Ordinal);
        var abstracts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            var id = Attr(reference, "val")!;
            if (!numbers.TryGetValue(id, out var newId))
            {
                var num = sourceRoot.Elements(W + "num").FirstOrDefault(n => Attr(n, "numId") == id);
                var abstractId = Val(num, "abstractNumId");
                var definition = sourceRoot.Elements(W + "abstractNum").FirstOrDefault(a => Attr(a, "abstractNumId") == abstractId);
                if (num is null || abstractId is null || definition is null)
                {
                    continue;
                }

                if (!abstracts.TryGetValue(abstractId, out var newAbstract))
                {
                    newAbstract = Format(nextAbstract++);
                    var copy = new XElement(definition);
                    copy.SetAttributeValue(W + "abstractNumId", newAbstract);
                    copy.Elements(W + "nsid").Remove();
                    AddAbstract(targetRoot, copy);
                    abstracts[abstractId] = newAbstract;
                }

                newId = Format(nextNum++);
                var instance = new XElement(num);
                instance.SetAttributeValue(W + "numId", newId);
                instance.Element(W + "abstractNumId")!.SetAttributeValue(W + "val", newAbstract);
                AddInstance(targetRoot, instance);
                numbers[id] = newId;
            }

            reference.SetAttributeValue(W + "val", newId);
        }
    }

    private XElement NumberingRoot()
    {
        var part = _target.Relationships(target.MainPart).Find(r => r.Type == NumberingType && !r.External)?.Target;
        if (part is not null && _target.GetXml(part)?.Root is { } root)
        {
            return root;
        }

        root = new XElement(W + "numbering", new XAttribute(XNamespace.Xmlns + "w", W));
        _target.SetXml("word/numbering.xml", new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root), NumberingContentType);
        _target.AddRelationship(target.MainPart, NumberingType, "word/numbering.xml");
        return root;
    }

    // Definitions precede instances in the numbering part.
    private static void AddAbstract(XElement root, XElement definition)
    {
        if (root.Element(W + "num") is { } firstInstance)
        {
            firstInstance.AddBeforeSelf(definition);
        }
        else
        {
            root.Add(definition);
        }
    }

    private static void AddInstance(XElement root, XElement instance)
    {
        if (root.Element(W + "numIdMacAtCleanup") is { } cleanup)
        {
            cleanup.AddBeforeSelf(instance);
        }
        else
        {
            root.Add(instance);
        }
    }

    private void RenumberBookmarks(List<XElement> elements)
    {
        var offset = target.Body.Descendants(W + "bookmarkStart").Select(b => Int(Attr(b, "id")) ?? 0).DefaultIfEmpty(0).Max() + 1;
        foreach (var bookmark in elements.SelectMany(e => e.DescendantsAndSelf()).Where(e => e.Name == W + "bookmarkStart" || e.Name == W + "bookmarkEnd"))
        {
            if (Int(Attr(bookmark, "id")) is { } id)
            {
                bookmark.SetAttributeValue(W + "id", Format(id + offset));
            }
        }
    }

    private static void SetStart(XElement sectPr, bool startOnNewPage)
    {
        sectPr.Elements(W + "type").Remove();
        if (startOnNewPage)
        {
            return;
        }

        var type = ValElement("type", "continuous");
        var after = sectPr.Elements().FirstOrDefault(e => e.Name.LocalName is not ("headerReference" or "footerReference" or "footnotePr" or "endnotePr"));
        if (after is null)
        {
            sectPr.Add(type);
        }
        else
        {
            after.AddBeforeSelf(type);
        }
    }

    // The body-level section properties move into the last paragraph (or a new one) so they end that section.
    private static void CloseLastSection(XElement body)
    {
        var sectPr = body.Element(W + "sectPr") ?? new XElement(W + "sectPr");
        sectPr.Remove();
        var last = body.Elements().LastOrDefault();
        if (last is null || last.Name != W + "p" || last.Element(W + "pPr")?.Element(W + "sectPr") is not null)
        {
            last = new XElement(W + "p");
            body.Add(last);
        }

        var pPr = last.Element(W + "pPr");
        if (pPr is null)
        {
            pPr = new XElement(W + "pPr");
            last.AddFirst(pPr);
        }

        if (pPr.Element(W + "pPrChange") is { } change)
        {
            change.AddBeforeSelf(sectPr);
        }
        else
        {
            pPr.Add(sectPr);
        }
    }

    private static void RenumberDrawings(XElement body)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var properties = body.Descendants(Wp + "docPr").ToList();
        var next = properties.Select(p => Int((string?)p.Attribute("id")) ?? 0).DefaultIfEmpty(0).Max() + 1;
        foreach (var element in properties)
        {
            var id = (string?)element.Attribute("id") ?? string.Empty;
            if (!used.Add(id))
            {
                element.SetAttributeValue("id", (next++).ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static readonly Regex TrailingDigits = new(@"\d+$", RegexOptions.CultureInvariant);
}
