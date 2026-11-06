// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Word;
using static OmniEurope.Documents.Tests.Conversion.WordFloatWrapTests;

namespace OmniEurope.Documents.Tests.Conversion;

/// <summary>
/// The gaps of the engine carry a stable code whose message is shipped in English (the neutral resources, the text
/// of <c>Gaps</c>) and in French (the <c>fr</c> satellite): every code of the <c>*GapCodes</c> classes is swept.
/// </summary>
public sealed class DocumentGapTests
{
    private const string UnknownKey = "UnknownGap";
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly ResourceManager Messages = new("OmniEurope.Documents.Resources.GapMessages", typeof(DocumentGap).Assembly);

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();
        foreach (var code in AllCodes())
        {
            data.Add(code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void Every_gap_code_has_an_english_and_a_french_message_naming_the_same_values(string code)
    {
        var english = DocumentGap.Template(code, CultureInfo.InvariantCulture);
        var french = DocumentGap.Template(code, CultureInfo.GetCultureInfo("fr"));

        Assert.False(string.IsNullOrWhiteSpace(english), $"{code}: no English message");
        Assert.False(string.IsNullOrWhiteSpace(french), $"{code}: no French message");
        Assert.NotEqual(english, french);
        // A French message may leave out a value (an exception text), never name one the English message lacks.
        Assert.Subset(Placeholders(english!), Placeholders(french!));
    }

    [Fact]
    public void Every_message_of_the_resources_belongs_to_a_gap_code()
    {
        var codes = AllCodes().ToHashSet(StringComparer.Ordinal);
        var keys = Keys(CultureInfo.InvariantCulture).Concat(Keys(CultureInfo.GetCultureInfo("fr"))).Where(k => k != UnknownKey);

        Assert.Empty(keys.Where(k => !codes.Contains(k)).Distinct());
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_gap_of_the_engine_keeps_its_english_text_in_gaps_and_reads_in_french_under_a_french_culture()
    {
        var result = WordToPdf.Convert(Document(new WordParagraph("بت 12")));

        var gap = Assert.Single(result.GapDetails, g => g.Code == "ArabicWithoutJoining");
        Assert.Contains("Arabic letters drawn without joining forms", result.Gaps);
        Assert.Equal("Arabic letters drawn without joining forms", gap.Message);
        Assert.Equal("lettres arabes dessinées sans leurs formes liées", gap.Describe(French));
        Assert.Equal(gap.Message, gap.Describe(CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal(result.Gaps, result.GapDetails.Select(g => g.Message));
    }

    [Fact]
    public void Describe_follows_the_current_ui_culture()
    {
        var gap = Assert.Single(WordToPdf.Convert(Document(new WordParagraph("بت 12"))).GapDetails, g => g.Code == "ArabicWithoutJoining");
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = French;
            Assert.Equal("lettres arabes dessinées sans leurs formes liées", gap.Describe());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void A_text_the_caller_adds_to_the_gaps_reads_as_a_generic_french_label_and_as_itself_in_english()
    {
        var document = Document(new WordParagraph("Bonjour"));
        document.Gaps.Add("my own remark");

        var result = WordToPdf.Convert(document);

        var gap = Assert.Single(result.GapDetails, g => g.Message == "my own remark");
        Assert.Equal(string.Empty, gap.Code);
        Assert.Equal("Limite de conversion non détaillée", gap.Describe(French));
        Assert.Equal("my own remark", gap.Describe(CultureInfo.GetCultureInfo("en-GB")));
    }

    private static IEnumerable<string> AllCodes() =>
        typeof(DocumentGap).Assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(f => f.FieldType == typeof(GapCode))
            .Select(f => ((GapCode)f.GetValue(null)!).Value)
            .Order(StringComparer.Ordinal);

    private static IEnumerable<string> Keys(CultureInfo culture) =>
        Messages.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key);

    private static HashSet<string> Placeholders(string template) =>
        PlaceholderPattern.Matches(template).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static readonly Regex PlaceholderPattern = new(@"\{\d+\}", RegexOptions.CultureInvariant);
}
