// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Validation;

/// <summary>
/// A place where a part of a Word package breaks the Office Open XML schemas or the markup compatibility rules.
/// </summary>
/// <param name="Part">The part name, without the leading slash (<c>word/document.xml</c>).</param>
/// <param name="Line">The line of the offending element or attribute in the part (1-based), 0 when unknown.</param>
/// <param name="Position">Its position on that line (1-based), 0 when unknown.</param>
/// <param name="Path">The element path within the part (<c>/w:document[1]/w:body[1]/w:p[2]/@w:rsidR</c>), indexes
/// counted among the siblings of the same name, empty for a part-level error. For a schema error the path is that of
/// the part once markup compatibility is processed (ignored markup removed, alternate content resolved); the line
/// and position always point into the part as stored.</param>
/// <param name="Message">What is wrong, in the words of the schema validator.</param>
public sealed record WordSchemaError(string Part, int Line, int Position, string Path, string Message)
{
    /// <summary>The error on one line: part, line and position, path, message.</summary>
    public override string ToString() => $"{Part} ({Line},{Position}) {Path}: {Message}";
}
