namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// An immutable set of faces, indexed by every name a document might use for their family.
/// </summary>
/// <remarks>
/// A family is looked up by its typographic name first, so "Specimen Sans" finds the semibold face that calls itself
/// "Specimen Sans SemiBold" in its legacy name; then by the legacy name, so the legacy name still works; and last by
/// full or PostScript name, so "Noto Sans Bold" or "NotoSans-Bold" finds that one face.
/// </remarks>
internal sealed class FontFamilyIndex
{
    public static readonly FontFamilyIndex Empty = new FontFamilyIndex([]);

    private readonly Dictionary<string, List<FontFaceInfo>> _preferred = Create();
    private readonly Dictionary<string, List<FontFaceInfo>> _legacy = Create();
    private readonly Dictionary<string, List<FontFaceInfo>> _faceNames = Create();

    public FontFamilyIndex(IReadOnlyList<FontFaceInfo> faces)
    {
        Faces = faces;

        foreach (FontFaceInfo face in faces)
        {
            FontNames names = face.Names;

            Add(_preferred, face, names.PreferredFamily);

            foreach (string alias in names.PreferredFamilyAliases)
                Add(_preferred, face, alias);

            Add(_legacy, face, names.Family);

            foreach (string alias in names.FamilyAliases)
                Add(_legacy, face, alias);

            Add(_faceNames, face, names.FullName);
            Add(_faceNames, face, names.PostScriptName);
        }
    }

    public IReadOnlyList<FontFaceInfo> Faces { get; }

    /// <summary>The distinct preferred family names, sorted.</summary>
    public IReadOnlyList<string> Families =>
        _preferred.Values.Select(faces => faces[0].Names.PreferredFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(family => family, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>The faces of a family, or none when no face carries the name.</summary>
    public IReadOnlyList<FontFaceInfo> Find(string family)
    {
        string key = family.Trim();

        if (_preferred.TryGetValue(key, out List<FontFaceInfo>? faces) ||
            _legacy.TryGetValue(key, out faces) ||
            _faceNames.TryGetValue(key, out faces))
        {
            return faces;
        }

        return [];
    }

    private static Dictionary<string, List<FontFaceInfo>> Create() =>
        new Dictionary<string, List<FontFaceInfo>>(StringComparer.OrdinalIgnoreCase);

    private static void Add(Dictionary<string, List<FontFaceInfo>> index, FontFaceInfo face, string name)
    {
        if (name.Length == 0)
            return;

        if (!index.TryGetValue(name, out List<FontFaceInfo>? faces))
        {
            faces = [];
            index.Add(name, faces);
        }

        // A face listed under the same name in several languages is still one candidate.
        if (faces.Count == 0 || !ReferenceEquals(faces[faces.Count - 1], face))
            faces.Add(face);
    }
}
