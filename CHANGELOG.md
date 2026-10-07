# Changelog

Notable changes to this project are documented here, following the Keep a Changelog format.

## [Unreleased]

### Added

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

### Changed

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
