namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>The kinds of block a generated layout tree is built from.</summary>
internal enum NodeKind
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
