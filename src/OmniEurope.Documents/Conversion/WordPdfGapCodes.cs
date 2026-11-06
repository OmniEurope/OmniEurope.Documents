// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion;

/// <summary>The codes of the gaps <see cref="WordToPdf"/> reports itself; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class WordPdfGapCodes
{
    public static readonly GapCode GlyphlessCharactersOmitted = new(nameof(GlyphlessCharactersOmitted));
    public static readonly GapCode FootnoteRestartUnsettled = new(nameof(FootnoteRestartUnsettled));
    public static readonly GapCode TightWrapByBoundingBox = new(nameof(TightWrapByBoundingBox));
    public static readonly GapCode NoWrapOutsideBody = new(nameof(NoWrapOutsideBody));
    public static readonly GapCode ArabicWithoutJoining = new(nameof(ArabicWithoutJoining));
}
