// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Fonts;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>The kind of an interactive form field (ISO 32000-1 §12.7.4).</summary>
public enum PdfFieldKind
{
    /// <summary>A text field (<c>/FT /Tx</c>).</summary>
    Text,

    /// <summary>A check box (<c>/FT /Btn</c>, neither radio nor push button).</summary>
    CheckBox,

    /// <summary>A group of radio buttons (<c>/FT /Btn</c> with the Radio flag).</summary>
    RadioButton,

    /// <summary>A push button: it holds no value.</summary>
    PushButton,

    /// <summary>A list box (<c>/FT /Ch</c> without the Combo flag).</summary>
    ListBox,

    /// <summary>A combo box (<c>/FT /Ch</c> with the Combo flag).</summary>
    ComboBox,

    /// <summary>A signature field (<c>/FT /Sig</c>).</summary>
    Signature,
}

/// <summary>One choice of a list or combo box: the value stored in the field and the text shown.</summary>
/// <param name="ExportValue">The value written to the field when this choice is selected.</param>
/// <param name="DisplayText">The text the box shows (the export value when the option is a single string).</param>
public sealed record PdfChoiceOption(string ExportValue, string DisplayText);

/// <summary>One widget of a field: where it is drawn and what its current appearance shows.</summary>
/// <param name="PageNumber">The 1-based page whose annotations list the widget; 0 when no page lists it.</param>
/// <param name="Rectangle">The widget rectangle in the page's user space.</param>
/// <param name="OnState">The appearance state that means "on" for a check box or radio button widget, else null.</param>
/// <param name="AppearanceText">The text drawn by the widget's normal appearance (what a viewer that does not
/// regenerate appearances shows), empty when it has none.</param>
public sealed record PdfFormWidget(int PageNumber, PdfRectangle Rectangle, string? OnState, string AppearanceText);

/// <summary>
/// A terminal field of a PDF form with its value. <see cref="Value"/> is the text of a text field, the selected
/// state of a check box or radio group (<c>Off</c> when none), the selected export value of a choice box; a
/// multiple selection list gives every selected value in <see cref="Values"/>.
/// </summary>
/// <param name="Name">The fully qualified name: the partial names from the root, joined with dots.</param>
/// <param name="Kind">The field kind.</param>
/// <param name="Value">The value, or null when the field has none.</param>
/// <param name="Values">The selected values of a list box (one or more), else the value alone or nothing.</param>
/// <param name="Options">The choices of a list or combo box.</param>
/// <param name="States">The "on" states of a check box or radio group, in widget order.</param>
/// <param name="Widgets">The widgets of the field.</param>
public sealed record PdfFormField(string Name, PdfFieldKind Kind, string? Value, IReadOnlyList<string> Values, IReadOnlyList<PdfChoiceOption> Options,
    IReadOnlyList<string> States, IReadOnlyList<PdfFormWidget> Widgets)
{
    /// <summary>The field flags (<c>Ff</c>, inherited).</summary>
    public int Flags { get; init; }

    /// <summary>The longest text a text field accepts, or null.</summary>
    public int? MaxLength { get; init; }

    /// <summary>The user may not change the value (flag bit 1).</summary>
    public bool IsReadOnly => (Flags & 1) != 0;

    /// <summary>The field must have a value when the form is submitted (flag bit 2).</summary>
    public bool IsRequired => (Flags & 2) != 0;

    /// <summary>A text field on several lines (flag bit 13).</summary>
    public bool IsMultiline => Kind == PdfFieldKind.Text && (Flags & (1 << 12)) != 0;

    /// <summary>A list box that accepts several selected values (flag bit 22).</summary>
    public bool IsMultiSelect => Kind == PdfFieldKind.ListBox && (Flags & (1 << 21)) != 0;
}

/// <summary>A value to write into a field by <see cref="PdfForm.Fill"/>.</summary>
public sealed record PdfFieldValue
{
    /// <summary>A text, a check box or radio state name (<c>Off</c> clears it), or a choice (its export value or
    /// its displayed text).</summary>
    public PdfFieldValue(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Values = [value];
    }

    /// <summary>Several selected choices of a multiple selection list box (none clears it).</summary>
    public PdfFieldValue(string name, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(values);
        Name = name;
        Values = [.. values];
    }

    private PdfFieldValue(string name, bool on)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Values = [];
        Checked = on;
    }

    /// <summary>The fully qualified field name.</summary>
    public string Name { get; }

    /// <summary>The values to write.</summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>For <see cref="Check"/>: the check box is set to its (single) on state, or to <c>Off</c>.</summary>
    public bool? Checked { get; }

    /// <summary>Checks or clears a check box without naming its on state.</summary>
    public static PdfFieldValue Check(string name, bool on) => new(name, on);
}

/// <summary>Options of <see cref="PdfForm.Fill"/>.</summary>
public sealed record PdfFormFillOptions
{
    /// <summary>The fonts the appearances are drawn with (the field's font family, resolved like any text); the
    /// bundled fonts by default.</summary>
    public FontLibrary? Fonts { get; init; }

    /// <summary>After filling, draws every widget on its page and removes the form (<see cref="PdfForm.Flatten"/>).</summary>
    public bool Flatten { get; init; }
}
