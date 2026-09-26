namespace Rustaveli.Pdf.Testing;

/// <summary>
/// Named inks for tests: distinct markers for unit tests, and exact colours for the conformance specimens, whose
/// approved snapshots were rendered with these values. They are the Material Design shades the library's palette
/// provided before inks replaced it (docs/adr/0004-ink-colour-model.md), kept so no snapshot had to change.
/// </summary>
public static class TestInks
{
    public static Ink Black => Ink.Black;

    public static Ink White => Ink.White;

    public static Ink Transparent => Ink.Transparent;

    public static Ink Red { get; } = Ink.Hex("#F44336");

    public static Ink Blue { get; } = Ink.Hex("#2196F3");

    public static Ink Green { get; } = Ink.Hex("#4CAF50");

    public static Ink Grey { get; } = Ink.Hex("#9E9E9E");

    public static Ink Yellow { get; } = Ink.Hex("#FFEB3B");

    public static Ink Indigo { get; } = Ink.Hex("#3F51B5");

    public static Ink Cyan { get; } = Ink.Hex("#00BCD4");

    public static Ink Teal { get; } = Ink.Hex("#009688");

    public static Ink Amber { get; } = Ink.Hex("#FFC107");

    public static Ink GreyLighten3 { get; } = Ink.Hex("#EEEEEE");

    public static Ink GreyLighten4 { get; } = Ink.Hex("#F5F5F5");

    public static Ink AmberLighten3 { get; } = Ink.Hex("#FFE082");

    public static Ink AmberLighten4 { get; } = Ink.Hex("#FFECB3");

    public static Ink PinkLighten3 { get; } = Ink.Hex("#F48FB1");

    public static Ink TealLighten3 { get; } = Ink.Hex("#80CBC4");

    public static Ink IndigoLighten4 { get; } = Ink.Hex("#C5CAE9");

    public static Ink LimeLighten3 { get; } = Ink.Hex("#E6EE9C");
}
