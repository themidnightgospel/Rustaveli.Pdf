using System.Text;
using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Text;
using Rustaveli.Pdf.Text.Bidi;
using Rustaveli.Pdf.Text.LineBreaking;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A paragraph of styled text that wraps to the available width and flows across pages.
/// </summary>
/// <remarks>
/// Lines are rebuilt from scratch on every measurement because the offered width varies as the surrounding
/// layout resolves, and because dynamic spans can resolve differently per page. Only the count of lines already
/// committed to earlier pages is retained between passes.
/// </remarks>
internal sealed class TextBlock : Block
{
    private int _completedLines;
    private float _pinnedWidth = float.NaN;
    private List<TextLine>? _pinnedWrapping;

    /// <summary>The paragraph the text is, in a tagged document, where nothing else tags it.</summary>
    private StructureElement? _paragraph;

    // Not reset between passes: the lines of the same text at the same width do not change.
    private BuiltLines? _built;

    public List<Text.TextRun> Runs { get; } = [];

    /// <summary>
    /// How lines are aligned. The default follows the inherited content direction, so right-to-left text aligns
    /// right without being told to.
    /// </summary>
    public LineAlignment Alignment { get; set; }

    /// <summary>Adjusts the inherited style for every span in this block. Individual spans refine it further.</summary>
    public Func<TypeStyle, TypeStyle>? DefaultTypeRefinement { get; set; }

    /// <summary>Horizontal indent applied to the opening line of each paragraph.</summary>
    public float FirstLineIndent { get; set; }

    /// <summary>Vertical gap inserted before every paragraph after the first.</summary>
    public float SpaceBetweenParagraphs { get; set; }

    /// <summary>The most lines the block shows; text beyond them is cut, and the last line ends with <see cref="Ellipsis"/>.</summary>
    public int? MaxLines { get; set; }

    /// <summary>What ends the last line when <see cref="MaxLines"/> cuts text short.</summary>
    public string Ellipsis { get; set; } = "…";

    // Inline elements are children of this paragraph, so the engine can reset their state between passes.
    public override IEnumerable<Block?> GetChildren() => Runs.Select(span => span.Inline);

    /// <summary>
    /// The edge a line that is not stretched sits against, resolving start and end by the content direction. A
    /// justified paragraph's last line sits against the start.
    /// </summary>
    private HorizontalPlacement ResolveAlignment(PlanContext context)
    {
        bool rightToLeft = context.ReadingDirection == ReadingDirection.RightToLeft;

        return Alignment switch
        {
            LineAlignment.Left => HorizontalPlacement.Left,
            LineAlignment.Center => HorizontalPlacement.Center,
            LineAlignment.Right => HorizontalPlacement.Right,
            LineAlignment.End => rightToLeft ? HorizontalPlacement.Left : HorizontalPlacement.Right,
            _ => rightToLeft ? HorizontalPlacement.Right : HorizontalPlacement.Left,
        };
    }

    /// <summary>The edge lines start from in the content direction, where the first-line indent goes.</summary>
    private static HorizontalPlacement StartEdge(PlanContext context) =>
        context.ReadingDirection == ReadingDirection.RightToLeft ? HorizontalPlacement.Right : HorizontalPlacement.Left;

    /// <summary>
    /// The indent that will actually be drawn on a paragraph's opening line.
    /// </summary>
    /// <remarks>
    /// Only text aligned to the edge lines start from is indented, so for any other alignment this is zero — and
    /// it must be zero everywhere, not just at drawing time. Charging the wrap budget for an indent that is never
    /// drawn silently costs a line's worth of room and shows nothing for it.
    /// </remarks>
    private float EffectiveIndent(PlanContext context) =>
        ResolveAlignment(context) == StartEdge(context) ? Math.Max(0, FirstLineIndent) : 0f;

    protected override void ResetOwnState()
    {
        _completedLines = 0;
        _pinnedWidth = float.NaN;
        _pinnedWrapping = null;
        _paragraph = null;
    }

    // The pinned wrapping is replaced whole, never changed, so it is kept rather than copied. The paragraph element is
    // who the text is, not how far it has got, so it is not progress.
    protected override object? SaveOwnProgress() => (_completedLines, _pinnedWidth, _pinnedWrapping);

    protected override void RestoreOwnProgress(object progress) =>
        (_completedLines, _pinnedWidth, _pinnedWrapping) = ((int, float, List<TextLine>?))progress;

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        // Without usable width there is no wrapping that could succeed. Reporting a wrap sends the paragraph to
        // a fresh page, where the engine will either find room or raise a layout error naming the cause —
        // either is better than silently emitting one character per line forever.
        if (Runs.Count > 0 && float.IsNaN(_pinnedWidth)
            && availableSpace.Width - EffectiveIndent(context) <= Extent.Epsilon)
        {
            return Fit.Defer("There is no width available for text once the first-line indent is applied.");
        }

        List<TextLine> lines = BuildLines(availableSpace.Width, availableSpace.Height, context, out string? blocker);

        if (blocker is not null)
            return Fit.Defer(blocker);

        if (_completedLines >= lines.Count)
            return Fit.Nothing();

        (float height, float width, int count) = MeasureLines(lines, availableSpace.Height, EffectiveIndent(context));

        if (count == 0)
        {
            // Content that expands to fill whatever it is offered claims the paragraph's entire height, and the
            // line's own descender then pushes it past the page. Blaming the text height sends the reader looking
            // at point sizes, so name the real cause.
            return Fit.Defer(lines[_completedLines].Runs.Any(run => run.Inline is not null)
                ? "A line holding an inline frame is taller than the space available. Content that expands to "
                  + "fill the space offered to it, such as Expand, claims the whole page "
                  + "when set inline — give it an explicit Height instead."
                : "The available height is not sufficient for even a single line of text.");
        }

        Extent size = new Extent(width, height);

        return _completedLines + count >= lines.Count
            ? Fit.Complete(size)
            : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        // A blocker means Measure reported a wrap, so this paragraph should not have been asked to draw here.
        List<TextLine> lines = BuildLines(availableSpace.Width, availableSpace.Height, context.Planning, out string? blocker);

        if (blocker is not null || _completedLines >= lines.Count)
            return;

        float indent = EffectiveIndent(context.Planning);
        (float _, float _, int count) = MeasureLines(lines, availableSpace.Height, indent);

        if (count == 0)
            return;

        // Text set straight inside a section, a cell's row or the document itself is a paragraph of its own.
        TagStack tags = context.Tags;

        if (tags.Enabled && _paragraph is null && tags.Current.IsGrouping)
            _paragraph = tags.Create(ContentTag.Paragraph);

        using TagStack.Scope scope = tags.Enter(_paragraph);
        float top = 0f;

        for (int index = _completedLines; index < _completedLines + count; index++)
        {
            top += SpacingBefore(lines[index], index);

            bool endsParagraph = index == lines.Count - 1 || lines[index + 1].StartsParagraph;
            DrawLine(lines[index], availableSpace.Width, top, endsParagraph, context);
            top += lines[index].Height;
        }

        // Pin the wrapping itself, so continuation pages reproduce these exact lines rather than re-deriving
        // them from inline elements whose state this very draw has just advanced.
        if (float.IsNaN(_pinnedWidth))
            _pinnedWidth = availableSpace.Width;

        _pinnedWrapping ??= lines;

        _completedLines += count;
    }

    /// <summary>Accumulates whole lines until the next one would overflow.</summary>
    private (float Height, float Width, int Count) MeasureLines(List<TextLine> lines, float availableHeight, float indent)
    {
        float height = 0f;
        float width = 0f;
        int count = 0;

        for (int index = _completedLines; index < lines.Count; index++)
        {
            float spacing = SpacingBefore(lines[index], index);

            if (height + spacing + lines[index].Height > availableHeight + Extent.Epsilon)
                break;

            height += spacing + lines[index].Height;

            // The parent sizes its box from this width, so it has to cover the indent the line will be drawn at.
            width = Math.Max(width, lines[index].Width + (lines[index].StartsParagraph ? indent : 0f));
            count++;
        }

        return (height, width, count);
    }

    /// <summary>
    /// The gap that precedes a line. A paragraph opening at the very top of a page takes none, and a blank line
    /// is not a paragraph worth spacing.
    /// </summary>
    private float SpacingBefore(TextLine line, int index) =>
        line.StartsParagraph && line.Runs.Count > 0 && index > _completedLines ? SpaceBetweenParagraphs : 0f;

    private void DrawLine(TextLine line, float availableWidth, float top, bool endsParagraph, RenderContext context)
    {
        ISurface surface = context.Surface;
        float baseline = top + line.Ascent;
        float indent = line.StartsParagraph ? EffectiveIndent(context.Planning) : 0f;
        (int firstWord, int lastWord, int spaces) = line.WordGaps();
        bool stretched = Alignment == LineAlignment.Justified && !endsParagraph && spaces > 0;

        // A justified line fills the width from the start edge, the indent taking its place there, by sharing the
        // slack among the spaces between its words — as word spacing does, each space gets the same.
        float stretch = stretched ? Math.Max(0, availableWidth - indent - line.Width) / spaces : 0f;
        HorizontalPlacement alignment = stretched ? StartEdge(context.Planning) : ResolveAlignment(context.Planning);
        float width = line.Width + (stretch * spaces);

        float offset = alignment switch
        {
            HorizontalPlacement.Center => (availableWidth - width) / 2,
            HorizontalPlacement.Right => availableWidth - width - indent,
            _ => indent
        };

        float x = Math.Max(0, offset);
        List<TextRun> runs = new List<TextRun>(line.Runs.Count);

        for (int index = 0; index < line.Runs.Count; index++)
        {
            // Whitespace before the first word or after the last is kept as typed; only the gaps between stretch.
            TextRun run = line.Runs[index];

            if (stretch > 0 && index > firstWord && index < lastWord && IsWordGap(run))
                run = run with { Width = run.Width + (stretch * run.Text.Length) };

            runs.Add(run);
        }

        if (line.Bidi is not null)
            runs = InDisplayOrder(runs, line.Bidi, context.Measurer);

        if (stretch <= 0)
            runs = Coalesced(runs);

        foreach (TextRun run in runs)
        {
            // Linked words are a link in the structure, which the link itself belongs to.
            using TagStack.Scope link = run.Url is null && run.Destination is null
                ? default
                : context.Tags.Enter(context.Tags.Create("Link"));

            if (run.Inline is not null)
            {
                Extent inlineSize = new Extent(run.Width, run.Height);
                Offset inlineTop = new Offset(x, baseline + line.InlineTop(run));

                surface.Translate(inlineTop);
                run.Inline.Render(inlineSize, context);

                if (run.Url is not null)
                    surface.DrawExternalLink(run.Url, inlineSize);

                if (run.Destination is not null)
                    surface.DrawInternalLink(run.Destination, inlineSize);

                surface.Translate(inlineTop.Reverse());

                x += run.Width;
                continue;
            }

            TypeStyle style = run.Style;
            TypeMetrics metrics = context.Measurer.GetMetrics(style);
            float runTop = baseline - metrics.Ascent + style.BaselineOffset;
            Extent runSize = new Extent(run.Width, metrics.Ascent + metrics.Descent);

            if (!style.Highlight.IsTransparent)
                surface.DrawRectangle(new Offset(x, runTop), runSize, style.Highlight);

            surface.DrawText(run.Text, new Offset(x, baseline + style.BaselineOffset), style, run.RightToLeft);

            if (style.HasUnderline || style.HasStrikeThrough || style.HasOverline)
                DrawStrokes(surface, style, metrics, x, run.Width, baseline + style.BaselineOffset);

            if (run.Url is not null)
            {
                surface.Translate(new Offset(x, runTop));
                surface.DrawExternalLink(run.Url, runSize);
                surface.Translate(new Offset(x, runTop).Reverse());
            }

            if (run.Destination is not null)
            {
                surface.Translate(new Offset(x, runTop));
                surface.DrawInternalLink(run.Destination, runSize);
                surface.Translate(new Offset(x, runTop).Reverse());
            }

            x += run.Width;
        }
    }

    /// <summary>
    /// Puts a line's runs in the order they are displayed from left to right (UAX #9, rules L1 to L4): split where the
    /// direction changes, runs of right-to-left text reversed, and each piece of it set last character first with
    /// mirrored brackets. Text from outside the paragraph — an ellipsis — ends the line, which is its left end when
    /// the paragraph reads right to left.
    /// </summary>
    private static List<TextRun> InDisplayOrder(List<TextRun> runs, BidiParagraph bidi, ITypeMeasurer measurer)
    {
        int start = int.MaxValue;
        int end = 0;
        List<TextRun> outside = [];

        foreach (TextRun run in runs)
        {
            if (run.Offset < 0)
            {
                outside.Add(run);
                continue;
            }

            start = Math.Min(start, run.Offset);
            end = Math.Max(end, run.Offset + LogicalLength(run));
        }

        List<TextRun> ordered = new List<TextRun>(runs.Count + 2);

        if (start < end)
        {
            List<BidiRun> levels = [];
            bidi.GetVisualRuns(start, end - start, levels);

            foreach (BidiRun level in levels)
            {
                int first = ordered.Count;

                foreach (TextRun run in runs)
                {
                    int from = Math.Max(run.Offset, level.Start);
                    int to = Math.Min(run.Offset + LogicalLength(run), level.Start + level.Length);

                    if (run.Offset >= 0 && from < to)
                        ordered.Add(Piece(run, from - run.Offset, to - from, level.IsRightToLeft, measurer));
                }

                if (level.IsRightToLeft)
                    ordered.Reverse(first, ordered.Count - first);
            }
        }

        if (bidi.ParagraphLevel == 1)
            ordered.InsertRange(0, outside);
        else
            ordered.AddRange(outside);

        return ordered;
    }

    /// <summary>
    /// Joins neighbouring runs set in the same type, carrying the same link, into one, so a line of words is drawn as
    /// one piece of text rather than a word and a space at a time.
    /// </summary>
    /// <remarks>
    /// Drawing walks the joined text glyph by glyph exactly as measuring walked each piece, so the words land where
    /// the line was measured — with two exceptions, which are kept apart: tracking, which measuring each piece on its
    /// own leaves out between pieces, and a stretched space, whose width is more than its glyph's.
    /// </remarks>
    private static List<TextRun> Coalesced(List<TextRun> runs)
    {
        List<TextRun> joined = new List<TextRun>(runs.Count);
        int start = 0;

        while (start < runs.Count)
        {
            int end = start + 1;
            int length = runs[start].Text.Length;
            float width = runs[start].Width;

            while (end < runs.Count && Joins(runs[start], runs[end]))
            {
                length += runs[end].Text.Length;
                width += runs[end].Width;
                end++;
            }

            // One allocation per piece drawn, however many words it joins.
            joined.Add(end == start + 1 ? runs[start] : runs[start] with { Text = Concatenate(runs, start, end, length), Width = width });
            start = end;
        }

        return joined;
    }

    /// <summary>
    /// The joined text, in logical order: pieces of right-to-left text arrive in display order, last read first, so
    /// they are joined from the end.
    /// </summary>
    private static string Concatenate(List<TextRun> runs, int start, int end, int length)
    {
        char[] characters = new char[length];
        int at = 0;
        bool backwards = runs[start].RightToLeft;

        for (int step = 0; step < end - start; step++)
        {
            string text = runs[backwards ? end - 1 - step : start + step].Text;
            text.CopyTo(0, characters, at, text.Length);
            at += text.Length;
        }

        return new string(characters);
    }

    private static bool Joins(TextRun previous, TextRun next) =>
        previous.Inline is null
        && next.Inline is null
        && previous.Style.Tracking == 0
        && (ReferenceEquals(previous.Style, next.Style) || previous.Style.Equals(next.Style))
        && previous.Url == next.Url
        && previous.Destination == next.Destination
        && previous.RightToLeft == next.RightToLeft;

    /// <summary>How many code units a run takes in its paragraph's text: an inline frame stands in for one.</summary>
    private static int LogicalLength(TextRun run) => run.Inline is null ? run.Text.Length : 1;

    /// <summary>
    /// The part of a run from <paramref name="start"/> for <paramref name="length"/> code units, measured afresh unless
    /// it is the whole run, and set right to left when <paramref name="rightToLeft"/>.
    /// </summary>
    private static TextRun Piece(TextRun run, int start, int length, bool rightToLeft, ITypeMeasurer measurer)
    {
        if (run.Inline is not null)
            return run;

        bool whole = start == 0 && length == run.Text.Length;
        string text = whole ? run.Text : run.Text.Substring(start, length);

        // Spaces share a stretched width evenly; anything else is measured on its own.
        float width = whole
            ? run.Width
            : IsWordGap(run) ? run.Width * length / run.Text.Length : measurer.MeasureWidth(text, run.Style);

        return run with { Text = text, Width = width, RightToLeft = rightToLeft };
    }

    /// <summary>
    /// Draws a run's underline, strike-through and overline where the font places them and as thick as it draws
    /// them, unless the style says otherwise; a font silent on either falls back to proportions of the type.
    /// </summary>
    private static void DrawStrokes(ISurface surface, TypeStyle style, TypeMetrics metrics, float x, float width, float baseline)
    {
        Ink ink = style.StrokeInk ?? style.Ink;
        Offset Start(float y) => new Offset(x, y);
        Offset End(float y) => new Offset(x + width, y);

        if (style.HasUnderline)
        {
            float offset = metrics.UnderlineOffset > 0 ? metrics.UnderlineOffset : metrics.Descent * UnderlineDepthRatio;
            float weight = Weight(style, metrics.UnderlineWeight);
            surface.DrawLine(Start(baseline + offset), End(baseline + offset), weight, ink, style.StrokeStyle);
        }

        if (style.HasStrikeThrough)
        {
            float height = metrics.StrikeHeight > 0 ? metrics.StrikeHeight : metrics.Ascent * StrikethroughHeightRatio;
            float weight = Weight(style, metrics.StrikeWeight);
            surface.DrawLine(Start(baseline - height), End(baseline - height), weight, ink, style.StrokeStyle);
        }

        if (style.HasOverline)
        {
            // Along the top of the ascent, drawn within it rather than above the line.
            float weight = Weight(style, metrics.UnderlineWeight);
            float y = baseline - metrics.Ascent + (weight / 2);
            surface.DrawLine(Start(y), End(y), weight, ink, style.StrokeStyle);
        }
    }

    /// <summary>The style's own stroke weight, else the font's, never thinner than half a point.</summary>
    private static float Weight(TypeStyle style, float fontWeight) =>
        style.StrokeWeight ?? Math.Max(0.5f, fontWeight > 0 ? fontWeight : style.EffectivePointSize / 16f);

    private const float UnderlineDepthRatio = 0.5f;
    private const float StrikethroughHeightRatio = 0.3f;

    /// <summary>
    /// Breaks the spans into lines that fit <paramref name="maxWidth"/>, splitting on whitespace and falling
    /// back to mid-word breaks for words too long to fit on a line of their own.
    /// </summary>
    private List<TextLine> BuildLines(float maxWidth, float maxHeight, PlanContext context, out string? blocker)
    {
        blocker = null;

        // Once a page has been drawn the wrapping is frozen whole, not merely at its width. Rebuilding it
        // re-measures every inline element, and an element already consumed on an earlier page reports Empty
        // the second time — dropping its run, shifting every later line up by one, and leaving _completedLines
        // pointing at content that was never drawn. That deletes a line of text with no error.
        if (_pinnedWrapping is not null)
            return _pinnedWrapping;

        TypeStyle blockStyle = DefaultTypeRefinement?.Invoke(context.DefaultType) ?? context.DefaultType;

        // Lines depend on nothing but the text, the width, the type and the direction — unless a run's text depends
        // on the page or a frame's plan on the height — so a paragraph planned and drawn again, on every attempt of
        // both passes, builds them once.
        bool reusable = Runs.TrueForAll(run => run.Inline is null && run.DynamicText is null);

        if (reusable && _built is { } built && built.Width == maxWidth && ReferenceEquals(built.Measurer, context.Measurer)
            && built.Direction == context.ReadingDirection && built.Style.Equals(blockStyle))
        {
            return built.Lines;
        }

        List<TextLine> lines = BuildLinesAfresh(maxWidth, maxHeight, blockStyle, context, out blocker);

        if (reusable && blocker is null)
            _built = new BuiltLines(maxWidth, blockStyle, context.ReadingDirection, context.Measurer, lines);

        return lines;
    }

    private List<TextLine> BuildLinesAfresh(
        float maxWidth, float maxHeight, TypeStyle blockStyle, PlanContext context, out string? blocker)
    {
        blocker = null;
        List<TextLine> lines = new List<TextLine>();
        TextLine current = new TextLine();
        float indent = EffectiveIndent(context);

        // Only reached before anything is drawn: from then on the pinned wrapping above is returned whole, which
        // also fixes the width it was built at.
        float width = Math.Max(0, maxWidth);

        // The opening line of the block starts a paragraph; thereafter only a line following an explicit break
        // does. Carrying the flag on the line itself means the mid-word breaker keeps it right for free.
        current.StartsParagraph = true;

        void FlushLine(bool force)
        {
            if (current.Runs.Count == 0 && !force)
                return;

            current.Finalise(context.Measurer, blockStyle);
            lines.Add(current);
            current = new TextLine { StartsParagraph = force };
        }

        // Each paragraph's text is gathered as its lines are built, every run recording where it falls in it, so the
        // bidirectional algorithm can resolve the paragraph whole — a character's direction can depend on text
        // lines away — and each line can then be put in display order.
        StringBuilder paragraph = new StringBuilder();
        int paragraphStart = 0;
        BidiDirection direction = context.ReadingDirection == ReadingDirection.RightToLeft
            ? BidiDirection.RightToLeft
            : BidiDirection.LeftToRight;

        void CloseParagraph()
        {
            if (lines.Count > paragraphStart && paragraph.Length > 0)
            {
                BidiParagraph bidi = new BidiParagraph(paragraph.ToString().AsSpan(), direction);

                // Text that is left to right throughout is drawn as it is stored, and needs nothing more.
                if (!bidi.IsLeftToRightOnly)
                {
                    for (int index = paragraphStart; index < lines.Count; index++)
                        lines[index].Bidi = bidi;
                }
            }

            paragraph.Clear();
            paragraphStart = lines.Count;
        }

        // Past the limit, nothing more is shown, so nothing more is measured: a long text clamped to a line or
        // two costs no more than those lines, and an inline frame beyond them is never asked to plan. The limit is
        // passed once its last line is complete and something follows it.
        int limit = MaxLines ?? int.MaxValue;
        bool PastLimit() => lines.Count > limit || (lines.Count == limit && current.Runs.Count > 0);

        foreach (Text.TextRun span in Runs)
        {
            if (PastLimit())
                break;

            if (span.Inline is not null)
            {
                // The element is unbreakable, so if it does not fit on this line it moves down whole, exactly
                // like a word — but it is measured against the budget of a line it could actually occupy, and
                // against the height the paragraph really has. Offering an unbounded height instead makes any
                // element that fills what it is given — AlignMiddle, Extend — report the full 14400pt and drag
                // the whole paragraph into a wrap it can never satisfy.
                float lineBudget = current.StartsParagraph ? Math.Max(0, width - indent) : width;
                Fit inlinePlan = span.Inline.Plan(new Extent(lineBudget, maxHeight), context);

                if (inlinePlan.IsNothing)
                    continue;

                // Wrap and PartialRender both mean content is left over, and an inline run has no way to carry
                // a remainder onto the next line. Dropping it here deletes it from the document with no error,
                // so the paragraph defers as a whole instead and the engine reports it if no page can hold it.
                if (inlinePlan.IsDeferred || inlinePlan.IsPartial)
                {
                    blocker = "A paragraph holds an inline frame that does not fit the width available to it. "
                        + "Inline frames cannot be split across lines, so it has to fit on one.";

                    return lines;
                }

                if (current.Runs.Count > 0 && current.Width + inlinePlan.Size.Width > lineBudget + Extent.Epsilon)
                    FlushLine(force: false);

                // The frame stands in the paragraph's text as an object replacement character: a neutral, so it takes
                // the direction of the text around it.
                paragraph.Append(ObjectReplacement);
                current.Add(new TextRun(
                    string.Empty,
                    span.ResolveStyle(blockStyle),
                    inlinePlan.Size.Width,
                    span.Url,
                    span.Anchor,
                    span.Inline,
                    inlinePlan.Size.Height,
                    span.InlinePosition,
                    paragraph.Length - 1));

                continue;
            }

            string text = span.Resolve(context.Pagination);

            if (text.Length == 0)
                continue;

            TypeStyle style = span.ResolveStyle(blockStyle);

            // A run with a direction of its own is isolated in the paragraph's text (UAX #9 isolates), so it is
            // resolved in that direction as one unit; the controls are never drawn, since no run covers them.
            char? isolate = style.Direction switch
            {
                ReadingDirection.LeftToRight => LeftToRightIsolate,
                ReadingDirection.RightToLeft => RightToLeftIsolate,
                _ => null,
            };

            if (isolate is char opening)
                paragraph.Append(opening);

            foreach (string segment in Tokenise(text))
            {
                if (PastLimit())
                    break;

                if (segment == "\n")
                {
                    FlushLine(force: true);
                    CloseParagraph();

                    // The run goes on into the next paragraph, isolated there too.
                    if (isolate is char reopening)
                        paragraph.Append(reopening);

                    continue;
                }

                int offset = paragraph.Length;
                paragraph.Append(segment);

                bool isWhitespace = IsBreakableWhitespace(segment[0]);
                float segmentWidth = context.Measurer.MeasureWidth(segment, style);
                float lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (current.Width + segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Anchor, Offset: offset));
                    continue;
                }

                // Trailing whitespace never justifies a new line — drop it and break here instead.
                if (isWhitespace)
                {
                    FlushLine(force: false);
                    continue;
                }

                // Type that may break anywhere fills the line it is on before going on to the next.
                if (style.BreaksAnywhere)
                {
                    BreakWord(segment, span, style, width, indent, context, offset, current, lines);
                    continue;
                }

                if (current.Runs.Count > 0)
                    FlushLine(force: false);

                // The flush replaced the line, and a continuation is not indented — so the budget has to be
                // recomputed. Reusing the opening line's narrower budget would shatter words that do fit.
                lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Anchor, Offset: offset));
                    continue;
                }

                BreakWord(segment, span, style, width, indent, context, offset, current, lines);
            }

            if (isolate is not null)
                paragraph.Append(PopDirectionalIsolate);
        }

        FlushLine(force: false);

        // A paragraph consisting solely of blank spans still occupies one line.
        if (lines.Count == 0 && Runs.Count > 0)
        {
            current.Finalise(context.Measurer, blockStyle);
            lines.Add(current);
        }

        CloseParagraph();

        if (lines.Count > limit)
        {
            lines.RemoveRange(limit, lines.Count - limit);
            TextLine last = lines[^1];
            EndWithEllipsis(last, last.StartsParagraph ? Math.Max(0, width - indent) : width, blockStyle, context);
            last.Finalise(context.Measurer, blockStyle);
        }

        return lines;
    }

    /// <summary>
    /// Cuts the last line a limit allows back until <see cref="Ellipsis"/> fits after it — whole words first, then
    /// characters of the word that no longer fits, never leaving a space before it — and sets it there.
    /// </summary>
    /// <param name="line">The last line shown.</param>
    /// <param name="budget">The width the line may take.</param>
    /// <param name="style">
    /// The paragraph's own type, as CSS sets a text-overflow ellipsis: not whichever run it lands after, whose
    /// type could be cut away with it.
    /// </param>
    /// <param name="context">Supplies the measurer.</param>
    private void EndWithEllipsis(TextLine line, float budget, TypeStyle style, PlanContext context)
    {
        float ellipsisWidth = context.Measurer.MeasureWidth(Ellipsis, style);
        float room = budget - ellipsisWidth;

        while (line.Runs.Count > 0)
        {
            TextRun last = line.Runs[^1];

            if (!IsWordGap(last) && line.Width <= room + Extent.Epsilon)
                break;

            line.RemoveLast();

            if (IsWordGap(last) || last.Inline is not null)
                continue;

            int fitting = context.Measurer.MeasureCharactersFitting(last.Text, last.Style, room - line.Width);

            if (fitting > 0)
            {
                string kept = last.Text[..fitting];
                line.Add(last with { Text = kept, Width = context.Measurer.MeasureWidth(kept, last.Style) });
                break;
            }
        }

        if (Ellipsis.Length > 0)
            line.Add(new TextRun(Ellipsis, style, ellipsisWidth, null, null));
    }

    /// <summary>
    /// Splits a word into character-level chunks, filling whatever room <paramref name="current"/> has left, emitting
    /// full lines as it goes and leaving the remainder on <paramref name="current"/>. Used for a word too long for
    /// any line, and for every word in type that may break anywhere.
    /// </summary>
    private static void BreakWord(
        string word,
        Text.TextRun span,
        TypeStyle style,
        float width,
        float indent,
        PlanContext context,
        int offset,
        TextLine current,
        List<TextLine> lines)
    {
        string remaining = word;

        while (remaining.Length > 0)
        {
            // Recomputed per chunk: the first may continue an indented paragraph opening, every one after it is a
            // fresh continuation entitled to the full width.
            float room = (current.StartsParagraph ? Math.Max(0, width - indent) : width) - current.Width;

            int fitting = context.Measurer.MeasureCharactersFitting(remaining, style, room);

            // Not a character fits beside what the line already holds, so the word goes on to the next.
            if (fitting == 0 && current.Runs.Count > 0)
            {
                current.Finalise(context.Measurer, style);
                lines.Add(new TextLine(current));
                current.Clear();
                continue;
            }

            // Always consume at least one character, otherwise an impossibly narrow box would loop forever.
            fitting = Math.Clamp(fitting, 1, remaining.Length);

            string chunk = remaining[..fitting];
            float chunkWidth = context.Measurer.MeasureWidth(chunk, style);

            current.Add(new TextRun(chunk, style, chunkWidth, span.Url, span.Anchor, Offset: offset + word.Length - remaining.Length));
            remaining = remaining[fitting..];

            if (remaining.Length == 0)
                break;

            current.Finalise(context.Measurer, style);
            lines.Add(new TextLine(current));
            current.Clear();
        }
    }

    /// <summary>
    /// Whitespace a line may be broken at. Non-breaking forms are deliberately excluded: they exist precisely to
    /// hold "10 000" or "Fig. 4" together, so treating them as break opportunities defeats their only purpose.
    /// </summary>
    private static bool IsBreakableWhitespace(char character) =>
        char.IsWhiteSpace(character) && character is not ('\u00A0' or '\u202F' or '\u2007');

    /// <summary>A run of breakable whitespace between words, as the tokeniser splits it out.</summary>
    private static bool IsWordGap(TextRun run) =>
        run.Inline is null && run.Text.Length > 0 && IsAllBreakableWhitespace(run.Text);

    // A loop rather than LINQ: every run of every line passes through here, and an enumerator each would add up.
    private static bool IsAllBreakableWhitespace(string text)
    {
        foreach (char character in text)
        {
            if (!IsBreakableWhitespace(character))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Splits text at the places a line may end, by the Unicode line breaking rules: after spaces, after hyphens,
    /// between ideographs and so on. Each piece becomes the text before it may break, then the breakable whitespace
    /// that ends it, then "\n" where the line must end — so every character is kept but the line ending itself, and
    /// whitespace stays separate from the words for trimming and justification to find.
    /// </summary>
    private static List<string> Tokenise(string text)
    {
        List<string> tokens = [];
        int start = 0;

        foreach (LineBreak opportunity in LineBreaker.Enumerate(text.AsSpan()))
        {
            int end = opportunity.Position;
            int content = end;
            bool endsLine = opportunity.IsMandatory && end > start && IsLineTerminator(text[end - 1]);

            if (endsLine)
            {
                // A carriage return and line feed together are one line ending.
                content--;

                if (text[content] == '\n' && content > start && text[content - 1] == '\r')
                    content--;
            }

            int word = content;

            while (word > start && IsBreakableWhitespace(text[word - 1]))
                word--;

            if (word > start)
                tokens.Add(text[start..word]);

            if (content > word)
                tokens.Add(text[word..content]);

            if (endsLine)
                tokens.Add("\n");

            start = end;
        }

        return tokens;
    }

    /// <summary>The characters that end a line wherever they fall: the mandatory breaks of UAX #14.</summary>
    private static bool IsLineTerminator(char character) =>
        character is '\n' or '\r' or '\u000B' or '\u000C' or '\u0085' or LineSeparator or ParagraphSeparator;

    private const char LineSeparator = (char)0x2028;

    /// <summary>What an inline frame stands as in its paragraph's text.</summary>
    private const char ObjectReplacement = (char)0xFFFC;

    private const char LeftToRightIsolate = (char)0x2066;

    private const char RightToLeftIsolate = (char)0x2067;

    private const char PopDirectionalIsolate = (char)0x2069;

    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>The lines last built, and what they were built for.</summary>
    private sealed record BuiltLines(
        float Width, TypeStyle Style, ReadingDirection Direction, ITypeMeasurer Measurer, List<TextLine> Lines);

    /// <summary>
    /// One piece of a line: either a stretch of text, or an element sitting inline among the words.
    /// </summary>
    private sealed record TextRun(
        string Text,
        TypeStyle Style,
        float Width,
        string? Url,
        string? Destination,
        Block? Inline = null,
        float Height = 0f,
        InlinePosition Position = InlinePosition.OnBaseline,
        int Offset = -1,
        bool RightToLeft = false);

    private sealed class TextLine
    {
        public TextLine()
        {
        }

        /// <summary>Copies an in-progress line, used when a mid-word break commits the current content.</summary>
        public TextLine(TextLine source)
        {
            Runs.AddRange(source.Runs);
            StartsParagraph = source.StartsParagraph;
            Width = source.Width;
            Ascent = source.Ascent;
            Descent = source.Descent;
            Height = source.Height;
            TypeAscent = source.TypeAscent;
            TypeDescent = source.TypeDescent;
        }

        public List<TextRun> Runs { get; } = [];

        /// <summary>True when this line opens a paragraph, and so takes the first-line indent and spacing.</summary>
        public bool StartsParagraph { get; set; }

        public float Width { get; private set; }

        public float Ascent { get; private set; }

        public float Descent { get; private set; }

        public float Height { get; private set; }

        /// <summary>
        /// How far the type on the line reaches above the baseline, not counting inline frames: what a frame set
        /// level with the top of the type, or centred on it, is placed against. A line of frames alone has no type
        /// and so none, which makes each frame on it a line of its own height wherever it is placed.
        /// </summary>
        public float TypeAscent { get; private set; }

        /// <summary>How far the type on the line reaches below the baseline, not counting inline frames.</summary>
        public float TypeDescent { get; private set; }

        /// <summary>
        /// The resolved paragraph the line belongs to, which puts its runs in display order; null for a paragraph that
        /// is left to right throughout, whose runs are displayed as they are stored.
        /// </summary>
        public BidiParagraph? Bidi { get; set; }

        public void Add(TextRun run)
        {
            Runs.Add(run);
            Width += run.Width;
        }

        public void RemoveLast()
        {
            Width -= Runs[^1].Width;
            Runs.RemoveAt(Runs.Count - 1);
        }

        /// <summary>
        /// Whether a trailing run may be dropped: breakable whitespace only, and carrying no annotation.
        /// </summary>
        private static bool IsTrimmable(TextRun run) =>
            run.Url is null
            && run.Destination is null
            && IsWordGap(run);

        /// <summary>
        /// Where the line's words begin and end, as run indexes, and how many spaces lie between them: the spaces
        /// justification widens. With no word on the line both indexes are -1.
        /// </summary>
        public (int First, int Last, int Spaces) WordGaps()
        {
            int first = Runs.FindIndex(run => !IsWordGap(run));
            int last = Runs.FindLastIndex(run => !IsWordGap(run));
            int spaces = 0;

            for (int index = first + 1; index < last; index++)
            {
                if (IsWordGap(Runs[index]))
                    spaces += Runs[index].Text.Length;
            }

            return (first, last, spaces);
        }

        public void Clear()
        {
            Runs.Clear();

            // Anything left after a mid-word break continues the paragraph rather than opening one.
            StartsParagraph = false;
            Width = 0;
            Ascent = 0;
            Descent = 0;
            Height = 0;
        }

        /// <summary>Computes vertical metrics once the line's runs are known.</summary>
        /// <param name="measurer">Supplies the font metrics the line's height and baseline are derived from.</param>
        /// <param name="fallbackStyle">
        /// Used to size a line with no runs. A blank line separating two headings should be as tall as the
        /// surrounding text, not as tall as the library-wide default.
        /// </param>
        public void Finalise(ITypeMeasurer measurer, TypeStyle fallbackStyle)
        {
            // A space that happens to land at the end of a line is not part of the line's ink. Counting it
            // would shift centred text left, leave right-aligned text short of the margin, and overstate the
            // width that Auto columns and table cells are sized from.
            //
            // The predicate has to agree with the tokeniser: a non-breaking space is deliberately treated as
            // ink, so trimming it would delete the very content it exists to hold together. A run carrying a
            // link is never trimmed either, since dropping it would silently remove the annotation with it.
            while (Runs.Count > 0 && IsTrimmable(Runs[^1]))
                RemoveLast();

            Width = Math.Max(0, Width);

            // A line cut short by a line limit is finalised again, and must not keep the height of what was cut.
            Ascent = 0;
            Descent = 0;
            Height = 0;

            if (Runs.Count == 0)
            {
                TypeMetrics fallback = measurer.GetMetrics(fallbackStyle);
                Ascent = TypeAscent = fallback.Ascent;
                Descent = TypeDescent = fallback.Descent;
                Height = fallback.LineSpacing;
                return;
            }

            foreach (TextRun run in Runs)
            {
                if (run.Inline is not null)
                    continue;

                TypeMetrics metrics = measurer.GetMetrics(run.Style);
                float offset = run.Style.BaselineOffset;

                // A superscript has a negative offset and so extends the line upwards; a subscript downwards.
                Ascent = Math.Max(Ascent, metrics.Ascent - Math.Min(0, offset));
                Descent = Math.Max(Descent, metrics.Descent + Math.Max(0, offset));
                Height = Math.Max(Height, metrics.LineSpacing * run.Style.Leading);
            }

            TypeAscent = Ascent;
            TypeDescent = Descent;

            // Frames go in once the type is known, since all but those on the baseline are placed against it.
            foreach (TextRun run in Runs)
            {
                if (run.Inline is null)
                    continue;

                (float above, float below) = Reach(run);
                Ascent = Math.Max(Ascent, above);
                Descent = Math.Max(Descent, below);
                Height = Math.Max(Height, run.Height);
            }

            Height = Math.Max(Height, Ascent + Descent);
        }

        /// <summary>Where an inline frame's top sits, relative to the baseline; negative is above it.</summary>
        public float InlineTop(TextRun run) => run.Position switch
        {
            InlinePosition.BelowBaseline => 0f,
            InlinePosition.TextTop => -TypeAscent,
            InlinePosition.TextBottom => TypeDescent - run.Height,
            InlinePosition.Middle => (-(TypeAscent - TypeDescent) / 2) - (run.Height / 2),
            _ => -run.Height,
        };

        /// <summary>How far an inline frame reaches above the baseline and below it; either may be negative.</summary>
        private (float Above, float Below) Reach(TextRun run)
        {
            float top = InlineTop(run);
            return (-top, top + run.Height);
        }
    }
}
