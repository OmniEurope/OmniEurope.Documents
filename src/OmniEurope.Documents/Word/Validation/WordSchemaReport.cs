// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Validation;

/// <summary>What <see cref="WordSchemaValidator"/> found in a Word package.</summary>
public sealed class WordSchemaReport
{
    internal WordSchemaReport(IReadOnlyList<WordSchemaError> errors, IReadOnlyList<string> checkedParts, IReadOnlyList<string> uncheckedParts)
    {
        Errors = errors;
        CheckedParts = checkedParts;
        UncheckedParts = uncheckedParts;
    }

    /// <summary>Every schema, markup compatibility and relationship id error, in part order.</summary>
    public IReadOnlyList<WordSchemaError> Errors { get; }

    /// <summary>The XML parts validated against a shipped schema.</summary>
    public IReadOnlyList<string> CheckedParts { get; }

    /// <summary>
    /// The XML parts whose root namespace no shipped schema covers (core properties, custom XML data, application
    /// extensions): they were read as XML but not validated.
    /// </summary>
    public IReadOnlyList<string> UncheckedParts { get; }

    /// <summary>True when no error was found in the checked parts.</summary>
    public bool IsValid => Errors.Count == 0;
}
