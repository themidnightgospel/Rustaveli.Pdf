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
internal static class DocumentGenerator
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

    public static void Render(Document document, IDocumentCanvas canvas, ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(measurer);

        PageContext pageContext = new PageContext();
        int total = 0;

        // Counting is repeated until it settles. Feeding the total back in can change the answer: "of 9" is
        // narrower than "of 10", so learning the real total can wrap a footer onto a second line, shrink the
        // content area and produce another page. A single pass would then print a total that is off by one on
        // every page of the document.
        for (int attempt = 0; attempt < MaxCountingPasses; attempt++)
        {
            using NullDocumentCanvas probe = new NullDocumentCanvas();

            RunPass(document, probe, measurer, pageContext);

            if (probe.PageCount == total)
                break;

            total = probe.PageCount;
            pageContext.TotalPages = total;
            pageContext.IsDocumentLengthKnown = true;
        }

        RunPass(document, canvas, measurer, pageContext);
    }

    private static void RunPass(Document document, IDocumentCanvas canvas, ITextMeasurer measurer, PageContext pageContext)
    {
        pageContext.ResetForNewPass();

        foreach (Element? slot in document.Pages.SelectMany(descriptor => descriptor.Slots()))
            slot.ResetState();

        LayoutContext layout = new LayoutContext(measurer, pageContext);
        DrawContext context = new DrawContext(canvas, layout);
        int pageNumber = 0;

        foreach (PageDescriptor descriptor in document.Pages)
        {
            layout.DefaultTextStyle = descriptor.DefaultTextStyle;
            layout.ContentDirection = descriptor.Direction;

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
                    throw new DocumentLayoutException(
                        $"The document exceeded {MaxPagesPerRun} pages in a single page run, which usually means an element " +
                        "reports content remaining but never consumes any space.");
            }
        }
    }

    /// <summary>Draws one page and reports whether content remains for a following page.</summary>
    private static bool RenderPage(PageDescriptor descriptor, IDocumentCanvas pageCanvas, DrawContext context, LayoutContext layout)
    {
        // Headers and footers repeat in full, so clear the pagination state they accumulated on the previous
        // page while leaving document-wide counters such as "show once" intact.
        descriptor.HeaderSlot.ResetState(includeDocumentProgress: false);
        descriptor.FooterSlot.ResetState(includeDocumentProgress: false);
        descriptor.BackgroundSlot.ResetState(includeDocumentProgress: false);
        descriptor.ForegroundSlot.ResetState(includeDocumentProgress: false);

        if (descriptor.Size.Width <= 0 || descriptor.Size.Height <= 0)
            throw new DocumentLayoutException(
                $"The page size {descriptor.Size} is not drawable. Both dimensions must be greater than zero.");

        float contentWidth = descriptor.Size.Width - descriptor.Margin.Horizontal;

        if (contentWidth <= 0)
            throw new DocumentLayoutException(
                $"The horizontal margins ({descriptor.Margin.Horizontal:F1}) leave no room on a page {descriptor.Size.Width:F1} points wide.");

        float availableHeight = descriptor.IsContinuous
            ? MaxPageHeight - descriptor.Margin.Vertical
            : descriptor.Size.Height - descriptor.Margin.Vertical;

        if (availableHeight <= 0)
            throw new DocumentLayoutException(
                $"The vertical margins ({descriptor.Margin.Vertical:F1}) leave no room on a page {descriptor.Size.Height:F1} points tall.");

        Bands bands = MeasureBands(descriptor, new Size(contentWidth, availableHeight), layout);
        float contentHeight = availableHeight - bands.HeaderHeight - bands.FooterHeight;

        // Tolerate the same sub-epsilon overshoot every element accepts as fitting. A footer that fits by that
        // tolerance can leave a hair below zero here, and must not be reported as overflowing the page.
        if (contentHeight < -Size.Epsilon)
            throw new DocumentLayoutException(
                $"The header ({bands.HeaderHeight:F1}) and footer ({bands.FooterHeight:F1}) together exceed the {availableHeight:F1} points available for content.");

        Size contentSpace = new Size(contentWidth, contentHeight);
        SpacePlan contentPlan = descriptor.ContentSlot.Measure(contentSpace, layout);

        if (contentPlan.IsWrap)
            throw new DocumentLayoutException(
                "The page content cannot be drawn even on an empty page, so no additional page would help. " +
                $"Available space: {contentSpace}. Reason: {contentPlan.WrapReason}");

        Size pageSize = descriptor.IsContinuous
            ? new Size(
                descriptor.Size.Width,
                Math.Min(MaxPageHeight, descriptor.Margin.Vertical + bands.HeaderHeight + contentPlan.Size.Height + bands.FooterHeight))
            : descriptor.Size;

        try
        {
            DrawPage(descriptor, pageCanvas, context, pageSize, contentSpace, bands);
        }
        catch (Exception exception) when (exception is not DocumentLayoutException and not DocumentDrawingException)
        {
            // Failures raised from user content — a component that throws, an image that cannot be drawn — are
            // otherwise reported with a stack trace that says nothing about where in the document they occurred.
            throw new DocumentDrawingException(
                $"Drawing page {context.Page.CurrentPage} failed. See the inner exception for details.",
                exception);
        }

        return contentPlan.IsPartialRender;
    }

    private static void DrawPage(
        PageDescriptor descriptor,
        IDocumentCanvas pageCanvas,
        DrawContext context,
        Size pageSize,
        Size contentSpace,
        Bands bands)
    {
        ICanvas canvas = context.Canvas;
        Edges margin = descriptor.Margin;

        pageCanvas.BeginPage(pageSize);

        if (!descriptor.BackgroundColor.IsTransparent)
            canvas.DrawRectangle(Position.Zero, pageSize, descriptor.BackgroundColor);

        // Background and foreground deliberately ignore margins so watermarks can bleed to the page edge.
        descriptor.BackgroundSlot.Draw(pageSize, context);

        Position origin = new Position(margin.Left, margin.Top);
        canvas.Translate(origin);

        if (bands.HeaderHeight > 0)
            descriptor.HeaderSlot.Draw(new Size(contentSpace.Width, bands.HeaderHeight), context);

        canvas.Translate(new Position(0, bands.HeaderHeight));
        descriptor.ContentSlot.Draw(contentSpace, context);
        canvas.Translate(new Position(0, -bands.HeaderHeight));

        if (bands.FooterHeight > 0)
        {
            // The footer sits against the bottom margin rather than immediately after the content.
            float footerTop = pageSize.Height - margin.Vertical - bands.FooterHeight;
            canvas.Translate(new Position(0, footerTop));
            descriptor.FooterSlot.Draw(new Size(contentSpace.Width, bands.FooterHeight), context);
            canvas.Translate(new Position(0, -footerTop));
        }

        canvas.Translate(origin.Reverse());

        descriptor.ForegroundSlot.Draw(pageSize, context);

        pageCanvas.EndPage();
    }

    private static Bands MeasureBands(PageDescriptor descriptor, Size available, LayoutContext layout)
    {
        SpacePlan headerPlan = descriptor.HeaderSlot.Measure(available, layout);

        if (headerPlan.IsWrap)
            throw new DocumentLayoutException(
                $"The page header does not fit in {available}. Reason: {headerPlan.WrapReason}");

        Size remaining = new Size(available.Width, available.Height - headerPlan.Size.Height);

        if (remaining.IsNegative)
            throw new DocumentLayoutException($"The page header ({headerPlan.Size.Height:F1} points) is taller than the page.");

        // A header that swallows the whole page is nearly always an element that expands to fill whatever it is
        // offered — vertical alignment or Extend — placed in a band that has no height of its own to work with.
        if (remaining.Height <= Size.Epsilon)
            throw new DocumentLayoutException(
                $"The page header claimed the entire {available.Height:F1} points available, leaving no room for content or footer. " +
                "This usually means it contains an element that expands to fill the space offered to it, such as AlignMiddle, " +
                "AlignBottom or Extend. Give the header an explicit Height, or remove the expanding element.");

        SpacePlan footerPlan = descriptor.FooterSlot.Measure(remaining, layout);

        if (footerPlan.IsWrap)
            throw new DocumentLayoutException(
                $"The page footer does not fit in {remaining}. Reason: {footerPlan.WrapReason}");

        return new Bands(headerPlan.Size.Height, footerPlan.Size.Height);
    }

    private readonly record struct Bands(float HeaderHeight, float FooterHeight);
}
