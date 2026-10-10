// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;
using OmniEurope.Documents.Pdf.Reading;
using OmniEurope.Documents.Pdf.Text;

namespace OmniEurope.Documents.Pdf.Forms;

/// <summary>
/// The interactive form of a PDF (AcroForm, ISO 32000-1 §12.7): reads its fields, fills them and flattens them.
/// Filling and flattening append an incremental update, so the original bytes, and the signatures they hold,
/// stay intact. Filling writes each value and a new appearance for every widget it changes (drawn with embedded
/// font subsets), so the value shows in viewers that do not regenerate appearances. Encrypted and damaged files
/// are refused with <see cref="NotSupportedException"/>.
/// </summary>
public static class PdfForm
{
    /// <summary>The terminal fields of the document's form, in field tree order (none when it has no form).</summary>
    public static IReadOnlyList<PdfFormField> Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var store = document.Store;
        return FormTree.Collect(document).Select(node => ToField(store, node)).ToList();
    }

    /// <summary>Writes values into fields by their full names and appends the update.</summary>
    /// <exception cref="ArgumentException">An unknown field, a push button or signature, a state or choice the field
    /// does not have, several values for a single choice, or a text longer than the field's maximum length.</exception>
    /// <exception cref="NotSupportedException">An encrypted or damaged file, or a field or widget written as a direct object.</exception>
    public static byte[] Fill(byte[] original, IEnumerable<PdfFieldValue> values, PdfFormFillOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(values);
        options ??= new PdfFormFillOptions();
        var filled = FormFiller.Fill(original, values, options.Fonts);
        return options.Flatten ? FormFlattener.Flatten(filled) : filled;
    }

    /// <summary>
    /// Draws every widget that is not hidden on its page with its current appearance, then removes the widgets and
    /// the form: the values become page content. A file without widgets comes back unchanged.
    /// </summary>
    /// <exception cref="NotSupportedException">An encrypted or damaged file, or a page written as a direct object.</exception>
    public static byte[] Flatten(byte[] original)
    {
        ArgumentNullException.ThrowIfNull(original);
        return FormFlattener.Flatten(original);
    }

    internal static PdfDocument OpenForUpdate(byte[] original)
    {
        var document = PdfDocument.Open(original);
        if (document.Store.IsEncrypted)
        {
            throw new NotSupportedException("Encrypted PDFs cannot be updated.");
        }

        if (document.Store.Repaired || document.Store.StartXref <= 0)
        {
            throw new NotSupportedException("The PDF's cross-reference information is damaged; it cannot be updated incrementally.");
        }

        return document;
    }

    /// <summary>The state other than Off in a widget's normal (else down) appearances.</summary>
    internal static string? OnState(PdfObjectStore store, PdfDictionary widget)
    {
        var appearances = store.Get<PdfDictionary>(widget, "AP");
        foreach (var key in new[] { "N", "D" })
        {
            if (store.Get(appearances, key) is PdfDictionary states and not PdfStream
                && states.Entries.Select(e => e.Key).FirstOrDefault(k => k != "Off") is { } state)
            {
                return state;
            }
        }

        return null;
    }

    /// <summary>The options of a choice field: single strings, or export and display pairs.</summary>
    internal static List<PdfChoiceOption> Options(PdfObjectStore store, PdfDictionary field) =>
        (store.Get<PdfArray>(field, "Opt")?.Items ?? []).Select(store.Resolve).Select(item => item switch
        {
            PdfString text => new PdfChoiceOption(text.ToText(), text.ToText()),
            PdfArray { Count: >= 2 } pair => new PdfChoiceOption(Text(store, pair[0]) ?? string.Empty, Text(store, pair[1]) ?? string.Empty),
            _ => null,
        }).OfType<PdfChoiceOption>().ToList();

    private static PdfFormField ToField(PdfObjectStore store, FormFieldNode node)
    {
        var values = node.Value switch
        {
            PdfArray array => array.Items.Select(i => Text(store, i)).OfType<string>().ToList(),
            var single => Text(store, single) is { } text ? [text] : new List<string>(),
        };
        var kind = node.Kind;
        var value = kind switch
        {
            PdfFieldKind.CheckBox or PdfFieldKind.RadioButton => values.FirstOrDefault() ?? "Off",
            PdfFieldKind.PushButton or PdfFieldKind.Signature => null,
            _ => values.FirstOrDefault(),
        };
        var widgets = node.Widgets.Select(w => new PdfFormWidget(w.PageNumber, AnnotationAppearance.Rectangle(store, w.Dictionary),
            kind is PdfFieldKind.CheckBox or PdfFieldKind.RadioButton ? OnState(store, w.Dictionary) : null, AppearanceText(store, w.Dictionary))).ToList();
        var maxLength = store.Get(node.Dictionary, "MaxLen") is PdfNumber max ? max.IntValue : (int?)null;
        return new PdfFormField(node.Name, kind, value, value is null ? [] : kind is PdfFieldKind.ListBox ? values : [value],
            kind is PdfFieldKind.ListBox or PdfFieldKind.ComboBox ? Options(store, node.Dictionary) : [],
            widgets.Select(w => w.OnState).OfType<string>().Distinct(StringComparer.Ordinal).ToList(), widgets)
        {
            Flags = node.Flags,
            MaxLength = maxLength,
        };
    }

    private static string AppearanceText(PdfObjectStore store, PdfDictionary widget)
    {
        try
        {
            return AnnotationAppearance.Normal(store, widget) is { } form ? SimpleTextOrder.Build(TextExtractor.Extract(store, form)) : string.Empty;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return string.Empty;
        }
    }

    private static string? Text(PdfObjectStore store, PdfObject? value) => store.Resolve(value) switch
    {
        PdfString text => text.ToText(),
        PdfName name => name.Value,
        _ => null,
    };
}
