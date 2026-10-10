// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Pdf.Objects;

namespace OmniEurope.Documents.Pdf.Writing;

/// <summary>
/// What a PDF/A-2 file adds to its catalog (ISO 19005-2): the PDF/A output intent with its sRGB destination profile
/// (§6.2.3) and the unfiltered XMP metadata stream with the PDF/A identification (§6.6.2.1, §6.6.4).
/// </summary>
internal static class PdfArchive
{
    /// <summary>The output condition named by the intent: the colour space the profile describes.</summary>
    public const string OutputCondition = "sRGB IEC61966-2.1";

    /// <summary>Refuses what the writer cannot make conform.</summary>
    public static void Check(bool encrypted, bool deviceCmyk)
    {
        if (encrypted)
        {
            throw new InvalidOperationException("PDF/A forbids encryption (ISO 19005-2, 6.1.3): remove the encryption or the conformance.");
        }

        if (deviceCmyk)
        {
            throw new InvalidOperationException("A CMYK image was added before the PDF/A conformance was set; with an sRGB output intent DeviceCMYK is forbidden (ISO 19005-2, 6.2.4.3). Set the conformance first.");
        }
    }

    public static void Complete(PdfObjectTable table, PdfDictionary catalog, PdfConformance conformance, XmpProperties properties)
    {
        var metadata = new PdfStream(XmpPacket.Create(properties, 2, conformance == PdfConformance.PdfA2u ? "U" : "B"));
        metadata.SetName("Type", "Metadata").SetName("Subtype", "XML");
        catalog.Set("Metadata", table.Add(metadata));
        var profile = EmbeddedFont.Stream(SrgbProfile.Create());
        profile.SetNumber("N", 3);
        var intent = new PdfDictionary()
            .SetName("Type", "OutputIntent")
            .SetName("S", "GTS_PDFA1")
            .Set("OutputConditionIdentifier", PdfString.FromText(OutputCondition))
            .Set("Info", PdfString.FromText(OutputCondition))
            .Set("DestOutputProfile", table.Add(profile));
        catalog.Set("OutputIntents", new PdfArray(intent));
    }
}
