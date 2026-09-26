using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Turns a composed document into a sequence of drawn pages.
/// </summary>
/// <remarks>
/// Rendering runs the document more than once. Counting passes draw to a canvas that discards everything, purely
/// to discover how many pages the document occupies; they repeat until the count settles, because feeding the
/// total back in can itself change it. A final pass then draws for real. Every pass runs identical layout code,
/// so the count cannot drift between them.
/// </remarks>
internal static class Typesetter
{
    /// <summary>Upper bound on pages per run, so a layout that never terminates fails loudly instead of hanging.</summary>
    private const int MaxPagesPerRun = 10_000;

    /// <summary>The tallest page PDF permits, in points.</summary>
    private const float MaxPageHeight = 14_400f;

    /// <summary>
    /// How many times the page count may be recomputed before the result is accepted as-is. Documents settle in
    /// two passes in practice; the cap stops a pathological one that oscillates from looping forever.
    /// </summary>
    private const int MaxCountingPasses = 5;

    public static void Render(Document document, IPageSink canvas, ITypeMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(canvas);
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
            pageContext.TotalPages = total;
            pageContext.IsDocumentLengthKnown = true;
        }

        RunPass(document, canvas, measurer, pageContext);
    }

    private static void RunPass(Document document, IPageSink canvas, ITypeMeasurer measurer, Pagination pageContext)
    {
        pageContext.ResetForNewPass();

        foreach (Block? slot in document.Pages.SelectMany(descriptor => descriptor.Slots()))
            slot.ResetState();

        PlanContext layout = new PlanContext(measurer, pageContext);
        RenderContext context = new RenderContext(canvas, layout);
        int pageNumber = 0;

        foreach (Section descriptor in document.Pages)
        {
            layout.DefaultTextStyle = descriptor.DefaultType;
            layout.ContentDirection = descriptor.ReadingDirection;

            int renderedInRun = 0;

            while (true)
            {
                pageNumber++;
                pageContext.CurrentPage = pageNumber;

                // Until the real total is known, quote the page count as the current page so that dynamic text
                // such as "3 of 3" occupies a realistic width and does not shift the layout on the second pass.
                if (!pageContext.IsDocumentLengthKnown)
                    pageContext.TotalPages = pageNumber;

                bool hasMore = RenderPage(descriptor, canvas, context, layout);

                if (!hasMore)
                    break;

                // Counts pages that still left content over, so reaching the cap means the run needs more than
                // MaxPagesPerRun pages. Testing with > would let one extra page through.
                if (++renderedInRun >= MaxPagesPerRun)
                    throw new OversetException(
                        $"The document exceeded {MaxPagesPerRun} pages in a single page run, which usually means an element " +
                        "reports content remaining but never consumes any space.");
            }
        }
    }

    /// <summary>Draws one page and reports whether content remains for a following page.</summary>
    private static bool RenderPage(Section descriptor, IPageSink pageCanvas, RenderContext context, PlanContext layout)
    {
        // Headers and footers repeat in full, so clear the pagination state they accumulated on the previous
        // page while leaving document-wide counters such as "show once" intact.
        descriptor.RunningHeadSlot.ResetState(includeDocumentProgress: false);
        descriptor.RunningFootSlot.ResetState(includeDocumentProgress: false);
        descriptor.UnderlaySlot.ResetState(includeDocumentProgress: false);
        descriptor.OverlaySlot.ResetState(includeDocumentProgress: false);

        if (descriptor.Trim.Width <= 0 || descriptor.Trim.Height <= 0)
            throw new OversetException(
                $"The page size {descriptor.Trim} is not drawable. Both dimensions must be greater than zero.");

        float contentWidth = descriptor.Trim.Width - descriptor.Margins.Horizontal;

        if (contentWidth <= 0)
            throw new OversetException(
                $"The horizontal margins ({descriptor.Margins.Horizontal:F1}) leave no room on a page {descriptor.Trim.Width:F1} points wide.");

        float availableHeight = descriptor.Continuous
            ? MaxPageHeight - descriptor.Margins.Vertical
            : descriptor.Trim.Height - descriptor.Margins.Vertical;

        if (availableHeight <= 0)
            throw new OversetException(
                $"The vertical margins ({descriptor.Margins.Vertical:F1}) leave no room on a page {descriptor.Trim.Height:F1} points tall.");

        Bands bands = MeasureBands(descriptor, new Extent(contentWidth, availableHeight), layout);
        float contentHeight = availableHeight - bands.HeaderHeight - bands.FooterHeight;

        // Tolerate the same sub-epsilon overshoot every element accepts as fitting. A footer that fits by that
        // tolerance can leave a hair below zero here, and must not be reported as overflowing the page.
        if (contentHeight < -Extent.Epsilon)
            throw new OversetException(
                $"The header ({bands.HeaderHeight:F1}) and footer ({bands.FooterHeight:F1}) together exceed the {availableHeight:F1} points available for content.");

        Extent contentSpace = new Extent(contentWidth, contentHeight);
        Fit contentPlan = descriptor.BodySlot.Plan(contentSpace, layout);

        if (contentPlan.IsDeferred)
            throw new OversetException(
                "The page content cannot be drawn even on an empty page, so no additional page would help. " +
                $"Available space: {contentSpace}. Reason: {contentPlan.DeferReason}");

        Extent pageSize = descriptor.Continuous
            ? new Extent(
                descriptor.Trim.Width,
                Math.Min(MaxPageHeight, descriptor.Margins.Vertical + bands.HeaderHeight + contentPlan.Size.Height + bands.FooterHeight))
            : descriptor.Trim;

        try
        {
            DrawPage(descriptor, pageCanvas, context, pageSize, contentSpace, bands);
        }
        catch (Exception exception) when (exception is not OversetException and not RenderingException)
        {
            // Failures raised from user content — a component that throws, an image that cannot be drawn — are
            // otherwise reported with a stack trace that says nothing about where in the document they occurred.
            throw new RenderingException(
                $"Drawing page {context.Page.CurrentPage} failed. See the inner exception for details.",
                exception);
        }

        return contentPlan.IsPartial;
    }

    private static void DrawPage(
        Section descriptor,
        IPageSink pageCanvas,
        RenderContext context,
        Extent pageSize,
        Extent contentSpace,
        Bands bands)
    {
        ISurface canvas = context.Canvas;
        Sides margin = descriptor.Margins;

        pageCanvas.BeginPage(pageSize);

        if (!descriptor.Paper.IsTransparent)
            canvas.DrawRectangle(Offset.Zero, pageSize, descriptor.Paper);

        // Background and foreground deliberately ignore margins so watermarks can bleed to the page edge.
        descriptor.UnderlaySlot.Render(pageSize, context);

        Offset origin = new Offset(margin.Left, margin.Top);
        canvas.Translate(origin);

        if (bands.HeaderHeight > 0)
            descriptor.RunningHeadSlot.Render(new Extent(contentSpace.Width, bands.HeaderHeight), context);

        canvas.Translate(new Offset(0, bands.HeaderHeight));
        descriptor.BodySlot.Render(contentSpace, context);
        canvas.Translate(new Offset(0, -bands.HeaderHeight));

        if (bands.FooterHeight > 0)
        {
            // The footer sits against the bottom margin rather than immediately after the content.
            float footerTop = pageSize.Height - margin.Vertical - bands.FooterHeight;
            canvas.Translate(new Offset(0, footerTop));
            descriptor.RunningFootSlot.Render(new Extent(contentSpace.Width, bands.FooterHeight), context);
            canvas.Translate(new Offset(0, -footerTop));
        }

        canvas.Translate(origin.Reverse());

        descriptor.OverlaySlot.Render(pageSize, context);

        pageCanvas.EndPage();
    }

    private static Bands MeasureBands(Section descriptor, Extent available, PlanContext layout)
    {
        Fit headerPlan = descriptor.RunningHeadSlot.Plan(available, layout);

        if (headerPlan.IsDeferred)
            throw new OversetException(
                $"The page header does not fit in {available}. Reason: {headerPlan.DeferReason}");

        Extent remaining = new Extent(available.Width, available.Height - headerPlan.Size.Height);

        if (remaining.IsNegative)
            throw new OversetException($"The page header ({headerPlan.Size.Height:F1} points) is taller than the page.");

        // A header that swallows the whole page is nearly always an element that expands to fill whatever it is
        // offered — vertical alignment or Extend — placed in a band that has no height of its own to work with.
        if (remaining.Height <= Extent.Epsilon)
            throw new OversetException(
                $"The page header claimed the entire {available.Height:F1} points available, leaving no room for content or footer. " +
                "This usually means it contains an element that expands to fill the space offered to it, such as AlignMiddle, " +
                "AlignBottom or Extend. Give the header an explicit Height, or remove the expanding element.");

        Fit footerPlan = descriptor.RunningFootSlot.Plan(remaining, layout);

        if (footerPlan.IsDeferred)
            throw new OversetException(
                $"The page footer does not fit in {remaining}. Reason: {footerPlan.DeferReason}");

        return new Bands(headerPlan.Size.Height, footerPlan.Size.Height);
    }

    private readonly record struct Bands(float HeaderHeight, float FooterHeight);
}
