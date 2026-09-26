using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfDocumentInfoTests
{
    [Fact]
    public void StartsEmpty()
    {
        PdfDocumentInfo info = new PdfDocumentInfo();

        Assert.True(info.IsEmpty);
        Assert.Equal("<<>>", Latin1.Written(writer => writer.WriteDictionary(info.ToDictionary())));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("Author")]
    [InlineData("Subject")]
    [InlineData("Keywords")]
    [InlineData("Creator")]
    [InlineData("Producer")]
    [InlineData("CreationDate")]
    [InlineData("ModificationDate")]
    public void IsNotEmptyOnceAnyEntryIsSet(string property)
    {
        PdfDocumentInfo info = new PdfDocumentInfo();
        DateTimeOffset date = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        switch (property)
        {
            case "Title": info.Title = string.Empty; break;
            case "Author": info.Author = string.Empty; break;
            case "Subject": info.Subject = string.Empty; break;
            case "Keywords": info.Keywords = string.Empty; break;
            case "Creator": info.Creator = string.Empty; break;
            case "Producer": info.Producer = string.Empty; break;
            case "CreationDate": info.CreationDate = date; break;
            default: info.ModificationDate = date; break;
        }

        Assert.False(info.IsEmpty);
        Assert.Single(info.ToDictionary());
    }

    [Fact]
    public void WritesEveryEntryInTheSpecificationsOrder()
    {
        PdfDocumentInfo info = new PdfDocumentInfo
        {
            ModificationDate = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(4)),
            CreationDate = new DateTimeOffset(2026, 9, 25, 8, 30, 0, TimeSpan.FromHours(-5)),
            Producer = "Rustaveli.Pdf",
            Creator = "Invoices",
            Keywords = "a, b",
            Subject = "Q3",
            Author = "Nino",
            Title = "ვეფხისტყაოსანი",
        };

        string written = Latin1.Written(writer => writer.WriteDictionary(info.ToDictionary()));

        Assert.Equal(
            "<</Title<FEFF10D510D410E410EE10D810E110E210E710D010DD10E110D010DC10D8>/Author(Nino)/Subject(Q3)/Keywords(a, b)"
            + "/Creator(Invoices)/Producer(Rustaveli.Pdf)/CreationDate(D:20260925083000-05'00')"
            + "/ModDate(D:20260926120000+04'00')>>",
            written);
    }
}
