// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using OmniEurope.Documents.Conversion;
using OmniEurope.Documents.Word;
using OmniEurope.Documents.Word.Editing;
using OmniEurope.Documents.Word.Validation;

namespace OmniEurope.Documents.Tests.Word;

public sealed class WordSchemaValidatorTests
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    private const string Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    private const string MainType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private const string Run = "<w:r><w:t>Text</w:t></w:r>";

    [Fact]
    public void The_package_writer_produces_a_valid_document()
    {
        var report = WordSchemaValidator.Validate(SampleDocuments.Report().ToArray());

        Assert.True(report.IsValid, string.Join("\n", report.Errors));
        Assert.Contains("word/document.xml", report.CheckedParts);
        Assert.Contains("word/styles.xml", report.CheckedParts);
        Assert.Contains("word/numbering.xml", report.CheckedParts);
        Assert.Contains("word/footnotes.xml", report.CheckedParts);
        Assert.Contains("[Content_Types].xml", report.CheckedParts);
        Assert.Contains("_rels/.rels", report.CheckedParts);
        Assert.Contains("docProps/core.xml", report.UncheckedParts);
    }

    public static TheoryData<string> Writers() => [.. WrittenDocuments.Keys];

    [Theory]
    [MemberData(nameof(Writers))]
    public void Every_writer_of_the_package_produces_a_valid_document(string writer)
    {
        var report = WordSchemaValidator.Validate(WrittenDocuments[writer]());

        Assert.True(report.IsValid, string.Join("\n", report.Errors));
        Assert.Contains("word/document.xml", report.CheckedParts);
    }

    private static readonly Dictionary<string, Func<byte[]>> WrittenDocuments = new()
    {
        ["tracked changes, comments, endnotes, columns"] = () =>
        {
            var document = SampleDocuments.Report();
            var paragraph = document.AddParagraph("Texte ");
            paragraph.Add(new WordText("ajouté") { Revision = new WordRevision(WordRevisionKind.Inserted, "A", new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero)) });
            paragraph.Add(new WordText("retiré") { Revision = new WordRevision(WordRevisionKind.Deleted, "B") });
            paragraph.Add(new WordCommentReference(1));
            paragraph.Add(document.AddEndnote("Note de fin."));
            var comment = new WordComment(1) { Author = "A", Initials = "A", Date = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero) };
            comment.Blocks.Add(new WordParagraph("Remarque."));
            document.Comments.Add(comment);
            var table = new WordTable(100);
            table.AddRow("Gardée");
            table.AddRow("Supprimée").Revision = new WordRevision(WordRevisionKind.Deleted);
            document.AddTable(table);
            document.Sections[0].Page = document.Sections[0].Page with { Columns = 2, ColumnSeparator = true, ColumnWidths = [100, 200] };
            return document.ToArray();
        },
        ["HTML to Word"] = () => HtmlToWord.Convert(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Word", "source.html"))).ToArray(),
        ["Markdown to Word"] = () => MarkdownToWord.Convert("# Titre\n\nTexte **gras**, *italique*, [lien](https://example.org/) et `code`.\n\n1. un\n2. deux\n   - a\n   - b\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n> Citation\n\n```\nbloc\n```\n\n---\n").ToArray(),
        ["Excel to Word"] = () => ExcelToPdf.ToWord(SampleDocuments.Workbook()).ToArray(),
        ["merge"] = () => WordEditor.Merge(SampleDocuments.Report().ToArray(), SampleDocuments.Report().ToArray()),
    };

    [Fact]
    public void A_document_edited_by_the_package_stays_valid()
    {
        var editor = WordEditor.Open(SampleDocuments.Report().ToArray());
        var paragraph = editor.Paragraphs().First(p => p.Text.Length > 0);
        paragraph.SetContent([new WordTextPiece("Remplacé", new WordRunProperties { Bold = true })]);

        var report = WordSchemaValidator.Validate(editor.ToArray());

        Assert.True(report.IsValid, string.Join("\n", report.Errors));
    }

    [Fact]
    public void A_hand_written_valid_document_passes()
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr>{Run}</w:p><w:sectPr/>")));

        Assert.True(report.IsValid, string.Join("\n", report.Errors));
        Assert.Equal(["[Content_Types].xml", "_rels/.rels", "word/document.xml"], report.CheckedParts.Order(StringComparer.Ordinal));
        Assert.Empty(report.UncheckedParts);
    }

    [Fact]
    public void An_unknown_element_fails_with_its_path_and_line()
    {
        var report = WordSchemaValidator.Validate(Package(Document("<w:p>\n<w:frobnicate/></w:p>")));

        var error = Assert.Single(report.Errors);
        Assert.Equal("word/document.xml", error.Part);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/w:frobnicate[1]", error.Path);
        Assert.Equal(2, error.Line);
        Assert.Equal(2, error.Position);
        Assert.Contains("invalid child element 'frobnicate'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrong_attribute_value_fails_on_the_attribute()
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p><w:pPr><w:jc w:val=\"middle\"/></w:pPr>{Run}</w:p>")));

        var error = Assert.Single(report.Errors);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/w:pPr[1]/w:jc[1]/@w:val", error.Path);
        Assert.Contains("'middle'", error.Message, StringComparison.Ordinal);
        Assert.Contains("Enumeration constraint failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrong_order_fails_on_the_misplaced_element()
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}<w:pPr><w:jc w:val=\"left\"/></w:pPr></w:p>")));

        var error = Assert.Single(report.Errors);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/w:pPr[1]", error.Path);
        Assert.Contains("invalid child element 'pPr'", error.Message, StringComparison.Ordinal);
        Assert.Equal($"word/document.xml ({error.Line},{error.Position}) {error.Path}: {error.Message}", error.ToString());
    }

    [Fact]
    public void An_undeclared_attribute_fails()
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p w:colour=\"red\">{Run}</w:p>")));

        var error = Assert.Single(report.Errors);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/@w:colour", error.Path);
        Assert.Contains(":colour' attribute is not declared", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignorable_extensions_are_removed_before_validation()
    {
        var body = $"<w:p w14:paraId=\"1A2B3C4D\"><w14:unknownExtension><w:frobnicate/></w14:unknownExtension>{Run}</w:p>";

        var ignored = WordSchemaValidator.Validate(Package(Document(body, $"xmlns:w14=\"{W14}\" mc:Ignorable=\"w14\"")));
        var notIgnorable = WordSchemaValidator.Validate(Package(Document(body, $"xmlns:w14=\"{W14}\"")));

        Assert.True(ignored.IsValid, string.Join("\n", ignored.Errors));
        Assert.Contains(notIgnorable.Errors, e => e.Path.EndsWith("/@w14:paraId", StringComparison.Ordinal));
        Assert.Contains(notIgnorable.Errors, e => e.Path.EndsWith("/w14:unknownExtension[1]", StringComparison.Ordinal));
    }

    [Fact]
    public void Process_content_keeps_the_content_of_an_ignorable_element()
    {
        var valid = $"<w:p><w14:wrapper>{Run}</w14:wrapper></w:p>";
        var invalid = "<w:p><w14:wrapper><w:frobnicate/></w14:wrapper></w:p>";
        var declarations = $"xmlns:w14=\"{W14}\" mc:Ignorable=\"w14\" mc:ProcessContent=\"w14:wrapper\"";

        Assert.True(WordSchemaValidator.Validate(Package(Document(valid, declarations))).IsValid);
        var error = Assert.Single(WordSchemaValidator.Validate(Package(Document(invalid, declarations))).Errors);
        Assert.Contains("'frobnicate'", error.Message, StringComparison.Ordinal);
        Assert.True(WordSchemaValidator.Validate(Package(Document(invalid, $"xmlns:w14=\"{W14}\" mc:Ignorable=\"w14\" mc:ProcessContent=\"w14:*\""))).Errors.Count == 1);
    }

    [Fact]
    public void Alternate_content_resolves_to_the_fallback_when_the_choice_is_not_understood()
    {
        string Alternate(string fallback) =>
            $"<w:p><w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><wps:anything/></mc:Choice><mc:Fallback>{fallback}</mc:Fallback></mc:AlternateContent></w:r></w:p>";
        var declarations = $"xmlns:wps=\"{Wps}\"";

        Assert.True(WordSchemaValidator.Validate(Package(Document(Alternate("<w:t>Text</w:t>"), declarations))).IsValid);
        var error = Assert.Single(WordSchemaValidator.Validate(Package(Document(Alternate("<w:frobnicate/>"), declarations))).Errors);
        Assert.Contains("'frobnicate'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Markup_as_word_writes_it_validates_through_its_vml_fallback()
    {
        const string W15 = "http://schemas.microsoft.com/office/word/2012/wordml";
        var body = "<w:p w14:paraId=\"00A1B2C3\" w14:textId=\"77777777\"><w:pPr><w15:collapsed/></w:pPr><w:r><mc:AlternateContent>"
            + "<mc:Choice Requires=\"wps\"><w:drawing><wps:wsp/></w:drawing></mc:Choice>"
            + "<mc:Fallback><w:pict><v:rect style=\"width:10pt;height:10pt\" filled=\"f\"><v:textbox/></v:rect></w:pict></mc:Fallback>"
            + "</mc:AlternateContent></w:r></w:p>";
        var declarations = $"xmlns:w14=\"{W14}\" xmlns:w15=\"{W15}\" xmlns:wps=\"{Wps}\" xmlns:v=\"urn:schemas-microsoft-com:vml\" mc:Ignorable=\"w14 w15\"";

        var valid = WordSchemaValidator.Validate(Package(Document(body, declarations)));
        var badVml = WordSchemaValidator.Validate(Package(Document(body.Replace("filled=\"f\"", "filled=\"maybe\"", StringComparison.Ordinal), declarations)));

        Assert.True(valid.IsValid, string.Join("\n", valid.Errors));
        var error = Assert.Single(badVml.Errors);
        Assert.EndsWith("/w:pict[1]/v:rect[1]/@filled", error.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void A_relationship_id_the_part_does_not_declare_is_an_error()
    {
        const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var body = $"<w:p><w:hyperlink r:id=\"rId7\">{Run}</w:hyperlink></w:p>";
        var declarations = $"xmlns:r=\"{R}\"";
        var relationships = "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId7\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"https://example.org/\" TargetMode=\"External\"/></Relationships>";

        var declared = WordSchemaValidator.Validate(Package(Document(body, declarations), extra: [("word/_rels/document.xml.rels", relationships)]));
        var dangling = WordSchemaValidator.Validate(Package(Document(body, declarations)));
        var ignored = WordSchemaValidator.Validate(Package(Document($"<w:p w14:link=\"rId9\">{Run}</w:p><w14:x r:id=\"rId9\"/>", $"{declarations} xmlns:w14=\"{W14}\" mc:Ignorable=\"w14\"")));

        Assert.True(declared.IsValid, string.Join("\n", declared.Errors));
        var error = Assert.Single(dangling.Errors);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/w:hyperlink[1]/@r:id", error.Path);
        Assert.Equal("The relationship id 'rId7' is not declared in word/_rels/document.xml.rels.", error.Message);
        Assert.True(ignored.IsValid, string.Join("\n", ignored.Errors));
    }

    [Fact]
    public void Alternate_content_takes_the_first_understood_choice()
    {
        var body = "<w:p><w:r><mc:AlternateContent><mc:Choice Requires=\"w\"><w:t>Read</w:t></mc:Choice><mc:Fallback><w:frobnicate/></mc:Fallback></mc:AlternateContent></w:r></w:p>";

        Assert.True(WordSchemaValidator.Validate(Package(Document(body))).IsValid);
    }

    [Fact]
    public void Alternate_content_without_a_usable_choice_or_fallback_disappears()
    {
        var body = $"<w:p><w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><wps:anything/></mc:Choice></mc:AlternateContent><w:t>Text</w:t></w:r></w:p>";

        Assert.True(WordSchemaValidator.Validate(Package(Document(body, $"xmlns:wps=\"{Wps}\""))).IsValid);
    }

    [Theory]
    [InlineData("<mc:AlternateContent><mc:Fallback/></mc:AlternateContent>", "holds no mc:Choice")]
    [InlineData("<mc:AlternateContent><mc:Choice Requires=\"w\"/><w:t/></mc:AlternateContent>", "only mc:Choice and mc:Fallback are allowed")]
    [InlineData("<mc:AlternateContent><mc:Fallback/><mc:Choice Requires=\"w\"/></mc:AlternateContent>", "then at most one mc:Fallback")]
    [InlineData("<mc:AlternateContent><mc:Choice/></mc:AlternateContent>", "has no Requires attribute")]
    [InlineData("<mc:AlternateContent><mc:Choice Requires=\"zz\"/></mc:AlternateContent>", "Requires prefix 'zz', which is not declared")]
    [InlineData("<mc:Choice Requires=\"w\"/>", "mc:Choice is not allowed here")]
    public void Malformed_markup_compatibility_is_reported(string run, string message)
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p><w:r>{run}</w:r></w:p>")));

        Assert.Contains(report.Errors, e => e.Message.Contains(message, StringComparison.Ordinal) && e.Path.StartsWith("/w:document[1]/w:body[1]/w:p[1]/w:r[1]", StringComparison.Ordinal));
    }

    [Fact]
    public void Must_understand_of_an_unknown_namespace_is_an_error()
    {
        var report = WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}</w:p>", $"xmlns:w14=\"{W14}\" mc:MustUnderstand=\"w14\"")));

        var error = Assert.Single(report.Errors);
        Assert.Equal("/w:document[1]/@mc:MustUnderstand", error.Path);
        Assert.Contains($"'{W14}', which is not understood", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mc:Ignorable=\"zz\"", "mc:Ignorable names prefix 'zz'")]
    [InlineData("mc:ProcessContent=\"zz:a\"", "mc:ProcessContent names 'zz:a'")]
    public void Undeclared_prefixes_in_markup_compatibility_attributes_are_errors(string attribute, string message)
    {
        var error = Assert.Single(WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}</w:p>", attribute))).Errors);

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_part_of_an_extension_namespace_is_read_but_not_checked()
    {
        var part = $"<?xml version=\"1.0\"?><w14:extension xmlns:w14=\"{W14}\" xmlns:mc=\"{Mc}\" mc:Ignorable=\"w14\"/>";

        var report = WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}</w:p>"), extra: [("word/extension.xml", part)]));

        Assert.Contains("word/extension.xml", report.UncheckedParts);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void A_strict_document_is_validated_against_the_strict_schemas()
    {
        const string Strict = "http://purl.oclc.org/ooxml/wordprocessingml/main";
        string Part(string body) => $"<?xml version=\"1.0\"?><w:document xmlns:w=\"{Strict}\" w:conformance=\"strict\"><w:body>{body}</w:body></w:document>";

        Assert.True(WordSchemaValidator.Validate(Package(Part($"<w:p><w:pPr><w:jc w:val=\"start\"/></w:pPr>{Run}</w:p>"))).IsValid);
        var error = Assert.Single(WordSchemaValidator.Validate(Package(Part($"<w:p><w:pPr><w:jc w:val=\"left\"/></w:pPr>{Run}</w:p>"))).Errors);
        Assert.Equal("/w:document[1]/w:body[1]/w:p[1]/w:pPr[1]/w:jc[1]/@w:val", error.Path);
    }

    [Fact]
    public void A_part_that_is_not_well_formed_is_reported_with_its_line()
    {
        var report = WordSchemaValidator.Validate(Package("<?xml version=\"1.0\"?>\n<w:document xmlns:w=\"" + W + "\"><w:body></w:document>"));

        var error = Assert.Single(report.Errors);
        Assert.Equal("word/document.xml", error.Part);
        Assert.Equal(2, error.Line);
        Assert.Equal(string.Empty, error.Path);
        Assert.DoesNotContain("word/document.xml", report.CheckedParts);
    }

    [Fact]
    public void The_packaging_parts_are_validated_without_markup_compatibility()
    {
        var types = $"<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" xmlns:mc=\"{Mc}\" mc:Ignorable=\"x\" xmlns:x=\"urn:x\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"{MainType}\"/></Types>";

        var report = WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}</w:p>"), types: types));

        Assert.Contains(report.Errors, e => e.Part == "[Content_Types].xml" && e.Path == "/Types[1]/@mc:Ignorable");
    }

    [Fact]
    public void A_part_declared_xml_by_its_content_type_is_read()
    {
        var types = $"<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"bin\" ContentType=\"application/octet-stream\"/><Default Extension=\"svg\" ContentType=\"image/svg+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"{MainType}\"/><Override PartName=\"/word/settings.data\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/><Override PartName=\"/custom/data.dat\" ContentType=\"text/xml\"/></Types>";
        var settings = $"<?xml version=\"1.0\"?><w:settings xmlns:w=\"{W}\"><w:zoom w:percent=\"abc\"/></w:settings>";

        var report = WordSchemaValidator.Validate(Package(Document($"<w:p>{Run}</w:p>"), types: types, extra: [("word/settings.data", settings), ("custom/data.dat", "<data/>"), ("word/blob.bin", "not xml"), ("word/media/picture.svg", "<!DOCTYPE svg><svg/>")]));

        Assert.Contains("word/settings.data", report.CheckedParts);
        Assert.Contains("custom/data.dat", report.UncheckedParts);
        Assert.DoesNotContain("word/blob.bin", report.CheckedParts.Concat(report.UncheckedParts));
        Assert.DoesNotContain("word/media/picture.svg", report.CheckedParts.Concat(report.UncheckedParts));
        Assert.Single(report.Errors);
        Assert.Contains(report.Errors, e => e.Part == "word/settings.data" && e.Path == "/w:settings[1]/w:zoom[1]/@w:percent");
    }

    [Fact]
    public void A_stream_is_validated_like_bytes()
    {
        using var stream = new MemoryStream(SampleDocuments.Report().ToArray());

        Assert.True(WordSchemaValidator.Validate(stream).IsValid);
    }

    [Fact]
    public void Bytes_that_are_not_a_package_throw()
    {
        Assert.Throws<DocumentFormatException>(() => WordSchemaValidator.Validate(Encoding.UTF8.GetBytes("not a zip")));
        Assert.Throws<ArgumentNullException>(() => WordSchemaValidator.Validate((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => WordSchemaValidator.Validate((Stream)null!));
    }

    [Fact]
    public void The_shipped_schemas_come_with_the_ecma_notice()
    {
        Assert.Contains("COPYRIGHT NOTICE", OfficeSchemas.Notice(), StringComparison.Ordinal);
        Assert.Contains("ECMA-376", OfficeSchemas.Notice(), StringComparison.Ordinal);
        Assert.Equal(41, OfficeSchemas.Resources.Count());
        Assert.Contains(W, OfficeSchemas.Namespaces);
        Assert.Contains("http://purl.oclc.org/ooxml/wordprocessingml/main", OfficeSchemas.Namespaces);
        Assert.Contains("urn:schemas-microsoft-com:vml", OfficeSchemas.Namespaces);
        Assert.Equal(3, OfficeSchemas.StrictDefaultsCorrected);
    }

    [Fact]
    public void Validations_run_in_parallel_with_the_same_result()
    {
        var bytes = SampleDocuments.Report().ToArray();
        var invalid = Package(Document("<w:p><w:frobnicate/></w:p>"));

        var results = Enumerable.Range(0, 16).AsParallel().Select(i => WordSchemaValidator.Validate(i % 2 == 0 ? bytes : invalid).Errors.Count).ToList();

        Assert.Equal(Enumerable.Range(0, 16).Select(i => i % 2 == 0 ? 0 : 1), results);
    }

    private static string Document(string body, string declarations = "") =>
        $"<?xml version=\"1.0\"?><w:document xmlns:w=\"{W}\" xmlns:mc=\"{Mc}\" {declarations}><w:body>{body}</w:body></w:document>";


    private static byte[] Package(string document, string? types = null, (string Name, string Content)[]? extra = null)
    {
        types ??= $"<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"{MainType}\"/></Types>";
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", types);
            Add(zip, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Add(zip, "word/document.xml", document);
            foreach (var (name, content) in extra ?? [])
            {
                Add(zip, name, content);
            }
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes(content));
    }
}
