// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Internal;
using OmniEurope.Documents.Word.Reading;
using static OmniEurope.Documents.Word.WordXml;

namespace OmniEurope.Documents.Word.Editing;

/// <summary>State shared by the paragraphs of one <see cref="WordEditor.Paragraphs"/> call: the read context and
/// the style sheet and numbering, read once when first needed.</summary>
internal sealed class WordEditContext(OpcPackage package, string mainPart)
{
    private WordStyleSheet? _styles;
    private WordNumbering? _numbering;

    public WordReadContext Read { get; } = new(package);

    public WordStyleSheet Styles => _styles ??= WordPartsReader.Styles(Part(StylesType), Part(ThemeType));

    public WordNumbering Numbering => _numbering ??= WordPartsReader.Numbering(Part(NumberingType), Styles);

    private System.Xml.Linq.XDocument? Part(string type) => WordDocumentReader.Part(package, package.Relationships(mainPart), type);
}
