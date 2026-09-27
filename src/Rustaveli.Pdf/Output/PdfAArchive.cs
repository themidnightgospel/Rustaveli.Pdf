using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// What a PDF/A document declares beyond its metadata: the part and level it claims, and the sRGB output intent its
/// colours are shown through.
/// </summary>
internal static class PdfAArchive
{
    private static readonly PdfName OutputIntents = new PdfName("OutputIntents");
    private static readonly PdfName OutputIntent = new PdfName("OutputIntent");
    private static readonly PdfName S = new PdfName("S");
    private static readonly PdfName PdfA = new PdfName("GTS_PDFA1");
    private static readonly PdfName OutputConditionIdentifier = new PdfName("OutputConditionIdentifier");
    private static readonly PdfName Info = new PdfName("Info");
    private static readonly PdfName DestOutputProfile = new PdfName("DestOutputProfile");

    /// <summary>The part of PDF/A and its level, as XMP names them.</summary>
    public static (int Part, char Level) Of(PdfAConformance conformance) => conformance switch
    {
        PdfAConformance.PdfA2B => (2, 'B'),
        PdfAConformance.PdfA2U => (2, 'U'),
        PdfAConformance.PdfA2A => (2, 'A'),
        PdfAConformance.PdfA3B => (3, 'B'),
        PdfAConformance.PdfA3U => (3, 'U'),
        PdfAConformance.PdfA3A => (3, 'A'),
        _ => throw new ArgumentOutOfRangeException(nameof(conformance), conformance, "Not a PDF/A part and level."),
    };

    /// <summary>Adds the output intent: sRGB, with a profile describing it.</summary>
    public static void Declare(PdfDocumentWriter writer)
    {
        PdfReference profile = writer.File.WriteStream(new PdfDictionary { [PdfNames.N] = 3 }, SrgbProfile.Bytes);

        writer.Catalog[OutputIntents] = new PdfArray(1)
        {
            new PdfDictionary
            {
                [PdfNames.Type] = OutputIntent,
                [S] = PdfA,
                [OutputConditionIdentifier] = PdfString.FromText("sRGB IEC61966-2.1"),
                [Info] = PdfString.FromText("sRGB IEC61966-2.1"),
                [DestOutputProfile] = profile,
            },
        };
    }
}
