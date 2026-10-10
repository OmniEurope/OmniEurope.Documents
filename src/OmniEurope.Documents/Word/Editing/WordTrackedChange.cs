// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Word.Editing;

/// <summary>What a tracked change (ECMA-376 Part 1 §17.13.5) records.</summary>
public enum WordTrackedChangeKind
{
    /// <summary>Inserted content (<c>w:ins</c> around runs).</summary>
    Insertion,

    /// <summary>Deleted content (<c>w:del</c> around runs).</summary>
    Deletion,

    /// <summary>Content moved away (<c>w:moveFrom</c>); with a move range, its source and destination are one change.</summary>
    MoveFrom,

    /// <summary>Content moved here (<c>w:moveTo</c>) without a named move range.</summary>
    MoveTo,

    /// <summary>A move with a named range: the source (<c>w:moveFrom</c>) and the destination (<c>w:moveTo</c>) of one name.</summary>
    Move,

    /// <summary>An inserted paragraph mark (<c>w:pPr/w:rPr/w:ins</c>): rejecting it joins the paragraph with the next one.</summary>
    ParagraphMarkInsertion,

    /// <summary>A deleted paragraph mark (<c>w:pPr/w:rPr/w:del</c>): accepting it joins the paragraph with the next one.</summary>
    ParagraphMarkDeletion,

    /// <summary>Changed character formatting (<c>w:rPrChange</c>), in a run, a paragraph mark, a style or a list level.</summary>
    RunFormatting,

    /// <summary>Changed paragraph formatting (<c>w:pPrChange</c>).</summary>
    ParagraphFormatting,

    /// <summary>Changed section properties (<c>w:sectPrChange</c>).</summary>
    SectionFormatting,

    /// <summary>Changed table, table exception, row or cell properties, or table grid (<c>w:tblPrChange</c>,
    /// <c>w:tblPrExChange</c>, <c>w:trPrChange</c>, <c>w:tcPrChange</c>, <c>w:tblGridChange</c>).</summary>
    TableFormatting,

    /// <summary>An inserted table row (<c>w:trPr/w:ins</c>).</summary>
    RowInsertion,

    /// <summary>A deleted table row (<c>w:trPr/w:del</c>).</summary>
    RowDeletion,

    /// <summary>An inserted table cell (<c>w:cellIns</c>).</summary>
    CellInsertion,

    /// <summary>A deleted table cell (<c>w:cellDel</c>).</summary>
    CellDeletion,

    /// <summary>A changed vertical merge of a cell (<c>w:cellMerge</c>).</summary>
    CellMerge,

    /// <summary>A recorded former list number (<c>w:numberingChange</c>, a legacy record removed on accept and on reject).</summary>
    NumberingChange,

    /// <summary>List numbering applied with tracking (<c>w:numPr/w:ins</c>): rejecting it removes the paragraph's numbering.</summary>
    NumberingInsertion,
}

/// <summary>
/// A tracked change listed by <see cref="WordEditor.TrackedChanges"/>. <see cref="Index"/> is its position in that
/// list; accepting or rejecting a change moves the ones after it, so list the changes again after each one.
/// </summary>
/// <param name="Index">Position in the list, from 0.</param>
/// <param name="Kind">What the change records.</param>
/// <param name="PartName">The part holding it (<c>word/document.xml</c>, a header, the styles...).</param>
/// <param name="Id">The <c>w:id</c> attribute, or null; for a move, the range name.</param>
/// <param name="Author">The author, or null.</param>
/// <param name="Date">The date, or null when absent or unreadable.</param>
/// <param name="Text">The text inserted, deleted or moved, or the text whose formatting changed (empty for a
/// paragraph mark or a property without text).</param>
public sealed record WordTrackedChange(int Index, WordTrackedChangeKind Kind, string PartName, string? Id, string? Author, DateTimeOffset? Date, string Text);
