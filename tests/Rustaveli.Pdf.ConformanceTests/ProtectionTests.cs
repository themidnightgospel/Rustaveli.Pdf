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
}
