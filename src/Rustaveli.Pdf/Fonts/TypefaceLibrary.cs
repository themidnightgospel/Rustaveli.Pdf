using System.Reflection;
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
        // Replaced whole under the lock, never changed in place: a volatile read sees the latest list without taking it.
        get => Volatile.Read(ref _fallbacks);
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
        get => Volatile.Read(ref _complex);
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
    /// <param name="data">The font file.</param>
    /// <param name="typeface">
    /// A typeface name to register it under besides its own, as a style names it; its faces keep their own weights and
    /// slants.
    /// </param>
    /// <exception cref="ArgumentException">The data is not a font this library can read.</exception>
    public void Register(byte[] data, string? typeface = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        Registering(() => _catalog.Register(data.ToArray(), typeface), nameof(data));
    }

    /// <summary>Registers every face in a font read from <paramref name="stream"/>, which is read to its end.</summary>
    /// <param name="stream">The font file.</param>
    /// <param name="typeface">A typeface name to register it under besides its own.</param>
    /// <exception cref="ArgumentException">The stream does not hold a font this library can read.</exception>
    public void Register(Stream stream, string? typeface = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Registering(() => _catalog.Register(stream, typeface), nameof(stream));
    }

    /// <summary>Registers every face in the font file at <paramref name="path"/>.</summary>
    /// <param name="path">The font file.</param>
    /// <param name="typeface">A typeface name to register it under besides its own.</param>
    /// <exception cref="ArgumentException">The file is not a font this library can read.</exception>
    public void RegisterFile(string path, string? typeface = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        Registering(() => _catalog.RegisterFile(path, typeface), nameof(path));
    }

    /// <summary>Registers every face in a font embedded in <paramref name="assembly"/> as a manifest resource.</summary>
    /// <param name="assembly">The assembly the font is embedded in.</param>
    /// <param name="resource">The resource's full name, as <c>Assembly.GetManifestResourceNames</c> lists it.</param>
    /// <param name="typeface">A typeface name to register it under besides its own.</param>
    /// <exception cref="ArgumentException">The assembly has no such resource, or it is not a font this library can read.</exception>
    public void RegisterResource(Assembly assembly, string resource, string? typeface = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resource);

        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new ArgumentException(
                $"{assembly.GetName().Name} embeds no resource named \"{resource}\"; it embeds: {string.Join(", ", assembly.GetManifestResourceNames())}.",
                nameof(resource));

        Registering(() => _catalog.Register(stream, typeface), nameof(resource));
    }

    /// <summary>
    /// Searches a folder, and the folders inside it, for typefaces as installed ones are searched: after the registered
    /// typefaces and before the installed ones, each read in full only once a document uses it.
    /// </summary>
    /// <param name="path">The folder; one that is missing or cannot be read adds nothing.</param>
    public void SearchFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_lock)
        {
            _catalog.AddFolder(path);

            // Faces resolved before now may be shadowed by what the folder holds.
            Volatile.Write(ref _shaper, new TypeShaper(_catalog, _fallbacks, _complex));
        }
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
