using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Tagging;
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
    /// <summary>
    /// How many times the page count may be recomputed before the result is accepted as-is. Documents settle in
    /// two passes in practice; the cap stops a pathological one that oscillates from looping forever.
    /// </summary>
    private const int MaxCountingPasses = 5;

    /// <summary>
    /// Sets <paramref name="document"/>: counting passes until the page count settles, then a final pass that draws.
    /// </summary>
    /// <param name="document">The document to set.</param>
    /// <param name="pages">Where the final pass draws.</param>
    /// <param name="measurer">What measures text.</param>
    /// <param name="resolution">The resolution generated images are asked for.</param>
    /// <param name="tagged">Whether the final pass records the document's structure, for a tagged PDF.</param>
    /// <param name="inspection">Where the final pass records every frame it draws, for a preview's inspector.</param>
    public static void Render(Document document, IPageSink pages, ITypeMeasurer measurer, float resolution = 288, bool tagged = false, LayoutInspection? inspection = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(measurer);

        // The document's own tree, or a copy composed afresh while another export is laying the tree out.
        using Document.ExportLease lease = document.ForExport();
        document = lease.Document;

        // Content composed as pages are set — per page, or later — names styles as content composed up front does.
        using StyleSheet.Scope styles = document.Styles.Use();
        Pagination pageContext = new Pagination();
        int[] counted = [];

        // Counting is repeated until it settles. Feeding the total back in can change the answer: "of 9" is
        // narrower than "of 10", so learning the real total can wrap a footer onto a second line, shrink the
        // content area and produce another page. A single pass would then print a total that is off by one on
        // every page of the document.
        for (int attempt = 0; attempt < MaxCountingPasses; attempt++)
        {
            using CountingPageSink probe = new CountingPageSink();

            int[] pagesByPart = RunPass(document, probe, measurer, pageContext, resolution);

            if (pagesByPart.SequenceEqual(counted))
                break;

            counted = pagesByPart;
            pageContext.PageCount = counted.Sum();
            pageContext.PartPageCounts = counted;
            pageContext.IsPageCountKnown = true;
        }

        RunPass(document, pages, measurer, pageContext, resolution, tagged ? new StructureElement("Document", null) : null, inspection);
    }

    /// <summary>Runs the document once through <paramref name="pages"/>; returns how many pages each merged document took.</summary>
    private static int[] RunPass(Document document, IPageSink pages, ITypeMeasurer measurer, Pagination pageContext, float resolution, StructureElement? structure = null, LayoutInspection? inspection = null)
    {
        pageContext.ResetForNewPass();

        PlanContext layout = new PlanContext(measurer, pageContext) { Resolution = resolution };
        RenderContext direct = new RenderContext(pages, layout, structure) { Inspection = inspection };
        int pageNumber = 0;
        int[] pagesByPart = new int[document.PartCount];

        for (int index = 0; index < document.Sections.Count; index++)
        {
            Section section = document.Sections[index];
            int part = document.PartOf(index);

            // A section starts afresh: one merged in twice is set twice, the second time from its beginning.
            foreach (Block? slot in section.Slots())
                slot.ResetState();

            // Content composed as its pages are set names styles from the document it came from.
            using StyleSheet.Scope styles = document.StylesOf(part).Use();

            // Pages whose content sets a draw order are held back and drawn in that order; counted pages are thrown
            // away, so they need no order.
            bool ordered = pages is not CountingPageSink && section.Slots().Any(slot => slot.Traverse().Any(block => block is DrawOrderBlock));
            IPageSink sink = ordered ? new LayeredPageSink(pages) : pages;
            RenderContext context = ordered ? new RenderContext(sink, layout, structure) { Inspection = inspection } : direct;

            layout.DefaultType = section.DefaultType;
            layout.ReadingDirection = section.ReadingDirection;

            while (true)
            {
                // A layout that never stops asking for another page fails loudly, rather than hanging, once the
                // document has as many pages as it allows.
                if (++pageNumber > document.PageLimit)
                {
                    throw new OversetException(
                        $"The document exceeded {document.PageLimit} pages, which usually means some content reports more to come " +
                        "but never takes any space. A document that really is longer can raise its PageLimit.");
                }

                pagesByPart[part]++;
                pageContext.Folio = document.NumbersPartsApart ? pagesByPart[part] : pageNumber;

                // Until the real total is known, quote the page count as the current page so that dynamic text
                // such as "3 of 3" occupies a realistic width and does not shift the layout on the second pass.
                if (!pageContext.IsPageCountKnown)
                    pageContext.PageCount = pageContext.Folio;
                else if (document.NumbersPartsApart && pageContext.PartPageCounts is { } counts && part < counts.Length)
                    pageContext.PageCount = counts[part];

                bool hasMore = RenderPage(section, sink, context, layout);

                if (!hasMore)
                    break;
            }
        }

        return pagesByPart;
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
                FormattableString.Invariant($"The horizontal margins ({section.Margins.Horizontal:F1}) leave no room on a page {largest.Width:F1} points wide."));

        float availableHeight = largest.Height - section.Margins.Vertical;

        if (availableHeight <= 0)
            throw new OversetException(
                FormattableString.Invariant($"The vertical margins ({section.Margins.Vertical:F1}) leave no room on a page {largest.Height:F1} points tall."));

        Bands bands = PlanBands(section, new Extent(contentWidth, availableHeight), layout);
        float contentHeight = availableHeight - bands.HeadHeight - bands.FootHeight;

        // Tolerate the same sub-epsilon overshoot every element accepts as fitting. A footer that fits by that
        // tolerance can leave a hair below zero here, and must not be reported as overflowing the page.
        if (contentHeight < -Extent.Epsilon)
            throw new OversetException(
                FormattableString.Invariant($"The running head ({bands.HeadHeight:F1}) and running foot ({bands.FootHeight:F1}) together exceed the {availableHeight:F1} points available for the body."));

        Extent bodySpace = new Extent(contentWidth, contentHeight);
        layout.PageBody = bodySpace;
        Fit contentPlan = section.BodySlot.Plan(bodySpace, layout);

        if (contentPlan.IsDeferred)
            throw new OversetException(
                "The body cannot be set even on an empty page, so no further page would help. " +
                $"Space available: {bodySpace}. Reason: {contentPlan.DeferReason}" + Trace(section.BodySlot, bodySpace, layout));

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
        context.Inspection?.BeginPage();

        // Only the body is the document's content; paper, underlay, running head and foot and overlay are the page's,
        // repeated on every one, and left out of its structure.
        using (context.Tags.Untag())
        {
            if (!section.Paper.IsTransparent)
            {
                // The paper lies beneath everything, even content drawn beneath the rest.
                LayeredPageSink? layers = pages as LayeredPageSink;
                layers?.Order = int.MinValue;
                surface.DrawRectangle(Offset.Zero, pageSize, section.Paper);
                layers?.Order = 0;
            }

            // Background and foreground deliberately ignore margins so watermarks can bleed to the page edge.
            section.UnderlaySlot.Render(pageSize, context);
        }

        Offset origin = new Offset(margin.Left, margin.Top);
        surface.Translate(origin);

        // A band with content is drawn even at no height: what takes no room — an anchor, a bookmark, a marker — must
        // still take effect.
        if (section.RunningHeadSlot.Child is not null)
        {
            using (context.Tags.Untag())
                section.RunningHeadSlot.Render(new Extent(contentSpace.Width, bands.HeadHeight), context);
        }

        surface.Translate(new Offset(0, bands.HeadHeight));
        section.BodySlot.Render(contentSpace, context);
        surface.Translate(new Offset(0, -bands.HeadHeight));

        if (section.RunningFootSlot.Child is not null)
        {
            // The footer sits against the bottom margin rather than immediately after the content.
            float footTop = pageSize.Height - margin.Vertical - bands.FootHeight;
            surface.Translate(new Offset(0, footTop));

            using (context.Tags.Untag())
                section.RunningFootSlot.Render(new Extent(contentSpace.Width, bands.FootHeight), context);

            surface.Translate(new Offset(0, -footTop));
        }

        surface.Translate(origin.Reverse());

        using (context.Tags.Untag())
            section.OverlaySlot.Render(pageSize, context);

        pages.EndPage();
    }

    private static Bands PlanBands(Section section, Extent available, PlanContext layout)
    {
        Fit headPlan = section.RunningHeadSlot.Plan(available, layout);

        if (headPlan.IsDeferred)
            throw new OversetException(
                $"The running head does not fit in {available}. Reason: {headPlan.DeferReason}" + Trace(section.RunningHeadSlot, available, layout));

        Extent remaining = new Extent(available.Width, available.Height - headPlan.Size.Height);

        if (remaining.IsNegative)
            throw new OversetException(FormattableString.Invariant($"The running head ({headPlan.Size.Height:F1} points) is taller than the page."));

        // A running head that swallows the whole page nearly always holds content that expands to fill whatever
        // it is offered — vertical placement or Expand — in a band with no height of its own to work with.
        if (remaining.Height <= Extent.Epsilon)
            throw new OversetException(
                FormattableString.Invariant($"The running head took all {available.Height:F1} points available, leaving no room for the body or the running foot. ") +
                "This usually means it holds content that expands to fill the space offered to it, such as " +
                "Expand. Give the running head an explicit Height, or remove the expanding content.");

        Fit footPlan = section.RunningFootSlot.Plan(remaining, layout);

        if (footPlan.IsDeferred)
            throw new OversetException(
                $"The running foot does not fit in {remaining}. Reason: {footPlan.DeferReason}" + Trace(section.RunningFootSlot, remaining, layout));

        return new Bands(headPlan.Size.Height, footPlan.Size.Height, Math.Max(headPlan.Size.Width, footPlan.Size.Width));
    }

    /// <summary>
    /// Measures <paramref name="slot"/> again, recording every measurement, and describes the path down to the frame
    /// that could not fit. Measuring changes nothing, so doing it again only to explain is safe.
    /// </summary>
    private static string Trace(Block slot, Extent space, PlanContext layout)
    {
        PlanTrace trace = new PlanTrace();
        layout.Trace = trace;

        try
        {
            slot.Plan(space, layout);
        }
        finally
        {
            layout.Trace = null;
        }

        return trace.Describe();
    }

    private static float Clamp(float value, float smallest, float largest) => Math.Min(largest, Math.Max(smallest, value));

    /// <summary>The height of the running head and foot, and the width the wider of them takes.</summary>
    private readonly record struct Bands(float HeadHeight, float FootHeight, float Width);
}
