using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Ends the page it is drawn on, so that whatever follows it starts the next.
/// </summary>
internal sealed class NewPageBlock : Block
{
    private bool _broken;

    protected override void ResetOwnState() => _broken = false;

    protected override object? SaveOwnProgress() => _broken;

    protected override void RestoreOwnProgress(object progress) => _broken = (bool)progress;

    /// <remarks>
    /// Taking every point of height left fills the page, and Partial tells the parent there is more to come, so the
    /// parent stops here and continues on a fresh page — where, drawn once already, this block is Nothing.
    /// </remarks>
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _broken ? Fit.Nothing() : Fit.Partial(0, availableSpace.Height);

    protected override void RenderCore(Extent availableSpace, RenderContext context) => _broken = true;
}
