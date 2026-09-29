using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Text;
using Buffer = HarfBuzzSharp.Buffer;
using Font = HarfBuzzSharp.Font;

namespace Rustaveli.Pdf.Shaping;

/// <summary>
/// Shapes runs in complex scripts with HarfBuzz: joining, reordering, mark placement and the rest of each script's
/// rules, from the face's own tables.
/// </summary>
/// <remarks>
/// <para>
/// A HarfBuzz font is made once per face, from the face's file, and scaled to its units per em, so positions come
/// back in font units and are scaled to the run's size here. HarfBuzz fonts are immutable once made and may shape
/// on several threads at once; each thread keeps a buffer of its own.
/// </para>
/// <para>
/// HarfBuzz hands right-to-left text back in display order. The walk wants logical order, and puts right-to-left
/// text in display order itself, a cluster at a time with each cluster kept as it is — so clusters are reversed here
/// but the glyphs within each keep HarfBuzz's order, which is the order their offsets were worked out in.
/// </para>
/// </remarks>
internal sealed class HarfBuzzShaper : IComplexShaper
{
    private readonly ConcurrentDictionary<OpenTypeFont, Font> _fonts = new();
    private readonly ConcurrentDictionary<TypeFeatures, Feature[]> _features = new();

    private static readonly Language Undetermined = new Language("und");

    [ThreadStatic]
    private static Buffer? _buffer;

    public bool Handles(ReadOnlySpan<char> run)
    {
        // Read a character at a time, not a code unit: Adlam, Brahmi and the like lie beyond the Basic Multilingual Plane.
        for (int index = 0; index < run.Length; index++)
        {
            int character = run[index];

            if (char.IsHighSurrogate(run[index]) && index + 1 < run.Length && char.IsLowSurrogate(run[index + 1]))
                character = char.ConvertToUtf32(run[index], run[++index]);

            if (ComplexScriptCharacters.Contains(character))
                return true;
        }

        return false;
    }

    public void Shape(
        OpenTypeFont face, ReadOnlySpan<char> run, float pointSize, TypeFeatures features, bool rightToLeft, List<ComplexGlyph> output)
    {
        // Looked up before it is added, so that finding it does not make a delegate for the method each time.
        Font font = _fonts.TryGetValue(face, out Font? made) ? made : _fonts.GetOrAdd(face, Create);
        Buffer buffer = _buffer ??= new Buffer();

        Prepare(buffer, run, rightToLeft);
        font.Shape(buffer, FeaturesOf(features));

        ReadOnlySpan<GlyphInfo> infos = buffer.GetGlyphInfoSpan();
        ReadOnlySpan<GlyphPosition> positions = buffer.GetGlyphPositionSpan();
        int first = output.Count;

        for (int index = 0; index < infos.Length; index++)
        {
            GlyphPosition position = positions[index];

            output.Add(new ComplexGlyph(
                (ushort)infos[index].Codepoint,
                (int)infos[index].Cluster,
                face.ToPoints(position.XAdvance, pointSize),
                face.ToPoints(position.XOffset, pointSize),
                face.ToPoints(position.YOffset, pointSize)));
        }

        if (buffer.Direction == Direction.RightToLeft)
            ReverseClusters(output, first);
    }

    /// <summary>Fills <paramref name="buffer"/> with a run to shape, and says how it is to be shaped.</summary>
    internal static void Prepare(Buffer buffer, ReadOnlySpan<char> run, bool rightToLeft)
    {
        buffer.ClearContents();
        buffer.AddUtf16(run);
        buffer.GuessSegmentProperties();

        // The guess fills in the language from the process's locale, which would choose a face's localized forms by
        // the machine the document is made on. The core sets text in no language in particular, the default language
        // system of each face, and so does HarfBuzz: "und" is undetermined, which no face has forms of its own for.
        buffer.Language = Undetermined;

        // HarfBuzz mirrors brackets and the like in text it sets right to left, so it is given the run as typed and
        // told the direction the paragraph gave it, rather than a guess from the script.
        if (rightToLeft)
            buffer.Direction = Direction.RightToLeft;
    }

    /// <summary>
    /// Puts glyphs HarfBuzz set right to left back in logical order, cluster by cluster, in place: the whole run
    /// reversed, then each cluster's glyphs turned back to the order HarfBuzz drew them in.
    /// </summary>
    private static void ReverseClusters(List<ComplexGlyph> glyphs, int first)
    {
        glyphs.Reverse(first, glyphs.Count - first);

        for (int start = first; start < glyphs.Count;)
        {
            int end = start + 1;

            while (end < glyphs.Count && glyphs[end].Cluster == glyphs[start].Cluster)
                end++;

            glyphs.Reverse(start, end - start);
            start = end;
        }
    }

    private static Font Create(OpenTypeFont face)
    {
        // HarfBuzz reads the file for as long as the font lives, so it is given memory the garbage collector cannot
        // move, freed when HarfBuzz lets the file go. A blob over a managed array would point at wherever the array
        // used to be after the next compaction.
        byte[] file = face.FileData.ToArray();
        IntPtr memory = Marshal.AllocHGlobal(file.Length);
        Marshal.Copy(file, 0, memory, file.Length);

        using Blob blob = new Blob(memory, file.Length, MemoryMode.ReadOnly, () => Marshal.FreeHGlobal(memory));
        using Face harfBuzzFace = new Face(blob, face.FaceIndex);

        Font font = new Font(harfBuzzFace);
        font.SetScale(face.UnitsPerEm, face.UnitsPerEm);
        return font;
    }

    /// <summary>
    /// The features a style sets, as HarfBuzz takes them: parsed once per set of features, since every run is shaped
    /// with them and few documents use more than a handful.
    /// </summary>
    private Feature[] FeaturesOf(TypeFeatures features) =>
        _features.TryGetValue(features, out Feature[]? known)
            ? known
            : _features.GetOrAdd(features, static wanted => wanted.Settings.Select(setting => Feature.Parse($"{setting.Tag}={setting.Value}")).ToArray());
}
