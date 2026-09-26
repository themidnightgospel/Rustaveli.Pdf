namespace Rustaveli.Pdf.Fonts;

/// <summary>One entry of a font's table directory.</summary>
/// <param name="Tag">The table's tag; see <see cref="TableTag"/>.</param>
/// <param name="Checksum">The checksum the font records for the table.</param>
/// <param name="Offset">Where the table starts, from the beginning of the file (not the face).</param>
/// <param name="Length">The table's length in bytes, without padding.</param>
internal readonly record struct TableRecord(uint Tag, uint Checksum, int Offset, int Length);
