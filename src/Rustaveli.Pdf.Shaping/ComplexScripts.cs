namespace Rustaveli.Pdf;

/// <summary>
/// Turns on shaping of complex scripts — Arabic, Hebrew with points, the scripts of India and South-East Asia and
/// others — for the text a <see cref="TypefaceLibrary"/> sets.
/// </summary>
/// <remarks>
/// Without it such text is set a character at a time: Arabic letters in their isolated forms, Indic vowel signs
/// where they were typed. With it, runs holding such characters are shaped by HarfBuzz from the face's own tables,
/// and every other run as before.
/// </remarks>
public static class ComplexScripts
{
    /// <summary>Shapes complex scripts in all text <paramref name="library"/> sets from now on.</summary>
    /// <returns>The library, so the call can end a chain.</returns>
    public static TypefaceLibrary ShapeComplexScripts(this TypefaceLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        library.ComplexShaper ??= new Shaping.HarfBuzzShaper();
        return library;
    }
}
