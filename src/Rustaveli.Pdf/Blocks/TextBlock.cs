using System.Buffers;
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

    /// <summary>The link each linked span is, in a tagged document, whichever page and line its pieces fall on.</summary>
    private Dictionary<Text.TextRun, StructureElement?>? _links;

    // Not reset between passes: the lines of the same text at the same width do not change.
    private BuiltLines? _built;


    public List<Text.TextRun> Runs { get; } = [];

    /// <summary>
    /// How lines are aligned. The default follows the inherited reading direction, so right-to-left text aligns
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

    // Inline frames are children of this paragraph, so the engine can reset their state between passes. The engine
    // asks on every pass and most paragraphs hold none, so those answer without allocating. Runs can change after
    // composition, so the paragraph looks each time rather than remembering.
    public override IEnumerable<Block?> GetChildren()
    {
        for (int index = 0; index < Runs.Count; index++)
        {
            if (Runs[index].Inline != null)
            {
                return Runs.Select(span => span.Inline);
            }
        }

        return Array.Empty<Block?>();
    }

    /// <summary>
    /// The edge a line that is not stretched sits against, resolving start and end by the reading direction. A
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

    /// <summary>The edge lines start from in the reading direction, where the first-line indent goes.</summary>
    private static HorizontalPlacement StartEdge(PlanContext context) =>
        context.ReadingDirection == ReadingDirection.RightToLeft ? HorizontalPlacement.Right : HorizontalPlacement.Left;

    /// <summary>
    /// The indent that will actually be drawn on a paragraph's opening line.
    /// </summary>
    /// <remarks>
    /// Only text aligned to the edge lines start from is indented, so for any other alignment this is zero — and
    /// it must be zero everywhere, not just at drawing time. Charging the line budget for an indent that is never
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
        _links = null;
    }

    // The pinned wrapping is replaced whole, never changed, so it is kept rather than copied. The paragraph element is
    // who the text is, not how far it has got, so it is not progress.
    protected override object? SaveOwnProgress() => (_completedLines, _pinnedWidth, _pinnedWrapping);

    protected override void RestoreOwnProgress(object progress) =>
        (_completedLines, _pinnedWidth, _pinnedWrapping) = ((int, float, List<TextLine>?))progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        // Without usable width there is no wrapping that could succeed. Deferring sends the paragraph to
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
            return Fit.Defer(lines[_completedLines].HoldsInline
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

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        // A blocker means planning deferred, so this paragraph should not have been asked to draw here.
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
        // them from inline frames whose state this very draw has just advanced.
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
        line.StartsParagraph && line.Count > 0 && index > _completedLines ? SpaceBetweenParagraphs : 0f;

    private void DrawLine(TextLine line, float availableWidth, float top, bool endsParagraph, RenderContext context)
    {
        // A pass that only counts pages draws nothing, so a line of text alone has nothing to do in it; a line
        // holding an inline frame is set as usual, for the frame may record where it lands.
        if (context.Surface is CountingPageSink && !line.HoldsInline)
            return;

        float baseline = top + line.Ascent;
        float indent = line.StartsParagraph ? EffectiveIndent(context.Planning) : 0f;
        (int firstWord, int lastWord, int spaces) = Alignment == LineAlignment.Justified ? line.WordGaps() : (-1, -1, 0);
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

        // A line joined when it was sealed is drawn as it is.
        if (line.IsJoined)
        {
            DrawPieces(line.Pieces, line, x, baseline, context);
            return;
        }

        // Otherwise its pieces are gathered apart from it, since it is built once and drawn on every page it may land
        // on, and never changed: the gaps between its words widened when it is stretched, and put in display order.
        Piece[] borrowed = ArrayPool<Piece>.Shared.Rent(Math.Max(1, line.Count));

        try
        {
            for (int index = 0; index < line.Count; index++)
            {
                // Whitespace before the first word or after the last is kept as typed; only the gaps between stretch.
                Piece run = line[index];

                if (stretch > 0 && index > firstWord && index < lastWord && IsWordGap(run))
                    run = run with { Width = run.Width + (stretch * run.Length) };

                borrowed[index] = run;
            }

            Piece[] runs = line.Bidi is null ? borrowed : InDisplayOrder(borrowed, line.Count, line.Bidi, context.Measurer);
            int count = line.Bidi is null ? line.Count : runs.Length;

            if (stretch <= 0)
                count = Coalesce(runs, 0, count, 0);

            DrawPieces(new ReadOnlySpan<Piece>(runs, 0, count), line, x, baseline, context);
        }
        finally
        {
            // Pieces hold their text and type, which a pooled array would otherwise keep alive.
            ArrayPool<Piece>.Shared.Return(borrowed, clearArray: true);
        }
    }

    /// <summary>Draws a line's pieces from left to right, the first at <paramref name="x"/>.</summary>
    private void DrawPieces(ReadOnlySpan<Piece> runs, TextLine line, float x, float baseline, RenderContext context)
    {
        ISurface surface = context.Surface;

        foreach (Piece run in runs)
        {
            // Linked words are a link in the structure, which the link itself belongs to: one for the whole span, however
            // many pieces it is drawn in — a word at a time when justified, and over several lines.
            using TagStack.Scope link = run.Url is null && run.Destination is null
                ? default
                : context.Tags.Enter(LinkFor(run.Source!, context.Tags));

            if (run.Inline is not null)
            {
                Extent inlineSize = new Extent(run.Width, run.Height);
                Offset inlineTop = new Offset(x, baseline + line.InlineTop(run));

                surface.MoveOrigin(inlineTop);
                run.Inline.Render(inlineSize, context);
                surface.MoveOrigin(inlineTop.Reverse());

                if (run.Url is not null)
                    surface.LinkToUrl(run.Url, inlineTop, inlineSize);

                if (run.Destination is not null)
                    surface.LinkToDestination(run.Destination, inlineTop, inlineSize);

                x += run.Width;
                continue;
            }

            TypeStyle style = run.Style;
            TypeMetrics metrics = context.Measurer.GetMetrics(style);
            float runTop = baseline - metrics.Ascent + style.BaselineOffset;
            Extent runSize = new Extent(run.Width, metrics.Ascent + metrics.Descent);

            if (!style.Highlight.IsTransparent)
                surface.FillRectangle(new Offset(x, runTop), runSize, style.Highlight);

            ReadingDirection direction = run.RightToLeft ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight;
            surface.ShowText(run.ToText(), new Offset(x, baseline + style.BaselineOffset), style, direction);

            if (style.HasUnderline || style.HasStrikeThrough || style.HasOverline)
                DrawStrokes(surface, style, metrics, x, run.Width, baseline + style.BaselineOffset);

            // The spaces between linked words are no place to click, and would each be an annotation of their own; a link
            // on nothing but a space keeps its annotation, which is all there is of it.
            bool clickable = !IsBlank(run.Characters) || string.IsNullOrWhiteSpace(run.Source?.Text);

            if (run.Url is not null && clickable)
                surface.LinkToUrl(run.Url, new Offset(x, runTop), runSize);

            if (run.Destination is not null && clickable)
                surface.LinkToDestination(run.Destination, new Offset(x, runTop), runSize);

            x += run.Width;
        }
    }

    /// <summary>The link element of a linked span, made when its first piece is drawn; none when not tagging.</summary>
    private StructureElement? LinkFor(Text.TextRun span, TagStack tags)
    {
        _links ??= [];

        if (!_links.TryGetValue(span, out StructureElement? link))
            _links[span] = link = tags.Create("Link");

        return link;
    }

    /// <summary>
    /// Puts a line's runs in the order they are displayed from left to right (UAX #9, rules L1 to L4): split where the
    /// direction changes, runs of right-to-left text reversed, and each piece of it set last character first with
    /// mirrored brackets. Text from outside the paragraph — an ellipsis — ends the line, which is its left end when
    /// the paragraph reads right to left.
    /// </summary>
    private static Piece[] InDisplayOrder(Piece[] runs, int count, BidiParagraph bidi, ITypeMeasurer measurer)
    {
        int start = int.MaxValue;
        int end = 0;
        List<Piece> outside = [];

        for (int index = 0; index < count; index++)
        {
            Piece run = runs[index];

            if (run.Offset < 0)
            {
                outside.Add(run);
                continue;
            }

            start = Math.Min(start, run.Offset);
            end = Math.Max(end, run.Offset + LogicalLength(run));
        }

        List<Piece> ordered = new List<Piece>(count + 2);

        if (start < end)
        {
            List<BidiRun> levels = [];
            bidi.GetVisualRuns(start, end - start, levels);

            foreach (BidiRun level in levels)
            {
                int first = ordered.Count;

                for (int index = 0; index < count; index++)
                {
                    Piece run = runs[index];
                    int from = Math.Max(run.Offset, level.Start);
                    int to = Math.Min(run.Offset + LogicalLength(run), level.Start + level.Length);

                    if (run.Offset >= 0 && from < to)
                        ordered.Add(Cut(run, from - run.Offset, to - from, level.IsRightToLeft, measurer));
                }

                if (level.IsRightToLeft)
                    ordered.Reverse(first, ordered.Count - first);
            }
        }

        if (bidi.ParagraphLevel == 1)
            ordered.InsertRange(0, outside);
        else
            ordered.AddRange(outside);

        return ordered.ToArray();
    }

    /// <summary>
    /// Joins neighbouring pieces set in the same type, carrying the same link, into one, so a line of words is drawn
    /// as one piece of text rather than a word and a space at a time.
    /// </summary>
    /// <remarks>
    /// Drawing walks the joined text glyph by glyph exactly as measuring walked each piece, so the words land where
    /// the line was measured — with two exceptions, which are kept apart: tracking, which measuring each piece on its
    /// own leaves out between pieces, and a stretched space, whose width is more than its glyph's.
    /// </remarks>
    /// <returns>
    /// How many pieces the <paramref name="count"/> from <paramref name="from"/> join into, which are written from
    /// <paramref name="to"/>, at or before where they were read.
    /// </returns>
    private static int Coalesce(Piece[] runs, int from, int count, int to)
    {
        int written = to;
        int start = from;
        int stop = from + count;

        while (start < stop)
        {
            int end = start + 1;
            int length = runs[start].Length;
            float width = runs[start].Width;

            while (end < stop && Joins(runs[start], runs[end]))
            {
                length += runs[end].Length;
                width += runs[end].Width;
                end++;
            }

            // Only what this has read is written over: the pieces before start.
            runs[written++] = end == start + 1 ? runs[start] : Joined(runs, start, end, length, width);
            start = end;
        }

        return written - to;
    }

    /// <summary>
    /// The pieces from <paramref name="start"/> to <paramref name="end"/> as one: still a stretch of the text they
    /// were cut from when they lie side by side in it, else the text joined. Pieces of right-to-left text arrive in
    /// display order, last read first, so they are joined from the end, in the order they are read.
    /// </summary>
    private static Piece Joined(Piece[] runs, int start, int end, int length, float width)
    {
        Piece first = runs[start];
        bool backwards = first.RightToLeft;
        Piece reading = runs[backwards ? end - 1 : start];
        bool adjacent = true;

        for (int step = 1; step < end - start && adjacent; step++)
        {
            Piece previous = runs[backwards ? end - step : start + step - 1];
            Piece next = runs[backwards ? end - 1 - step : start + step];
            adjacent = ReferenceEquals(next.Text, reading.Text) && next.Start == previous.Start + previous.Length;
        }

        return adjacent
            ? first with { Text = reading.Text, Start = reading.Start, Length = length, Width = width }
            : first with { Text = Concatenate(runs, start, end, length), Start = 0, Length = length, Width = width };
    }

    /// <summary>The joined text of pieces that do not lie side by side, in the order they are read.</summary>
    private static string Concatenate(Piece[] runs, int start, int end, int length)
    {
#if NET
        // Written straight into the string, with no buffer to copy it from.
        return string.Create(length, (runs, start, end), static (characters, state) => Join(characters, state.runs, state.start, state.end));
#else
        char[] characters = new char[length];
        Join(characters, runs, start, end);
        return new string(characters);
#endif
    }

    private static void Join(Span<char> characters, Piece[] runs, int start, int end)
    {
        int at = 0;
        bool backwards = runs[start].RightToLeft;

        for (int step = 0; step < end - start; step++)
        {
            ReadOnlySpan<char> text = runs[backwards ? end - 1 - step : start + step].Characters;
            text.CopyTo(characters.Slice(at));
            at += text.Length;
        }
    }

    private static bool Joins(Piece previous, Piece next) =>
        previous.Inline is null
        && next.Inline is null
        && previous.Style.Tracking == 0
        && (ReferenceEquals(previous.Style, next.Style) || previous.Style.Equals(next.Style))
        && previous.Url == next.Url
        && previous.Destination == next.Destination
        && previous.RightToLeft == next.RightToLeft;

    /// <summary>How many code units a piece takes in its paragraph's text: an inline frame stands in for one.</summary>
    private static int LogicalLength(Piece run) => run.Inline is null ? run.Length : 1;

    /// <summary>
    /// The part of a piece from <paramref name="start"/> for <paramref name="length"/> code units, measured afresh
    /// unless it is the whole piece, and set right to left when <paramref name="rightToLeft"/>.
    /// </summary>
    private static Piece Cut(Piece run, int start, int length, bool rightToLeft, ITypeMeasurer measurer)
    {
        if (run.Inline is not null)
            return run;

        bool whole = start == 0 && length == run.Length;

        // Spaces share a stretched width evenly; anything else is measured on its own.
        float width = whole
            ? run.Width
            : IsWordGap(run) ? run.Width * length / run.Length : measurer.MeasureWidth(run.Characters.Slice(start, length), run.Style);

        return run with { Start = run.Start + start, Length = length, Width = width, RightToLeft = rightToLeft };
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
        // plans every inline frame again, and one already drawn on an earlier page plans to nothing
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

        // Only an inline frame can block the lines, and a paragraph holding one is never reused, so what is reusable
        // was built whole.
        if (reusable)
            _built = new BuiltLines(maxWidth, blockStyle, context.ReadingDirection, context.Measurer, lines);

        return lines;
    }

    /// <summary>
    /// Builds the lines word by word, at the end of pieces borrowed for the purpose, then seals them: each line's
    /// pieces joined where drawing would join them, and all kept in one array, as long as they are and no longer.
    /// </summary>
    private List<TextLine> BuildLinesAfresh(
        float maxWidth, float maxHeight, TypeStyle blockStyle, PlanContext context, out string? blocker)
    {
        PieceList pieces = PieceList.Borrow();

        try
        {
            List<TextLine> lines = WrapLines(pieces, maxWidth, maxHeight, blockStyle, context, out blocker);
            Seal(lines, pieces);
            return lines;
        }
        finally
        {
            PieceList.GiveBack(pieces);
        }
    }

    /// <summary>
    /// Gives the lines their pieces for good. A line is joined as drawing would join it, unless it is drawn word by
    /// word — justified, to stretch the gaps between its words, or holding right-to-left text, to be cut where the
    /// direction changes — and every line's pieces go into one array.
    /// </summary>
    private void Seal(List<TextLine> lines, PieceList pieces)
    {
        bool joins = Alignment != LineAlignment.Justified;
        Piece[] items = pieces.Items;
        int written = 0;

        // Each line's pieces are moved down to follow the line before's, and never past where they were.
        foreach (TextLine line in lines)
        {
            bool joined = joins && line.Bidi is null;
            int count = joined ? Coalesce(items, line.First, line.Count, written) : Move(items, line.First, line.Count, written);
            line.Place(written, count, joined);
            written += count;
        }

        Piece[] kept = written == 0 ? [] : new Piece[written];
        Array.Copy(items, kept, written);

        foreach (TextLine line in lines)
            line.Seal(kept);
    }

    /// <summary>Moves <paramref name="count"/> pieces from <paramref name="from"/> down to <paramref name="to"/>.</summary>
    private static int Move(Piece[] pieces, int from, int count, int to)
    {
        Array.Copy(pieces, from, pieces, to, count);
        return count;
    }

    private List<TextLine> WrapLines(
        PieceList pieces, float maxWidth, float maxHeight, TypeStyle blockStyle, PlanContext context, out string? blocker)
    {
        blocker = null;
        List<TextLine> lines = new List<TextLine>();
        TextLine current = new TextLine(pieces);
        float indent = EffectiveIndent(context);

        // Only reached before anything is drawn: from then on the pinned wrapping above is returned whole, which
        // also fixes the width it was built at.
        float width = Math.Max(0, maxWidth);

        // The opening line of the block starts a paragraph; thereafter only a line following an explicit break
        // does. Carrying the flag on the line itself means the mid-word breaker keeps it right for free.
        current.StartsParagraph = true;

        void FlushLine(bool force)
        {
            if (current.Count == 0 && !force)
                return;

            current.Finalise(context.Measurer, blockStyle);
            lines.Add(current);
            current = new TextLine(pieces) { StartsParagraph = force };
        }

        // Each paragraph's text is gathered as its lines are built, every run recording where it falls in it, so the
        // bidirectional algorithm can resolve the paragraph whole — a character's direction can depend on text
        // lines away — and each line can then be put in display order.
        ParagraphText paragraph = default;
        int paragraphStart = 0;
        BidiDirection direction = context.ReadingDirection == ReadingDirection.RightToLeft
            ? BidiDirection.RightToLeft
            : BidiDirection.LeftToRight;

        void CloseParagraph()
        {
            if (lines.Count > paragraphStart && paragraph.Length > 0)
            {
                BidiParagraph bidi = new BidiParagraph(paragraph.Text, direction);

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
        bool PastLimit() => lines.Count > limit || (lines.Count == limit && current.Count > 0);

        foreach (Text.TextRun span in Runs)
        {
            if (PastLimit())
                break;

            if (span.Inline is not null)
            {
                // An inline frame is unbreakable, so if it does not fit on this line it moves down whole, exactly
                // like a word — but it is planned against the budget of a line it could actually occupy, and
                // against the height the paragraph really has. Offering an unbounded height instead makes any
                // frame that fills what it is given — Middle, Expand — claim the full 14,400 points and defer
                // the whole paragraph to a page it can never fit.
                float lineBudget = current.StartsParagraph ? Math.Max(0, width - indent) : width;
                Fit inlinePlan = span.Inline.Plan(new Extent(lineBudget, maxHeight), context);

                if (inlinePlan.IsNothing)
                    continue;

                // Defer and Partial both mean content is left over, and an inline run has no way to carry
                // a remainder onto the next line. Dropping it here deletes it from the document with no error,
                // so the paragraph defers as a whole instead and the engine reports it if no page can hold it.
                if (inlinePlan.IsDeferred || inlinePlan.IsPartial)
                {
                    blocker = "A paragraph holds an inline frame that does not fit the width available to it. "
                        + "Inline frames cannot be split across lines, so it has to fit on one.";

                    paragraph.Release();
                    return lines;
                }

                if (current.Count > 0 && current.Width + inlinePlan.Size.Width > lineBudget + Extent.Epsilon)
                    FlushLine(force: false);

                // The frame stands in the paragraph's text as an object replacement character: a neutral, so it takes
                // the direction of the text around it.
                paragraph.Append(ObjectReplacement);
                current.Add(new Piece(
                    string.Empty,
                    0,
                    0,
                    span.ResolveStyle(blockStyle),
                    inlinePlan.Size.Width,
                    span,
                    inlinePlan.Size.Height,
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

            foreach (Token token in Tokenise(text))
            {
                if (PastLimit())
                    break;

                if (token.EndsLine)
                {
                    FlushLine(force: true);
                    CloseParagraph();

                    // The run goes on into the next paragraph, isolated there too.
                    if (isolate is char reopening)
                        paragraph.Append(reopening);

                    continue;
                }

                int offset = paragraph.Length;
                ReadOnlySpan<char> segment = text.AsSpan(token.Start, token.Length);
                paragraph.Append(segment);

                bool isWhitespace = IsBreakableWhitespace(segment[0]);
                float segmentWidth = context.Measurer.MeasureWidth(segment, style);
                float lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                // A soft hyphen ending the piece is shown if the line breaks at it, so the line keeps room for it.
                float hyphen = EndsWithSoftHyphen(segment) ? context.Measurer.MeasureWidth(ShownHyphen, style) : 0f;
                Piece piece = new Piece(text, token.Start, token.Length, style, segmentWidth, span, Offset: offset);

                if (current.Width + segmentWidth + hyphen <= lineWidth + Extent.Epsilon)
                {
                    current.Add(piece);
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
                    BreakWord(piece, width, indent, context, current, lines);
                    continue;
                }

                if (current.Count > 0)
                {
                    ShowSoftHyphen(current, context.Measurer);
                    FlushLine(force: false);
                }

                // The flush replaced the line, and a continuation is not indented — so the budget has to be
                // recomputed. Reusing the opening line's narrower budget would shatter words that do fit.
                lineWidth = current.StartsParagraph ? Math.Max(0, width - indent) : width;

                if (segmentWidth <= lineWidth + Extent.Epsilon)
                {
                    current.Add(piece);
                    continue;
                }

                BreakWord(piece, width, indent, context, current, lines);
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
        paragraph.Release();

        if (lines.Count > limit)
        {
            lines.RemoveRange(limit, lines.Count - limit);
            TextLine last = lines[^1];

            // The last line shown ends the pieces, so it can be cut back and the ellipsis set after it.
            pieces.RemoveFrom(last.First + last.Count);
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

        while (line.Count > 0)
        {
            Piece last = line.Last;

            if (!IsWordGap(last) && line.Width <= room + Extent.Epsilon)
                break;

            line.RemoveLast();

            if (IsWordGap(last) || last.Inline is not null)
                continue;

            int fitting = context.Measurer.MeasureCharactersFitting(last.Characters, last.Style, room - line.Width);

            if (fitting > 0)
            {
                float kept = context.Measurer.MeasureWidth(last.Characters.Slice(0, fitting), last.Style);
                line.Add(last with { Length = fitting, Width = kept });
                break;
            }
        }

        if (Ellipsis.Length > 0)
            line.Add(new Piece(Ellipsis, 0, Ellipsis.Length, style, ellipsisWidth, null));
    }

    /// <summary>
    /// Splits a word into character-level chunks, filling whatever room <paramref name="current"/> has left, emitting
    /// full lines as it goes and leaving the remainder on <paramref name="current"/>. Used for a word too long for
    /// any line, and for every word in type that may break anywhere.
    /// </summary>
    private static void BreakWord(
        Piece word,
        float width,
        float indent,
        PlanContext context,
        TextLine current,
        List<TextLine> lines)
    {
        TypeStyle style = word.Style;
        int taken = 0;

        while (taken < word.Length)
        {
            // Recomputed per chunk: the first may continue an indented paragraph opening, every one after it is a
            // fresh continuation entitled to the full width.
            float room = (current.StartsParagraph ? Math.Max(0, width - indent) : width) - current.Width;
            ReadOnlySpan<char> remaining = word.Characters.Slice(taken);

            int fitting = context.Measurer.MeasureCharactersFitting(remaining, style, room);

            // Not a character fits beside what the line already holds, so the word goes on to the next.
            if (fitting == 0 && current.Count > 0)
            {
                current.Finalise(context.Measurer, style);
                lines.Add(current.Close());
                continue;
            }

            // Always consume at least one character, otherwise an impossibly narrow box would loop forever — and a whole
            // one, as a reader sees it, so a surrogate pair or a letter and its accent are never split across lines.
            if (fitting == 0)
                fitting = GraphemeBoundaries.FirstLength(remaining);

            float chunkWidth = context.Measurer.MeasureWidth(remaining.Slice(0, fitting), style);

            current.Add(word with { Start = word.Start + taken, Length = fitting, Width = chunkWidth, Offset = word.Offset + taken });
            taken += fitting;

            if (taken == word.Length)
                break;

            current.Finalise(context.Measurer, style);
            lines.Add(current.Close());
        }
    }

    /// <summary>
    /// Shows the soft hyphen a line ends with as a hyphen, now that the line breaks at it. Elsewhere a soft hyphen is
    /// set as nothing.
    /// </summary>
    private static void ShowSoftHyphen(TextLine line, ITypeMeasurer measurer)
    {
        Piece last = line.Last;

        if (!EndsWithSoftHyphen(last.Characters))
            return;

        // Replaced one for one, so the piece still covers the same characters of its paragraph's text.
        string shown = last.Characters.Slice(0, last.Length - 1).ToString() + ShownHyphen;
        line.RemoveLast();
        line.Add(last with { Text = shown, Start = 0, Length = shown.Length, Width = measurer.MeasureWidth(shown, last.Style) });
    }

    private static bool EndsWithSoftHyphen(ReadOnlySpan<char> text) =>
        text.Length > 0 && text[text.Length - 1] == InvisibleCharacters.SoftHyphen;

    /// <summary>What a soft hyphen at the end of a line is shown as: the hyphen every face has.</summary>
    private const string ShownHyphen = "-";

    /// <summary>
    /// Whitespace a line may be broken at. Non-breaking forms are deliberately excluded: they exist precisely to
    /// hold "10 000" or "Fig. 4" together, so treating them as break opportunities defeats their only purpose.
    /// </summary>
    private static bool IsBreakableWhitespace(char character) =>
        char.IsWhiteSpace(character) && character is not ('\u00A0' or '\u202F' or '\u2007');

    /// <summary>A run of breakable whitespace between words, as the tokeniser splits it out.</summary>
    private static bool IsWordGap(Piece run) =>
        run.Inline is null && run.Length > 0 && IsAllBreakableWhitespace(run.Characters);

    // A loop rather than LINQ: every piece of every line passes through here, and an enumerator each would add up.
    private static bool IsAllBreakableWhitespace(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!IsBreakableWhitespace(character))
                return false;
        }

        return true;
    }

    /// <summary>Whether text is empty or nothing but whitespace, breakable or not.</summary>
    private static bool IsBlank(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!char.IsWhiteSpace(character))
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
    private static Tokens Tokenise(string text) => new Tokens(text);

    /// <summary>A stretch of a run's text, or a line ending, as <see cref="Tokenise"/> splits it.</summary>
    /// <param name="Start">Where it starts in the text.</param>
    /// <param name="Length">How many code units it takes.</param>
    /// <param name="EndsLine">Whether it is the line ending, which takes no characters of its own.</param>
    private readonly record struct Token(int Start, int Length, bool EndsLine);

    /// <summary>
    /// The pieces <see cref="Tokenise"/> splits text into, handed out one at a time as the line breaking rules find
    /// them, rather than gathered into a list first: a paragraph of a hundred words would otherwise grow one to hold
    /// two hundred pieces, to be read once and dropped.
    /// </summary>
    private ref struct Tokens
    {
        private readonly string _text;
        private LineBreakEnumerator _breaks;

        // The piece between two break opportunities: its word, from the start, then its whitespace, from the word,
        // up to the content's end; then the line ending, if it has one, up to the end.
        private int _start;
        private int _word;
        private int _content;
        private int _end;
        private bool _endsLine;

        /// <summary>Which of the piece's tokens comes next: 0 when the next piece is still to be found.</summary>
        private int _next;

        public Tokens(string text)
        {
            _text = text;
            _breaks = LineBreaker.Enumerate(text.AsSpan());
        }

        public Token Current { get; private set; }

        public readonly Tokens GetEnumerator() => this;

        public bool MoveNext()
        {
            while (true)
            {
                switch (_next)
                {
                    case 0:
                        if (!_breaks.MoveNext())
                            return false;

                        FindPiece(_breaks.Current);
                        _next = 1;
                        break;

                    case 1:
                        _next = 2;

                        if (_word > _start)
                        {
                            Current = new Token(_start, _word - _start, EndsLine: false);
                            return true;
                        }

                        break;

                    case 2:
                        _next = 3;

                        if (_content > _word)
                        {
                            Current = new Token(_word, _content - _word, EndsLine: false);
                            return true;
                        }

                        break;

                    default:
                        _next = 0;

                        if (_endsLine)
                        {
                            Current = new Token(_content, _end - _content, EndsLine: true);
                            _start = _end;
                            return true;
                        }

                        _start = _end;

                        break;
                }
            }
        }

        private void FindPiece(LineBreak opportunity)
        {
            _end = opportunity.Position;
            _content = _end;
            _endsLine = opportunity.IsMandatory && _end > _start && IsLineTerminator(_text[_end - 1]);

            if (_endsLine)
            {
                // A carriage return and line feed together are one line ending.
                _content--;

                if (_text[_content] == '\n' && _content > _start && _text[_content - 1] == '\r')
                    _content--;
            }

            _word = _content;

            while (_word > _start && IsBreakableWhitespace(_text[_word - 1]))
                _word--;
        }
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

    /// <summary>
    /// A paragraph's text, gathered as its lines are built, in a buffer borrowed from the shared pool: the
    /// bidirectional algorithm reads it where it lies, and the next paragraph writes over it, so a paragraph costs no
    /// string and no buffer of its own. A buffer not given back, when building stops on an exception, is simply
    /// collected.
    /// </summary>
    private struct ParagraphText
    {
        private char[]? _buffer;

        public int Length { get; private set; }

        public readonly ReadOnlySpan<char> Text => _buffer.AsSpan(0, Length);

        public void Append(char character)
        {
            Reserve(1);
            _buffer![Length++] = character;
        }

        public void Append(ReadOnlySpan<char> text)
        {
            Reserve(text.Length);
            text.CopyTo(_buffer.AsSpan(Length));
            Length += text.Length;
        }

        public void Clear() => Length = 0;

        public void Release()
        {
            if (_buffer is not null)
                ArrayPool<char>.Shared.Return(_buffer);

            _buffer = null;
            Length = 0;
        }

        private void Reserve(int more)
        {
            if (_buffer is not null && Length + more <= _buffer.Length)
                return;

            char[] larger = ArrayPool<char>.Shared.Rent(Math.Max(256, Math.Max(Length + more, (_buffer?.Length ?? 0) * 2)));

            if (_buffer is not null)
            {
                _buffer.AsSpan(0, Length).CopyTo(larger);
                ArrayPool<char>.Shared.Return(_buffer);
            }

            _buffer = larger;
        }
    }

    /// <summary>The lines last built, and what they were built for.</summary>
    private sealed record BuiltLines(
        float Width, TypeStyle Style, ReadingDirection Direction, ITypeMeasurer Measurer, List<TextLine> Lines);

    /// <summary>
    /// One piece of a line: a stretch of text — a word, the space after it, part of a word broken across lines, an
    /// ellipsis — or a frame sitting inline among the words.
    /// </summary>
    /// <remarks>
    /// A paragraph holds a piece for every word and every space, so a piece is a value, kept with the rest of its
    /// paragraph's, and it names the text it is cut from rather than holding a copy. What it links to, and the inline
    /// frame and how it sits, are the run's it was cut from.
    /// </remarks>
    /// <param name="Text">The text it is cut from: its run's, or one of its own; empty for an inline frame.</param>
    /// <param name="Start">Where it starts in <paramref name="Text"/>.</param>
    /// <param name="Length">How many code units of <paramref name="Text"/> it takes.</param>
    /// <param name="Style">The type it is set in.</param>
    /// <param name="Width">Its width, stretched if the line is justified.</param>
    /// <param name="Source">The run it was cut from; none for an ellipsis, which comes from no run.</param>
    /// <param name="Height">An inline frame's height.</param>
    /// <param name="Offset">Where it falls in its paragraph's text; -1 for an ellipsis, from outside it.</param>
    /// <param name="RightToLeft">Whether it is set right to left.</param>
    private readonly record struct Piece(
        string Text,
        int Start,
        int Length,
        TypeStyle Style,
        float Width,
        Text.TextRun? Source,
        float Height = 0f,
        int Offset = -1,
        bool RightToLeft = false)
    {
        /// <summary>Its characters, where they lie in the text it is cut from.</summary>
        public ReadOnlySpan<char> Characters => Text.AsSpan(Start, Length);

        public string? Url => Source?.Url;

        public string? Destination => Source?.Anchor;

        public Block? Inline => Source?.Inline;

        public InlinePosition Position => Source?.InlinePosition ?? InlinePosition.OnBaseline;

        /// <summary>Its characters as a string of their own, which the whole text it is cut from already is.</summary>
        public string ToText() => Start == 0 && Length == Text.Length ? Text : Text.Substring(Start, Length);
    }

    /// <summary>
    /// A paragraph's pieces while its lines are built, a piece for every word and every space. One is kept for each
    /// thread and lent to one paragraph at a time, so building lines costs no more than the lines it ends with; a
    /// paragraph built while another is — one set in a frame inline in it — has one of its own.
    /// </summary>
    private sealed class PieceList
    {
        [ThreadStatic]
        private static PieceList? _spare;

        public Piece[] Items { get; private set; } = new Piece[256];

        public int Count { get; private set; }

        public static PieceList Borrow()
        {
            PieceList pieces = _spare ?? new PieceList();
            _spare = null;
            return pieces;
        }

        public static void GiveBack(PieceList pieces)
        {
            // Pieces hold their text and type, which are not to be kept alive once the lines built from them are gone.
            Array.Clear(pieces.Items, 0, pieces.Count);
            pieces.Count = 0;

            // A paragraph far longer than most would otherwise keep a buffer its size for as long as the thread lives.
            if (pieces.Items.Length <= MostKept)
                _spare = pieces;
        }

        public void Add(Piece piece)
        {
            if (Count == Items.Length)
            {
                Piece[] larger = new Piece[Items.Length * 2];
                Array.Copy(Items, larger, Count);
                Items = larger;
            }

            Items[Count++] = piece;
        }

        public void RemoveLast() => Items[--Count] = default;

        /// <summary>Drops every piece from <paramref name="index"/> on.</summary>
        public void RemoveFrom(int index)
        {
            Array.Clear(Items, index, Count - index);
            Count = index;
        }

        private const int MostKept = 16384;
    }

    /// <summary>
    /// A line: a stretch of its paragraph's pieces, which every line of the paragraph shares, and how the line
    /// measures.
    /// </summary>
    /// <remarks>
    /// A line is built at the end of the pieces — added to, its trailing spaces dropped, its last piece changed — and
    /// the next starts where it ends, so only the last line built ever changes, and it changes at the end of them.
    /// Once every line is built, each is sealed onto the paragraph's pieces for good, and never changes again.
    /// </remarks>
    private sealed class TextLine
    {
        /// <summary>The pieces the line is being built at the end of; none once it is sealed.</summary>
        private PieceList? _building;

        private Piece[] _pieces = [];

        /// <summary>A line starting after every piece there is so far.</summary>
        /// <param name="pieces">The pieces of the paragraph the line is part of.</param>
        public TextLine(PieceList pieces)
        {
            _building = pieces;
            First = pieces.Count;
        }

        /// <summary>Copies an in-progress line, used when a mid-word break commits the current content.</summary>
        private TextLine(TextLine source)
        {
            _building = source._building;
            First = source.First;
            Count = source.Count;
            StartsParagraph = source.StartsParagraph;
            Width = source.Width;
            Ascent = source.Ascent;
            Descent = source.Descent;
            Height = source.Height;
            TypeAscent = source.TypeAscent;
            TypeDescent = source.TypeDescent;
        }

        /// <summary>Where the line's pieces start among its paragraph's.</summary>
        public int First { get; private set; }

        /// <summary>How many pieces the line holds.</summary>
        public int Count { get; private set; }

        public Piece this[int index] => (_building?.Items ?? _pieces)[First + index];

        public Piece Last => this[Count - 1];

        /// <summary>The line's pieces, once it is sealed.</summary>
        public ReadOnlySpan<Piece> Pieces => new ReadOnlySpan<Piece>(_pieces, First, Count);

        /// <summary>Whether the line's pieces were joined when it was sealed, as drawing would join them.</summary>
        public bool IsJoined { get; private set; }

        /// <summary>Whether a frame sits inline on the line.</summary>
        public bool HoldsInline
        {
            get
            {
                for (int index = 0; index < Count; index++)
                {
                    if (this[index].Inline is not null)
                        return true;
                }

                return false;
            }
        }

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

        public void Add(Piece run)
        {
            _building!.Add(run);
            Count++;
            Width += run.Width;
        }

        public void RemoveLast()
        {
            Width -= Last.Width;
            _building!.RemoveLast();
            Count--;
        }

        /// <summary>Where the line's pieces are to be, once its paragraph's pieces have been moved together.</summary>
        public void Place(int first, int count, bool joined)
        {
            First = first;
            Count = count;
            IsJoined = joined;
        }

        /// <summary>Gives the line the pieces it is placed among, for good.</summary>
        public void Seal(Piece[] pieces)
        {
            _pieces = pieces;
            _building = null;
        }

        /// <summary>
        /// The line so far, finished, as a line of its own: this one goes on after it, with none of its pieces, as a
        /// continuation of the paragraph.
        /// </summary>
        public TextLine Close()
        {
            TextLine closed = new TextLine(this);
            First += Count;
            Count = 0;
            Clear();
            return closed;
        }

        /// <summary>
        /// Whether a trailing piece may be dropped: breakable whitespace only, and carrying no annotation.
        /// </summary>
        private static bool IsTrimmable(Piece run) =>
            run.Url is null
            && run.Destination is null
            && IsWordGap(run);

        /// <summary>
        /// Where the line's words begin and end, as piece indexes, and how many spaces lie between them: the spaces
        /// justification widens. With no word on the line both indexes are -1.
        /// </summary>
        public (int First, int Last, int Spaces) WordGaps()
        {
            int first = -1;
            int last = -1;

            for (int index = 0; index < Count; index++)
            {
                if (!IsWordGap(this[index]))
                {
                    first = first < 0 ? index : first;
                    last = index;
                }
            }

            int spaces = 0;

            for (int index = first + 1; index < last; index++)
            {
                if (IsWordGap(this[index]))
                    spaces += this[index].Length;
            }

            return (first, last, spaces);
        }

        private void Clear()
        {
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
            // width that natural-width columns and table cells are sized from.
            //
            // The predicate has to agree with the tokeniser: a non-breaking space is deliberately treated as
            // ink, so trimming it would delete the very content it exists to hold together. A run carrying a
            // link is never trimmed either, since dropping it would silently remove the annotation with it.
            while (Count > 0 && IsTrimmable(Last))
                RemoveLast();

            Width = Math.Max(0, Width);

            // A line cut short by a line limit is finalised again, and must not keep the height of what was cut.
            Ascent = 0;
            Descent = 0;
            Height = 0;

            if (Count == 0)
            {
                TypeMetrics fallback = measurer.GetMetrics(fallbackStyle);
                Ascent = TypeAscent = fallback.Ascent;
                Descent = TypeDescent = fallback.Descent;
                Height = fallback.LineSpacing;
                return;
            }

            for (int index = 0; index < Count; index++)
            {
                Piece run = this[index];

                if (run.Inline is not null)
                    continue;

                // As tall as the faces the piece is set in, fallbacks included, so that a script the style's face
                // lacks, drawn from a face that reaches further, does not overlap the lines around it.
                TypeMetrics metrics = measurer.GetMetrics(run.Characters, run.Style);
                float offset = run.Style.BaselineOffset;

                // A superscript has a negative offset and so extends the line upwards; a subscript downwards.
                Ascent = Math.Max(Ascent, metrics.Ascent - Math.Min(0, offset));
                Descent = Math.Max(Descent, metrics.Descent + Math.Max(0, offset));
                Height = Math.Max(Height, metrics.LineSpacing * run.Style.Leading);
            }

            TypeAscent = Ascent;
            TypeDescent = Descent;

            // Frames go in once the type is known, since all but those on the baseline are placed against it.
            for (int index = 0; index < Count; index++)
            {
                Piece run = this[index];

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
        public float InlineTop(Piece run) => run.Position switch
        {
            InlinePosition.BelowBaseline => 0f,
            InlinePosition.TextTop => -TypeAscent,
            InlinePosition.TextBottom => TypeDescent - run.Height,
            InlinePosition.Middle => (-(TypeAscent - TypeDescent) / 2) - (run.Height / 2),
            _ => -run.Height,
        };

        /// <summary>How far an inline frame reaches above the baseline and below it; either may be negative.</summary>
        private (float Above, float Below) Reach(Piece run)
        {
            float top = InlineTop(run);
            return (-top, top + run.Height);
        }
    }
}
