<!-- SPDX-License-Identifier: EUPL-1.2 -->
# OmniEurope.Documents

A .NET library to **create, edit, convert and read documents**: Word, Excel, PDF, Markdown and CSV. One
NuGet package, no user interface, no dependency, licensed under EUPL-1.2.

> **Status: pre-release (0.1.0).** The work done and planned is tracked in [docs/plans](docs/plans/README.md).

## What the package does

| Format | Create | Edit | Read | Convert |
|---|---|---|---|---|
| Word (`.docx`) | yes | yes, tracked changes accepted or rejected, fields and contents updated | text and structure, charts, schema validation | to PDF, to HTML, to Markdown |
| Excel (`.xlsx`) | yes | yes, in place without loss | cells, sheets and charts | to PDF, to CSV, to HTML |
| PDF | yes, AES-256 encryption, PDF/A-2b and PDF/A-2u | merge, split, compress, reorder, rotate, stamp, encrypt, redact, fill and flatten forms, sign (PAdES B-B) | text, form fields, signatures verified, **no OCR** | from Word, Excel, Markdown, HTML, images; to PNG |
| Markdown | yes | yes | yes | to PDF, to Word, to HTML |
| CSV | yes | yes | yes | to Excel |

PDF operations: merge several files, split a file (by pages or ranges), compress (images and streams),
extract the text of a PDF that contains text, read the metadata, render a page to a PNG image (`PdfRenderer`:
paths, text, images including JBIG2 and JPEG 2000, shadings, tiling and shading patterns, the sixteen blend modes,
soft masks and transparency groups, composited in RGB). Forms
(`PdfForm`) are read, filled with appearances drawn in embedded fonts and flattened, as incremental updates that
keep the original bytes. `PdfDocumentBuilder.Conformance` and the conversion options write PDF/A-2b or
PDF/A-2u (sRGB output intent with a profile computed in code, XMP identification, no encryption). `PdfSigner`
signs with an application certificate (PAdES baseline B-B, detached CMS written with `System.Formats.Asn1`) and
verifies signatures (byte range, digest, signature, signing certificate; certificate trust is the application's).

`PdfRedactor.Redact` truly redacts (the text and image pixels under an area are removed from the new file, not covered), and refuses (`PdfRedactionException`, no
document produced) a page whose text is shown with a font it cannot resolve, since it could not tell whether that text
lies under an area.

Charts (`Chart`, read from `XlsxWorksheet.Charts` and from `WordChart`) are read with their series, titles, legend
and colours, and drawn as vector graphics in the PDF of a Word or Excel conversion: clustered, stacked, percent
stacked and horizontal bars, lines, pies and doughnuts. A chart of another type (3-D, area, scatter, radar, stock,
surface, bubble) is drawn as its frame and title only, its type reported as a gap. A combination chart draws its first
bar, line, pie or doughnut type with that type's series and reports the other types of its plot area as gaps. A chart
of a type introduced by Office 2016 (waterfall, treemap, funnel...) is skipped and reported in a Word document, and
drawn as an empty frame, reported, in an Excel sheet. Axes use an automatic scale, and Word to HTML and Markdown leave
charts out (reported). A series holds at most 100,000 points and a chart at most 1,000,000 (its series times its
longest series): the series past that are left out and the chart is reported. Sizes are those of the points a chart
holds, not those it declares; a number that is not finite reads as missing, and the gap width, hole size and first
slice angle are held to the ranges of the schema. A chart, drawing or theme part that cannot be read is skipped (a
theme: the Office colours are used) and reported (for a workbook, in the gaps of Excel to PDF), and does not fail the
load of the document or workbook.

Word to HTML gives one standalone page (styles embedded, pictures as `data:` URIs, nothing fetched) in which
every paragraph carries `data-address`, the address `WordEditor` gives the same paragraph, so a viewer can
point at the paragraph an edit will touch. Document text is always encoded and only `http`, `https` and
`mailto` links keep their address.

Companion tools: HTML parsing and sanitising, text diff (lines and words).

## What it does not do

- **No OCR**: a scanned PDF (images only) gives no text.
- **No user interface**: components that display or edit documents belong to a separate UI library, which
  can use this package.
- **Saving a loaded `XlsxWorkbook` is not lossless**: it keeps cells, styles, merges, panes and filters only.
  `XlsxEditor` edits cells in place and keeps everything else the file holds (charts, pictures, conditional
  formats, validations, comments, pivot tables). Its charts are read (`XlsxWorksheet.Charts`) but not written.
- **Formulas are recalculated only on request** (`XlsxWorkbook.Recalculate`) and only with the operators and
  common functions the engine computes; defined names, table and multi-sheet references, INDIRECT and OFFSET
  keep their last computed result and are reported.
- Word to HTML leaves line and page breaks to the browser and lists what it approximates in its gaps; Word to
  Markdown lists what Markdown cannot carry (underline, headers and footers, comments, merged cells...).
- Word to PDF wraps text around the floating shapes of body paragraphs (tight and through wrapping by the bounding
  box; not in tables, headers or footers), orders right-to-left and mixed text by the Unicode Bidirectional Algorithm
  with bidirectional classes derived from the Unicode categories, and draws Arabic letters without joining forms.

## Layout

One package, one namespace per format, conversions on their own:

- `OmniEurope.Documents.Pdf`
- `OmniEurope.Documents.Pdf.Forms` (`PdfForm`: read, fill and flatten interactive forms)
- `OmniEurope.Documents.Pdf.Signing` (`PdfSigner`: PAdES baseline B-B signing and verification)
- `OmniEurope.Documents.Word`
- `OmniEurope.Documents.Word.Editing` (`WordEditor`: in-place edits, tracked changes accepted or rejected)
- `OmniEurope.Documents.Word.Validation` (`WordSchemaValidator`: validation against the ECMA-376 schemas)
- `OmniEurope.Documents.Excel` (`XlsxWorkbook.Recalculate`: formula recalculation)
- `OmniEurope.Documents.Excel.Editing` (`XlsxEditor`: in-place cell edits)
- `OmniEurope.Documents.Markdown`
- `OmniEurope.Documents.Csv`
- `OmniEurope.Documents.Html`
- `OmniEurope.Documents.Diff`
- `OmniEurope.Documents.Conversion` (Word to PDF, Word to HTML, Word to Markdown, Excel to PDF, Excel to HTML,
  HTML and Markdown to Word or PDF, images to PDF; `WordFieldUpdater`: tables of contents and fields updated from
  the layout)

## Dependencies

**None.** The package uses only the .NET base class library (`System.IO.Compression`, `System.Xml`,
`System.Security.Cryptography`, `System.Formats.Asn1`). Everything else (Word, Excel and PDF reading and writing, fonts, images,
Markdown, CSV, HTML, diff, rendering) is written in this repository. Adding a NuGet package to `src/` is
the owner's decision.

The test projects use xUnit v3, `Microsoft.Testing.Extensions.TrxReport` and `coverlet.MTP`; they never
ship in the package.

Third-party data shipped in the package:

- the Liberation (Sans, Serif, Mono), Carlito and Caladea fonts and Noto Sans Symbols 2 (release
  `NotoSansSymbols2-v2.008` of https://github.com/notofonts/symbols, unhinted TrueType), under the SIL Open
  Font License 1.1 (free commercial use, embedding allowed, sold only with software; licences in
  `src/OmniEurope.Documents/Fonts/Bundled/LICENSE-*.txt`, packed under `fonts/`);
- the Symbol font metrics of Adobe's Core 14 AFM files (`Symbol.afm` from
  https://download.macromedia.com/pub/developer/opentype/tech-notes/Core14_AFMs.zip), unmodified, with
  their licence `MustRead.html` (use, copy and distribution for any purpose, copyright notices kept,
  modifications noted), in `src/OmniEurope.Documents/Fonts/Bundled/Adobe/`, packed under `fonts/adobe/`
  and embedded in the assembly next to the metrics;
- the Office Open XML schemas (W3C XML Schema) of ECMA-376 5th edition, from
  https://ecma-international.org/publications-and-standards/standards/ecma-376/: the transitional schemas of
  Part 4 (December 2016), the strict schemas of Part 1 (December 2016) and the content types and relationships
  schemas of Part 2 (December 2021), only those a Word package needs, unmodified, under Ecma's default copyright
  notice (copies and implementing works allowed, files unmodified, notice kept), in
  `src/OmniEurope.Documents/Word/Schemas/` with their notice `NOTICE-Ecma.txt` (sources, archive hashes, the one
  in-memory workaround), embedded in the assembly and the notice packed under `schemas/`.

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
