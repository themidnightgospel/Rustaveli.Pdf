[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Rustaveli.Pdf.Preview.HotReload))]

namespace Rustaveli.Pdf.Preview;

/// <summary>
/// Told by the runtime, under <c>dotnet watch</c>, each time code is changed while the application runs: every open
/// preview composes its document again and redraws it.
/// </summary>
internal static class HotReload
{
    /// <summary>Called first, to drop what the changed code had cached; a preview caches only its drawn pages.</summary>
    public static void ClearCache(Type[]? updatedTypes) => PreviewSession.RefreshAll();

    /// <summary>Called once the application has been updated.</summary>
    public static void UpdateApplication(Type[]? updatedTypes) => PreviewSession.RefreshAll();
}
