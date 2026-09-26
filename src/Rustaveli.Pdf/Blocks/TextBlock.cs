using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

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
    }

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
                  + "fill the space offered to it, such as Middle, FlushBottom or Expand, claims the whole page "
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

        for (int index = 0; index < line.Runs.Count; index++)
        {
            // Whitespace before the first word or after the last is kept as typed; only the gaps between stretch.
            TextRun run = line.Runs[index];

            if (stretch > 0 && index > firstWord && index < lastWord && IsWordGap(run))
                run = run with { Width = run.Width + (stretch * run.Text.Length) };

            if (run.Inline is not null)
            {
                Extent inlineSize = new Extent(run.Width, run.Height);
                Offset inlineTop = new Offset(x, baseline - run.Height);

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

            surface.DrawText(run.Text, new Offset(x, baseline + style.BaselineOffset), style);

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

        List<TextLine> lines = new List<TextLine>();
        TextLine current = new TextLine();
        TypeStyle blockStyle = DefaultTypeRefinement?.Invoke(context.DefaultType) ?? context.DefaultType;
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

                current.Add(new TextRun(
                    string.Empty,
                    span.ResolveStyle(blockStyle),
                    inlinePlan.Size.Width,
                    span.Url,
                    span.Anchor,
                    span.Inline,
                    inlinePlan.Size.Height));

                continue;
            }

            string text = span.Resolve(context.Pagination);

            if (text.Length == 0)
                continue;

            TypeStyle style = span.ResolveStyle(blockStyle);

            foreach (string segment in Tokenise(text))
            {
                if (PastLimit())
                    break;

                if (segment == "\n")
                {
                    FlushLine(force: true);
                    continue;
                }

                bool isWhitespace = IsBreakableWhitespace(segment[0]);
                float segmentWidth = context.Measurer.MeasureWidth(segment, style);
                float lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (current.Width + segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Anchor));
                    continue;
                }

                // Trailing whitespace never justifies a new line — drop it and break here instead.
                if (isWhitespace)
                {
                    FlushLine(force: false);
                    continue;
                }

                if (current.Runs.Count > 0)
                    FlushLine(force: false);

                // The flush replaced the line, and a continuation is not indented — so the budget has to be
                // recomputed. Reusing the opening line's narrower budget would shatter words that do fit.
                lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Anchor));
                    continue;
                }

                BreakOversizedWord(segment, span, style, width, indent, context, current, lines);
            }
        }

        FlushLine(force: false);

        // A paragraph consisting solely of blank spans still occupies one line.
        if (lines.Count == 0 && Runs.Count > 0)
        {
            current.Finalise(context.Measurer, blockStyle);
            lines.Add(current);
        }

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
    /// Splits a word that cannot fit on any line into character-level chunks, emitting full lines as it goes and
    /// leaving the remainder on <paramref name="current"/>.
    /// </summary>
    private static void BreakOversizedWord(
        string word,
        Text.TextRun span,
        TypeStyle style,
        float width,
        float indent,
        PlanContext context,
        TextLine current,
        List<TextLine> lines)
    {
        string remaining = word;

        while (remaining.Length > 0)
        {
            // Recomputed per chunk: the first may be an indented paragraph opening, every one after it is a
            // continuation entitled to the full width.
            float maxWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

            int fitting = context.Measurer.MeasureCharactersFitting(remaining, style, maxWidth);

            // Always consume at least one character, otherwise an impossibly narrow box would loop forever.
            fitting = Math.Clamp(fitting, 1, remaining.Length);

            string chunk = remaining[..fitting];
            float chunkWidth = context.Measurer.MeasureWidth(chunk, style);

            current.Add(new TextRun(chunk, style, chunkWidth, span.Url, span.Anchor));
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
        run.Inline is null && run.Text.Length > 0 && run.Text.All(IsBreakableWhitespace);

    /// <summary>Splits text into newlines, breakable whitespace runs and word runs, preserving all characters.</summary>
    private static IEnumerable<string> Tokenise(string text)
    {
        int index = 0;

        while (index < text.Length)
        {
            char character = text[index];

            if (character == '\n')
            {
                yield return "\n";
                index++;
                continue;
            }

            if (character == '\r')
            {
                // A lone carriage return is still a line break; only the pair counts as one.
                index++;

                if (index < text.Length && text[index] == '\n')
                    index++;

                yield return "\n";
                continue;
            }

            int start = index;
            bool isWhitespace = IsBreakableWhitespace(character);

            while (index < text.Length
                   && text[index] != '\n'
                   && text[index] != '\r'
                   && IsBreakableWhitespace(text[index]) == isWhitespace)
            {
                index++;
            }

            yield return text[start..index];
        }
    }

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
        float Height = 0f);

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
        }

        public List<TextRun> Runs { get; } = [];

        /// <summary>True when this line opens a paragraph, and so takes the first-line indent and spacing.</summary>
        public bool StartsParagraph { get; set; }

        public float Width { get; private set; }

        public float Ascent { get; private set; }

        public float Descent { get; private set; }

        public float Height { get; private set; }

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
                Ascent = fallback.Ascent;
                Descent = fallback.Descent;
                Height = fallback.LineSpacing;
                return;
            }

            foreach (TextRun run in Runs)
            {
                if (run.Inline is not null)
                {
                    // An inline element rests on the baseline, so its whole height sits above it.
                    Ascent = Math.Max(Ascent, run.Height);
                    Height = Math.Max(Height, run.Height);
                    continue;
                }

                TypeMetrics metrics = measurer.GetMetrics(run.Style);
                float offset = run.Style.BaselineOffset;

                // A superscript has a negative offset and so extends the line upwards; a subscript downwards.
                Ascent = Math.Max(Ascent, metrics.Ascent - Math.Min(0, offset));
                Descent = Math.Max(Descent, metrics.Descent + Math.Max(0, offset));
                Height = Math.Max(Height, metrics.LineSpacing * run.Style.Leading);
            }

            Height = Math.Max(Height, Ascent + Descent);
        }
    }
}
