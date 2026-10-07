<!-- SPDX-License-Identifier: EUPL-1.2 -->
# OmniEurope.Documents

A .NET library to **create, edit, convert and read documents**: Word, Excel, PDF, Markdown and CSV. One
NuGet package, no user interface, no dependency, licensed under EUPL-1.2.

> **Status: pre-release (0.1.0).** The work done and planned is tracked in [docs/plans](docs/plans/README.md).

## What the package does

| Format | Create | Edit | Read | Convert |
|---|---|---|---|---|
| Word (`.docx`) | yes | yes | text and structure | to PDF, to HTML |
| Excel (`.xlsx`) | yes | yes | cells and sheets | to PDF, to CSV |
| PDF | yes | merge, split, compress, reorder, rotate, stamp | text, **no OCR** | from Word, Excel, Markdown, HTML, images; to PNG |
| Markdown | yes | yes | yes | to PDF, to Word, to HTML |
| CSV | yes | yes | yes | to Excel |

PDF operations: merge several files, split a file (by pages or ranges), compress (images and streams),
extract the text of a PDF that contains text, read the metadata, render a page to a PNG image.

Word to HTML gives one standalone page (styles embedded, pictures as `data:` URIs, nothing fetched) in which
every paragraph carries `data-address`, the address `WordEditor` gives the same paragraph, so a viewer can
point at the paragraph an edit will touch. Document text is always encoded and only `http`, `https` and
`mailto` links keep their address.

Companion tools: HTML parsing and sanitising, text diff (lines and words).

## What it does not do

- **No OCR**: a scanned PDF (images only) gives no text.
- **No user interface**: components that display or edit documents belong to a separate UI library, which
  can use this package.
- **No lossless workbook editing yet**: saving a loaded `.xlsx` keeps cells, styles, merges, panes and
  filters, but drops charts, pictures, conditional formats, data validation, comments and pivot tables.
- **No formula evaluation**: formulas are kept with their last computed result, never recalculated.
- **No Word to Markdown yet**; Word to HTML leaves line and page breaks to the browser and lists what it
  approximates in its gaps.

## Layout

One package, one namespace per format, conversions on their own:

- `OmniEurope.Documents.Pdf`
- `OmniEurope.Documents.Word`
- `OmniEurope.Documents.Excel`
- `OmniEurope.Documents.Markdown`
- `OmniEurope.Documents.Csv`
- `OmniEurope.Documents.Html`
- `OmniEurope.Documents.Diff`
- `OmniEurope.Documents.Conversion` (Word to PDF, Word to HTML, Excel to PDF, HTML and Markdown to Word or
  PDF, images to PDF)

## Dependencies

**None.** The package uses only the .NET base class library (`System.IO.Compression`, `System.Xml`,
`System.Security.Cryptography`). Everything else (Word, Excel and PDF reading and writing, fonts, images,
Markdown, CSV, HTML, diff, rendering) is written in this repository. Adding a NuGet package to `src/` is
the owner's decision.

The test projects use xUnit v3, `Microsoft.Testing.Extensions.TrxReport` and `coverlet.MTP`; they never
ship in the package.

Third-party data shipped in the package: the Liberation (Sans, Serif, Mono), Carlito and Caladea fonts,
under the SIL Open Font License 1.1 (free commercial use, embedding allowed, licence included in
`src/OmniEurope.Documents/Fonts/`).

## Development

```powershell
dotnet build                                                   # build
dotnet test --project tests/OmniEurope.Documents.Tests         # fast tests (a few seconds)
dotnet test --project tests/OmniEurope.Documents.StressTests   # long tests, before every release
```

The long tests read thousands of damaged files and large documents, and compare conversions run in
parallel.

Git flow: `main` for released versions, `develop` for integration, `feature/*` for work in progress.

## Licence

EUPL-1.2, see [LICENSE](LICENSE).
