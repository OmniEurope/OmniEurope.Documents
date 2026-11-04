// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Fonts;

/// <summary>
/// The font that drew a requested family in a converted document.
/// </summary>
/// <param name="Requested">The family the document asks for (Calibri, Segoe UI...).</param>
/// <param name="Used">The family of the font drawn instead or as is (Carlito, Selawik, Liberation Sans...).</param>
/// <param name="Exact">True when the requested family itself is drawn (registered or bundled under that name).</param>
/// <param name="Note">Why another family is drawn and what differs (metric-compatible look-alike, no light
/// weight, class fallback...); null for an exact use.</param>
public sealed record FontUse(string Requested, string Used, bool Exact, string? Note);
