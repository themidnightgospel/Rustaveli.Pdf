namespace Rustaveli.Pdf.Layout;

/// <summary>How far each element of a tree had progressed when it was saved.</summary>
internal sealed class Progress(IReadOnlyList<(Block Block, object Progress)> saved)
{
    public IReadOnlyList<(Block Block, object Progress)> Saved { get; } = saved;
}
