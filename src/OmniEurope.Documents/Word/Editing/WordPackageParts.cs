// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Internal;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>Removes the parts an edit left unreferenced, so a replaced or removed drawing leaves nothing behind.</summary>
internal static class WordPackageParts
{
    /// <summary>
    /// Removes <paramref name="part"/> when no relationship of the package targets it any more, then the parts only it
    /// related to (a chart's workbook), by the same rule.
    /// </summary>
    public static void Release(OpcPackage package, string part)
    {
        if (!package.Contains(part) || IsTarget(package, part))
        {
            return;
        }

        var children = package.Relationships(part).Where(r => !r.External).Select(r => r.Target).ToList();
        package.RemovePart(part);
        foreach (var child in children)
        {
            Release(package, child);
        }
    }

    private static bool IsTarget(OpcPackage package, string part)
    {
        var owners = package.PartNames.Where(n => !n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).Prepend(string.Empty);
        return owners.Any(owner => package.Relationships(owner).Any(r => !r.External && string.Equals(r.Target, part, StringComparison.OrdinalIgnoreCase)));
    }
}
