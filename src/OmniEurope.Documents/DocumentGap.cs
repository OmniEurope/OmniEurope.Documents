// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Resources;

namespace OmniEurope.Documents;

/// <summary>
/// Something a read or a conversion could not render faithfully, with a stable code. <see cref="Message"/> is the
/// English text listed in the <c>Gaps</c> of a result; <see cref="Describe"/> gives it in the language of a
/// culture (English and French are shipped). A gap without a code (a text added by the caller to
/// <c>WordDocument.Gaps</c>) is described with a generic label in a language other than English.
/// </summary>
public sealed class DocumentGap : IEquatable<DocumentGap>
{
    private const string UnknownKey = "UnknownGap";
    private static readonly ResourceManager Messages = new("OmniEurope.Documents.Resources.GapMessages", typeof(DocumentGap).Assembly);
    private readonly string[] _arguments;

    private DocumentGap(string code, string[] arguments, string message)
    {
        Code = code;
        _arguments = arguments;
        Message = message;
    }

    /// <summary>The stable code of the gap (<c>FontReplaced</c>, <c>ArabicWithoutJoining</c>...); empty for a gap
    /// known only by its text.</summary>
    public string Code { get; }

    /// <summary>The values the message names (a font family, a picture format...), in order.</summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>The English text, as listed in the <c>Gaps</c> of a result.</summary>
    public string Message { get; }

    /// <summary>The gap in the language of <paramref name="culture"/> (<see cref="CultureInfo.CurrentUICulture"/>
    /// when null); English for a language that is not shipped.</summary>
    public string Describe(CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        if (Code.Length == 0)
        {
            var label = Messages.GetString(UnknownKey, culture);
            return string.IsNullOrEmpty(label) ? Message : label;
        }

        var template = Messages.GetString(Code, culture) ?? throw new InvalidOperationException($"No message for gap {Code}.");
        return string.Format(culture, template, _arguments);
    }

    /// <summary>The English text.</summary>
    public override string ToString() => Message;

    /// <summary>Two gaps are equal when their English texts are.</summary>
    public bool Equals(DocumentGap? other) => other is not null && string.Equals(Message, other.Message, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DocumentGap);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Message);

    // A gap of the engine: its English text is the neutral message of its code.
    internal static DocumentGap Of(GapCode code, params string[] arguments)
    {
        var template = Messages.GetString(code.Value, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"No message for gap {code.Value}.");
        return new DocumentGap(code.Value, arguments, string.Format(CultureInfo.InvariantCulture, template, arguments));
    }

    // A gap known only by its text (added by the caller to a public list of gaps).
    internal static DocumentGap FromText(string text) => new(string.Empty, [], text);

    // The neutral (English) and the localized template of a code, for the tests that sweep the codes.
    internal static string? Template(string key, CultureInfo culture) =>
        Messages.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(key);
}

/// <summary>The code of a gap: a key of <c>Resources/GapMessages.resx</c>. The codes are the static fields of the
/// <c>*GapCodes</c> classes, one per area.</summary>
internal readonly record struct GapCode(string Value);
