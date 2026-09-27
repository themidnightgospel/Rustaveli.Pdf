using System.Collections.Concurrent;

namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Finds fonts for documents: fonts the user registered, then fonts installed on the machine, matched by family and
/// style, with a fallback search for characters the chosen font lacks.
/// </summary>
/// <remarks>
/// <para>
/// A registered family shadows an installed family of the same name entirely, as a CSS @font-face rule does: once a
/// document supplies "Noto Sans", every "Noto Sans" request is answered from what it supplied, so output cannot
/// change with what happens to be installed.
/// </para>
/// <para>
/// Safe for concurrent use without a process-wide lock. Matching reads an immutable snapshot of the registered
/// fonts; registering swaps in a new snapshot together with fresh caches, so a match computed against the old
/// fonts can never be served after the new ones arrive.
/// </para>
/// </remarks>
internal sealed class FontCatalog
{
    private readonly object _registrationLock = new object();
    private State _state = new State(FontFamilyIndex.Empty);

    /// <summary>A catalog over the fonts installed on this machine.</summary>
    public FontCatalog()
        : this(SystemFontIndex.Current)
    {
    }

    public FontCatalog(SystemFontIndex system)
    {
        ArgumentNullException.ThrowIfNull(system);
        System = system;
    }

    /// <summary>The installed fonts consulted after the registered ones.</summary>
    public SystemFontIndex System { get; }

    public IReadOnlyList<FontFaceInfo> RegisteredFaces => Volatile.Read(ref _state).Registered.Faces;

    /// <summary>A catalog of registered fonts only, whose output cannot depend on the machine.</summary>
    public static FontCatalog WithoutSystemFonts() => new FontCatalog(SystemFontIndex.Empty);

    /// <summary>Registers every face of a font file held in memory.</summary>
    /// <exception cref="FontFormatException">The data is not a font this library can read.</exception>
    public IReadOnlyList<FontFaceInfo> Register(ReadOnlyMemory<byte> data) => Register(new FontFileSource(data));

    /// <summary>Registers every face of a font read from a stream, which is read to its end.</summary>
    /// <exception cref="FontFormatException">The data is not a font this library can read.</exception>
    public IReadOnlyList<FontFaceInfo> Register(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return Register(copy.ToArray());
    }

    /// <summary>Registers every face of a font file.</summary>
    /// <exception cref="FontFormatException">The file is not a font this library can read.</exception>
    public IReadOnlyList<FontFaceInfo> RegisterFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        FontFileSource source = new FontFileSource(path);
        _ = source.Data;
        return Register(source);
    }

    /// <summary>
    /// The face best matching the request: from the registered fonts when any carry the family, else from the
    /// installed ones. Null when no font has the family.
    /// </summary>
    public FontFaceInfo? FindFace(FontRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Family);

        State state = Volatile.Read(ref _state);

        return state.Matches.GetOrAdd(request, key =>
        {
            IReadOnlyList<FontFaceInfo> candidates = state.Registered.Find(key.Family);

            if (candidates.Count == 0)
                candidates = System.Families.Find(key.Family);

            return FontMatcher.Select(candidates, key.Style);
        });
    }

    /// <summary>The font best matching the request, loaded; null when no font has the family.</summary>
    public OpenTypeFont? Match(FontRequest request) => FindFace(request)?.Load();

    /// <summary>
    /// A face that has a glyph for <paramref name="codepoint"/> and can be embedded, for text the requested font
    /// cannot show: the first of <paramref name="fallbackFamilies"/> that covers it, then the registered face and
    /// then the installed face closest to the requested style. Null when no font covers the character.
    /// </summary>
    /// <param name="codepoint">The character to find a glyph for.</param>
    /// <param name="request">The style to stay close to; its family is not consulted.</param>
    /// <param name="fallbackFamilies">Families to try first, in order, each matched to the requested style.</param>
    public FontFaceInfo? FindFallbackFace(
        int codepoint, FontRequest request, IReadOnlyList<string>? fallbackFamilies = null)
    {
        foreach (string family in fallbackFamilies ?? [])
        {
            FontFaceInfo? face = FindFace(request with { Family = family });

            if (face is { IsEmbeddable: true } && face.Covers(codepoint))
                return face;
        }

        State state = Volatile.Read(ref _state);

        foreach (FontFaceInfo face in state.RegisteredFor(request.Style))
        {
            if (face.IsEmbeddable && face.Covers(codepoint))
                return face;
        }

        return System.FindCovering(codepoint, request.Style);
    }

    /// <inheritdoc cref="FindFallbackFace"/>
    public OpenTypeFont? FindFallback(
        int codepoint, FontRequest request, IReadOnlyList<string>? fallbackFamilies = null) =>
        FindFallbackFace(codepoint, request, fallbackFamilies)?.Load();

    private IReadOnlyList<FontFaceInfo> Register(FontFileSource source)
    {
        IReadOnlyList<OpenTypeFont> fonts = OpenTypeFont.LoadAll(source.Data);
        FontFaceInfo[] faces = fonts.Select(font => FontFaceInfo.FromFont(source, font, registered: true)).ToArray();

        lock (_registrationLock)
        {
            FontFaceInfo[] all = [.. _state.Registered.Faces, .. faces];
            Volatile.Write(ref _state, new State(new FontFamilyIndex(all)));
        }

        return faces;
    }

    /// <summary>The registered fonts and every cache computed from them, replaced together on registration.</summary>
    private sealed class State(FontFamilyIndex registered)
    {
        private readonly ConcurrentDictionary<FaceStyle, FontFaceInfo[]> _registeredByStyle = new();

        public FontFamilyIndex Registered { get; } = registered;

        public ConcurrentDictionary<FontRequest, FontFaceInfo?> Matches { get; } = new(FamilyIgnoringCase.Instance);

        public FontFaceInfo[] RegisteredFor(FaceStyle style) =>
            _registeredByStyle.GetOrAdd(style, key => Registered.Faces
                .OrderBy(face => FontMatcher.Rank(face.Style, key))
                .ToArray());
    }

    /// <summary>Requests compared as matching compares them: family names without regard to case or padding.</summary>
    internal sealed class FamilyIgnoringCase : IEqualityComparer<FontRequest>
    {
        public static readonly FamilyIgnoringCase Instance = new FamilyIgnoringCase();

        public bool Equals(FontRequest x, FontRequest y) =>
            x.Weight == y.Weight && x.Slant == y.Slant && x.Width == y.Width &&
            string.Equals(x.Family.Trim(), y.Family.Trim(), StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(FontRequest request) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(request.Family.Trim()) ^ (request.Weight * 31) ^
            ((int)request.Slant << 12) ^ (request.Width << 16);
    }
}
