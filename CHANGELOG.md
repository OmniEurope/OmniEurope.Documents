# Changelog

Notable changes to this project are documented here, following the Keep a Changelog format.

## [Unreleased]

### Added

- `WordSchemaValidator.Validate` (`OmniEurope.Documents.Word.Validation`, `WordSchemaReport`, `WordSchemaError`):
  validates every XML part of a Word package against the Office Open XML schemas of ECMA-376 5th edition shipped
  in the package (transitional WordprocessingML of Part 4 with the DrawingML, VML, math, shared types, document
  properties, custom XML and bibliography schemas it uses, strict WordprocessingML of Part 1, content types and
  relationships of Part 2; source https://ecma-international.org/publications-and-standards/standards/ecma-376/,
  files unmodified under Ecma's default copyright notice, `Word/Schemas/NOTICE-Ecma.txt`, packed under
  `schemas/`). Markup compatibility (Part 3) is processed first: namespaces declared ignorable and not understood
  (`w14`, `w15`...) are removed, `mc:ProcessContent` keeps their content, `mc:AlternateContent` resolves to its
  first understood choice or its fallback, `mc:MustUnderstand` of an unknown namespace and malformed alternate
  content are errors; the content types and relationships parts are validated without it. Each attribute of the
  relationships namespace (`r:id`, `r:embed`...) must name a relationship its part declares. Each error gives the
  part, line, position, element path and the validator's message. Parts no shipped schema covers (core
  properties, custom XML data, extensions) are listed as unchecked; pictures are not read. The schemas compile
  once (the strict schema's three invalid `"off"` defaults read as `false`, see the notice) and validations run
  one at a time. Every Word writer of the package (model, editor, merge, HTML, Markdown and Excel to Word)
  produces documents that validate clean.
- `WordToMarkdown.Convert` (`WordMarkdownOptions`, `WordMarkdownResult`, `WordMarkdownImage`): a Word document to
  GitHub-flavoured Markdown. Headings from outline levels 1 to 6 (a numbered heading keeps its label, the bold or
  italic of its style is not marked), paragraphs, hard line breaks, bold, italic and strike (`**`, `*`, `~~`, spaces
  kept outside the markers), nested bulleted and numbered lists (each item indented under its parent's content,
  numbers counted from the level's start), pipe tables (first row as header, merged cells spread as empty cells),
  `http`, `https` and `mailto` links, footnotes and endnotes as `[^1]` and `[^e1]` defined at the end, pictures as
  reference images whose bytes are returned with their paths, text boxes after their paragraph; tracked changes
  accepted, hidden text left out, text escaped so it reads back literally. What Markdown cannot carry is listed in
  `Gaps`: underline, superscript, subscript and other formatting, headers, footers, comments, page and column
  breaks, empty paragraphs, bookmarks and other link schemes, letter or roman list numbers (written as numbers),
  several paragraphs, lists or nested tables in a cell or a note (joined on one line).
- `ExcelToHtml.Convert` (`ExcelHtmlOptions`): a workbook to one standalone HTML page, each sheet's used range a table
  of the values as Excel displays them (the texts of the CSV export), with column widths (`7w + 5` pixels), merged
  cells as `colspan` and `rowspan` (clipped to the used range, an overlapping range from a damaged file ignored),
  and the font, size, weight, slant, underline, strike, colour, fill, alignment (General: numbers and dates right,
  booleans and errors centred), wrapping and border of each cell as classes of an embedded style sheet; grid lines
  and sheet titles optional, a cell bound (`MaxCells`, default 1,000,000). Workbook text is encoded and font names
  reduced to letters, digits, spaces, hyphens and underscores, colours accepted only as `RRGGBB`; the body passes
  the default `HtmlSanitizer` unchanged. Not rendered: row heights, hidden rows and columns, text running on into
  empty cells (clipped to its cell), number format colours, conditional formats, pictures and charts.
- Fraction number formats are shown as fractions (they were shown as decimals): `# ?/?`, `# ??/??` and more
  placeholders take the fraction closest to the value whose denominator has at most that many digits (up to nine),
  a written denominator (`# ?/8`, `# ??/100`) is kept and the numerator rounded half away from zero, without an
  integer part the fraction is improper (`?/?` shows `3/2`). A zero integer part is left to its placeholders
  (`# ?/?` shows ` 1/2`), a zero fraction becomes spaces of its width (`2    `), a value rounding to zero shows
  `0` without a sign, `?` pads the numerator on the left and the denominator on the right; grouping, literals,
  percent and sections apply as for other numbers.
- PDF images with the `JBIG2Decode` filter (scanned pages) are decoded, read and rendered: the JBIG2 page their data
  holds, after the segments of their `JBIG2Globals`. Generic regions (arithmetic with typical prediction and adaptive
  pixels, or MMR), refinement regions (with typical prediction), text regions (arithmetic or Huffman, every reference
  corner, transposed, with refined instances), symbol dictionaries (generic, refined and aggregate symbols, Huffman
  collective bitmaps, retained contexts), pattern dictionaries and halftone regions, the fifteen standard Huffman
  tables and table segments, page information with its default pixel, combination operators, end of stripe on a page
  of unknown height. Not decoded: extended generic templates (the image is reported as not drawn); colour extensions
  and profiles are ignored.
- PDF images with the `JPXDecode` filter (JPEG 2000, ITU-T T.800 | ISO/IEC 15444-1) are decoded, read and rendered:
  bare codestreams and JP2/JPX files, every tile and tile-part, the five progression orders and POC changes,
  precincts, quality layers, packed packet headers (PPM, PPT), SOP and EPH markers, every code-block style (bypass,
  context reset, termination of each pass, vertically causal contexts, predictable termination, segmentation
  symbols), scalar quantization (derived or expounded) or none, the region of interest shift, the reversible 5-3 and
  irreversible 9-7 wavelets, the RCT and ICT component transforms, subsampled and signed components of 1 to 30 bits,
  the JP2 palette, component mapping, channel definitions (colours reordered, opacity, premultiplied opacity) and
  colour specification (sRGB, grey, sYCC, CMYK, CIE Lab with its ranges, ICC profiles by their data colour space).
  An image without `ColorSpace` takes the colour space of its data, an `Indexed` one takes the samples as indices,
  and `SMaskInData` 1 or 2 makes the opacity channel the soft mask. A codestream cut short decodes what it holds.
  Refused, the image then reported as not drawn: the high-throughput block coder (ISO/IEC 15444-15), part 2
  markers and wavelets, components of more than 30 bits, and a codestream claiming far more samples than its data
  can code (more than 16 million and 4096 per byte). ICC profiles are not applied.
- `XlsxWorkbook.Recalculate` computes every formula of a workbook again, in dependency order across sheets
  (without recursion), and stores the results; a date cell stays a date. Operators with Excel precedence
  (`-2^2` is 4), text and boolean coercion, comparison of numbers, text and booleans, ranges and array constants
  computed element by element, absolute references, whole rows and columns, references to other sheets, Excel
  errors (`#DIV/0!`, `#VALUE!`, `#REF!`, `#NAME?`, `#NUM!`, `#N/A`), and 101 functions: mathematical and
  statistical (SUM, SUMIFS, SUMPRODUCT, AVERAGEIFS, COUNTIFS, ROUND...), logical and information (IF, IFS,
  IFERROR, SWITCH, IS...), lookup (VLOOKUP, HLOOKUP, XLOOKUP, INDEX, MATCH), text (TEXT, TEXTJOIN, SUBSTITUTE,
  FIND...) and dates (DATE, EDATE, EOMONTH, DATEDIF, WEEKDAY...). A cycle throws
  `XlsxCircularReferenceException` with its cells. A formula it does not compute (a defined name, a table
  reference, a reference over several sheets, the union or intersection operator, INDIRECT, OFFSET, another
  function, a syntax error) keeps its stored result and is listed in `XlsxRecalculation.Unsupported`; a formula
  returning an array fills its own cell only.
- Loading an `.xlsx` gives each cell of a shared formula its own formula, the group's first one with its
  relative references moved to the cell (they were read without a formula before).
- `XlsxEditor` (`OmniEurope.Documents.Excel.Editing`): opens an `.xlsx` and edits cells in place.
  `XlsxEditableSheet.SetValue` (number, text, boolean, date, empty) and `SetFormula` change one cell of the
  worksheet part; every other part (charts, pictures, conditional formats, validations, comments, tables, pivot
  caches) is saved byte for byte. A new cell takes the format of its row or column; text is written inline;
  Excel recalculates on opening (`fullCalcOnLoad`), and the calculation chain is dropped when a formula cell
  changes. `GetValue` and `GetFormula` read what the file holds. Cells heading a shared formula, inside an
  array formula or in a table header are refused with `NotSupportedException`.
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

- Word to PDF draws Symbol and Wingdings text closer to Word. Symbol text (runs, symbols and list labels in the
  Symbol font) is still drawn with Liberation Sans look-alikes but each character advances by the Symbol font's
  own width, read from Adobe's Core 14 metrics file `Symbol.afm` (shipped unmodified with its licence
  `MustRead.html`, packed under `fonts/adobe/`): a Symbol bullet is 0.46 em wide. Wingdings and Webdings text is
  drawn with the bundled Noto Sans Symbols 2 (SIL OFL 1.1, Liberation Sans for a character it lacks) and keeps the
  line height of Liberation Sans, the Noto line (1.7 em) being much taller. `FontLibrary` resolves
  `Noto Sans Symbols 2` in every style to its regular face. Unchanged: the Wingdings table keeps its 21 look-alikes
  (an unknown code is still drawn as a bullet) and the line height of Symbol text is that of Liberation Sans.

- Word to PDF lays lines out as Word does: a single line holds the font's external leading (its hhea line gap beyond
  the Windows ascent and descent) above its text; the extra space of a multiple line spacing goes below the text;
  after a page or column break the paragraph mark starts a line of its own; a paragraph opening with a page or
  column break keeps its first-line indent for what follows the break, and its space before on the page it opens.
- Word to PDF: between two paragraphs the larger of the space after and the space before is kept, not their sum, as
  Word does; the full space before stays at the top of a page or column a hard break opens.
- Reading a Word: a VML shape or picture positioned absolutely (CSS `position:absolute`) floats like an anchored
  drawing: its margins place it from the column, margin, page or character and the paragraph, margin, page or line
  (`mso-position-*-relative`), with its `w10:wrap` and a negative z-index behind the text; Word to PDF no longer lays
  it out in the flow of the text.
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
- CCITT Group 4 (and two-dimensional Group 3) decoding finds the reference changes in one pass per row: very wide
  rows no longer take a time growing with the square of their changes.

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
