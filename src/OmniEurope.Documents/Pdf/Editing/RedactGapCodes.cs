// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>The codes of the gaps the PDF redaction reports; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class RedactGapCodes
{
    public static readonly GapCode RedactVectorsPaintedOver = new(nameof(RedactVectorsPaintedOver));
    public static readonly GapCode RedactTilingPatternsRemoved = new(nameof(RedactTilingPatternsRemoved));
    public static readonly GapCode RedactGlyphsMayMove = new(nameof(RedactGlyphsMayMove));
    public static readonly GapCode RedactFormsNestedTooDeep = new(nameof(RedactFormsNestedTooDeep));
    public static readonly GapCode RedactUndecodableForms = new(nameof(RedactUndecodableForms));
}
