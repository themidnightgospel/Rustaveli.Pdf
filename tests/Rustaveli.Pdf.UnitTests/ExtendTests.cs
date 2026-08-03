namespace Rustaveli.Pdf.UnitTests;

public class ExtendTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExtendingHorizontally()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, Child = new FixedElement(10, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(new Size(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExtendingFully()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, ExtendVertical = true, Child = new FixedElement(10, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }
}
