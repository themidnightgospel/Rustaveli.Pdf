namespace Rustaveli.Pdf.Writing;

/// <summary>The kind of object a <see cref="PdfValue"/> holds.</summary>
/// <remarks>
/// Streams are absent on purpose. PDF only permits a stream as an indirect object, never nested in another value,
/// so streams are written by the file writer directly, as numbered objects, and the type system rules out the
/// invalid nesting.
/// </remarks>
internal enum PdfValueKind
{
    Null,
    Boolean,
    Integer,
    Real,
    Name,
    String,
    Array,
    Dictionary,
    Reference,
}
