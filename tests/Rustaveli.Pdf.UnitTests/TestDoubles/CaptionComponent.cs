namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// A component that writes a single line of text, standing in for any reusable fragment of a document.
/// </summary>
public sealed class CaptionComponent(string caption) : IComponent
{
    public const string DefaultCaption = "caption";

    public CaptionComponent() : this(DefaultCaption)
    {
    }

    public void Compose(IFrame container) => container.Text(caption);
}
