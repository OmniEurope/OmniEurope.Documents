# Changelog

Notable changes to this project are documented here, following the Keep a Changelog format.

## [Unreleased]

### Added

- Word, Excel, PDF, Markdown, CSV and HTML reading, writing and conversion, with the .NET base class
  library only.

### Changed

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
