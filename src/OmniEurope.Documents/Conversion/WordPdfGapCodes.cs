// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion;

/// <summary>The codes of the gaps <see cref="WordToPdf"/> reports itself; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class WordPdfGapCodes
{
    public static readonly GapCode PdfGlyphlessCharactersOmitted = new(nameof(PdfGlyphlessCharactersOmitted));
    public static readonly GapCode PdfFootnoteRestartUnsettled = new(nameof(PdfFootnoteRestartUnsettled));
    public static readonly GapCode PdfTightWrapByBoundingBox = new(nameof(PdfTightWrapByBoundingBox));
    public static readonly GapCode PdfNoWrapOutsideBody = new(nameof(PdfNoWrapOutsideBody));
    public static readonly GapCode PdfArabicWithoutJoining = new(nameof(PdfArabicWithoutJoining));
}
