using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Protection by password at every strength, checked against qpdf both ways: files this library encrypts open in qpdf
/// with their passwords and decode cleanly, and files qpdf encrypts open here.
/// </summary>
public class ProtectionTests
{
    private const string User = "open sesame";
    private const string Owner = "the keeper";

    public static TheoryData<EncryptionLevel, string, string> Levels => new TheoryData<EncryptionLevel, string, string>
    {
        { EncryptionLevel.Rc4With40Bits, "R = 2", "40" },
        { EncryptionLevel.Rc4With128Bits, "R = 3", "128" },
        { EncryptionLevel.AesWith128Bits, "R = 4", "128" },
        { EncryptionLevel.AesWith256Bits, "R = 6", "256" },
    };

    private static byte[] Specimen() => SpecimenCatalog.All.First(specimen => specimen.Name.Contains("image", StringComparison.OrdinalIgnoreCase)).Build().ExportPdf();

    [Theory]
    [MemberData(nameof(Levels))]
    public void AProtectedExportOpensInQpdfWithEitherPassword(EncryptionLevel level, string revision, string bits)
    {
        TestFonts.EnsureRegistered();
        byte[] pdf = SpecimenCatalog.All[0].Build().ExportPdf(new PdfExportOptions
        {
            Protection = new Protection { UserPassword = User, OwnerPassword = Owner, Encryption = level, AllowCopying = false },
        });

        (int _, string encryption, _) = Qpdf.Run(pdf, $"--password={Owner}", "--show-encryption", "{input}");
        Assert.Contains(revision, encryption, StringComparison.Ordinal);
        Assert.Contains("extract for any purpose: not allowed", encryption, StringComparison.Ordinal);
        Assert.Contains("print high resolution: allowed", encryption, StringComparison.Ordinal);
        _ = bits;

        foreach (string password in new[] { User, Owner })
        {
            (int code, string report, _) = Qpdf.Run(pdf, $"--password={password}", "--check", "{input}");
            Assert.True(code == 0, report);
            Assert.Contains("No syntax or stream encoding errors found", report, StringComparison.Ordinal);
        }

        (int refused, string reason, _) = Qpdf.Run(pdf, "--password=wrong", "--check", "{input}");
        Assert.NotEqual(0, refused);
        Assert.Contains("invalid password", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Aes256IsDeclaredAsTheAdobeExtensionAPdf17FileNeedsForIt(EncryptionLevel level, string revision, string bits)
    {
        // 256-bit AES (/V 5 /R 6) came after PDF 1.7: a 1.7 file declares it as Adobe's extension level 8 in its catalog.
        TestFonts.EnsureRegistered();
        Protection protection = new Protection { Encryption = level };
        byte[] plain = Specimen();
        _ = (revision, bits);

        // Exported protected, protected afterwards, and protected and laid out for the web, which encrypts as it lays out.
        foreach (byte[] pdf in new[]
        {
            SpecimenCatalog.All[0].Build().ExportPdf(new PdfExportOptions { Protection = protection }),
            PdfFile.Open(plain).Protect(protection).ToArray(),
            PdfFile.Open(plain).Protect(protection).OptimizeForWeb().ToArray(),
        })
        {
            string trailer = Qpdf.Run(pdf, "--show-object=trailer", "{input}").Output;
            string root = System.Text.RegularExpressions.Regex.Match(trailer, @"/Root (\d+) 0 R").Groups[1].Value;
            string catalog = Qpdf.Run(pdf, $"--show-object={root}", "{input}").Output;

            Assert.Contains("/Type /Catalog", catalog, StringComparison.Ordinal);
            Assert.Equal(
                level == EncryptionLevel.AesWith256Bits,
                System.Text.RegularExpressions.Regex.IsMatch(catalog, @"/Extensions << /ADBE << /BaseVersion /1\.7 /ExtensionLevel 8 >> >>"));
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void FilesQpdfProtectsOpenHereAndKeepTheirProtection(EncryptionLevel level, string revision, string bits)
    {
        TestFonts.EnsureRegistered();
        string[] aes = level switch
        {
            EncryptionLevel.Rc4With128Bits => ["--use-aes=n"],
            EncryptionLevel.AesWith128Bits => ["--use-aes=y"],
            _ => [],
        };

        (int code, string printed, byte[]? encrypted) = Qpdf.Run(Specimen(), ["--allow-weak-crypto", "--encrypt", User, Owner, bits, .. aes, "--", "{input}", "{output}"]);
        Assert.True(code == 0 && encrypted is not null, printed);

        Assert.Throws<IncorrectPasswordException>(() => PdfFile.Open(encrypted!));
        Assert.Throws<IncorrectPasswordException>(() => PdfFile.Open(encrypted!, "wrong"));

        foreach (string password in new[] { User, Owner })
        {
            PdfFile file = PdfFile.Open(encrypted!, password);
            Assert.True(file.WasProtected);

            byte[] kept = file.ToArray();
            (int _, string encryption, _) = Qpdf.Run(kept, $"--password={User}", "--show-encryption", "{input}");
            Assert.Contains(revision, encryption, StringComparison.Ordinal);
            Assert.Contains("No syntax or stream encoding errors found", Qpdf.Run(kept, $"--password={User}", "--check", "{input}").Output, StringComparison.Ordinal);

            byte[] plain = PdfFile.Open(encrypted!, password).Unprotect().ToArray();
            Assert.Contains("File is not encrypted", Qpdf.Run(plain, "--show-encryption", "{input}").Output, StringComparison.Ordinal);
            Assert.Contains("No syntax or stream encoding errors found", Qpdf.Check(plain), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AFileIsReprotectedAtAnotherStrength()
    {
        TestFonts.EnsureRegistered();
        byte[] weak = PdfFile.Open(Specimen()).Protect(new Protection { UserPassword = User, Encryption = EncryptionLevel.Rc4With40Bits }).ToArray();

        byte[] strong = PdfFile.Open(weak, User).Protect(new Protection { UserPassword = "new", Encryption = EncryptionLevel.AesWith256Bits }).ToArray();

        Assert.Contains("R = 6", Qpdf.Run(strong, "--password=new", "--show-encryption", "{input}").Output, StringComparison.Ordinal);
        Assert.Contains("No syntax or stream encoding errors found", Qpdf.Run(strong, "--password=new", "--check", "{input}").Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileWithNoUserPasswordOpensWithoutOne()
    {
        TestFonts.EnsureRegistered();
        byte[] restricted = PdfFile.Open(Specimen()).Protect(new Protection { AllowPrinting = false }).ToArray();

        Assert.Contains("print low resolution: not allowed", Qpdf.Run(restricted, "--show-encryption", "{input}").Output, StringComparison.Ordinal);
        Assert.True(PdfFile.Open(restricted).WasProtected);
    }

    [Fact]
    public void ProtectionAndPdfACannotBeAskedForTogether()
    {
        TestFonts.EnsureRegistered();

        Assert.Throws<InvalidOperationException>(() => SpecimenCatalog.All[0].Build().ExportPdf(new PdfExportOptions
        {
            Conformance = PdfAConformance.PdfA2B,
            Protection = new Protection(),
        }));
    }

    private static Document Accessible()
    {
        Document document = SpecimenCatalog.All[0].Build();
        document.Info.Title ??= "Protected";
        document.Info.Language ??= "en";
        return document;
    }

    [Fact]
    public void PdfUACannotWithholdAccessFromAssistiveTechnology()
    {
        // ISO 14289-1, 7.16: a protected PDF/UA file must let assistive technology read its content.
        TestFonts.EnsureRegistered();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Accessible().ExportPdf(new PdfExportOptions
        {
            Accessibility = PdfUAConformance.PdfUA1,
            Protection = new Protection { AllowAccessibility = false },
        }));

        Assert.Contains(nameof(Protection.AllowAccessibility), error.Message, StringComparison.Ordinal);

        // Outside PDF/UA the permission is the author's to withhold.
        byte[] withheld = Accessible().ExportPdf(new PdfExportOptions { Protection = new Protection { AllowAccessibility = false } });
        Assert.Contains("accessibility: not allowed", Qpdf.Run(withheld, "--show-encryption", "{input}").Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AProtectedPdfUAFileThatAllowsAccessibilityMeetsPdfUA()
    {
        TestFonts.EnsureRegistered();
        IReadOnlyList<string> broken = [];

        // veraPDF 1.30.2 fails to open about one AES-256 file in twenty with the empty password that opens it, for some
        // of the random salts the key is derived with: 150 files each opened in qpdf, and eight of them veraPDF refused.
        // qpdf opening each one shows the file is sound, so a refusal is veraPDF's, and a fresh export, with fresh salts,
        // is checked instead; five refusals in a row would be a real one.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            byte[] pdf = Accessible().ExportPdf(new PdfExportOptions
            {
                Accessibility = PdfUAConformance.PdfUA1,
                Protection = new Protection { AllowCopying = false },
            });

            Qpdf.Check(pdf);
            broken = VeraPdf.Validate(new Dictionary<string, byte[]> { ["protected-ua1"] = pdf })["protected-ua1"];

            if (!broken.Any(problem => problem.Contains("unknown or wrong password", StringComparison.Ordinal)))
                break;
        }

        Assert.True(broken.Count == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }
}
