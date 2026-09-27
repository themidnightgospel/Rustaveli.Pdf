using Rustaveli.Pdf.Operations.Reading;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>A page of the file being assembled: the page it copies, and pages laid beneath and over it.</summary>
internal sealed class PageEntry(SourcePage page)
{
    public SourcePage Page { get; } = page;

    /// <summary>Pages drawn beneath the page, first lowest.</summary>
    public List<SourcePage> Beneath { get; } = [];

    /// <summary>Pages drawn over the page, first lowest.</summary>
    public List<SourcePage> Over { get; } = [];

    public PageEntry Clone()
    {
        PageEntry clone = new PageEntry(Page);
        clone.Beneath.AddRange(Beneath);
        clone.Over.AddRange(Over);
        return clone;
    }
}
