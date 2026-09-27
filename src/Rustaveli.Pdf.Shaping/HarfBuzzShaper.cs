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

    [ThreadStatic]
    private static Buffer? _buffer;

    public bool Handles(ReadOnlySpan<char> run)
    {
        foreach (char character in run)
        {
            if (ComplexScriptCharacters.Contains(character))
                return true;
        }

        return false;
    }

    public void Shape(OpenTypeFont face, ReadOnlySpan<char> run, float pointSize, TypeFeatures features, List<ComplexGlyph> output)
    {
        Font font = _fonts.GetOrAdd(face, Create);
        Buffer buffer = _buffer ??= new Buffer();

        buffer.ClearContents();
        buffer.AddUtf16(run);
        buffer.GuessSegmentProperties();
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

    /// <summary>Puts glyphs HarfBuzz set right to left back in logical order, cluster by cluster.</summary>
    private static void ReverseClusters(List<ComplexGlyph> glyphs, int first)
    {
        List<ComplexGlyph> display = glyphs.GetRange(first, glyphs.Count - first);
        glyphs.RemoveRange(first, glyphs.Count - first);

        int end = display.Count;

        while (end > 0)
        {
            int start = end - 1;

            while (start > 0 && display[start - 1].Cluster == display[end - 1].Cluster)
                start--;

            for (int index = start; index < end; index++)
                glyphs.Add(display[index]);

            end = start;
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

    private static Feature[] FeaturesOf(TypeFeatures features) =>
        features.Settings.Select(setting => Feature.Parse($"{setting.Tag}={setting.Value}")).ToArray();
}
