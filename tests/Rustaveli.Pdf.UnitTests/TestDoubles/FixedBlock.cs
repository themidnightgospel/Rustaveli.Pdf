namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element of a fixed intrinsic size that wraps when it does not fit.
/// </summary>
/// <remarks>
/// Layout behaviour is far easier to assert against a shape of known size than against real content, whose
/// dimensions depend on font metrics and wrapping.
/// </remarks>
internal sealed class FixedBlock(Extent size, Ink? color = null) : Block
{
    public FixedBlock(float width, float height) : this(new Extent(width, height))
    {
    }

    public FixedBlock(float width, float height, Ink color) : this(new Extent(width, height), color)
    {
    }

    public Ink Color { get; } = color ?? TestInks.Black;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        size.FitsIn(availableSpace)
            ? Fit.Complete(size)
            : Fit.Defer($"The block requires {size} but only {availableSpace} is available.");

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (size.FitsIn(availableSpace))
            context.Surface.DrawRectangle(Offset.Zero, size, Color);
    }
}
