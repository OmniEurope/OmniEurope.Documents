// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Conversion.WordLayout;
using OmniEurope.Documents.Word;

namespace OmniEurope.Documents.Conversion;

/// <summary>Effective paragraph and run formatting, shared by the conversions: the style sheet resolution,
/// then, in a table cell, the conditional formats of the table style under the direct formatting.</summary>
internal static class WordResolution
{
    public static WordParagraphProperties Paragraph(WordDocument document, WordParagraph paragraph, CellStyle? cell)
    {
        var resolved = document.Styles.ResolveParagraph(paragraph.Properties, document.Numbering, cell?.TableStyleId);
        return cell?.ParagraphOverlay is { } overlay ? resolved.Overlay(overlay).Overlay(paragraph.Properties with { StyleId = resolved.StyleId }) : resolved;
    }

    public static WordRunProperties Run(WordStyleSheet styles, WordParagraphProperties paragraph, WordRunProperties direct, CellStyle? cell)
    {
        var resolved = styles.ResolveRun(paragraph, direct, cell?.TableStyleId);
        return cell?.RunOverlay is { } overlay ? resolved.Overlay(overlay).Overlay(direct with { StyleId = null }) : resolved;
    }
}
