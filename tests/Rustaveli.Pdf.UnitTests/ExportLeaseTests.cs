namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// A document exported while another export is laying it out is composed afresh for the second, so that one
/// document can be exported from many threads at once.
/// </summary>
public class ExportLeaseTests
{
    [Fact]
    public void AnExportUsesTheDocumentItselfWhileNoOtherIs()
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Text("One")));

        using (Document.ExportLease first = document.ForExport())
            Assert.Same(document, first.Document);

        // Handed back, the document's own tree serves the next export.
        using Document.ExportLease again = document.ForExport();
        Assert.Same(document, again.Document);
    }

    [Fact]
    public void AnExportThatBeginsDuringAnotherIsGivenAFreshlyComposedCopy()
    {
        int composed = 0;
        Document document = Document.Compose(composition =>
        {
            composed++;
            composition.Section(section => section.Body().Text("One"));
        });

        using Document.ExportLease first = document.ForExport();
        using Document.ExportLease second = document.ForExport();
        using Document.ExportLease third = document.ForExport();

        Assert.Same(document, first.Document);
        Assert.NotSame(document, second.Document);
        Assert.NotSame(second.Document, third.Document);
        Assert.NotSame(document.Sections[0], second.Document.Sections[0]);
        Assert.Equal(3, composed);
    }

    [Fact]
    public void AFreshCopyKeepsWhatWasSetOnTheDocumentSinceItWasComposed()
    {
        Document document = Document.Compose(composition => composition.Section(section => section.Body().Text("One")));
        document.Info.Title = "Title";
        document.Info.Author = "Author";
        document.Info.Subject = "Subject";
        document.Info.Keywords = "Keywords";
        document.Info.Creator = "Creator";
        document.Info.Producer = "Producer";
        document.Info.CreationDate = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        document.Info.ModificationDate = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        document.Info.Language = "ka";
        document.PageLimit = 7;
        document.NumberPartsSeparately();
        document.Styles.DefineType("Late", type => type.Bold());

        Document fresh = document.Recompose();

        Assert.Equal(
            ("Title", "Author", "Subject", "Keywords", "Creator", "Producer", document.Info.CreationDate, document.Info.ModificationDate, "ka"),
            (fresh.Info.Title, fresh.Info.Author, fresh.Info.Subject, fresh.Info.Keywords, fresh.Info.Creator, fresh.Info.Producer, fresh.Info.CreationDate, fresh.Info.ModificationDate, fresh.Info.Language));
        Assert.Equal(7, fresh.PageLimit);
        Assert.True(fresh.NumbersPartsApart);
        Assert.Same(document.Styles, fresh.StylesOf(0));
    }

    [Fact]
    public void AMergedDocumentLaysOutCopiesOfItsParts()
    {
        Document cover = Document.Compose(composition => composition.Section(section => section.Body().Text("Cover")));
        Document report = Document.Compose(composition => composition.Section(section => section.Body().Text("Report")));

        Document merged = Document.Merge(cover, report);
        Document again = merged.Recompose();

        // Exporting a part never lays out the tree an export of the merged document is using, nor the other way.
        Assert.DoesNotContain(merged.Sections, section => ReferenceEquals(section, cover.Sections[0]) || ReferenceEquals(section, report.Sections[0]));
        Assert.Equal(2, again.Sections.Count);
        Assert.NotSame(merged.Sections[0], again.Sections[0]);
        Assert.Same(cover.Styles, again.StylesOf(0));
        Assert.Same(report.Styles, again.StylesOf(1));
    }

    [Fact]
    public void OneDocumentExportedFromManyThreadsAtOnceComesOutTheSameEveryTime()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 100);
            section.RunningFoot().Text(text =>
            {
                text.Folio();
                text.Run("/");
                text.PageCount();
            });
            section.Body().Stack(stack =>
            {
                for (int line = 0; line < 40; line++)
                    stack.Add().Text("Line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture));
            });
        }));

        string alone = Pages(LayoutHarness.Render(document));
        string[] together = new string[16];

        Parallel.For(0, together.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index =>
            together[index] = Pages(LayoutHarness.Render(document)));

        Assert.All(together, pages => Assert.Equal(alone, pages));
    }

    /// <summary>Every page's text, in order.</summary>
    private static string Pages(RecordingSurface surface) =>
        string.Join("|", surface.Pages.Select(page => string.Join(" ", page.Operations.OfType<TextOperation>().Select(text => text.Text))));
}
