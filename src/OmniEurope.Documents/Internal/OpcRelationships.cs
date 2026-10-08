// SPDX-License-Identifier: EUPL-1.2
using System.Xml;

namespace OmniEurope.Documents.Internal;

/// <summary>One relationship of a package part: its id, type URI, resolved target path and mode.</summary>
internal sealed record OpcRelationship(string Id, string Type, string Target, bool External);

/// <summary>Reads relationship parts (<c>_rels/*.rels</c>) and resolves their targets.</summary>
internal static class OpcRelationships
{
    /// <summary>The relationships of <paramref name="partPath"/> (empty when it has none).</summary>
    public static List<OpcRelationship> Read(SafeZip zip, string partPath)
    {
        var result = new List<OpcRelationship>();
        using var xml = zip.ReadXml(RelationshipsPath(partPath));
        if (xml is null)
        {
            return result;
        }

        var directory = DirectoryOf(partPath);
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "Relationship")
            {
                continue;
            }

            var id = xml.GetAttribute("Id") ?? string.Empty;
            var type = xml.GetAttribute("Type") ?? string.Empty;
            var target = xml.GetAttribute("Target") ?? string.Empty;
            var external = string.Equals(xml.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
            result.Add(new OpcRelationship(id, type, external ? target : Resolve(directory, target), external));
        }

        return result;
    }

    /// <summary>The path of the relationships part of <paramref name="partPath"/> (<c>""</c> is the package).</summary>
    public static string RelationshipsPath(string partPath)
    {
        var directory = DirectoryOf(partPath);
        var name = partPath[(partPath.LastIndexOf('/') + 1)..];
        return (directory.Length == 0 ? string.Empty : directory + "/") + "_rels/" + name + ".rels";
    }

    /// <summary>The part a relationships part belongs to (<c>""</c> for the package's own <c>_rels/.rels</c>).</summary>
    public static string OwnerOf(string relationshipsPath)
    {
        var directory = DirectoryOf(relationshipsPath);
        var owner = DirectoryOf(directory);
        var name = relationshipsPath[(relationshipsPath.LastIndexOf('/') + 1)..^".rels".Length];
        return name.Length == 0 ? string.Empty : (owner.Length == 0 ? name : owner + "/" + name);
    }

    /// <summary>Resolves a relative target against a directory; a leading slash means the package root.</summary>
    public static string Resolve(string directory, string target)
    {
        var path = target.StartsWith('/') ? target[1..] : (directory.Length == 0 ? target : directory + "/" + target);
        var parts = new List<string>();
        foreach (var part in Uri.UnescapeDataString(path).Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part.Length > 0 && part != ".")
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    public static string DirectoryOf(string partPath)
    {
        var slash = partPath.LastIndexOf('/');
        return slash < 0 ? string.Empty : partPath[..slash];
    }
}
