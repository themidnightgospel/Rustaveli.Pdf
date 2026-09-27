using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// The typefaces documents are set in: those registered here, then those installed on the machine.
/// </summary>
/// <remarks>
/// <para>
/// A registered typeface shadows an installed one of the same name entirely, so a document whose typefaces are all
/// registered comes out the same on every machine. <see cref="Shared"/> serves any export given no library of its
/// own; a library created with <c>includeInstalled: false</c> sees only what is registered with it.
/// </para>
/// <para>
/// Text is set in the face nearest its style's weight and slant. A character that face lacks is set in the first
/// of the <see cref="Fallbacks"/> that has it, then any registered face, then any installed one. Only the glyphs a
/// document uses are embedded.
/// </para>
/// <para>Safe for concurrent use. A document already exporting keeps the typefaces it started with.</para>
/// </remarks>
public sealed class TypefaceLibrary
{
    private readonly FontCatalog _catalog;
    private readonly object _lock = new object();
    private IReadOnlyList<string> _fallbacks = [];
    private TypeShaper _shaper;
    private IComplexShaper? _complex;

    /// <summary>A library of the installed typefaces, to which more can be registered.</summary>
    public TypefaceLibrary()
        : this(includeInstalled: true)
    {
    }

    /// <summary>A library that sees installed typefaces only when <paramref name="includeInstalled"/> is set.</summary>
    public TypefaceLibrary(bool includeInstalled)
        : this(includeInstalled ? new FontCatalog() : FontCatalog.WithoutSystemFonts())
    {
    }

    internal TypefaceLibrary(FontCatalog catalog)
    {
        _catalog = catalog;
        _shaper = new TypeShaper(catalog, _fallbacks);
    }

    /// <summary>The library used by any export that names none.</summary>
    public static TypefaceLibrary Shared { get; } = new TypefaceLibrary();

    /// <summary>
    /// Typefaces tried in order for a character a run's own typeface lacks, before the registered and installed
    /// ones. Naming them makes the choice the same on every machine.
    /// </summary>
    public IReadOnlyList<string> Fallbacks
    {
        get => _fallbacks;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (_lock)
            {
                _fallbacks = value.ToArray();
                Volatile.Write(ref _shaper, new TypeShaper(_catalog, _fallbacks, _complex));
            }
        }
    }

    internal TypeShaper Shaper => Volatile.Read(ref _shaper);

    /// <summary>
    /// The shaper for complex scripts, which the Rustaveli.Pdf.Shaping package installs; null until then, when such
    /// text is set glyph for glyph.
    /// </summary>
    internal IComplexShaper? ComplexShaper
    {
        get => _complex;
        set
        {
            lock (_lock)
            {
                _complex = value;
                Volatile.Write(ref _shaper, new TypeShaper(_catalog, _fallbacks, _complex));
            }
        }
    }

    /// <summary>Registers every face in a font file: TrueType or OpenType, single or a collection.</summary>
    /// <exception cref="ArgumentException">The data is not a font this library can read.</exception>
    public void Register(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Registering(() => _catalog.Register(data.ToArray()), nameof(data));
    }

    /// <summary>Registers every face in a font read from <paramref name="stream"/>, which is read to its end.</summary>
    /// <exception cref="ArgumentException">The stream does not hold a font this library can read.</exception>
    public void Register(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Registering(() => _catalog.Register(stream), nameof(stream));
    }

    /// <summary>Registers every face in the font file at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The file is not a font this library can read.</exception>
    public void RegisterFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Registering(() => _catalog.RegisterFile(path), nameof(path));
    }

    private void Registering(Func<IReadOnlyList<FontFaceInfo>> register, string parameter)
    {
        lock (_lock)
        {
            try
            {
                register();
            }
            catch (FontFormatException exception)
            {
                throw new ArgumentException("This is not a font this library can read: " + exception.Message, parameter, exception);
            }

            // Faces resolved before now may be shadowed by what was just registered.
            Volatile.Write(ref _shaper, new TypeShaper(_catalog, _fallbacks, _complex));
        }
    }
}
