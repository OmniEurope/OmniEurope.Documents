// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Conversion;

/// <summary>The codes of the gaps the Word and Excel to PDF conversions report (the layout, the charts, the
/// pictures and the fonts); each is a key of <c>Resources/GapMessages.resx</c>.</summary>
internal static class WordPdfGapCodes
{
    public static readonly GapCode PdfGlyphlessCharactersOmitted = new(nameof(PdfGlyphlessCharactersOmitted));
    public static readonly GapCode PdfFootnoteRestartUnsettled = new(nameof(PdfFootnoteRestartUnsettled));
    public static readonly GapCode PdfTightWrapByBoundingBox = new(nameof(PdfTightWrapByBoundingBox));
    public static readonly GapCode PdfNoWrapOutsideBody = new(nameof(PdfNoWrapOutsideBody));
    public static readonly GapCode PdfArabicWithoutJoining = new(nameof(PdfArabicWithoutJoining));
    public static readonly GapCode PdfPictureFormatEmptyFrame = new(nameof(PdfPictureFormatEmptyFrame));
    public static readonly GapCode PdfUnreadablePictureFrame = new(nameof(PdfUnreadablePictureFrame));
    public static readonly GapCode PdfFontReplaced = new(nameof(PdfFontReplaced));
    public static readonly GapCode PdfCalibriLightReplaced = new(nameof(PdfCalibriLightReplaced));
    public static readonly GapCode PdfContentTallerThanPage = new(nameof(PdfContentTallerThanPage));
    public static readonly GapCode PdfVerticalAlignmentNotApplied = new(nameof(PdfVerticalAlignmentNotApplied));
    public static readonly GapCode PdfSymbolCharactersApproximated = new(nameof(PdfSymbolCharactersApproximated));
    public static readonly GapCode PdfChartTypeNotDrawn = new(nameof(PdfChartTypeNotDrawn));
    public static readonly GapCode PdfChartPointLimit = new(nameof(PdfChartPointLimit));
    public static readonly GapCode PdfEmfRotatedTextHorizontal = new(nameof(PdfEmfRotatedTextHorizontal));
    public static readonly GapCode PdfEmfBitmapSkipped = new(nameof(PdfEmfBitmapSkipped));
}
