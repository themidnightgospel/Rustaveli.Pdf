using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Existing files as another writer lays them out. qpdf rewrites the specimens into structures this library's own
/// writer never makes — object streams on and off, streams left uncompressed or compressed again, linearised, and
/// encrypted its own way — and each is read page by page and saved as a file qpdf finds sound.
/// </summary>
public class OtherWritersTests
{
    private const string Password = "reader";

    /// <summary>qpdf's arguments for each way of laying a file out, around <c>{input}</c> and <c>{output}</c>.</summary>
    private static readonly Dictionary<string, string[]> Layouts = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["object streams"] = ["--object-streams=generate", "{input}", "{output}"],
        ["no object streams"] = ["--object-streams=disable", "{input}", "{output}"],
        ["uncompressed"] = ["--stream-data=uncompress", "{input}", "{output}"],
        ["recompressed"] = ["--recompress-flate", "--compression-level=9", "{input}", "{output}"],
        ["inspection mode"] = ["--qdf", "--object-streams=disable", "{input}", "{output}"],
        ["linearised"] = ["--linearize", "{input}", "{output}"],
        ["line end before endstream"] = ["--newline-before-endstream", "{input}", "{output}"],
        ["AES-256"] = ["--encrypt", Password, "owner", "256", "--", "{input}", "{output}"],
        ["AES-128"] = ["--encrypt", Password, "owner", "128", "--use-aes=y", "--", "{input}", "{output}"],
        ["RC4-128"] = ["--allow-weak-crypto", "--encrypt", Password, "owner", "128", "--use-aes=n", "--", "{input}", "{output}"],
        ["RC4-40"] = ["--allow-weak-crypto", "--encrypt", Password, "owner", "40", "--", "{input}", "{output}"],
    };

    public static TheoryData<string, string> Cases()
    {
        TheoryData<string, string> data = new TheoryData<string, string>();

        foreach (Specimen specimen in SpecimenCatalog.All)
        {
            foreach (string layout in Layouts.Keys)
                data.Add(specimen.Name, layout);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AFileAnotherWriterLaidOutReadsAndSavesSoundly(string specimen, string layout)
    {
        TestFonts.EnsureRegistered();
        byte[] original = SpecimenCatalog.All.Single(candidate => candidate.Name == specimen).Build().ExportPdf();
        bool encrypted = Layouts[layout].Contains("--encrypt");
        string? password = encrypted ? Password : null;

        // 3 is qpdf's "succeeded with warnings"; the rewrite must be one it made without complaint.
        (int code, string report, byte[]? rewritten) = Qpdf.Run(original, Layouts[layout]);
        Assert.True(code == 0 && rewritten is not null, report);

        PdfFile file = PdfFile.Open(rewritten!, password);
        byte[] saved = file.ToArray();

        (int checkCode, string check, _) = Qpdf.Run(saved, $"--password={password}", "--check", "{input}");
        Assert.True(checkCode == 0, check);

        Assert.Equal(PageTexts(original, null), PageTexts(saved, password));
        Assert.Equal(encrypted, file.WasProtected);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PagesFromAFileAnotherWriterLaidOutJoinADocumentOfOurs(string specimen, string layout)
    {
        TestFonts.EnsureRegistered();
        byte[] original = SpecimenCatalog.All.Single(candidate => candidate.Name == specimen).Build().ExportPdf();
        byte[] ours = SpecimenCatalog.All[0].Build().ExportPdf();
        string? password = Layouts[layout].Contains("--encrypt") ? Password : null;

        (int code, string report, byte[]? rewritten) = Qpdf.Run(original, Layouts[layout]);
        Assert.True(code == 0 && rewritten is not null, report);

        byte[] joined = PdfFile.Open(ours)
            .Append(PdfFile.Open(rewritten!, password))
            .Overlay(PdfFile.Open(rewritten!, password), onto: "1", from: "1")
            .ToArray();

        IReadOnlyList<string> pages = PageTexts(joined, null);
        Assert.Equal(PageTexts(ours, null).Count + PageTexts(original, null).Count, pages.Count);
        Assert.Equal(PageTexts(original, null), pages.Skip(PageTexts(ours, null).Count));

        (int checkCode, string check, _) = Qpdf.Run(joined, "--check", "{input}");
        Assert.True(checkCode == 0, check);
    }

    /// <summary>Each page's words, in reading order, joined by spaces.</summary>
    private static IReadOnlyList<string> PageTexts(byte[] pdf, string? password)
    {
        ParsingOptions options = new ParsingOptions();

        if (password is not null)
            options.Password = password;

        using PdfDocument document = PdfDocument.Open(pdf, options);
        return document.GetPages().Select(page => string.Join(" ", page.GetWords().Select(word => word.Text))).ToList();
    }
}
