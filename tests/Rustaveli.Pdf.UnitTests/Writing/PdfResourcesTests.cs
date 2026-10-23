using System.Diagnostics;
using Rustaveli.Pdf.Writing;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfResourcesTests(ITestOutputHelper output)
{
    private static string Written(PdfResources resources) =>
        Latin1.Written(writer => writer.WriteDictionary(resources.ToDictionary()));

    [Fact]
    public void ANewNameIsComparedWithNoNameBeforeIt()
    {
        // A page of twenty thousand QR codes has twenty thousand images. Each new name is unique by construction, so
        // nothing needs to compare it with the names before it; doing so made the page quadratic in its images.
        PdfResources resources = new PdfResources();
        long compared = PdfDictionary.Compared;
        Stopwatch watch = Stopwatch.StartNew();

        for (int number = 1; number <= 50_000; number++)
            resources.GetXObjectName(new PdfReference(number));

        watch.Stop();
        output.WriteLine($"50,000 distinct XObjects named in {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(0, PdfDictionary.Compared - compared);

        PdfDictionary written = resources.ToDictionary();
        Assert.Equal("X50000", resources.GetXObjectName(new PdfReference(50_000)).Value);
        Assert.Equal(50_000, written[PdfNames.XObject].AsDictionary().Count);
    }

    [Fact]
    public void StartsEmpty()
    {
        PdfResources resources = new PdfResources();

        Assert.True(resources.IsEmpty);
        Assert.Equal("<<>>", Written(resources));
    }

    [Fact]
    public void NamesEachCategoryInOrderOfFirstUse()
    {
        PdfResources resources = new PdfResources();

        Assert.Equal("F1", resources.GetFontName(new PdfReference(10)).Value);
        Assert.Equal("F2", resources.GetFontName(new PdfReference(7)).Value);
        Assert.Equal("X1", resources.GetXObjectName(new PdfReference(10)).Value);
        Assert.Equal("GS1", resources.GetExtGStateName(new PdfReference(3)).Value);
        Assert.Equal("CS1", resources.GetColorSpaceName(new PdfReference(4)).Value);
        Assert.Equal("X2", resources.GetXObjectName(new PdfReference(11)).Value);
    }

    [Fact]
    public void ReturnsTheSameNameForTheSameObject()
    {
        PdfResources resources = new PdfResources();
        PdfName first = resources.GetFontName(new PdfReference(5));
        resources.GetFontName(new PdfReference(6));

        PdfName again = resources.GetFontName(new PdfReference(5));

        Assert.Same(first, again);
        Assert.Equal("F1", again.Value);
        Assert.Equal("F3", resources.GetFontName(new PdfReference(8)).Value);
    }

    [Fact]
    public void WritesOnlyTheCategoriesInUse()
    {
        PdfResources resources = new PdfResources();
        resources.GetExtGStateName(new PdfReference(9));
        resources.GetFontName(new PdfReference(12));
        resources.GetFontName(new PdfReference(13));
        resources.GetFontName(new PdfReference(12));

        Assert.False(resources.IsEmpty);
        Assert.Equal("<</Font<</F1 12 0 R/F2 13 0 R>>/ExtGState<</GS1 9 0 R>>>>", Written(resources));
    }

    [Fact]
    public void WritesEveryCategory()
    {
        PdfResources resources = new PdfResources();
        resources.GetPatternName(new PdfReference(5));
        resources.GetColorSpaceName(new PdfReference(4));
        resources.GetExtGStateName(new PdfReference(3));
        resources.GetXObjectName(new PdfReference(2));
        resources.GetFontName(new PdfReference(1));

        Assert.Equal(
            "<</Font<</F1 1 0 R>>/XObject<</X1 2 0 R>>/ExtGState<</GS1 3 0 R>>/ColorSpace<</CS1 4 0 R>>"
            + "/Pattern<</P1 5 0 R>>>>",
            Written(resources));
    }

    [Theory]
    [InlineData("Font")]
    [InlineData("XObject")]
    [InlineData("ExtGState")]
    [InlineData("ColorSpace")]
    [InlineData("Pattern")]
    public void IsNotEmptyOnceAnyCategoryIsUsed(string category)
    {
        PdfResources resources = new PdfResources();

        _ = category switch
        {
            "Font" => resources.GetFontName(new PdfReference(1)),
            "XObject" => resources.GetXObjectName(new PdfReference(1)),
            "ExtGState" => resources.GetExtGStateName(new PdfReference(1)),
            "ColorSpace" => resources.GetColorSpaceName(new PdfReference(1)),
            _ => resources.GetPatternName(new PdfReference(1)),
        };

        Assert.False(resources.IsEmpty);
    }

    [Fact]
    public void RefusesAReferenceThatWasNeverAssigned()
    {
        PdfResources resources = new PdfResources();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => resources.GetFontName(default));

        Assert.Equal("reference", exception.ParamName);
        Assert.StartsWith("The reference was never assigned an object number.", exception.Message, StringComparison.Ordinal);
        Assert.True(resources.IsEmpty);
        Assert.Equal("<<>>", Written(resources));
    }
}
