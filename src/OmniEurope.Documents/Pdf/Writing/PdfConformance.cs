// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>The archival conformance a written PDF claims and follows (ISO 19005-2).</summary>
public enum PdfConformance
{
    /// <summary>A plain PDF.</summary>
    None,

    /// <summary>PDF/A-2b, level B (basic): the visual appearance is preserved.</summary>
    PdfA2b,

    /// <summary>PDF/A-2u, level U: level B, and every character also maps to Unicode.</summary>
    PdfA2u,
}
