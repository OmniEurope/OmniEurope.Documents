// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using OmniEurope.Documents.Fonts;
using OmniEurope.Documents.Pdf.Editing;
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Writing;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>
/// Writes field values and their appearances as an incremental update (ISO 32000-1 §12.7.4): <c>V</c> on the
/// terminal field, <c>AS</c> on each button widget, <c>I</c> on a list box, and a new normal appearance on each
/// widget whose look depends on the value.
/// </summary>
internal sealed class FormFiller
{
    private readonly PdfObjectStore _store;
    private readonly IncrementalUpdate _update;
    private readonly PdfDocumentBuilder _builder;
    private readonly FieldAppearance _appearance;

    private FormFiller(PdfObjectStore store, FontLibrary? fonts)
    {
        _store = store;
        _update = new IncrementalUpdate(store);
        var number = store.NextObjectNumber.ToString(CultureInfo.InvariantCulture);
        _builder = new PdfDocumentBuilder(store.NextObjectNumber, "OEForm" + number + "_") { Fonts = fonts ?? FontLibrary.Default };
        _appearance = new FieldAppearance(_builder, store, FormTree.AcroForm(store));
    }

    public static byte[] Fill(byte[] original, IEnumerable<PdfFieldValue> values, FontLibrary? fonts)
    {
        var document = PdfForm.OpenForUpdate(original);
        var fields = FormTree.Collect(document).GroupBy(f => f.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var filler = new FormFiller(document.Store, fonts);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(values));
            if (!fields.TryGetValue(value.Name, out var field))
            {
                throw new ArgumentException($"The form has no field named '{value.Name}'.", nameof(values));
            }

            filler.Set(field, value);
        }

        filler.DropXfa();
        filler._builder.WriteFonts();
        var objects = filler._builder.Table.Objects.Select(o => (o.Number, 0, o.Value)).Concat(filler._update.Objects).ToList();
        return PdfIncrementalWriter.Append(original, document.Store, objects);
    }

    private void Set(FormFieldNode field, PdfFieldValue value)
    {
        switch (field.Kind)
        {
            case PdfFieldKind.Text:
                SetText(field, value);
                break;
            case PdfFieldKind.CheckBox or PdfFieldKind.RadioButton:
                SetToggle(field, value);
                break;
            case PdfFieldKind.ListBox or PdfFieldKind.ComboBox:
                SetChoice(field, value);
                break;
            default:
                throw new ArgumentException($"Field '{field.Name}' is a {field.Kind} and holds no value to fill.", nameof(value));
        }
    }

    private void SetText(FormFieldNode field, PdfFieldValue value)
    {
        var text = Single(field, value);
        var maxLength = (int)_store.Number(field.Dictionary, "MaxLen");
        if (maxLength > 0 && text.EnumerateRunes().Count() > maxLength)
        {
            throw new ArgumentException($"Field '{field.Name}' accepts at most {maxLength} characters.", nameof(value));
        }

        _update.Edit(field.Dictionary, field.Reference).Set("V", PdfString.FromText(text));
        foreach (var widget in field.Widgets)
        {
            _update.Edit(widget.Dictionary, widget.Reference).Set("AP", new PdfDictionary().Set("N", _appearance.Text(field, widget, text)));
        }
    }

    private void SetToggle(FormFieldNode field, PdfFieldValue value)
    {
        var states = field.Widgets.Select(w => PdfForm.OnState(_store, w.Dictionary)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var target = value.Checked switch
        {
            true when states.Count <= 1 => states.FirstOrDefault() ?? "Yes",
            true => throw new ArgumentException($"Field '{field.Name}' has several on states ({string.Join(", ", states)}): name one.", nameof(value)),
            false => "Off",
            null => Single(field, value),
        };
        if (target != "Off" && states.Count > 0 && !states.Contains(target, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Field '{field.Name}' has no state '{target}' (states: {string.Join(", ", states)}, Off).", nameof(value));
        }

        _update.Edit(field.Dictionary, field.Reference).Set("V", PdfName.Of(target));
        foreach (var widget in field.Widgets)
        {
            var on = PdfForm.OnState(_store, widget.Dictionary);
            var edited = _update.Edit(widget.Dictionary, widget.Reference);
            if (on is null)
            {
                // A widget without appearances gets a drawn on state (the value's, or Yes) and an empty Off state.
                on = target == "Off" ? "Yes" : target;
                edited.Set("AP", new PdfDictionary().Set("N", _appearance.Toggle(field, widget, on)));
            }

            edited.SetName("AS", on == target ? on : "Off");
        }
    }

    private void SetChoice(FormFieldNode field, PdfFieldValue value)
    {
        var options = PdfForm.Options(_store, field.Dictionary);
        var editable = field.Kind == PdfFieldKind.ComboBox && (field.Flags & (1 << 18)) != 0;
        var multiple = field.Kind == PdfFieldKind.ListBox && (field.Flags & (1 << 21)) != 0;
        if (value.Checked is not null || (value.Values.Count > 1 && !multiple))
        {
            throw new ArgumentException($"Field '{field.Name}' takes {(multiple ? "a list of choices" : "one choice")}.", nameof(value));
        }

        var chosen = value.Values.Select(v => Choose(field, options, v, editable)).ToList();
        var dictionary = _update.Edit(field.Dictionary, field.Reference);
        dictionary.Set("V", chosen.Count switch
        {
            0 => null,
            1 => PdfString.FromText(chosen[0].ExportValue),
            _ => new PdfArray(chosen.Select(c => (PdfObject)PdfString.FromText(c.ExportValue))),
        });
        var indexes = chosen.Select(c => options.IndexOf(c)).Where(i => i >= 0).Order().ToList();
        dictionary.Set("I", field.Kind == PdfFieldKind.ListBox && indexes.Count > 0 ? PdfArray.OfNumbers(indexes.Select(i => (double)i)) : null);
        var selected = chosen.Select(c => c.ExportValue).ToHashSet(StringComparer.Ordinal);
        foreach (var widget in field.Widgets)
        {
            var appearance = field.Kind == PdfFieldKind.ComboBox
                ? _appearance.Text(field, widget, chosen.FirstOrDefault()?.DisplayText ?? string.Empty)
                : _appearance.List(field, widget, options, selected);
            _update.Edit(widget.Dictionary, widget.Reference).Set("AP", new PdfDictionary().Set("N", appearance));
        }
    }

    // An option given by its export value or its displayed text; free text only in an editable combo box.
    private static PdfChoiceOption Choose(FormFieldNode field, List<PdfChoiceOption> options, string value, bool editable) =>
        options.Find(o => o.ExportValue == value) ?? options.Find(o => o.DisplayText == value)
        ?? (editable ? new PdfChoiceOption(value, value) : throw new ArgumentException($"Field '{field.Name}' has no choice '{value}'.", nameof(value)));

    private static string Single(FormFieldNode field, PdfFieldValue value) =>
        value is { Checked: null, Values.Count: 1 } ? value.Values[0] : throw new ArgumentException($"Field '{field.Name}' takes one value.", nameof(value));

    // A hybrid form's XFA stream would be shown instead of the filled fields by viewers that read it: it is dropped.
    private void DropXfa()
    {
        if (FormTree.AcroForm(_store) is not { } acroForm || !acroForm.ContainsKey("XFA"))
        {
            return;
        }

        if (_store.Catalog["AcroForm"] is PdfReference reference)
        {
            _update.Edit(acroForm, reference).Remove("XFA");
            return;
        }

        var copy = new PdfDictionary();
        foreach (var (key, entry) in acroForm.Entries.Where(e => e.Key != "XFA"))
        {
            copy.Set(key, entry);
        }

        _update.Edit(_store.Catalog, _store.Trailer["Root"] as PdfReference).Set("AcroForm", copy);
    }
}
