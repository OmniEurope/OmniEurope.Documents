// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Editing;

/// <summary>
/// A redaction was refused because it cannot be guaranteed: text is shown with a font that cannot be resolved (no
/// <c>Tf</c> before it, or a <c>Tf</c> naming a font absent from the resources in force), so where its glyphs lie
/// cannot be computed and the text could stay under an area. No document is produced.
/// </summary>
public sealed class PdfRedactionException(string message) : InvalidOperationException(message);
