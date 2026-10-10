// SPDX-License-Identifier: EUPL-1.2
namespace OmniEurope.Documents.Excel;

/// <summary>
/// An Excel workbook (.xlsx): create one, or load one to read or change it, then save it. Loading keeps
/// values, formulas (as text, with their last result), the styles this model describes, column widths,
/// merged ranges, frozen panes and the auto-filter. Charts, pictures, conditional formats, data
/// validation, comments and pivot tables are not part of the model: saving a loaded workbook drops them.
/// </summary>
public sealed class XlsxWorkbook
{
    private readonly List<XlsxWorksheet> _worksheets = [];

    /// <summary>The worksheets, in tab order.</summary>
    public IReadOnlyList<XlsxWorksheet> Worksheets => _worksheets;

    /// <summary>The document title (core properties).</summary>
    public string? Title { get; set; }

    /// <summary>The author (core properties).</summary>
    public string? Author { get; set; }

    /// <summary>True when serial numbers count from 1904 (old Mac workbooks).</summary>
    public bool Date1904 { get; set; }

    /// <summary>When true (default), text that starts like a formula (<c>= + - @</c>) is saved with Excel's
    /// literal-text marker, so editing the cell later never runs it.</summary>
    public bool ProtectFormulaLikeText { get; set; } = true;

    /// <summary>Adds a worksheet at the end.</summary>
    public XlsxWorksheet AddWorksheet(string name)
    {
        if (_worksheets.Exists(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"A sheet named '{name}' already exists.", nameof(name));
        }

        var worksheet = new XlsxWorksheet(this, name);
        _worksheets.Add(worksheet);
        return worksheet;
    }

    /// <summary>The worksheet named <paramref name="name"/> (case-insensitive).</summary>
    public XlsxWorksheet Worksheet(string name) =>
        _worksheets.Find(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"No sheet named '{name}'.");

    /// <summary>
    /// Computes every formula of the workbook again and stores the results as the cells' values, in dependency
    /// order across sheets. Operators, references and ranges between sheets, array constants and the common
    /// mathematical, statistical, logical, lookup, text and date functions are computed; errors follow Excel
    /// (<c>#DIV/0!</c>, <c>#VALUE!</c>, <c>#REF!</c>, <c>#N/A</c>...). A formula using something else (a defined
    /// name, a table reference, a reference over several sheets, the union or intersection operator, INDIRECT,
    /// OFFSET, a function not computed) keeps its stored result and is listed in
    /// <see cref="XlsxRecalculation.Unsupported"/>. A formula whose result is an array stores its first element in
    /// its own cell only. Call it before exporting to PDF or CSV after changing values.
    /// </summary>
    /// <exception cref="XlsxCircularReferenceException">Formulas depend on their own result.</exception>
    public XlsxRecalculation Recalculate(XlsxRecalculationOptions? options = null) =>
        new Formulas.XlsxCalculator(this, options?.Now ?? DateTime.Now).Run();

    /// <summary>Removes a worksheet.</summary>
    public void RemoveWorksheet(XlsxWorksheet worksheet) => _worksheets.Remove(worksheet);

    /// <summary>Loads a workbook.</summary>
    /// <exception cref="DocumentFormatException">The file is not a valid workbook or breaks the limits.</exception>
    public static XlsxWorkbook Load(Stream stream, PackageLimits? limits = null) => XlsxPackageReader.Read(stream, limits);

    /// <summary>Loads a workbook from bytes.</summary>
    public static XlsxWorkbook Load(byte[] bytes, PackageLimits? limits = null) => Load(new MemoryStream(bytes, writable: false), limits);

    /// <summary>Saves the workbook to <paramref name="stream"/>.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_worksheets.Count == 0)
        {
            throw new InvalidOperationException("A workbook needs at least one worksheet.");
        }

        XlsxPackageWriter.Write(this, stream);
    }

    /// <summary>Saves the workbook to a byte array.</summary>
    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return stream.ToArray();
    }
}
