using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// Marked content: sequences of a content stream a tagged document's structure points at, or marks as decoration.
/// </summary>
public class MarkedContentTests
{
    [Fact]
    public void WritesMarkedSequencesAndTheirNumbers()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        content.BeginMarkedContent(new PdfName("Artifact"));
        Assert.Equal(1, content.MarkedDepth);
        content.EndMarkedContent();
        content.BeginMarkedContent(new PdfName("P"), 12);
        content.BeginMarkedContent(new PdfName("Span"), 0);
        Assert.Equal(2, content.MarkedDepth);
        content.EndMarkedContent();
        content.EndMarkedContent();

        Assert.Equal(0, content.MarkedDepth);
        Assert.Equal("/Artifact BMC\nEMC\n/P<</MCID 12>>BDC\n/Span<</MCID 0>>BDC\nEMC\nEMC\n", Latin1.Text(content.Content));
        content.EnsureComplete();
    }

    [Fact]
    public void EndingASequenceNeverBegunIsRefused()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(content.EndMarkedContent);

        Assert.Contains("EMC", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, content.Length);
    }

    [Fact]
    public void AStreamIsNotCompleteWhileASequenceIsOpen()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginMarkedContent(new PdfName("P"), 0);
        content.BeginMarkedContent(new PdfName("Artifact"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(content.EnsureComplete);

        Assert.Contains("2 marked sequence(s)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASequenceCanHoldText()
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.BeginMarkedContent(new PdfName("P"), 0);
        content.BeginText();
        content.EndText();
        content.EndMarkedContent();

        Assert.Equal("/P<</MCID 0>>BDC\nBT\nET\nEMC\n", Latin1.Text(content.Content));
    }
}
