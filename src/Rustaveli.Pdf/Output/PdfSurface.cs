using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Text;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Draws pages straight into a PDF: the engine's surface over the managed writer.
/// </summary>
/// <remarks>
/// <para>
/// The engine draws with the origin at the top left and Y running down; PDF puts the origin at the bottom left with
/// Y running up. Each page therefore begins by flipping its coordinate system, and text and images are flipped back
/// where they are placed so they read the right way up. The surface tracks the resulting transform itself, because
/// link rectangles and anchors are written in the page's own space, not the drawing's.
/// </para>
/// <para>
/// Text is set through the same <see cref="GlyphWalk"/> that measured it, so every glyph lands where layout
/// reserved its space: the font's widths position glyphs, character spacing adds tracking, and each kerning pair
/// is an adjustment in the text array.
/// </para>
/// <para>
/// Colour and opacity are written only when they change, and the surface remembers them across saved states just
/// as the graphics state does.
/// </para>
/// </remarks>
internal sealed class PdfSurface : IPageSink
{
    private static readonly PdfName Separation = new PdfName("Separation");
    private static readonly PdfName DeviceRgb = new PdfName("DeviceRGB");
    private static readonly PdfName DeviceCmyk = new PdfName("DeviceCMYK");
    private static readonly PdfName FunctionType = new PdfName("FunctionType");
    private static readonly PdfName Domain = new PdfName("Domain");
    private static readonly PdfName C0 = new PdfName("C0");
    private static readonly PdfName C1 = new PdfName("C1");
    private static readonly PdfName Artifact = new PdfName("Artifact");
    private static readonly PdfName StructParents = new PdfName("StructParents");
    private static readonly PdfName StructParent = new PdfName("StructParent");
    private static readonly PdfName Tabs = new PdfName("Tabs");

    /// <summary>Control-point distance, as a fraction of the radius, of a cubic Bézier approximating a quarter circle.</summary>
    private const double Kappa = 0.5522847498307936;

    private readonly PdfDocumentWriter _writer;
    private readonly TypeShaper _shaper;
    private readonly FontEmbedder _fonts;
    private readonly ImageEmbedder _images;
    private readonly ImageAdjuster _adjuster;

    /// <summary>
    /// Whether the file is PDF/A: every ink is written as RGB, as its sRGB output intent needs, and nothing asks a viewer
    /// to smooth an image.
    /// </summary>
    private readonly bool _archival;
    private readonly Dictionary<(string Name, InkModel Model, (float, float, float, float) Components), PdfReference> _separations = [];
    private readonly Dictionary<(Extent Size, Corners Corners, float Deviation, Ink Ink), (PdfReference Image, ShadowMask Mask)> _shadows = [];
    private readonly Stack<State> _saved = new Stack<State>();
    private byte[] _codes = new byte[128];
    private PdfPage? _page;
    private float _pageHeight;
    private State _state;

    /// <summary>The pattern fills and strokes are painted with instead of their ink, and its opacity, while set.</summary>
    private (PdfName Pattern, float Opacity)? _gradient;

    /// <summary>Whether the gradient begun could not be placed, so that fills and strokes until it ends are left out.</summary>
    private bool _unplacedGradient;

    /// <summary>Whether content is marked for the structure tree, as a tagged PDF needs.</summary>
    private readonly bool _tagging;
    private readonly Dictionary<string, PdfName> _roles = new Dictionary<string, PdfName>(StringComparer.Ordinal);

    /// <summary>
    /// What leads back from content to its element: each page's elements by marked-content number, and each
    /// annotation's element, under keys shared between them.
    /// </summary>
    private readonly Dictionary<int, object> _parents = [];
    private int _nextParentKey;

    /// <summary>The document every element descends from, once content has been tagged.</summary>
    private StructureElement? _root;

    /// <summary>The element what is drawn next belongs to, or null for decoration.</summary>
    private StructureElement? _tag;

    /// <summary>Whether a marked sequence is open, and the element it marks, null for decoration.</summary>
    private bool _marking;
    private StructureElement? _markedFor;

    /// <summary>The open page's elements, by the number its marked content is known by.</summary>
    private List<StructureElement>? _pageMarks;

    public PdfSurface(PdfDocumentWriter writer, TypeShaper shaper, PdfExportOptions? options = null)
    {
        _writer = writer;
        _shaper = shaper;
        _fonts = new FontEmbedder(writer.File, options?.KeepFontHinting == true);
        _images = new ImageEmbedder(writer.File);
        _adjuster = new ImageAdjuster(options);
        _archival = options?.Conformance is { } conformance && conformance != PdfAConformance.None;
        _tagging = options?.WritesStructure == true;
    }

    private PdfPage Page => _page ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");

    private ContentStreamBuilder Content => Page.Content;

    public void BeginPage(Extent size)
    {
        if (_page is not null)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        _page = _writer.BeginPage(size.Width, size.Height);
        _pageHeight = size.Height;
        _saved.Clear();

        Transform flip = new Transform(1, 0, 0, -1, 0, size.Height);
        _state = State.Initial(flip);
        Content.Transform(flip.A, flip.B, flip.C, flip.D, flip.E, flip.F);
    }

    public void EndPage()
    {
        PdfPage page = Page;
        EndMark();

        if (_tagging)
        {
            if (_pageMarks is { } marks)
            {
                page.Entries[StructParents] = _nextParentKey;
                _parents.Add(_nextParentKey++, marks);
            }

            // Links are visited in the order they are read.
            page.Entries[Tabs] = PdfNames.S;
            _pageMarks = null;
        }

        _writer.EndPage(page);
        _page = null;
        _gradient = null;
    }

    /// <summary>Writes the fonts and the structure, now that every page has been drawn, and completes the file.</summary>
    public void Finish()
    {
        _fonts.WriteAll();

        if (_tagging && _root is not null)
            StructureTree.Write(_writer, _root, _parents, _nextParentKey);

        _writer.Finish();
    }

    // A marked sequence never straddles the states blocks save and restore, so each nests properly within them.
    public void Save()
    {
        EndMark();
        Push();
    }

    public void Restore()
    {
        EndMark();
        Pop();
    }

    public void Tag(StructureElement? element)
    {
        _tag = element;

        if (_root is null && element is not null)
        {
            StructureElement root = element;
            while (root.Parent is { } parent)
                root = parent;

            _root = root;
        }
    }

    private void Push()
    {
        Content.SaveState();
        _saved.Push(_state);
    }

    private void Pop()
    {
        Content.RestoreState();
        _state = _saved.Pop();
    }

    /// <summary>
    /// Marks what is about to be drawn: as the content of the element in force, for text, or for anything in an
    /// illustration; as decoration otherwise. A sequence already open for the same owner goes on.
    /// </summary>
    private void Mark(bool text)
    {
        if (!_tagging)
            return;

        StructureElement? owner = _tag is { } element && (text || element.IsIllustration) ? element : null;

        if (_marking && ReferenceEquals(owner, _markedFor))
            return;

        EndMark();
        ContentStreamBuilder content = Content;

        if (owner is null)
        {
            content.BeginMarkedContent(Artifact);
        }
        else
        {
            List<StructureElement> marks = _pageMarks ??= [];
            int identifier = marks.Count;
            marks.Add(owner);
            owner.Kids.Add(new MarkedContentReference(Page.Reference, identifier));

            if (!_roles.TryGetValue(owner.Role, out PdfName? role))
            {
                role = new PdfName(owner.Role);
                _roles.Add(owner.Role, role);
            }

            content.BeginMarkedContent(role, identifier);
        }

        _marking = true;
        _markedFor = owner;
    }

    private void EndMark()
    {
        if (!_marking)
            return;

        Content.EndMarkedContent();
        _marking = false;
    }

    /// <summary>
    /// Entries placing a link annotation in the structure, in the link element it belongs to — the one in force, or a new
    /// one — or null when the document is not tagged.
    /// </summary>
    private (PdfDictionary Entries, StructureElement Link)? LinkEntries(string description)
    {
        if (!_tagging || _root is null)
            return null;

        StructureElement link = _tag is { Role: "Link" } current ? current : new StructureElement("Link", _tag ?? _root);

        if (!ReferenceEquals(link, _tag))
            link.Parent!.Kids.Add(link);

        int key = _nextParentKey++;
        _parents.Add(key, link);

        PdfDictionary entries = new PdfDictionary
        {
            [StructParent] = key,
            [PdfNames.Contents] = PdfString.FromText(description),
        };

        return (entries, link);
    }

    // Blocks translate by their offsets whether or not those are zero; writing the identity would only add bytes.
    public Offset Origin
    {
        get
        {
            // The page's own space runs Y up from the bottom; the engine's runs down from the top.
            (double x, double y) = _state.Matrix.Apply(0, 0);
            return new Offset((float)x, (float)(_pageHeight - y));
        }
    }

    public void Translate(Offset offset)
    {
        if (offset.X != 0 || offset.Y != 0)
            Concatenate(Transform.Translation(offset.X, offset.Y));
    }

    public void Scale(float scaleX, float scaleY)
    {
        if (scaleX != 1 || scaleY != 1)
            Concatenate(Transform.Scaling(scaleX, scaleY));
    }

    // In the flipped, Y-down space the engine draws in, this matrix turns clockwise.
    public void Rotate(float degrees) => Concatenate(Transform.Rotation(degrees));

    public void Concatenate(float a, float b, float c, float d, float e, float f) => Concatenate(new Transform(a, b, c, d, e, f));

    public void FillPath(VectorPath path, Ink ink, FillRule rule)
    {
        if (ink.IsTransparent || path.IsEmpty || _unplacedGradient)
            return;

        Mark(text: false);

        SetFill(ink);
        AppendPath(path);

        if (rule == FillRule.EvenOdd)
            Content.FillEvenOdd();
        else
            Content.Fill();
    }

    public void StrokePath(VectorPath path, Ink ink, LineStyle style)
    {
        if (ink.IsTransparent || path.IsEmpty || style.Weight <= 0 || _unplacedGradient)
            return;

        Mark(text: false);

        SetStroke(ink);

        // Caps, joins and dashes are graphics state that nothing else sets, so they are scoped to this stroke.
        Push();
        ContentStreamBuilder content = Content;
        SetLineWidth(style.Weight);

        if (style.Cap != LineCap.Butt)
            content.SetLineCap(style.Cap == LineCap.Round ? PdfLineCap.Round : PdfLineCap.ProjectingSquare);

        if (style.Join != LineJoin.Miter)
            content.SetLineJoin(style.Join == LineJoin.Round ? PdfLineJoin.Round : PdfLineJoin.Bevel);

        if (style.MiterLimit != 10)
            content.SetMiterLimit(Math.Max(1, style.MiterLimit));

        if (style.Dashes is { Count: > 0 } dashes && dashes.Any(length => length > 0))
        {
            double[] pattern = new double[dashes.Count];
            for (int index = 0; index < pattern.Length; index++)
                pattern[index] = Math.Max(0, dashes[index]);

            content.SetDashPattern(pattern, style.DashOffset);
        }

        AppendPath(path);
        content.Stroke();
        Pop();
    }

    public void ClipPath(VectorPath path, FillRule rule)
    {
        ContentStreamBuilder content = Content;

        // An empty path encloses nothing, so nothing drawn after it shows.
        if (path.IsEmpty)
            content.Rectangle(0, 0, 0, 0);
        else
            AppendPath(path);

        if (rule == FillRule.EvenOdd)
            content.ClipEvenOdd();
        else
            content.Clip();

        content.EndPath();
    }

    private void AppendPath(VectorPath path)
    {
        ContentStreamBuilder content = Content;
        IReadOnlyList<Offset> points = path.Points;
        int point = 0;

        foreach (PathVerb verb in path.Verbs)
        {
            switch (verb)
            {
                case PathVerb.Move:
                    content.MoveTo(points[point].X, points[point].Y);
                    point++;
                    break;

                case PathVerb.Line:
                    content.LineTo(points[point].X, points[point].Y);
                    point++;
                    break;

                case PathVerb.Cubic:
                    content.CurveTo(points[point].X, points[point].Y, points[point + 1].X, points[point + 1].Y, points[point + 2].X, points[point + 2].Y);
                    point += 3;
                    break;

                default:
                    content.ClosePath();
                    break;
            }
        }
    }

    public void ClipRectangle(Extent size)
    {
        ContentStreamBuilder content = Content;
        content.Rectangle(0, 0, size.Width, size.Height);
        content.Clip();
        content.EndPath();
    }

    public void DrawRectangle(Offset position, Extent size, Ink color)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        Mark(text: false);

        SetFill(color);
        Content.Rectangle(position.X, position.Y, size.Width, size.Height);
        Content.Fill();
    }

    public void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        Mark(text: false);

        if (strokeWidth > 0)
        {
            SetStroke(color);
            SetLineWidth(strokeWidth);
        }
        else
        {
            SetFill(color);
        }

        AppendRoundedRectangle(position.X, position.Y, size.Width, size.Height, corners.FittedTo(size));

        if (strokeWidth > 0)
            Content.Stroke();
        else
            Content.Fill();
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        Mark(text: false);

        SetStroke(color);
        ContentStreamBuilder content = Content;

        switch (style)
        {
            case StrokeStyle.Double:
                Offset shift = StrokeGeometry.DoubleOffset(from, to, thickness);
                SetLineWidth(thickness);
                StrokeSegment(from + shift, to + shift);
                StrokeSegment(from + shift.Reverse(), to + shift.Reverse());
                break;

            case StrokeStyle.Dotted or StrokeStyle.Dashed:
                // Caps and dashes are graphics state that nothing else sets, so they are scoped to this line.
                Push();
                SetLineWidth(thickness);

                if (style == StrokeStyle.Dotted)
                {
                    // A dash of no length with a round cap is a dot as wide as the stroke.
                    content.SetLineCap(PdfLineCap.Round);
                    content.SetDashPattern([0, thickness * 2], 0);
                }
                else
                {
                    content.SetDashPattern([thickness * 3, thickness * 2], 0);
                }

                StrokeSegment(from, to);
                Pop();
                break;

            case StrokeStyle.Wavy:
                SetLineWidth(thickness);
                content.MoveTo(from.X, from.Y);
                foreach (CubicSegment segment in StrokeGeometry.Wave(from, to, thickness))
                    content.CurveTo(segment.Control1.X, segment.Control1.Y, segment.Control2.X, segment.Control2.Y, segment.End.X, segment.End.Y);

                content.Stroke();
                break;

            default:
                SetLineWidth(thickness);
                StrokeSegment(from, to);
                break;
        }
    }

    public void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        Mark(text: false);

        double[] dashes = new double[pattern.Count];
        for (int index = 0; index < dashes.Length; index++)
            dashes[index] = pattern[index];

        SetStroke(color);

        // The dash pattern is graphics state that nothing else sets, so it is scoped to this line.
        Push();
        SetLineWidth(thickness);
        Content.SetDashPattern(dashes, 0);
        StrokeSegment(from, to);
        Pop();
    }

    private void StrokeSegment(Offset from, Offset to)
    {
        ContentStreamBuilder content = Content;
        content.MoveTo(from.X, from.Y);
        content.LineTo(to.X, to.Y);
        content.Stroke();
    }

    public void DrawText(string text, Offset baselineStart, TypeStyle style, bool rightToLeft = false)
    {
        float size = style.EffectivePointSize;
        if (string.IsNullOrEmpty(text) || style.Ink.IsTransparent || size <= 0)
            return;

        Mark(text: true);

        SetFill(style.Ink);

        ContentStreamBuilder content = Content;
        content.BeginText();
        SetCharacterSpacing(style.Tracking);

        EmbeddedFont? current = null;
        int pending = 0;
        double pen = baselineStart.X;
        float previousAdvance = 0f;
        float previousExtra = 0f;
        float previousShortfall = 0f;
        bool first = true;
        bool placed = false;

        foreach (ShapedGlyph glyph in _shaper.Walk(text.AsSpan(), style, rightToLeft))
        {
            // Beyond the widths and character spacing a reader applies itself: kerning, word spacing after a space,
            // and any difference between the advance the glyph was set with and the width the font declares for it.
            float adjustment = glyph.Kerning + previousExtra + previousShortfall;

            if (!first)
                pen += previousAdvance + style.Tracking + glyph.Kerning + previousExtra;

            EmbeddedFont font = _fonts.For(glyph.Face);
            bool displaced = glyph.XOffset != 0 || glyph.YOffset != 0;

            // A face change, and a glyph set off its pen position — a mark placed on its letter — or the glyph after
            // one, starts a new array placed exactly, so nothing drifts from where layout put it.
            if (!ReferenceEquals(font, current) || displaced || placed)
            {
                if (current is not null)
                {
                    Flush(content, ref pending);
                    content.EndTextArray();
                }

                if (!ReferenceEquals(font, current))
                    content.SetFont(Page.Resources.GetFontName(font.Reference), size);

                content.SetTextMatrix(1, 0, 0, -1, pen + glyph.XOffset, baselineStart.Y - glyph.YOffset);
                content.BeginTextArray();
                current = font;
            }
            else if (adjustment != 0)
            {
                Flush(content, ref pending);
                content.AppendAdjustment(-adjustment * 1000.0 / size);
            }

            ushort code = font.CodeFor(glyph);
            Buffer(ref pending, code);

            previousAdvance = glyph.Advance;
            previousExtra = glyph.Extra;
            previousShortfall = glyph.Advance - glyph.Face.GetAdvance(glyph.Glyph, size);
            placed = displaced;
            first = false;
        }

        Flush(content, ref pending);
        content.EndTextArray();
        content.EndText();
    }

    public void DrawImage(IImage image, Extent size)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image is not RasterImage raster)
        {
            throw new ArgumentException(
                $"Only images loaded with {nameof(RasterImage)} can be exported to PDF; this is a {image.GetType().Name}.",
                nameof(image));
        }

        if (size.Width <= 0 || size.Height <= 0)
            return;

        Mark(text: false);

        raster = _adjuster.Adjust(raster, size);
        PdfName name = Page.Resources.GetXObjectName(_images.Reference(raster));
        Transform placement = Placement(raster.Orientation, size.Width, size.Height);

        ContentStreamBuilder content = Content;
        content.SaveState();
        content.Transform(placement.A, placement.B, placement.C, placement.D, placement.E, placement.F);
        content.PaintXObject(name);
        content.RestoreState();
    }

    public void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow)
    {
        if (shadow.Ink.IsTransparent)
            return;

        (Offset at, Extent grown, Corners radii) = shadow.Shape(position, size, corners);

        if (grown.Width <= 0 || grown.Height <= 0)
            return;

        Mark(text: false);

        float deviation = shadow.Deviation;

        if (deviation <= 0)
        {
            DrawRoundedRectangle(at, grown, radii, shadow.Ink);
            return;
        }

        // PDF has no blur, so the shadow is an image in its ink, seen through a soft mask of its blurred coverage.
        // Identical shadows, such as those of a table's cells, share one image.
        Ink colour = shadow.Ink.WithOpacity(1);
        (Extent Size, Corners Corners, float Deviation, Ink Ink) key = (grown, radii, deviation, colour);

        if (!_shadows.TryGetValue(key, out (PdfReference Image, ShadowMask Mask) cast))
        {
            ShadowMask mask = ShadowMask.Create(grown, radii, deviation);
            cast = (ShadowImage.Write(_writer.File, mask, colour, _archival), mask);
            _shadows.Add(key, cast);
        }

        SetOpacity(shadow.Ink.Opacity, _state.StrokeAlpha);

        Extent span = cast.Mask.Size;
        float margin = cast.Mask.Margin;
        ContentStreamBuilder content = Content;
        content.SaveState();
        content.Transform(span.Width, 0, 0, -span.Height, at.X - margin, at.Y - margin + span.Height);
        content.PaintXObject(Page.Resources.GetXObjectName(cast.Image));
        content.RestoreState();
    }

    public void DrawExternalLink(string url, Extent size)
    {
        if (string.IsNullOrEmpty(url))
            return;

        (PdfDictionary Entries, StructureElement Link)? tagged = LinkEntries(url);
        PdfReference annotation = Page.AddUriLink(PageArea(size), url, tagged?.Entries);
        tagged?.Link.Kids.Add(new ObjectReference(annotation, Page.Reference));
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        (PdfDictionary Entries, StructureElement Link)? tagged = LinkEntries(destinationName);
        PdfReference annotation = Page.AddDestinationLink(PageArea(size), destinationName, tagged?.Entries);
        tagged?.Link.Kids.Add(new ObjectReference(annotation, Page.Reference));
    }

    public void DrawDestination(string destinationName)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        (double x, double y) = _state.Matrix.Apply(0, 0);

        // The first anchor of a name wins, as it does for a reader following the link.
        _writer.AddNamedDestination(destinationName, Page.Reference, x, y);
    }

    public void DrawBookmark(string title, int level)
    {
        (double x, double y) = _state.Matrix.Apply(0, 0);
        _writer.AddOutlineEntry(title, level, Page.Reference, x, y);
    }

    /// <summary>
    /// Ends a page still open. A render that fails part-way unwinds with one begun; ending it keeps the writer
    /// consistent for whatever the caller does next.
    /// </summary>
    public void Dispose()
    {
        if (_page is null)
            return;

        PdfPage page = _page;
        _page = null;

        try
        {
            _writer.EndPage(page);
        }
        catch (Exception)
        {
            // Already unwinding from an earlier failure; a second fault here would hide the first.
        }
    }

    /// <summary>
    /// Where a unit-square image goes so it fills the box upright: stored rows run down the page, and an EXIF
    /// orientation turns or mirrors the stored image into its upright form.
    /// </summary>
    internal static Transform Placement(ExifOrientation orientation, double width, double height) => orientation switch
    {
        ExifOrientation.FlipHorizontal => new Transform(-width, 0, 0, -height, width, height),
        ExifOrientation.Rotate180 => new Transform(-width, 0, 0, height, width, 0),
        ExifOrientation.FlipVertical => new Transform(width, 0, 0, height, 0, 0),
        ExifOrientation.Transpose => new Transform(0, height, -width, 0, width, 0),
        ExifOrientation.Rotate90 => new Transform(0, height, width, 0, 0, 0),
        ExifOrientation.Transverse => new Transform(0, -height, width, 0, 0, height),
        ExifOrientation.Rotate270 => new Transform(0, -height, -width, 0, width, height),
        _ => new Transform(width, 0, 0, -height, 0, height),
    };

    private void Concatenate(Transform transform)
    {
        Content.Transform(transform.A, transform.B, transform.C, transform.D, transform.E, transform.F);
        _state.Matrix = _state.Matrix.After(transform);
    }

    /// <summary>A rectangle at the current origin, in the page's own space, as annotations need it.</summary>
    private PdfRectangle PageArea(Extent size)
    {
        Transform matrix = _state.Matrix;
        (double x0, double y0) = matrix.Apply(0, 0);
        (double x1, double y1) = matrix.Apply(size.Width, 0);
        (double x2, double y2) = matrix.Apply(0, size.Height);
        (double x3, double y3) = matrix.Apply(size.Width, size.Height);

        return new PdfRectangle(
            Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)),
            Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)),
            Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)),
            Math.Max(Math.Max(y0, y1), Math.Max(y2, y3)));
    }

    /// <summary>
    /// A rectangle with each corner rounded to its own radius, as a quarter ellipse approximated by a cubic; square
    /// where a radius is zero, and a plain rectangle where all are.
    /// </summary>
    private void AppendRoundedRectangle(double x, double y, double width, double height, Corners radii)
    {
        ContentStreamBuilder content = Content;

        if (!radii.IsRounded)
        {
            content.Rectangle(x, y, width, height);
            return;
        }

        double right = x + width;
        double bottom = y + height;
        double topLeft = radii.TopLeft;
        double topRight = radii.TopRight;
        double bottomRight = radii.BottomRight;
        double bottomLeft = radii.BottomLeft;

        content.MoveTo(x + topLeft, y);
        content.LineTo(right - topRight, y);

        if (topRight > 0)
            content.CurveTo(right - topRight + (topRight * Kappa), y, right, y + topRight - (topRight * Kappa), right, y + topRight);

        content.LineTo(right, bottom - bottomRight);

        if (bottomRight > 0)
            content.CurveTo(right, bottom - bottomRight + (bottomRight * Kappa), right - bottomRight + (bottomRight * Kappa), bottom, right - bottomRight, bottom);

        content.LineTo(x + bottomLeft, bottom);

        if (bottomLeft > 0)
            content.CurveTo(x + bottomLeft - (bottomLeft * Kappa), bottom, x, bottom - bottomLeft + (bottomLeft * Kappa), x, bottom - bottomLeft);

        content.LineTo(x, y + topLeft);

        if (topLeft > 0)
            content.CurveTo(x, y + topLeft - (topLeft * Kappa), x + topLeft - (topLeft * Kappa), y, x + topLeft, y);

        content.ClosePath();
    }

    private void Buffer(ref int pending, ushort code)
    {
        if (pending + 2 > _codes.Length)
            Array.Resize(ref _codes, _codes.Length * 2);

        _codes[pending] = (byte)(code >> 8);
        _codes[pending + 1] = (byte)code;
        pending += 2;
    }

    /// <summary>Shows the codes buffered so far. Every caller has buffered at least one glyph since the last flush.</summary>
    private void Flush(ContentStreamBuilder content, ref int pending)
    {
        content.AppendText(_codes.AsSpan(0, pending));
        pending = 0;
    }

    public void BeginGradient(Gradient gradient, Offset position, Extent size)
    {
        // A pattern is placed in the page's own space, by the transform in force, which transforms that could each be
        // written may have multiplied beyond what can be. The gradient cannot be placed then, and what it would paint is
        // left out, since painting it in any ink would be wrong.
        if (!_state.Matrix.IsWritable)
        {
            _unplacedGradient = true;
            return;
        }

        (Offset start, Offset end) = gradient.Axis(position, size);
        PdfReference pattern = _writer.File.Write(GradientPattern.Create(gradient, start, end, _state.Matrix, _archival));

        _gradient = (Page.Resources.GetPatternName(pattern), gradient.Opacity);
    }

    public void EndGradient()
    {
        _gradient = null;
        _unplacedGradient = false;
    }

    private void SetFill(Ink ink)
    {
        if (_gradient is { } gradient)
        {
            Content.SetFillColorSpace(PdfNames.Pattern);
            Content.SetFillColorN([], gradient.Pattern);

            // The pattern replaced whatever colour was set, so the next ink is written again.
            _state.Fill = null;
            SetOpacity(gradient.Opacity, _state.StrokeAlpha);
            return;
        }

        Ink color = ink.WithOpacity(1);
        if (_state.Fill != color)
        {
            WriteColor(color, stroke: false);
            _state.Fill = color;
        }

        SetOpacity(ink.Opacity, _state.StrokeAlpha);
    }

    private void SetStroke(Ink ink)
    {
        if (_gradient is { } gradient)
        {
            Content.SetStrokeColorSpace(PdfNames.Pattern);
            Content.SetStrokeColorN([], gradient.Pattern);
            _state.Stroke = null;
            SetOpacity(_state.FillAlpha, gradient.Opacity);
            return;
        }

        Ink color = ink.WithOpacity(1);
        if (_state.Stroke != color)
        {
            WriteColor(color, stroke: true);
            _state.Stroke = color;
        }

        SetOpacity(_state.FillAlpha, ink.Opacity);
    }

    private void SetOpacity(double fill, double stroke)
    {
        if (fill == _state.FillAlpha && stroke == _state.StrokeAlpha)
            return;

        Content.SetGraphicsState(Page.Resources.GetExtGStateName(_writer.GetOpacityState(fill, stroke)));
        _state.FillAlpha = fill;
        _state.StrokeAlpha = stroke;
    }

    private void SetLineWidth(double width)
    {
        if (width == _state.LineWidth)
            return;

        Content.SetLineWidth(width);
        _state.LineWidth = width;
    }

    private void SetCharacterSpacing(double spacing)
    {
        if (spacing == _state.CharacterSpacing)
            return;

        Content.SetCharacterSpacing(spacing);
        _state.CharacterSpacing = spacing;
    }

    private void WriteColor(Ink ink, bool stroke)
    {
        ContentStreamBuilder content = Content;

        // Under PDF/A every ink is written as the sRGB its output intent names; the model is kept only otherwise.
        switch (_archival ? InkModel.Rgb : ink.Model)
        {
            case InkModel.Cmyk:
                (float cyan, float magenta, float yellow, float black) = ink.ToCmyk();
                if (stroke)
                    content.SetStrokeCmyk(cyan, magenta, yellow, black);
                else
                    content.SetFillCmyk(cyan, magenta, yellow, black);
                break;

            case InkModel.Spot:
                PdfName space = Page.Resources.GetColorSpaceName(SeparationFor(ink));
                ReadOnlySpan<double> tint = [ink.SpotTint];
                if (stroke)
                {
                    content.SetStrokeColorSpace(space);
                    content.SetStrokeColor(tint);
                }
                else
                {
                    content.SetFillColorSpace(space);
                    content.SetFillColor(tint);
                }

                break;

            default:
                (float red, float green, float blue) = ink.ToRgb();
                if (stroke)
                    content.SetStrokeRgb(red, green, blue);
                else
                    content.SetFillRgb(red, green, blue);
                break;
        }
    }

    /// <summary>
    /// The separation colour space of a spot ink, written once per ink: its own plate, with a tint transform to
    /// the process fallback for devices that cannot print it.
    /// </summary>
    private PdfReference SeparationFor(Ink ink)
    {
        (string, InkModel, (float, float, float, float)) key = (ink.SpotName!, ink.FallbackModel, ink.Components);

        if (_separations.TryGetValue(key, out PdfReference existing))
            return existing;

        (float first, float second, float third, float fourth) = ink.Components;
        bool cmyk = ink.FallbackModel == InkModel.Cmyk;

        PdfDictionary tintTransform = new PdfDictionary
        {
            [FunctionType] = 2,
            [Domain] = new PdfArray(2) { 0, 1 },
            [C0] = cmyk ? new PdfArray(4) { 0, 0, 0, 0 } : new PdfArray(3) { 1, 1, 1 },
            [C1] = cmyk ? new PdfArray(4) { first, second, third, fourth } : new PdfArray(3) { first, second, third },
            [PdfNames.N] = 1,
        };

        PdfReference reference = _writer.File.Write(new PdfArray(4)
        {
            Separation,
            new PdfName(ink.SpotName!),
            cmyk ? DeviceCmyk : DeviceRgb,
            tintTransform,
        });

        _separations.Add(key, reference);
        return reference;
    }

    /// <summary>What the content stream's graphics state holds, as far as this surface has set it.</summary>
    private struct State
    {
        public Transform Matrix;
        public Ink? Fill;
        public Ink? Stroke;
        public double FillAlpha;
        public double StrokeAlpha;
        public double LineWidth;
        public double CharacterSpacing;

        public static State Initial(Transform matrix) => new State
        {
            Matrix = matrix,
            FillAlpha = 1,
            StrokeAlpha = 1,
            LineWidth = 1,
            CharacterSpacing = 0,
        };
    }
}
