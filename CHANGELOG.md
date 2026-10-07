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

- TIFF: a tile or strip size of zero is refused with `InvalidDataException`.
- PDF compression: images made transparent by a `/Mask` (colour key or explicit mask) are kept as they are
  instead of being re-encoded without their transparency.
