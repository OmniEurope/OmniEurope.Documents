// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents;

/// <summary>The code of a gap: a key of <c>Resources/GapMessages.resx</c>. The codes are the static fields of the
/// <c>*GapCodes</c> classes, one per area.</summary>
internal readonly record struct GapCode(string Value);
