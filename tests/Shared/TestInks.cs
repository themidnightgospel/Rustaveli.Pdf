namespace Rustaveli.Pdf.Testing;

/// <summary>
/// Named inks for tests: distinct markers for unit tests, and exact colours for the conformance specimens, whose
/// approved snapshots were rendered with these values. They are the Material Design shades the library's palette
/// provided before inks replaced it (docs/adr/0004-ink-colour-model.md), kept so no snapshot had to change.
/// </summary>
public static class TestInks
{
    public static Rustaveli.Pdf.Primitives.Ink Black => Rustaveli.Pdf.Primitives.Ink.Black;

    public static Rustaveli.Pdf.Primitives.Ink White => Rustaveli.Pdf.Primitives.Ink.White;

    public static Rustaveli.Pdf.Primitives.Ink Transparent => Rustaveli.Pdf.Primitives.Ink.Transparent;

    public static Rustaveli.Pdf.Primitives.Ink Red { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#F44336");

    public static Rustaveli.Pdf.Primitives.Ink Blue { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#2196F3");

    public static Rustaveli.Pdf.Primitives.Ink Green { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#4CAF50");

    public static Rustaveli.Pdf.Primitives.Ink Grey { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#9E9E9E");

    public static Rustaveli.Pdf.Primitives.Ink Yellow { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#FFEB3B");

    public static Rustaveli.Pdf.Primitives.Ink Indigo { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#3F51B5");

    public static Rustaveli.Pdf.Primitives.Ink Cyan { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#00BCD4");

    public static Rustaveli.Pdf.Primitives.Ink Teal { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#009688");

    public static Rustaveli.Pdf.Primitives.Ink Amber { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#FFC107");

    public static Rustaveli.Pdf.Primitives.Ink GreyLighten3 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#EEEEEE");

    public static Rustaveli.Pdf.Primitives.Ink GreyLighten4 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#F5F5F5");

    public static Rustaveli.Pdf.Primitives.Ink AmberLighten3 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#FFE082");

    public static Rustaveli.Pdf.Primitives.Ink AmberLighten4 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#FFECB3");

    public static Rustaveli.Pdf.Primitives.Ink PinkLighten3 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#F48FB1");

    public static Rustaveli.Pdf.Primitives.Ink TealLighten3 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#80CBC4");

    public static Rustaveli.Pdf.Primitives.Ink IndigoLighten4 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#C5CAE9");

    public static Rustaveli.Pdf.Primitives.Ink LimeLighten3 { get; } = Rustaveli.Pdf.Primitives.Ink.Hex("#E6EE9C");
}
