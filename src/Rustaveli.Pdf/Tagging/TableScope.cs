namespace Rustaveli.Pdf.Tagging;

/// <summary>The cells a table heading heads.</summary>
internal enum TableScope
{
    /// <summary>The cells below it, in its column.</summary>
    Column,

    /// <summary>The cells beside it, in its row.</summary>
    Row,
}
