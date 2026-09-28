using System.Text;
using System.Text.RegularExpressions;
using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// The CFF specimen is set in its own CFF faces, each embedded as a subset — not in faces the machine substitutes for
/// them, which would leave every rendering and validation of it proving nothing about CFF.
/// </summary>
public class CffSubsetTests
{
    [Fact]
    public void TheCffSpecimenEmbedsEachOfItsFacesAsACffSubset()
    {
        TestFonts.EnsureRegistered();
        Document specimen = SpecimenCatalog.All.Single(candidate => candidate.Name == "cff-fonts").Build();

        // Expanded by qpdf — every object outside object streams, no stream compressed — the font dictionaries read as
        // text.
        (int code, string report, byte[]? expanded) = Qpdf.Run(specimen.ExportPdf(), "--qdf", "--object-streams=disable", "{input}", "{output}");
        Assert.True(code == 0, report);
        string pdf = Encoding.Latin1.GetString(expanded!);
        string[] fonts = Regex.Matches(pdf, @"/FontName /([A-Z]{6}\+[A-Za-z0-9-]+)").Select(match => match.Groups[1].Value).Order().ToArray();

        Assert.Equal(["SpecimenCff-Regular", "SpecimenCjk-Regular", "SpecimenSubrs-Regular"], fonts.Select(font => font[7..]).Order());
        Assert.Equal(3, Regex.Matches(pdf, "/Subtype /CIDFontType0C").Count);
        Assert.DoesNotContain("/Subtype /OpenType", pdf, StringComparison.Ordinal);
    }
}
