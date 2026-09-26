namespace Rustaveli.Pdf.Text.LineBreaking;

/// <summary>
/// A place where a line may end: before the character at <paramref name="Position"/>, or after the last one when
/// <paramref name="Position"/> is the length of the text.
/// </summary>
/// <param name="Position">A UTF-16 index into the text, never inside a surrogate pair.</param>
/// <param name="IsMandatory">
/// The line must end here: after a line feed, carriage return, next line, line or paragraph separator, vertical tab
/// or form feed, and at the end of the text.
/// </param>
internal readonly record struct LineBreak(int Position, bool IsMandatory);
