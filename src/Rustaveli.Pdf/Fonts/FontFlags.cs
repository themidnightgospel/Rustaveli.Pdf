namespace Rustaveli.Pdf.Fonts;

/// <summary>The font descriptor flags of a PDF font (ISO 32000-1, table 123), with their bit values.</summary>
[Flags]
internal enum FontFlags
{
    None = 0,
    FixedPitch = 1 << 0,
    Serif = 1 << 1,
    Symbolic = 1 << 2,
    Script = 1 << 3,
    Nonsymbolic = 1 << 5,
    Italic = 1 << 6,
    AllCap = 1 << 16,
    SmallCap = 1 << 17,
    ForceBold = 1 << 18
}
