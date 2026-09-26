namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A component that writes a single line of text, standing in for any reusable fragment of a document.
/// </summary>
internal sealed class CaptionSnippet(string caption) : ISnippet
{
    public const string DefaultCaption = "caption";

    public CaptionSnippet() : this(DefaultCaption)
    {
    }

    public void Compose(IFrame container) => container.Text(caption);
}
