using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A body between a head band and a foot band, both drawn again on every page the body runs on to. Only the body
/// flows; the bands are drawn whole each time.
/// </summary>
internal sealed class BandsBlock : Block
{
    /// <summary>The band drawn above the body on every page.</summary>
    public Frame Head { get; } = new Frame();

    /// <summary>The content that flows between the bands.</summary>
    public Frame Body { get; } = new Frame();

    /// <summary>The band drawn below the body on every page.</summary>
    public Frame Foot { get; } = new Frame();

    internal override int ChildCount => 3;

    internal override Block? ChildAt(int index) => index switch
    {
        0 => Head,
        1 => Body,
        2 => Foot,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Arrange(availableSpace, context).Outcome;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Arrangement arrangement = Arrange(availableSpace, context.Planning);

        if (!arrangement.Outcome.PlacesContent)
            return;

        float width = availableSpace.Width;
        float bodyRoom = availableSpace.Height - arrangement.Head - arrangement.Foot;

        context.RenderAllotted(Head, new Extent(width, arrangement.Head), availableSpace.Height);

        Offset belowHead = new Offset(0, arrangement.Head);
        context.Surface.MoveOrigin(belowHead);
        Body.Render(new Extent(width, bodyRoom), context);
        context.Surface.MoveOrigin(belowHead.Reverse());

        // The foot follows the body as planned, not the bottom of the room the body was given.
        Offset belowBody = new Offset(0, arrangement.Head + arrangement.Body);
        context.Surface.MoveOrigin(belowBody);
        context.RenderAllotted(Foot, new Extent(width, arrangement.Foot), availableSpace.Height - arrangement.Head);
        context.Surface.MoveOrigin(belowBody.Reverse());

        // The bands go back to their beginning to be drawn whole on the next page, while what counts across the
        // document, such as content shown once, keeps count.
        Head.ResetState(includeDocumentProgress: false);
        Foot.ResetState(includeDocumentProgress: false);
    }

    /// <summary>
    /// Plans the bands and then the body in the room they leave, giving the outcome for the whole and the heights the
    /// head, body and foot take.
    /// </summary>
    private Arrangement Arrange(Extent room, PlanContext context)
    {
        Fit head = Head.Plan(room, context);
        Extent footRoom = new Extent(room.Width, room.Height - head.Size.Height);

        // A head that claims more than the room — content may report more than it was offered — leaves the foot a
        // room below nothing, which it is never asked to be planned in.
        if (head.IsDeferred || footRoom.IsNegative)
            return BandsDoNotFit;

        Fit foot = Foot.Plan(footRoom, context);

        if (foot.IsDeferred)
            return BandsDoNotFit;

        float headHeight = head.Size.Height;
        float footHeight = foot.Size.Height;
        float bodyHeight = room.Height - headHeight - footHeight;

        if (bodyHeight < -Extent.Epsilon)
            return new Arrangement(Fit.Defer("The head and foot bands leave no room for the body."), 0, 0, 0);

        Fit body = Body.Plan(new Extent(room.Width, bodyHeight), context);

        // With the body used up the bands have nothing left to accompany, and take no more pages of their own.
        if (!body.PlacesContent)
            return new Arrangement(body, 0, 0, 0);

        Extent size = new Extent(
            Math.Max(body.Size.Width, Math.Max(head.Size.Width, foot.Size.Width)),
            headHeight + body.Size.Height + footHeight);

        Fit whole = body.IsPartial ? Fit.Partial(size) : Fit.Complete(size);
        return new Arrangement(whole, headHeight, body.Size.Height, footHeight);
    }

    private static Arrangement BandsDoNotFit =>
        new Arrangement(Fit.Defer("The space available is too small for the head and foot bands."), 0, 0, 0);

    private readonly record struct Arrangement(Fit Outcome, float Head, float Body, float Foot);
}
