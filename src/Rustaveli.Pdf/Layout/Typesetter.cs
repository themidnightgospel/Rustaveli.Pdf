using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Turns a composed document into a sequence of drawn pages.
/// </summary>
/// <remarks>
/// Rendering runs the document more than once. Counting passes draw to a page sink that discards everything, purely
/// to discover how many pages the document occupies; they repeat until the count settles, because feeding the
/// total back in can itself change it. A final pass then draws for real. Every pass runs identical layout code,
/// so the count cannot drift between them.
/// </remarks>
internal static class Typesetter
{
    /// <summary>Upper bound on pages per run, so a layout that never terminates fails loudly instead of hanging.</summary>
    private const int MaxPagesPerRun = 10_000;

    /// <summary>
    /// How many times the page count may be recomputed before the result is accepted as-is. Documents settle in
    /// two passes in practice; the cap stops a pathological one that oscillates from looping forever.
    /// </summary>
    private const int MaxCountingPasses = 5;

    public static void Render(Document document, IPageSink pages, ITypeMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(measurer);

        Pagination pageContext = new Pagination();
        int total = 0;

        // Counting is repeated until it settles. Feeding the total back in can change the answer: "of 9" is
        // narrower than "of 10", so learning the real total can wrap a footer onto a second line, shrink the
        // content area and produce another page. A single pass would then print a total that is off by one on
        // every page of the document.
        for (int attempt = 0; attempt < MaxCountingPasses; attempt++)
        {
            using CountingPageSink probe = new CountingPageSink();

            RunPass(document, probe, measurer, pageContext);

            if (probe.PageCount == total)
                break;

            total = probe.PageCount;
            pageContext.PageCount = total;
            pageContext.IsPageCountKnown = true;
        }

        RunPass(document, pages, measurer, pageContext);
    }

    private static void RunPass(Document document, IPageSink pages, ITypeMeasurer measurer, Pagination pageContext)
    {
        pageContext.ResetForNewPass();

        foreach (Block? slot in document.Sections.SelectMany(section => section.Slots()))
            slot.ResetState();

        PlanContext layout = new PlanContext(measurer, pageContext);
        RenderContext context = new RenderContext(pages, layout);
        int pageNumber = 0;

        foreach (Section section in document.Sections)
        {
            layout.DefaultType = section.DefaultType;
            layout.ReadingDirection = section.ReadingDirection;

            int renderedInRun = 0;

            while (true)
            {
                pageNumber++;
                pageContext.Folio = pageNumber;

                // Until the real total is known, quote the page count as the current page so that dynamic text
                // such as "3 of 3" occupies a realistic width and does not shift the layout on the second pass.
                if (!pageContext.IsPageCountKnown)
                    pageContext.PageCount = pageNumber;

                bool hasMore = RenderPage(section, pages, context, layout);

                if (!hasMore)
                    break;

                // Counts pages that still left content over, so reaching the cap means the run needs more than
                // MaxPagesPerRun pages. Testing with > would let one extra page through.
                if (++renderedInRun >= MaxPagesPerRun)
                    throw new OversetException(
                        $"The document exceeded {MaxPagesPerRun} pages in a single section, which usually means some content " +
                        "reports more to come but never takes any space.");
            }
        }
    }

    /// <summary>Draws one page and reports whether content remains for a following page.</summary>
    private static bool RenderPage(Section section, IPageSink pages, RenderContext context, PlanContext layout)
    {
        // Headers and footers repeat in full, so clear the pagination state they accumulated on the previous
        // page while leaving document-wide counters such as "show once" intact.
        section.RunningHeadSlot.ResetState(includeDocumentProgress: false);
        section.RunningFootSlot.ResetState(includeDocumentProgress: false);
        section.UnderlaySlot.ResetState(includeDocumentProgress: false);
        section.OverlaySlot.ResetState(includeDocumentProgress: false);

        // A page is sized by its content between these bounds; a fixed page is one whose bounds are equal.
        Extent smallest = section.SmallestTrim;
        Extent largest = section.LargestTrim;

        if (largest.Width <= 0 || largest.Height <= 0)
            throw new OversetException(
                $"The trim size {largest} cannot be drawn. Both dimensions must be greater than zero.");

        if (smallest.Width > largest.Width || smallest.Height > largest.Height)
            throw new OversetException(
                $"The smallest trim {smallest} is larger than the largest {largest}.");

        float contentWidth = largest.Width - section.Margins.Horizontal;

        if (contentWidth <= 0)
            throw new OversetException(
                $"The horizontal margins ({section.Margins.Horizontal:F1}) leave no room on a page {largest.Width:F1} points wide.");

        float availableHeight = largest.Height - section.Margins.Vertical;

        if (availableHeight <= 0)
            throw new OversetException(
                $"The vertical margins ({section.Margins.Vertical:F1}) leave no room on a page {largest.Height:F1} points tall.");

        Bands bands = PlanBands(section, new Extent(contentWidth, availableHeight), layout);
        float contentHeight = availableHeight - bands.HeadHeight - bands.FootHeight;

        // Tolerate the same sub-epsilon overshoot every element accepts as fitting. A footer that fits by that
        // tolerance can leave a hair below zero here, and must not be reported as overflowing the page.
        if (contentHeight < -Extent.Epsilon)
            throw new OversetException(
                $"The running head ({bands.HeadHeight:F1}) and running foot ({bands.FootHeight:F1}) together exceed the {availableHeight:F1} points available for the body.");

        Extent bodySpace = new Extent(contentWidth, contentHeight);
        Fit contentPlan = section.BodySlot.Plan(bodySpace, layout);

        if (contentPlan.IsDeferred)
            throw new OversetException(
                "The body cannot be set even on an empty page, so no further page would help. " +
                $"Space available: {bodySpace}. Reason: {contentPlan.DeferReason}");

        Extent pageSize = new Extent(
            Clamp(section.Margins.Horizontal + Math.Max(contentPlan.Size.Width, bands.Width), smallest.Width, largest.Width),
            Clamp(section.Margins.Vertical + bands.HeadHeight + contentPlan.Size.Height + bands.FootHeight, smallest.Height, largest.Height));

        // The body is drawn in what the page leaves it, which is at least the room it measured.
        Extent contentSpace = new Extent(
            pageSize.Width - section.Margins.Horizontal,
            pageSize.Height - section.Margins.Vertical - bands.HeadHeight - bands.FootHeight);

        try
        {
            DrawPage(section, pages, context, pageSize, contentSpace, bands);
        }
        catch (Exception exception) when (exception is not OversetException and not RenderingException)
        {
            // Failures raised from user content — a component that throws, an image that cannot be drawn — are
            // otherwise reported with a stack trace that says nothing about where in the document they occurred.
            throw new RenderingException(
                $"Drawing page {context.Pagination.Folio} failed. See the inner exception for details.",
                exception);
        }

        return contentPlan.IsPartial;
    }

    private static void DrawPage(
        Section section,
        IPageSink pages,
        RenderContext context,
        Extent pageSize,
        Extent contentSpace,
        Bands bands)
    {
        ISurface surface = context.Surface;
        Sides margin = section.Margins;

        pages.BeginPage(pageSize);

        if (!section.Paper.IsTransparent)
            surface.DrawRectangle(Offset.Zero, pageSize, section.Paper);

        // Background and foreground deliberately ignore margins so watermarks can bleed to the page edge.
        section.UnderlaySlot.Render(pageSize, context);

        Offset origin = new Offset(margin.Left, margin.Top);
        surface.Translate(origin);

        if (bands.HeadHeight > 0)
            section.RunningHeadSlot.Render(new Extent(contentSpace.Width, bands.HeadHeight), context);

        surface.Translate(new Offset(0, bands.HeadHeight));
        section.BodySlot.Render(contentSpace, context);
        surface.Translate(new Offset(0, -bands.HeadHeight));

        if (bands.FootHeight > 0)
        {
            // The footer sits against the bottom margin rather than immediately after the content.
            float footTop = pageSize.Height - margin.Vertical - bands.FootHeight;
            surface.Translate(new Offset(0, footTop));
            section.RunningFootSlot.Render(new Extent(contentSpace.Width, bands.FootHeight), context);
            surface.Translate(new Offset(0, -footTop));
        }

        surface.Translate(origin.Reverse());

        section.OverlaySlot.Render(pageSize, context);

        pages.EndPage();
    }

    private static Bands PlanBands(Section section, Extent available, PlanContext layout)
    {
        Fit headPlan = section.RunningHeadSlot.Plan(available, layout);

        if (headPlan.IsDeferred)
            throw new OversetException(
                $"The running head does not fit in {available}. Reason: {headPlan.DeferReason}");

        Extent remaining = new Extent(available.Width, available.Height - headPlan.Size.Height);

        if (remaining.IsNegative)
            throw new OversetException($"The running head ({headPlan.Size.Height:F1} points) is taller than the page.");

        // A running head that swallows the whole page nearly always holds content that expands to fill whatever
        // it is offered — vertical placement or Expand — in a band with no height of its own to work with.
        if (remaining.Height <= Extent.Epsilon)
            throw new OversetException(
                $"The running head took all {available.Height:F1} points available, leaving no room for the body or the running foot. " +
                "This usually means it holds content that expands to fill the space offered to it, such as Middle, " +
                "FlushBottom or Expand. Give the running head an explicit Height, or remove the expanding content.");

        Fit footPlan = section.RunningFootSlot.Plan(remaining, layout);

        if (footPlan.IsDeferred)
            throw new OversetException(
                $"The running foot does not fit in {remaining}. Reason: {footPlan.DeferReason}");

        return new Bands(headPlan.Size.Height, footPlan.Size.Height, Math.Max(headPlan.Size.Width, footPlan.Size.Width));
    }

    private static float Clamp(float value, float smallest, float largest) => Math.Min(largest, Math.Max(smallest, value));

    /// <summary>The height of the running head and foot, and the width the wider of them takes.</summary>
    private readonly record struct Bands(float HeadHeight, float FootHeight, float Width);
}
