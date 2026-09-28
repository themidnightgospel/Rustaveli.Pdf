using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// What shaping a run needs besides the text: the glyph buffer and a substitution session, kept between walks on one
/// thread so that shaping, done for every word measured, allocates nothing once warm.
/// </summary>
internal sealed class ShapingScratch
{
    public GlyphBuffer Buffer { get; } = new GlyphBuffer();

    /// <summary>Where a complex shaper placed the glyphs in <see cref="Buffer"/>; empty for glyphs the core set.</summary>
    public List<ComplexGlyph> Placements { get; } = [];

    private SubstitutionSession? _session;

    /// <summary>A session applying <paramref name="table"/> to <see cref="Buffer"/>, as fresh as a new one.</summary>
    public SubstitutionSession SessionFor(GlyphSubstitutionTable table)
    {
        if (_session is null)
            return _session = new SubstitutionSession(table, Buffer);

        _session.Reset(table, Buffer);
        return _session;
    }
}
