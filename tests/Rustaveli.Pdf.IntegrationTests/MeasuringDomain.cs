#if !NET
namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Measuring on .NET Framework, which counts allocations only for a whole application domain: the work runs in a
/// domain of its own, where no other test's work, or what it leaves running, is counted with it.
/// </summary>
internal static class MeasuringDomain
{
    /// <summary>
    /// Runs <paramref name="work"/> in a new domain and returns what it left there, with
    /// <see cref="AppDomain.SetData(string, object)"/>, under each of <paramref name="names"/>.
    /// </summary>
    public static object[] Run(CrossAppDomainDelegate work, params string[] names)
    {
        AppDomain.MonitoringIsEnabled = true;
        AppDomain domain = AppDomain.CreateDomain(work.Method.Name, null, AppDomain.CurrentDomain.SetupInformation);

        try
        {
            domain.DoCallBack(work);
            return Array.ConvertAll(names, domain.GetData);
        }
        finally
        {
            AppDomain.Unload(domain);
        }
    }
}
#endif
