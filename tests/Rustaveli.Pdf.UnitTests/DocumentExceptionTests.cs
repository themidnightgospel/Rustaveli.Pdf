using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class DocumentExceptionTests
{
    [Fact]
    public void DrawingFailureCarriesItsMessageAndCause()
    {
        InvalidOperationException cause = new InvalidOperationException("image could not be decoded");

        DocumentDrawingException exception = new DocumentDrawingException("Drawing page 4 failed.", cause);

        Assert.Equal("Drawing page 4 failed.", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }

    [Fact]
    public void DrawingFailureNeedNotHaveACause()
    {
        Assert.Null(new DocumentDrawingException("Drawing failed.").InnerException);
    }

    [Fact]
    public void ComposeFailureCarriesItsMessageAndCause()
    {
        InvalidOperationException cause = new InvalidOperationException("boom");

        DocumentComposeException exception = new DocumentComposeException("Composing failed.", cause);

        Assert.Equal("Composing failed.", exception.Message);
        Assert.Same(cause, exception.InnerException);
        Assert.Null(new DocumentComposeException("Composing failed.").InnerException);
    }

    [Fact]
    public void LayoutFailureCarriesItsMessage()
    {
        DocumentLayoutException exception = new DocumentLayoutException("Nothing fits.");

        Assert.Equal("Nothing fits.", exception.Message);
        Assert.Null(exception.InnerException);
    }
}
