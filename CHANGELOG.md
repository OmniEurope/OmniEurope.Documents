# Changelog

Notable changes to this project are documented here, following the Keep a Changelog format.

## [Unreleased]

### Added

- `WordEditableParagraph.Location` (`WordParagraphLocation`, `WordPathStep`, `WordPartKind`): where a paragraph of
  `WordEditor.Paragraphs()` sits: its part and kind, the relationship id of a header or footer, the id of a note or
  comment (separator and continuation notes flagged), and the block path down to it (blocks, table rows and cells,
  content controls, custom XML, text boxes; row and cell wrappers are transparent, alternate-content fallbacks are
  left out). `WordEditor.FindParagraph(WordParagraphLocation)` finds it again.
- `WordEditableParagraph.Runs()` (`WordEditableRun`, `WordRunKind`): the paragraph's own runs in order (not those of
  its text boxes, not deleted ones), each with its kind (text, field, note reference, drawing, symbol, page break,
  other), its text, its direct formatting and the formatting it shows; `ResolveProperties()` and
  `ResolveRunProperties()` give the paragraph's resolved formatting.
- `WordEditableParagraph.SetContent` (`WordContentPiece`, `WordTextPiece`, `WordKeptRun`): rewrites the paragraph's
  runs from new text pieces and kept runs (a kept run of a field keeps the whole field; runs that are not text and
  that no piece names follow the new content), the new text in the format of the first text run with each piece's
  format on top, optionally in a language; nothing changes when the content is already the one asked.
- `PdfLayoutAnalyzer.Lines(PdfPage)`: the line segments of a page, each text direction read along its own baselines
  (lines top to bottom, segments left to right), so a table turned on the page is read row after row.
- `PdfCanvas.DrawRotatedText`: one line of text turned by any angle counter-clockwise around the start of its
  baseline (90 reads from bottom to top, 270 from top to bottom), with colour and character spacing.
- Word, Excel, PDF, Markdown, CSV and HTML reading, writing and conversion, with the .NET base class
  library only.
- Word to HTML (`WordToHtml.Convert`): one standalone page with each section at its page width and margins,
  the first section's default header and footer as running blocks, resolved paragraph and run formatting,
  lists with their computed labels, tables with spans and vertical merges, footnotes then endnotes linked
  both ways, pictures as `data:` URIs (TIFF and EMF turned into PNG), text boxes as blocks, safe links only
  (`http`, `https`, `mailto`, internal anchors) and tracked changes accepted or marked. Every paragraph read
  from a package carries `data-address`, the address `WordEditor` gives it; `HighlightAddress` marks one.
  Document text is always encoded; what the page approximates is listed in its gaps.
- `WordParagraph.SourceAddress`: the `WordEditor` address of a paragraph read from a package.
- `WordPageSetup.FootnoteNumbering` (`WordNoteNumbering`): a section's own footnote number style, first
  number and restart rule (`w:sectPr/w:footnotePr`), read and written.
- `ExcelPdfOptions.MaxCells`: the most cells Excel to PDF prints, empty cells of the used ranges included
  (250,000 by default); a larger workbook, such as a sheet holding only `A1` and `XFD1048576`, throws
  `DocumentFormatException` instead of exhausting memory.
- `XlsxCsvConverter.WriteCsv` and `ToCsv` take `maxCells` (100,000,000 by default): a used range holding more
  cells, empty ones included, throws `DocumentFormatException` before anything is written.

### Changed

- Word to PDF lays lines out as Word does: a single line holds the font's external leading (its hhea line gap beyond
  the Windows ascent and descent) above its text; the extra space of a multiple line spacing goes below the text;
  after a page or column break the paragraph mark starts a line of its own; a paragraph opening with a page or
  column break keeps its first-line indent for what follows the break, and its space before on the page it opens.
- Reading a Word: section properties without a page size or margins mean US Letter with one inch margins, and a
  styles part without document defaults (or no styles part) sets paragraphs 8 pt apart at 1.15 lines, as Word does.
- Word to PDF: footnote numbers restart at each page or each section as the document settings or the
  section ask, with the section's number style and first number, in the reference and in the note; the gap
  "footnote numbering restarts are not applied" is gone. Numbers restarting at each page are fixed by laying
  the document out again until no note changes page.
- Excel to PDF: text that does not wrap stays on one line and runs on into empty neighbouring cells (a fill
  or a border does not stop it, and no grid line is drawn under it); it is clipped at the first cell holding
  a value or a formula, and wraps in its cell when it would pass the edge of the printed range. Numbers
  never wrap: a column holds as many digits as its width in characters, beyond which a General number is
  shortened and any other number is filled with `#`.
- Word to PDF: table rows paint every cell fill first, then the contents, then the borders, so content
  running over a neighbouring cell stays visible and borders are never covered by a fill.
- CI: a test run now fails when it executes no test, or when a test errors, times out or is aborted.

### Fixed

- `CsvReader.Open` and `OpenAsync`: a header that cannot be read no longer leaves the stream or text reader
  the reader owns open (and its file locked); with `leaveOpen` it stays open as asked.
- `TextValueParser.TryParseDuration` returns false instead of throwing for a duration past what `TimeSpan`
  holds (`decimal.MaxValue` hours, `2147483647:00`).
- Excel dates: the last day is 31 December 9999 in each date system (serial 2958465 from 1900, 2957003 from
  1904); a date-formatted number past it shows `########` and is read as a number instead of throwing.
- `PdfFlowLayout`: adding content or a page after `Finish` throws `InvalidOperationException`, as documented,
  instead of giving a page without its footer.
- EMF pictures: the isotropic (7) and anisotropic (8) map modes were swapped; isotropic keeps the aspect ratio
  with the smaller scale, anisotropic scales each axis on its own.
- CFF glyphs: a first stem operator with an even number of operands no longer loses its first operand as a
  width, so the hint mask that follows skips the right number of bytes.
- PDF text extraction: the horizontal scale (`Tz`) is applied once to `PdfLetter.Width`, which is again the
  distance to the next letter.
- JPEG: a component whose sampling factor does not divide the largest one (2 for 3) takes, for each pixel, the
  sample covering it instead of a repeated edge.
- `PdfStamper`: a page whose `Contents` refers to an array of streams keeps its content after stamping.
- `WordEditor.Append`: styles and lists used only by a copied header or footer move with it.
- `WordEditor.ReplaceText`: a match no longer spans a tab, a break, a hyphen character, a field or a note
  reference.
- Word writing: one `WordHeaderFooter` used as both header and footer gives a header part and a footer part,
  and a `WordHyperlink` in a field result is written (as its runs when the field is a tracked change).
- Forged files no longer hang, exhaust memory or overflow the stack; each is read for what it really holds or
  refused with a documented exception. EMF point and polygon counts and bitmap offsets are bounded by their
  record. `CsvReaderOptions.MaxRecordLength` counts delimiters, so a record of delimiters alone is bounded too.
  PDF: predictor rows are computed in 64 bits (1, 2, 4, 8 or 16 bits, at most 32 colours), an object stream's
  `/N` is bounded by its header, a cross-reference stream with empty or oversized `/W` widths is rebuilt from the
  objects, a sampled function larger than its stream is not read, Type 1 subroutine numbers past 65,535 are
  skipped, and CMap code spaces outside one to four bytes are ignored. TrueType `cmap` segments and groups stop
  once the code space is covered. Markdown containers nest at most 128 deep (deeper markers are text). A Word
  part nesting elements deeper than 256 levels throws `DocumentFormatException`. A GIF frame larger than
  `RasterImage.MaxPixels` is refused before its pixels are allocated.
- PDF word extraction: a space alone whose advance runs under the next letters (a space ending a table cell, the
  next cell starting before its advance is over) ends its word again (`Sites Ports`, not `SitesPorts`); only a
  blank lying over visible letters and touching another blank (a row of spaces printed under the text) is left out.
- PDF word extraction (`PdfLayoutAnalyzer.Words`, `Lines`, `AnalyzePage`, `PdfPage.Text`): a row of spaces printed
  under the text (a blank lying for more than half its advance over visible letters) no longer splits every
  letter into a word of its own (`Official Journal`, not `O f f i c i a l J o u r n a l`). Letter-spaced
  (tracked) text is split into words against its own spacing: in a run of a line with at least three gaps
  between visible letters, when the median gap is wider than a word gap but at most three quarters of the size,
  and two thirds of the gaps lie within a tenth of the size of it, that gap is the run's tracking and is taken off
  every gap of the run before it is judged; a spaced-out title is one word, normal text and short words with a
  normal space are unchanged, and a space glyph still ends a word.
- PDF text drawn at an angle (a page or a table turned by 90 degrees, a vertical column heading, text read
  downwards) is grouped by direction first: `PdfPage.Text` reads each direction along its own baselines
  (directions in the order they are first drawn) instead of one letter per line mixed with the horizontal text.
  The layout analysis builds the blocks of each direction along it (the lines of a turned paragraph are one
  block), reads all blocks in the frame of the direction carrying the most letters, and judges running heads,
  footers and page numbers in the frame of each block's own direction, so the column of a turned table standing
  along the bottom edge of the page is no longer taken for page numbers.
- PDF layout analysis: a superscript or subscript (a smaller run raised or lowered against the text, such as a
  footnote reference) stays on the line of its text, in its reading order, instead of becoming a line or block of
  its own, and is a word of its own instead of being glued to the word before it (`assessed` and `1`, not
  `assessed1`). A word also ends where the baseline shifts by more than 15 % of the larger size (5 % when the size
  changes too). Small capitals (a full-size capital followed by smaller ones on the same baseline) stay one word,
  even beside a column whose baseline is slightly higher. Two lines printed over each other on nearly the same
  baseline stay two lines instead of interleaving their letters, and a stray blank drawn on a baseline of its own
  no longer splits a word. The gap that ends a word or a line segment is measured against the smaller of the two
  letters' sizes. `PdfPage.Text` groups its lines the same way: a footnote reference stays on its line, set off by
  spaces, and two baselines further apart than a third of the smaller size (a third of the larger one before), or
  printed over each other, are no longer merged into one line.
- `PdfTextLine.Baseline` is the baseline of the line's main text (the y shared by most letters, weighted by their
  size) instead of the y of its first letter, so a footnote number raised at the start of a line does not move it.
- TIFF: a tile or strip size of zero is refused with `InvalidDataException`.
- PDF compression: images made transparent by a `/Mask` (colour key or explicit mask) are kept as they are
  instead of being re-encoded without their transparency.
