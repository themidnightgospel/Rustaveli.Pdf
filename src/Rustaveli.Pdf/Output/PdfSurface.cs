using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Images;
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

    /// <summary>Control-point distance, as a fraction of the radius, of a cubic Bézier approximating a quarter circle.</summary>
    private const double Kappa = 0.5522847498307936;

    private readonly PdfDocumentWriter _writer;
    private readonly TypeShaper _shaper;
    private readonly FontEmbedder _fonts;
    private readonly ImageEmbedder _images;
    private readonly Dictionary<(string Name, InkModel Model, (float, float, float, float) Components), PdfReference> _separations = [];
    private readonly Stack<State> _saved = new Stack<State>();
    private byte[] _codes = new byte[128];
    private PdfPage? _page;
    private State _state;

    public PdfSurface(PdfDocumentWriter writer, TypeShaper shaper)
    {
        _writer = writer;
        _shaper = shaper;
        _fonts = new FontEmbedder(writer.File);
        _images = new ImageEmbedder(writer.File);
    }

    private PdfPage Page => _page ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");

    private ContentStreamBuilder Content => Page.Content;

    public void BeginPage(Extent size)
    {
        if (_page is not null)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        _page = _writer.BeginPage(size.Width, size.Height);
        _saved.Clear();

        Transform flip = new Transform(1, 0, 0, -1, 0, size.Height);
        _state = State.Initial(flip);
        Content.Transform(flip.A, flip.B, flip.C, flip.D, flip.E, flip.F);
    }

    public void EndPage()
    {
        _writer.EndPage(Page);
        _page = null;
    }

    /// <summary>Writes the fonts, now that every page has been drawn, and completes the file.</summary>
    public void Finish()
    {
        _fonts.WriteAll();
        _writer.Finish();
    }

    public void Save()
    {
        Content.SaveState();
        _saved.Push(_state);
    }

    public void Restore()
    {
        Content.RestoreState();
        _state = _saved.Pop();
    }

    // Blocks translate by their offsets whether or not those are zero; writing the identity would only add bytes.
    public void Translate(Offset offset)
    {
        if (offset.X != 0 || offset.Y != 0)
            Concatenate(new Transform(1, 0, 0, 1, offset.X, offset.Y));
    }

    public void Scale(float scaleX, float scaleY)
    {
        if (scaleX != 1 || scaleY != 1)
            Concatenate(new Transform(scaleX, 0, 0, scaleY, 0, 0));
    }

    public void Rotate(float degrees)
    {
        // In the flipped, Y-down space the engine draws in, this matrix turns clockwise.
        double radians = degrees * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        Concatenate(new Transform(cos, sin, -sin, cos, 0, 0));
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

        SetFill(color);
        Content.Rectangle(position.X, position.Y, size.Width, size.Height);
        Content.Fill();
    }

    public void DrawRoundedRectangle(Offset position, Extent size, float cornerRadius, Ink color, float strokeWidth = 0f)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        if (strokeWidth > 0)
        {
            SetStroke(color);
            SetLineWidth(strokeWidth);
        }
        else
        {
            SetFill(color);
        }

        // A radius beyond half the shorter side has no shape, so it is clamped.
        double radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(size.Width, size.Height) / 2));
        AppendRoundedRectangle(position.X, position.Y, size.Width, size.Height, radius);

        if (strokeWidth > 0)
            Content.Stroke();
        else
            Content.Fill();
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

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
                Save();
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
                Restore();
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

        SetFill(style.Ink);

        ContentStreamBuilder content = Content;
        content.BeginText();
        SetCharacterSpacing(style.Tracking);

        EmbeddedFont? current = null;
        int pending = 0;
        double pen = baselineStart.X;
        float previousAdvance = 0f;
        float previousExtra = 0f;
        bool first = true;

        foreach (ShapedGlyph glyph in _shaper.Walk(text.AsSpan(), style, rightToLeft))
        {
            // Beyond the widths and character spacing a reader applies itself: kerning, and word spacing after a space.
            float adjustment = glyph.Kerning + previousExtra;

            if (!first)
                pen += previousAdvance + style.Tracking + adjustment;

            EmbeddedFont font = _fonts.For(glyph.Face);

            if (!ReferenceEquals(font, current))
            {
                // A face change starts a new array placed at the pen, so a fallback run cannot drift from layout.
                if (current is not null)
                {
                    Flush(content, ref pending);
                    content.EndTextArray();
                }

                content.SetFont(Page.Resources.GetFontName(font.Reference), size);
                content.SetTextMatrix(1, 0, 0, -1, pen, baselineStart.Y);
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

        PdfName name = Page.Resources.GetXObjectName(_images.Reference(raster));
        Transform placement = Placement(raster.Orientation, size.Width, size.Height);

        ContentStreamBuilder content = Content;
        content.SaveState();
        content.Transform(placement.A, placement.B, placement.C, placement.D, placement.E, placement.F);
        content.PaintXObject(name);
        content.RestoreState();
    }

    public void DrawExternalLink(string url, Extent size)
    {
        if (string.IsNullOrEmpty(url))
            return;

        Page.AddUriLink(PageArea(size), url);
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        Page.AddDestinationLink(PageArea(size), destinationName);
    }

    public void DrawDestination(string destinationName)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        (double x, double y) = _state.Matrix.Apply(0, 0);

        // The first anchor of a name wins, as it does for a reader following the link.
        _writer.AddNamedDestination(destinationName, Page.Reference, x, y);
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

    private void AppendRoundedRectangle(double x, double y, double width, double height, double radius)
    {
        ContentStreamBuilder content = Content;

        if (radius <= 0)
        {
            content.Rectangle(x, y, width, height);
            return;
        }

        double k = radius * Kappa;
        double right = x + width;
        double bottom = y + height;

        content.MoveTo(x + radius, y);
        content.LineTo(right - radius, y);
        content.CurveTo(right - radius + k, y, right, y + radius - k, right, y + radius);
        content.LineTo(right, bottom - radius);
        content.CurveTo(right, bottom - radius + k, right - radius + k, bottom, right - radius, bottom);
        content.LineTo(x + radius, bottom);
        content.CurveTo(x + radius - k, bottom, x, bottom - radius + k, x, bottom - radius);
        content.LineTo(x, y + radius);
        content.CurveTo(x, y + radius - k, x + radius - k, y, x + radius, y);
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

    private void SetFill(Ink ink)
    {
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

        switch (ink.Model)
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
