namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// One recorded page.
/// </summary>
public sealed class RecordedPage(Extent size)
{
    public Extent Size { get; } = size;

    public List<DrawOperation> Operations { get; } = [];

    public IEnumerable<TextOperation> Texts => Operations.OfType<TextOperation>();

    /// <summary>All text drawn on the page, in draw order.</summary>
    public string Content => string.Concat(Texts.Select(operation => operation.Text));
}
