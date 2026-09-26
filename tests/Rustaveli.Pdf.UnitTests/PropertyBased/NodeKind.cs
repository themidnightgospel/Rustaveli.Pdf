namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>The kinds of element a generated layout tree is built from.</summary>
public enum NodeKind
{
    Text,
    Box,
    Column,
    Row,
    Table,
    Padding,
    Background,
    Border
}
