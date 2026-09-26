
namespace Rustaveli.Pdf.UnitTests;

public class ExceptionTests
{
    [Fact]
    public void DrawingFailureCarriesItsMessageAndCause()
    {
        InvalidOperationException cause = new InvalidOperationException("image could not be decoded");

        RenderingException exception = new RenderingException("Drawing page 4 failed.", cause);

        Assert.Equal("Drawing page 4 failed.", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }

    [Fact]
    public void DrawingFailureNeedNotHaveACause()
    {
        Assert.Null(new RenderingException("Drawing failed.").InnerException);
    }

    [Fact]
    public void ComposeFailureCarriesItsMessageAndCause()
    {
        InvalidOperationException cause = new InvalidOperationException("boom");

        CompositionException exception = new CompositionException("Composing failed.", cause);

        Assert.Equal("Composing failed.", exception.Message);
        Assert.Same(cause, exception.InnerException);
        Assert.Null(new CompositionException("Composing failed.").InnerException);
    }

    [Fact]
    public void LayoutFailureCarriesItsMessage()
    {
        OversetException exception = new OversetException("Nothing fits.");

        Assert.Equal("Nothing fits.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void EveryFailureSharesOneBase()
    {
        // One catch should handle anything the library reports.
        Assert.IsAssignableFrom<TypesettingException>(new CompositionException("x"));
        Assert.IsAssignableFrom<TypesettingException>(new OversetException("x"));
        Assert.IsAssignableFrom<TypesettingException>(new RenderingException("x"));
    }
}
