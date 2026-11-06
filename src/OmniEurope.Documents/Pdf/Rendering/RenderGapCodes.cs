// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Pdf.Rendering;

/// <summary>The codes of the gaps the PDF renderer reports; each is a key of
/// <c>Resources/GapMessages.resx</c>.</summary>
internal static class RenderGapCodes
{
    public static readonly GapCode RenderImageFormatUnsupported = new(nameof(RenderImageFormatUnsupported));
    public static readonly GapCode RenderOperationSkipped = new(nameof(RenderOperationSkipped));
    public static readonly GapCode RenderShadingNotDrawn = new(nameof(RenderShadingNotDrawn));
    public static readonly GapCode RenderPatternFlatColour = new(nameof(RenderPatternFlatColour));
    public static readonly GapCode RenderFontNotEmbedded = new(nameof(RenderFontNotEmbedded));
    public static readonly GapCode RenderEmbeddedFontUnreadable = new(nameof(RenderEmbeddedFontUnreadable));
    public static readonly GapCode RenderSoftMaskUnreadable = new(nameof(RenderSoftMaskUnreadable));
    public static readonly GapCode RenderSoftMaskTransferIgnored = new(nameof(RenderSoftMaskTransferIgnored));
}
