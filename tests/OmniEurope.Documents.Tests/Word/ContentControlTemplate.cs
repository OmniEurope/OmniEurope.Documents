// SPDX-License-Identifier: EUPL-1.2

namespace OmniEurope.Documents.Tests.Word;

/// <summary>A synthetic template (.dotx) with the content controls of a typical form: month and year
/// combo boxes, four date pickers, a placeholder, a check box, a plain text and a block rich-text control.</summary>
internal static class ContentControlTemplate
{
    public const string TemplateType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";
    public const string W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    private const string Run = """<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/><w:lang w:val="fr-FR"/></w:rPr>""";

    public static byte[] Build(string mainType = TemplateType)
    {
        var body = $"""
            <w:p>{Text("Du ")}{Date("debut", "Début", "01/01/2026", "2026-01-01T00:00:00Z")}{Text(" au ")}{Date("fin", "Fin", "31/01/2026", "2026-01-31T00:00:00Z")}{Text(".")}</w:p>
            <w:p>{Text("Loyer de ")}{Months()}{Text(" ")}{Years("2024")}{Text(" payé le ")}{Placeholder()}{Text(", signé le ")}{Date("signature", "Signature", "15/02/2026", "2026-02-15T00:00:00Z")}{Text(".")}</w:p>
            <w:p>{Text("Locataire : ")}{Name()}{Text(" Reçu : ")}{Checkbox()}</w:p>
            {Notes()}
            <w:p><w:r><w:t>Fin</w:t></w:r></w:p>
            """;
        var headerSdt = Years("2023");
        const string styles = $"""<?xml version="1.0" encoding="UTF-8"?><w:styles xmlns:w="{DocxFactory.W}"><w:style w:type="character" w:styleId="Textedelespacerserv"><w:name w:val="Placeholder Text"/><w:rPr><w:color w:val="808080"/></w:rPr></w:style></w:styles>""";
        var header = $"""<?xml version="1.0" encoding="UTF-8"?><w:hdr xmlns:w="{DocxFactory.W}" xmlns:w14="{W14}"><w:p>{headerSdt}</w:p></w:hdr>""";
        return DocxFactory.Build(
            body,
            mainType,
            new Dictionary<string, string> { ["word/styles.xml"] = styles, ["word/header1.xml"] = header },
            """<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>""",
            contentTypes: new Dictionary<string, string>
            {
                ["word/styles.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml",
                ["word/header1.xml"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml",
            });
    }

    private static string Text(string text) => $"""<w:r>{Run}<w:t xml:space="preserve">{text}</w:t></w:r>""";

    private static string Date(string tag, string alias, string shown, string full) => $"""
        <w:sdt xmlns:w14="{W14}"><w:sdtPr><w:alias w:val="{alias}"/><w:tag w:val="{tag}"/><w:id w:val="{tag.Length * 1000 + alias.Length}"/><w:date w:fullDate="{full}"><w:dateFormat w:val="dd/MM/yyyy"/><w:lid w:val="fr-FR"/><w:storeMappedDataAs w:val="dateTime"/><w:calendar w:val="gregorian"/></w:date></w:sdtPr><w:sdtContent><w:r>{Run}<w:t>{shown}</w:t></w:r></w:sdtContent></w:sdt>
        """;

    private static string Placeholder() => $"""
        <w:sdt xmlns:w14="{W14}"><w:sdtPr><w:alias w:val="Paiement"/><w:tag w:val="paiement"/><w:id w:val="4242"/><w:showingPlcHdr/><w:date><w:dateFormat w:val="dd/MM/yyyy"/><w:lid w:val="fr-FR"/><w:storeMappedDataAs w:val="dateTime"/><w:calendar w:val="gregorian"/></w:date></w:sdtPr><w:sdtContent><w:r><w:rPr><w:rStyle w:val="Textedelespacerserv"/><w:lang w:val="fr-FR"/></w:rPr><w:t>Choisissez une date</w:t></w:r></w:sdtContent></w:sdt>
        """;

    private static string Months()
    {
        var names = new[] { "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };
        var items = string.Concat(names.Select((n, i) => $"""<w:listItem w:displayText="de {n}" w:value="{i + 1}"/>"""));
        return Combo("mois", "Mois", items, "de janvier");
    }

    private static string Years(string shown) =>
        Combo("annee", "Année", string.Concat(Enumerable.Range(2022, 5).Select(y => $"""<w:listItem w:displayText="{y}" w:value="{y}"/>""")), shown);

    private static string Combo(string tag, string alias, string items, string shown) => $"""
        <w:sdt xmlns:w14="{W14}"><w:sdtPr><w:alias w:val="{alias}"/><w:tag w:val="{tag}"/><w:id w:val="{tag.Length * 100 + shown.Length}"/><w:comboBox>{items}</w:comboBox></w:sdtPr><w:sdtContent><w:r>{Run}<w:t>{shown}</w:t></w:r></w:sdtContent></w:sdt>
        """;

    private static string Name() => $"""
        <w:sdt><w:sdtPr><w:alias w:val="Locataire"/><w:tag w:val="locataire"/><w:id w:val="7"/><w:text/></w:sdtPr><w:sdtContent><w:r><w:rPr><w:b/><w:lang w:val="fr-FR"/></w:rPr><w:t>Dupont</w:t></w:r></w:sdtContent></w:sdt>
        """;

    private static string Checkbox() => $"""
        <w:sdt xmlns:w14="{W14}"><w:sdtPr><w:tag w:val="recu"/><w:id w:val="8"/><w14:checkbox><w14:checked w14:val="0"/><w14:checkedState w14:val="2612" w14:font="MS Gothic"/><w14:uncheckedState w14:val="2610" w14:font="MS Gothic"/></w14:checkbox></w:sdtPr><w:sdtContent><w:r><w:rPr><w:rFonts w:ascii="MS Gothic" w:eastAsia="MS Gothic" w:hAnsi="MS Gothic" w:hint="eastAsia"/></w:rPr><w:t>☐</w:t></w:r></w:sdtContent></w:sdt>
        """;

    private static string Notes() => """
        <w:sdt><w:sdtPr><w:alias w:val="Notes"/><w:tag w:val="notes"/><w:id w:val="9"/><w:richText/></w:sdtPr><w:sdtContent><w:p><w:pPr><w:jc w:val="both"/></w:pPr><w:r><w:rPr><w:i/></w:rPr><w:t>Première note</w:t></w:r></w:p><w:p><w:r><w:t>Seconde note</w:t></w:r></w:p></w:sdtContent></w:sdt>
        """;
}
