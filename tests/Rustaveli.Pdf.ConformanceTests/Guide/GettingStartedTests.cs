namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/getting-started.md, as written there.</summary>
[Collection(WorkingDirectoryCollection.Name)]
public class GettingStartedTests
{
    [Fact]
    public void AFirstDocument()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(15.Millimetres());
            section.DefaultType = TypeStyle.Default.WithTypeface("Noto Sans").WithPointSize(12);

            section.Body().Text("Hello from Rustaveli.Pdf.");
        }));

        document.ExportPdf("hello.pdf");

        GuideOutput.Show(document, "first-document");
        Assert.Equal(["Hello from Rustaveli.Pdf."], GuideReader.PageTexts("hello.pdf"));
    }

    [Fact]
    public void Frames()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(40);

            section.Body().Stack(stack =>
            {
                stack.Add()
                    .Stroke(1)
                    .StrokeInk(Ink.Hex("#1565C0"))
                    .RoundCorners(6)
                    .Fill(Ink.Hex("#E3F2FD"))
                    .Inset(12)
                    .Text("A note, inset from a rounded, filled and stroked frame.");
            });
        }));

        GuideOutput.Show(document, "frames", trim: true);
        Assert.Equal("A note, inset from a rounded, filled and stroked frame.", GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void ReusingPartsOfADocument()
    {
        Document letter = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A4;
            section.Margins = Sides.All(25.Millimetres());

            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(24);
                stack.Add().Snippet(new AddressBlock("Tamar Beridze", "12 Rustaveli Avenue", "Tbilisi 0108"));
                stack.Add().Text("Dear Tamar,");
            });
        }));

        GuideOutput.Show(letter, "reusing-parts", trim: true);
        Assert.Equal("Tamar Beridze 12 Rustaveli Avenue Tbilisi 0108 Dear Tamar,", GuideReader.Text(letter.ExportPdf()));
    }

    [Fact]
    public void Exporting()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Text("Exported")));

        byte[] bytes = document.ExportPdf();

        using (FileStream stream = File.Create("to-a-stream.pdf"))
            document.ExportPdf(stream);

        document.ExportPdf("to-a-file.pdf");

        document.Info.Title = "Hello";
        document.Info.Author = "Rustaveli.Pdf";
        document.Info.Language = "en";

        Assert.Equal("Exported", GuideReader.Text(bytes));
        Assert.Equal("Exported", GuideReader.Text("to-a-stream.pdf"));
        Assert.Equal("Exported", GuideReader.Text("to-a-file.pdf"));

        using UglyToad.PdfPig.PdfDocument titled = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        Assert.Equal("Hello", titled.Information.Title);
        Assert.Equal("Rustaveli.Pdf", titled.Information.Author);
    }

    public sealed class AddressBlock(string name, params string[] lines) : ISnippet
    {
        public void Compose(IFrame frame) => frame.Text(text =>
        {
            text.Line(name).Bold();

            foreach (string line in lines)
                text.Line(line);
        });
    }
}
