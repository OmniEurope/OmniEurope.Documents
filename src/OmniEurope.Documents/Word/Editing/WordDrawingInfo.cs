// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Editing;

/// <summary>What a drawing of a paragraph shows.</summary>
public enum WordDrawingKind
{
    /// <summary>A picture (DrawingML <c>pic:pic</c>, or a VML shape with image data).</summary>
    Picture,

    /// <summary>Freehand ink (Word 2010 <c>wordprocessingInk</c>).</summary>
    Ink,

    /// <summary>A chart.</summary>
    Chart,

    /// <summary>A text box (a shape holding paragraphs).</summary>
    TextBox,

    /// <summary>Any other drawing: a diagram, a group of shapes, a geometric shape without text.</summary>
    Other,
}

/// <summary>
/// A drawing anchored or inline in a paragraph, as listed by <see cref="WordEditableParagraph.Drawings"/>. Sizes
/// and offsets are in points. A snapshot: list the drawings again after an edit.
/// </summary>
/// <param name="Index">Position among the drawings of the paragraph, from 0, in document order.</param>
/// <param name="Kind">What the drawing shows.</param>
/// <param name="Width">Width of its box.</param>
/// <param name="Height">Height of its box.</param>
public sealed record WordDrawingInfo(int Index, WordDrawingKind Kind, double Width, double Height)
{
    /// <summary>True when the drawing is anchored (floats over or behind the text), false when it sits in the line.</summary>
    public bool IsFloating { get; init; }

    /// <summary>Horizontal offset from <see cref="HorizontalRelativeTo"/>; 0 for an inline drawing or one placed by alignment.</summary>
    public double OffsetX { get; init; }

    /// <summary>Vertical offset from <see cref="VerticalRelativeTo"/>; 0 for an inline drawing or one placed by alignment.</summary>
    public double OffsetY { get; init; }

    /// <summary>What <see cref="OffsetX"/> is measured from (<c>column</c>, <c>margin</c>, <c>page</c>, <c>character</c>...); null when inline.</summary>
    public string? HorizontalRelativeTo { get; init; }

    /// <summary>What <see cref="OffsetY"/> is measured from (<c>paragraph</c>, <c>margin</c>, <c>page</c>, <c>line</c>...); null when inline.</summary>
    public string? VerticalRelativeTo { get; init; }

    /// <summary>How text flows around the drawing; <see cref="WordWrap.None"/> when it floats without wrapping and when it is inline.</summary>
    public WordWrap Wrap { get; init; }

    /// <summary>True when the drawing lies behind the text, false when it lies in front of it (or is inline).</summary>
    public bool BehindText { get; init; }

    /// <summary>The name the file gives the drawing (<c>wp:docPr/@name</c>), if any.</summary>
    public string? Name { get; init; }

    /// <summary>The alternative text of the drawing, if any.</summary>
    public string? Description { get; init; }
}
