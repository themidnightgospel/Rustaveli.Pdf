using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Asking for tagged output: the options that record structure, the frame and cell settings that describe it, and a
/// page drawn in draw order.
/// </summary>
public class TaggingOptionsTests
{
    [Theory]
    [InlineData(false, PdfUAConformance.None, PdfAConformance.None, false)]
    [InlineData(true, PdfUAConformance.None, PdfAConformance.None, true)]
    [InlineData(false, PdfUAConformance.PdfUA1, PdfAConformance.None, true)]
    [InlineData(false, PdfUAConformance.None, PdfAConformance.PdfA2A, true)]
    [InlineData(false, PdfUAConformance.None, PdfAConformance.PdfA3A, true)]
    [InlineData(false, PdfUAConformance.None, PdfAConformance.PdfA2U, false)]
    [InlineData(false, PdfUAConformance.None, PdfAConformance.PdfA3B, false)]
    public void StructureIsWrittenWhenAskedForOrClaimed(bool tagged, PdfUAConformance accessibility, PdfAConformance conformance, bool writes)
    {
        PdfExportOptions options = new PdfExportOptions { Tagged = tagged, Accessibility = accessibility, Conformance = conformance };

        Assert.Equal(writes, options.WritesStructure);
    }

    [Fact]
    public void ByDefaultNothingIsTaggedOrClaimed()
    {
        PdfExportOptions options = new PdfExportOptions();

        Assert.False(options.Tagged);
        Assert.Equal(PdfUAConformance.None, options.Accessibility);
        Assert.False(options.WritesStructure);
    }

    [Theory]
    [InlineData(PdfAConformance.PdfA2B, 2, 'B')]
    [InlineData(PdfAConformance.PdfA2U, 2, 'U')]
    [InlineData(PdfAConformance.PdfA2A, 2, 'A')]
    [InlineData(PdfAConformance.PdfA3B, 3, 'B')]
    [InlineData(PdfAConformance.PdfA3U, 3, 'U')]
    [InlineData(PdfAConformance.PdfA3A, 3, 'A')]
    public void EachArchiveLevelIsAPartAndLetter(PdfAConformance conformance, int part, char level) =>
        Assert.Equal((part, level), PdfAArchive.Of(conformance));

    [Fact]
    public void NoArchiveIsNoPartOrLevel() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfAArchive.Of(PdfAConformance.None));

    [Fact]
    public void TaggingNeedsATag() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Tagged(null!)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ALanguageIsNamed(string? language) =>
        Assert.ThrowsAny<ArgumentException>(() => LayoutHarness.Build(frame => frame.Language(language!)));

    [Fact]
    public void ALanguageIsNamedWithoutSurroundingSpace()
    {
        Block built = LayoutHarness.Build(frame => frame.Language(" ka ").Blank());

        Assert.Equal("ka", built.Traverse().OfType<LanguageBlock>().Single().Language);
    }

    [Fact]
    public void FramesAreTaggedAndUntaggedAroundTheirContent()
    {
        Block built = LayoutHarness.Build(frame => frame.Tagged(ContentTag.Quote).Untagged().Text("Said"));

        TagBlock tag = built.Traverse().OfType<TagBlock>().Single();
        Assert.Same(ContentTag.Quote, tag.Tag);
        Assert.IsType<UntaggedBlock>(tag.Child);
    }

    [Fact]
    public void ACellMarkedAsItsRowsHeadingHeadsIt()
    {
        TableBlock table = new TableBlock();
        CellFrame cell = new TableComposer(table).Cell();

        Assert.False(table.Cells[0].HeadsRow);
        Assert.Same(cell, cell.RowHeading());
        Assert.True(table.Cells[0].HeadsRow);
    }

    [Fact]
    public void APageDrawnInDrawOrderTellsTheElementOfEachDrawing()
    {
        RecordingSurface pages = new RecordingSurface();
        LayeredPageSink sink = new LayeredPageSink(pages);
        StructureElement heading = new StructureElement("H1", null);
        StructureElement paragraph = new StructureElement("P", null);

        sink.BeginPage(new Extent(100, 100));
        sink.Tag(heading);
        sink.Order = 1;
        sink.DrawRectangle(Offset.Zero, new Extent(1, 1), TestInks.Red);
        sink.Tag(paragraph);
        sink.Order = 0;
        sink.DrawRectangle(Offset.Zero, new Extent(2, 2), TestInks.Red);
        sink.Tag(null);
        sink.DrawRectangle(Offset.Zero, new Extent(3, 3), TestInks.Red);
        sink.EndPage();

        List<object?> drawn = pages.Pages[0].Operations
            .Select(operation => operation switch
            {
                TagOperation tag => (object?)tag.Element?.Role ?? "none",
                RectangleOperation rectangle => rectangle.Size.Width,
                _ => null,
            })
            .Where(entry => entry is not null)
            .ToList();

        Assert.Equal(["P", 2f, "none", 3f, "H1", 1f], drawn);
    }

    [Fact]
    public void APageDrawnInDrawOrderUntaggedTellsNothing()
    {
        RecordingSurface pages = new RecordingSurface();
        LayeredPageSink sink = new LayeredPageSink(pages);

        sink.BeginPage(new Extent(100, 100));
        sink.DrawRectangle(Offset.Zero, new Extent(1, 1), TestInks.Red);
        sink.EndPage();

        Assert.DoesNotContain(pages.Pages[0].Operations, operation => operation is TagOperation);
    }
}
