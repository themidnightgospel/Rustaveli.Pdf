using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Adds a bookmark to the document's outline where its content starts: once, however many pages the content goes on
/// to cover.
/// </summary>
internal sealed class BookmarkBlock : EnclosingBlock
{
    private bool _added;

    public required string Title { get; init; }

    /// <summary>How deep in the outline the bookmark sits, from 1 for the outermost.</summary>
    public required int Level { get; init; }

    protected override void ResetOwnState() => _added = false;

    protected override object? SaveOwnProgress() => _added;

    protected override void RestoreOwnProgress(object progress) => _added = (bool)progress;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (!_added && !Plan(availableSpace, context.Planning).IsDeferred)
        {
            context.Surface.DrawBookmark(Title, Level);
            _added = true;
        }

        base.RenderCore(availableSpace, context);
    }
}
