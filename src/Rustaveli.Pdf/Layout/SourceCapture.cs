using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// While switched on for a thread, records for each block made on it the first line of code outside this library
/// that led to it — the line of the document's own code — so a preview can lead back to it.
/// </summary>
/// <remarks>
/// Reading the stack is slow, so it is done only while a preview composes a document, never while one is exported.
/// Lines are known only where the calling code was built with its symbols beside it, as it is by default.
/// </remarks>
internal static class SourceCapture
{
    /// <summary>The library's own assemblies, whose frames lie between a block and the code that asked for it.</summary>
    private static readonly HashSet<string> Library = new HashSet<string>(StringComparer.Ordinal)
    {
        "Rustaveli.Pdf", "Rustaveli.Pdf.Operations", "Rustaveli.Pdf.Preview", "Rustaveli.Pdf.Raster", "Rustaveli.Pdf.Shaping",
    };

    [ThreadStatic]
    private static bool _on;

    /// <summary>Records where blocks come from on this thread until the scope ends.</summary>
    public static Scope Record()
    {
        Scope scope = new Scope(_on);
        _on = true;
        return scope;
    }

    /// <summary>
    /// The file and line of the first caller outside this library whose symbols are at hand, or null when not
    /// recording or not known.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only a preview records sources, and a preview is never trimmed; a method the trimmer removed reads as unknown.")]
    public static string? Current()
    {
        if (!_on)
            return null;

        foreach (StackFrame frame in new StackTrace(1, fNeedFileInfo: true).GetFrames() ?? [])
        {
            string? assembly = frame.GetMethod()?.DeclaringType?.Assembly.GetName().Name;

            if (assembly is null || Library.Contains(assembly) || assembly.StartsWith("System", StringComparison.Ordinal))
                continue;

            // Code built without its symbols, such as a helper package, is passed through to the code that called it.
            if (frame.GetFileName() is { } file)
                return file + ":" + frame.GetFileLineNumber().ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>Restores whether recording was on before.</summary>
    internal readonly struct Scope(bool previous) : IDisposable
    {
        public void Dispose() => _on = previous;
    }
}
