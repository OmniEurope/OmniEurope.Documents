// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>
/// Fills content controls by tag. Every value is checked before the control is touched: a value that does not
/// fit (unparsable date, entry not in a drop-down list, unsupported kind) is reported as a gap and the control stays as
/// it was.
/// </summary>
internal sealed class WordContentControlFiller(IReadOnlyList<ScannedControl> controls, Func<string, bool> isPlaceholderStyle)
{
    private const string DefaultChecked = "2612";
    private const string DefaultUnchecked = "2610";

    public WordContentControlFill Fill(IReadOnlyDictionary<string, string> values) =>
        Run(values.Keys, (tag, control) => FillOne(control, values[tag] ?? string.Empty));

    public WordContentControlFill SetDate(string tag, DateOnly date) =>
        Run([tag], (_, control) => control.Info.Kind == WordContentControlKind.Date ? WriteDate(control, date) : DocumentGap.Of(ContentControlGapCodes.ContentControlNotDate));

    public WordContentControlFill SelectItem(string tag, string valueOrDisplayText) =>
        Run([tag], (_, control) => control.Info.Kind is WordContentControlKind.ComboBox or WordContentControlKind.DropDownList
            ? FillList(control, valueOrDisplayText)
            : DocumentGap.Of(ContentControlGapCodes.ContentControlNotList));

    public WordContentControlFill SetChecked(string tag, bool value) =>
        Run([tag], (_, control) => control.Info.Kind == WordContentControlKind.Checkbox ? WriteChecked(control, value) : DocumentGap.Of(ContentControlGapCodes.ContentControlNotCheckbox));

    // Applies an action to every control of every tag; the action returns null, or why it refused.
    private WordContentControlFill Run(IEnumerable<string> tags, Func<string, ScannedControl, DocumentGap?> apply)
    {
        var filled = new List<string>();
        var unknown = new List<string>();
        var gaps = new List<WordContentControlGap>();
        var count = 0;
        foreach (var tag in tags)
        {
            var matches = controls.Where(c => string.Equals(c.Info.Tag, tag, StringComparison.Ordinal)).ToList();
            if (matches.Count == 0)
            {
                unknown.Add(tag);
                continue;
            }

            foreach (var control in matches)
            {
                if (apply(tag, control) is { } reason)
                {
                    gaps.Add(new WordContentControlGap(tag, reason.Message) { ReasonDetail = reason });
                    continue;
                }

                count++;
                if (!filled.Contains(tag))
                {
                    filled.Add(tag);
                }
            }
        }

        return new WordContentControlFill(filled, unknown, gaps, count);
    }

    private DocumentGap? FillOne(ScannedControl control, string value) => control.Info.Kind switch
    {
        WordContentControlKind.Text or WordContentControlKind.RichText => WordContentControlText.Replace(control, value, isPlaceholderStyle),
        WordContentControlKind.Date => FillDate(control, value),
        WordContentControlKind.ComboBox or WordContentControlKind.DropDownList => FillList(control, value),
        WordContentControlKind.Checkbox => ParseBool(value) is { } state ? WriteChecked(control, state) : DocumentGap.Of(ContentControlGapCodes.ContentControlNotBoolean, value),
        _ => DocumentGap.Of(ContentControlGapCodes.ContentControlKindNotFillable, control.Info.Kind.ToString()),
    };

    private DocumentGap? FillList(ScannedControl control, string value)
    {
        var items = control.Info.ListItems;
        var item = items.FirstOrDefault(i => i.Value == value)
            ?? items.FirstOrDefault(i => i.DisplayText == value)
            ?? items.FirstOrDefault(i => string.Equals(i.DisplayText, value, StringComparison.OrdinalIgnoreCase));
        // A combo box accepts free text, as Word does; a drop-down list stays closed on its entries, even when it has none.
        if (item is null && control.Info.Kind != WordContentControlKind.ComboBox)
        {
            return DocumentGap.Of(ContentControlGapCodes.ContentControlValueNotInList, value);
        }

        return WordContentControlText.Replace(control, item?.DisplayText ?? value, isPlaceholderStyle);
    }

    private DocumentGap? FillDate(ScannedControl control, string value)
    {
        var (format, culture, cultureMissing) = Settings(control.Info.Date);
        value = value.Trim();
        DateOnly date;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            date = iso;
        }
        else if (!cultureMissing && DateOnly.TryParseExact(value, format, culture, DateTimeStyles.None, out var local))
        {
            date = local;
        }
        else
        {
            return DocumentGap.Of(
                cultureMissing ? ContentControlGapCodes.ContentControlDateUnparsableLanguageMissing : ContentControlGapCodes.ContentControlDateUnparsable,
                value,
                format);
        }

        return WriteDate(control, date);
    }

    private DocumentGap? WriteDate(ScannedControl control, DateOnly date)
    {
        var (format, culture, _) = Settings(control.Info.Date);
        var text = date.ToString(format, culture);
        var reason = WordContentControlText.Replace(control, text, isPlaceholderStyle);
        if (reason is null)
        {
            control.Sdt.Element(W + "sdtPr")!.Element(W + "date")!.SetAttributeValue(W + "fullDate", date.ToString("yyyy-MM-dd'T00:00:00Z'", CultureInfo.InvariantCulture));
        }

        return reason;
    }

    // The control's format and culture; the culture falls back to the invariant one when the host lacks it.
    private static (string Format, CultureInfo Culture, bool CultureMissing) Settings(WordContentControlDate? date)
    {
        var culture = CultureInfo.InvariantCulture;
        var missing = false;
        if (!string.IsNullOrEmpty(date?.Locale))
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(date.Locale);
            }
            catch (CultureNotFoundException)
            {
                missing = true;
            }
        }

        return (string.IsNullOrEmpty(date?.Format) ? culture.DateTimeFormat.ShortDatePattern : date.Format, culture, missing);
    }

    private DocumentGap? WriteChecked(ScannedControl control, bool value)
    {
        var checkbox = control.Sdt.Element(W + "sdtPr")!.Element(WordContentControlScanner.W14 + "checkbox")!;
        var state = checkbox.Element(WordContentControlScanner.W14 + (value ? "checkedState" : "uncheckedState"));
        var fallback = value ? DefaultChecked : DefaultUnchecked;
        var glyph = Glyph(state?.Attribute(WordContentControlScanner.W14 + "val")?.Value ?? fallback, fallback);
        var reason = WordContentControlText.Replace(control, glyph, isPlaceholderStyle, state?.Attribute(WordContentControlScanner.W14 + "font")?.Value);
        if (reason is not null)
        {
            return reason;
        }

        var element = checkbox.Element(WordContentControlScanner.W14 + "checked");
        if (element is null)
        {
            element = new XElement(WordContentControlScanner.W14 + "checked");
            checkbox.AddFirst(element);
        }

        element.SetAttributeValue(WordContentControlScanner.W14 + "val", value ? "1" : "0");
        return null;
    }

    private static string Glyph(string hex, string fallback) =>
        int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code is > 0 and <= 0x10FFFF && !(code is >= 0xD800 and <= 0xDFFF)
            ? char.ConvertFromUtf32(code)
            : char.ConvertFromUtf32(Convert.ToInt32(fallback, 16));

    private static readonly HashSet<string> TrueWords = new(StringComparer.OrdinalIgnoreCase) { "true", "1", "yes", "oui", "on", "checked" };
    private static readonly HashSet<string> FalseWords = new(StringComparer.OrdinalIgnoreCase) { "false", "0", "no", "non", "off", "unchecked" };

    private static bool? ParseBool(string value)
    {
        var word = value.Trim();
        return TrueWords.Contains(word) ? true : FalseWords.Contains(word) ? false : null;
    }
}
