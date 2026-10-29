#if NET
namespace Rustaveli.Pdf.Testing;

/// <summary>What code allocates on the calling thread, for the tests that hold it to a budget.</summary>
internal static class Allocations
{
    /// <summary>The bytes <paramref name="work"/> allocates on this thread.</summary>
    public static long Of(Action work)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        work();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>
    /// The fewest bytes <paramref name="measure"/> reports in three runs. What allocates only once in a while does not
    /// repeat, such as an array a shared pool dropped under memory load, which a run that would have rented it makes
    /// anew; a change that allocates more does so in every run.
    /// </summary>
    public static long Fewest(Func<long> measure) => Math.Min(measure(), Math.Min(measure(), measure()));
}
#endif
