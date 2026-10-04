#if !NET
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// Measuring on the build of the library that .NET Framework, .NET 8 and .NET 9 use. .NET Framework counts allocations
/// only for a whole application domain, so the measuring runs in a domain of its own, where no other test's work, or
/// what it leaves running, is counted with it.
/// </summary>
public class NetStandardWidthTests
{
    [Fact]
    public void MeasuringAWordAgainMakesNoStringOfIt()
    {
        // Allocation budget: a word measured before is found by its characters. Making a string of them for every
        // lookup cost a long report about 3.5 MB.
        const long Budget = 64 * 1024;
        AppDomain.MonitoringIsEnabled = true;
        AppDomain domain = AppDomain.CreateDomain(nameof(MeasuringAWordAgainMakesNoStringOfIt), null, AppDomain.CurrentDomain.SetupInformation);

        try
        {
            domain.DoCallBack(MeasureAWordTenThousandTimes);

            Assert.Equal(0, (int)domain.GetData("differing"));
            long allocated = (long)domain.GetData("allocated");
            Assert.True(allocated <= Budget, $"Measuring the word 10,000 times allocated {allocated:N0} bytes; its budget is {Budget:N0}.");
        }
        finally
        {
            AppDomain.Unload(domain);
        }
    }

    /// <summary>Runs in the measuring domain, and leaves what it found there.</summary>
    private static void MeasureAWordTenThousandTimes()
    {
        TypeStyle sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);
        char[] word = "Typesetting".ToCharArray();
        float width = measurer.MeasureWidth(word, sans);
        measurer.MeasureWidth(word, sans);
        int differing = 0;

        long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

        for (int round = 0; round < 10_000; round++)
        {
            if (measurer.MeasureWidth(word, sans) != width)
                differing++;
        }

        long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;

        AppDomain.CurrentDomain.SetData("differing", differing);
        AppDomain.CurrentDomain.SetData("allocated", allocated);
    }
}
#endif
