using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Changes the style text inherits, for everything beneath it.
/// </summary>
internal sealed class DefaultTypeBlock : EnclosingBlock
{
    public Func<TypeStyle, TypeStyle> Refinement { get; set; } = style => style;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        WithStyle(context, () => base.PlanCore(availableSpace, context));

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        WithStyle(context.Planning, () =>
        {
            base.RenderCore(availableSpace, context);
            return true;
        });

    private T WithStyle<T>(PlanContext context, Func<T> function)
    {
        TypeStyle previous = context.DefaultType;
        context.DefaultType = Refinement(previous);

        try
        {
            return function();
        }
        finally
        {
            context.DefaultType = previous;
        }
    }
}
