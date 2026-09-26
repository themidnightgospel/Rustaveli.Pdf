using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A paragraph of styled text that wraps to the available width and flows across pages.
/// </summary>
/// <remarks>
/// Lines are rebuilt from scratch on every measurement because the offered width varies as the surrounding
/// layout resolves, and because dynamic spans can resolve differently per page. Only the count of lines already
/// committed to earlier pages is retained between passes.
/// </remarks>
public sealed class TextBlock : Block
{
    private int _completedLines;
    private float _pinnedWidth = float.NaN;
    private List<TextLine>? _pinnedWrapping;

    public List<Text.TextRun> Spans { get; } = [];

    /// <summary>
    /// Overrides how lines are aligned. Null follows the inherited content direction, so right-to-left text
    /// aligns right without being told to.
    /// </summary>
    public HorizontalPlacement? Alignment { get; set; }

    /// <summary>Adjusts the inherited style for every span in this block. Individual spans refine it further.</summary>
    public Func<TypeStyle, TypeStyle>? DefaultStyleOverride { get; set; }

    /// <summary>Horizontal indent applied to the opening line of each paragraph.</summary>
    public float FirstLineIndent { get; set; }

    /// <summary>Vertical gap inserted before every paragraph after the first.</summary>
    public float ParagraphSpacing { get; set; }

    // Inline elements are children of this paragraph, so the engine can reset their state between passes.
    public override IEnumerable<Block?> GetChildren() => Spans.Select(span => span.InlineElement);

    /// <summary>
    /// Resolves how lines are aligned, falling back to the inherited content direction.
    /// </summary>
    private HorizontalPlacement ResolveAlignment(PlanContext context) =>
        Alignment ?? (context.ContentDirection == ReadingDirection.RightToLeft
            ? HorizontalPlacement.Right
            : HorizontalPlacement.Left);

    /// <summary>
    /// The indent that will actually be drawn on a paragraph's opening line.
    /// </summary>
    /// <remarks>
    /// Only left-aligned text is indented, so for any other alignment this is zero — and it must be zero
    /// everywhere, not just at drawing time. Charging the wrap budget for an indent that is never drawn silently
    /// costs a line's worth of room and shows nothing for it.
    /// </remarks>
    private float EffectiveIndent(PlanContext context) =>
        ResolveAlignment(context) == HorizontalPlacement.Left ? Math.Max(0, FirstLineIndent) : 0f;

    protected override void ResetOwnState()
    {
        _completedLines = 0;
        _pinnedWidth = float.NaN;
        _pinnedWrapping = null;
    }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        // Without usable width there is no wrapping that could succeed. Reporting a wrap sends the paragraph to
        // a fresh page, where the engine will either find room or raise a layout error naming the cause —
        // either is better than silently emitting one character per line forever.
        if (Spans.Count > 0 && float.IsNaN(_pinnedWidth)
            && availableSpace.Width - EffectiveIndent(context) <= Extent.Epsilon)
        {
            return Fit.Wrap("There is no width available for text once the first-line indent is applied.");
        }

        List<TextLine> lines = BuildLines(availableSpace.Width, availableSpace.Height, context, out string? blocker);

        if (blocker is not null)
            return Fit.Wrap(blocker);

        if (_completedLines >= lines.Count)
            return Fit.Empty();

        (float height, float width, int count) = MeasureLines(lines, availableSpace.Height, EffectiveIndent(context));

        if (count == 0)
        {
            // An element that expands to fill whatever it is offered claims the paragraph's entire height, and
            // the line's own descender then pushes it past the page. Blaming the text height sends the reader
            // looking at font sizes, so name the real cause.
            return Fit.Wrap(lines[_completedLines].Runs.Any(run => run.Inline is not null)
                ? "A line holding an inline element is taller than the space available. An element that expands "
                  + "to fill the space offered to it, such as AlignMiddle, AlignBottom or Extend, claims the "
                  + "whole page when placed inline — give it an explicit height instead."
                : "The available height is not sufficient for even a single line of text.");
        }

        Extent size = new Extent(width, height);

        return _completedLines + count >= lines.Count
            ? Fit.FullRender(size)
            : Fit.PartialRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        // A blocker means Measure reported a wrap, so this paragraph should not have been asked to draw here.
        List<TextLine> lines = BuildLines(availableSpace.Width, availableSpace.Height, context.Layout, out string? blocker);

        if (blocker is not null || _completedLines >= lines.Count)
            return;

        float indent = EffectiveIndent(context.Layout);
        (float _, float _, int count) = MeasureLines(lines, availableSpace.Height, indent);

        if (count == 0)
            return;

        float top = 0f;

        for (int index = _completedLines; index < _completedLines + count; index++)
        {
            top += SpacingBefore(lines[index], index);

            DrawLine(lines[index], availableSpace.Width, top, context);
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
        line.StartsParagraph && line.Runs.Count > 0 && index > _completedLines ? ParagraphSpacing : 0f;

    private void DrawLine(TextLine line, float availableWidth, float top, RenderContext context)
    {
        ISurface canvas = context.Canvas;
        float baseline = top + line.Ascent;

        HorizontalPlacement alignment = ResolveAlignment(context.Layout);

        float offset = alignment switch
        {
            HorizontalPlacement.Center => (availableWidth - line.Width) / 2,
            HorizontalPlacement.Right => availableWidth - line.Width,
            _ => 0f
        };

        if (line.StartsParagraph)
            offset += EffectiveIndent(context.Layout);

        float x = Math.Max(0, offset);

        foreach (TextRun run in line.Runs)
        {
            if (run.Inline is not null)
            {
                Extent inlineSize = new Extent(run.Width, run.Height);
                Offset inlineTop = new Offset(x, baseline - run.Height);

                canvas.Translate(inlineTop);
                run.Inline.Draw(inlineSize, context);

                if (run.Url is not null)
                    canvas.DrawExternalLink(run.Url, inlineSize);

                if (run.Destination is not null)
                    canvas.DrawInternalLink(run.Destination, inlineSize);

                canvas.Translate(inlineTop.Reverse());

                x += run.Width;
                continue;
            }

            TypeStyle style = run.Style;
            TypeMetrics metrics = context.TextMeasurer.GetMetrics(style);
            float runTop = baseline - metrics.Ascent + style.BaselineOffset;
            Extent runSize = new Extent(run.Width, metrics.Ascent + metrics.Descent);

            if (!style.BackgroundColor.IsTransparent)
                canvas.DrawRectangle(new Offset(x, runTop), runSize, style.BackgroundColor);

            canvas.DrawText(run.Text, new Offset(x, baseline + style.BaselineOffset), style);

            if (style.HasUnderline)
            {
                float y = baseline + style.BaselineOffset + metrics.Descent * UnderlineDepthRatio;
                canvas.DrawLine(new Offset(x, y), new Offset(x + run.Width, y), DecorationThickness(style), style.Color);
            }

            if (style.HasStrikethrough)
            {
                float y = baseline + style.BaselineOffset - metrics.Ascent * StrikethroughHeightRatio;
                canvas.DrawLine(new Offset(x, y), new Offset(x + run.Width, y), DecorationThickness(style), style.Color);
            }

            if (run.Url is not null)
            {
                canvas.Translate(new Offset(x, runTop));
                canvas.DrawExternalLink(run.Url, runSize);
                canvas.Translate(new Offset(x, runTop).Reverse());
            }

            if (run.Destination is not null)
            {
                canvas.Translate(new Offset(x, runTop));
                canvas.DrawInternalLink(run.Destination, runSize);
                canvas.Translate(new Offset(x, runTop).Reverse());
            }

            x += run.Width;
        }
    }

    private static float DecorationThickness(TypeStyle style) => Math.Max(0.5f, style.EffectiveFontSize / 16f);

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
        TypeStyle blockStyle = DefaultStyleOverride?.Invoke(context.DefaultTextStyle) ?? context.DefaultTextStyle;
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

            current.Finalise(context.TextMeasurer, blockStyle);
            lines.Add(current);
            current = new TextLine { StartsParagraph = force };
        }

        foreach (Text.TextRun span in Spans)
        {
            if (span.InlineElement is not null)
            {
                // The element is unbreakable, so if it does not fit on this line it moves down whole, exactly
                // like a word — but it is measured against the budget of a line it could actually occupy, and
                // against the height the paragraph really has. Offering an unbounded height instead makes any
                // element that fills what it is given — AlignMiddle, Extend — report the full 14400pt and drag
                // the whole paragraph into a wrap it can never satisfy.
                float lineBudget = current.StartsParagraph ? Math.Max(0, width - indent) : width;
                Fit inlinePlan = span.InlineElement.Measure(new Extent(lineBudget, maxHeight), context);

                if (inlinePlan.IsEmpty)
                    continue;

                // Wrap and PartialRender both mean content is left over, and an inline run has no way to carry
                // a remainder onto the next line. Dropping it here deletes it from the document with no error,
                // so the paragraph defers as a whole instead and the engine reports it if no page can hold it.
                if (inlinePlan.IsWrap || inlinePlan.IsPartialRender)
                {
                    blocker = "A paragraph contains an inline element that does not fit the width available to "
                        + "it. Inline elements cannot be split across lines, so it has to fit on one.";

                    return lines;
                }

                if (current.Runs.Count > 0 && current.Width + inlinePlan.Size.Width > lineBudget + Extent.Epsilon)
                    FlushLine(force: false);

                current.Add(new TextRun(
                    string.Empty,
                    span.ResolveStyle(blockStyle),
                    inlinePlan.Size.Width,
                    span.Url,
                    span.Destination,
                    span.InlineElement,
                    inlinePlan.Size.Height));

                continue;
            }

            string text = span.Resolve(context.Page);

            if (text.Length == 0)
                continue;

            TypeStyle style = span.ResolveStyle(blockStyle);

            foreach (string segment in Tokenise(text))
            {
                if (segment == "\n")
                {
                    FlushLine(force: true);
                    continue;
                }

                bool isWhitespace = IsBreakableWhitespace(segment[0]);
                float segmentWidth = context.TextMeasurer.MeasureWidth(segment, style);
                float lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (current.Width + segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Destination));
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
                    current.Add(new TextRun(segment, style, segmentWidth, span.Url, span.Destination));
                    continue;
                }

                BreakOversizedWord(segment, span, style, width, indent, context, current, lines);
            }
        }

        FlushLine(force: false);

        // A paragraph consisting solely of blank spans still occupies one line.
        if (lines.Count == 0 && Spans.Count > 0)
        {
            current.Finalise(context.TextMeasurer, blockStyle);
            lines.Add(current);
        }

        return lines;
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

            int fitting = context.TextMeasurer.MeasureCharactersFitting(remaining, style, maxWidth);

            // Always consume at least one character, otherwise an impossibly narrow box would loop forever.
            fitting = Math.Clamp(fitting, 1, remaining.Length);

            string chunk = remaining[..fitting];
            float chunkWidth = context.TextMeasurer.MeasureWidth(chunk, style);

            current.Add(new TextRun(chunk, style, chunkWidth, span.Url, span.Destination));
            remaining = remaining[fitting..];

            if (remaining.Length == 0)
                break;

            current.Finalise(context.TextMeasurer, style);
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

        /// <summary>
        /// Whether a trailing run may be dropped: breakable whitespace only, and carrying no annotation.
        /// </summary>
        private static bool IsTrimmable(TextRun run) =>
            run.Url is null
            && run.Destination is null
            && run.Text.Length > 0
            && run.Text.All(IsBreakableWhitespace);

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
            {
                Width -= Runs[^1].Width;
                Runs.RemoveAt(Runs.Count - 1);
            }

            Width = Math.Max(0, Width);

            if (Runs.Count == 0)
            {
                TypeMetrics fallback = measurer.GetMetrics(fallbackStyle);
                Ascent = fallback.Ascent;
                Descent = fallback.Descent;
                Height = fallback.LineHeight;
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
                Height = Math.Max(Height, metrics.LineHeight * run.Style.LineHeight);
            }

            Height = Math.Max(Height, Ascent + Descent);
        }
    }
}
