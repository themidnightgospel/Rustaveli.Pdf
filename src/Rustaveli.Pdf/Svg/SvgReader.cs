using System.Xml;
using System.Xml.Linq;
using Matrix = (float A, float B, float C, float D, float E, float F);

namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads an SVG document into <see cref="Artwork"/>: shapes and paths filled and stroked, with the transforms, clip
/// paths, linear gradients, text, embedded images and styles — presentation attributes, <c>style</c> and style
/// sheets — that drawing tools write.
/// </summary>
/// <remarks>
/// Radial gradients are drawn in the mean of their colours; filters, masks, patterns and markers are left out, and
/// animation is not played. Opacity is carried down to each fill and stroke rather than blending a group as one.
/// </remarks>
internal sealed class SvgReader
{
    private const int DeepestNesting = 64;

    private static readonly HashSet<string> Inherited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-opacity", "stroke-linecap",
        "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "color", "font-size",
        "font-family", "font-weight", "font-style", "visibility", "clip-rule", "text-anchor",
    };

    private static readonly HashSet<string> Properties = new HashSet<string>(Inherited, StringComparer.OrdinalIgnoreCase)
    {
        "opacity", "display", "clip-path", "stop-color", "stop-opacity",
    };

    private static readonly HashSet<string> NotDrawn = new HashSet<string>(StringComparer.Ordinal)
    {
        "defs", "title", "desc", "metadata", "style", "linearGradient", "radialGradient", "clipPath", "mask",
        "symbol", "pattern", "marker", "filter", "script",
    };

    private readonly Dictionary<string, XElement> _byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
    private readonly SvgStyleSheet _sheet = new SvgStyleSheet();
    private readonly HashSet<XElement> _using = [];

    private SvgReader(XElement root)
    {
        foreach (XElement element in root.DescendantsAndSelf())
        {
            if ((string?)element.Attribute("id") is { Length: > 0 } id && !_byId.ContainsKey(id))
                _byId.Add(id, element);

            if (element.Name.LocalName == "style")
                _sheet.Add(element.Value);
        }
    }

    /// <summary>The artwork <paramref name="svg"/> draws.</summary>
    /// <exception cref="FormatException">The text is not an SVG document.</exception>
    public static Artwork Read(TextReader svg)
    {
        XDocument document;

        try
        {
            // Entities declared in the document are expanded, as illustration tools write them; nothing is fetched.
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null,
                MaxCharactersFromEntities = 10_000_000,
            };

            using XmlReader reader = XmlReader.Create(svg, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new FormatException("The SVG is not well-formed XML: " + exception.Message, exception);
        }

        XElement root = document.Root!;

        if (root.Name.LocalName != "svg")
            throw new FormatException($"An SVG document starts with an <svg> element, not <{root.Name.LocalName}>.");

        return new SvgReader(root).Draw(root);
    }

    private Artwork Draw(XElement root)
    {
        float[]? viewBox = ViewBox(root);
        float width = SvgLength.Read((string?)root.Attribute("width"), viewBox?[2] ?? 300, viewBox?[2] ?? 300);
        float height = SvgLength.Read((string?)root.Attribute("height"), viewBox?[3] ?? 150, viewBox?[3] ?? 150);

        if (!(width > 0) || !(height > 0))
            throw new FormatException("The SVG has no size to draw at.");

        Dictionary<string, string> style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return Artwork.Draw(width * SvgLength.PointsPerPixel, height * SvgLength.PointsPerPixel, art =>
        {
            // User units are CSS pixels; the artwork is measured in points.
            art.Scale(SvgLength.PointsPerPixel, SvgLength.PointsPerPixel);

            if (MapViewBox(art, root, viewBox, width, height))
                Render(root, style, 1, art, 0, children: true);
        });
    }

    private void Render(XElement element, Dictionary<string, string> parent, float parentOpacity, ArtworkComposer art, int depth, bool children = false)
    {
        if (depth > DeepestNesting)
            return;

        string name = element.Name.LocalName;

        if (!children && NotDrawn.Contains(name))
            return;

        Dictionary<string, string> declared = Declared(element);

        if (Value(declared, "display") == "none")
            return;

        Dictionary<string, string> style = new Dictionary<string, string>(parent, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> property in declared)
        {
            if (Inherited.Contains(property.Key) && property.Value != "inherit")
                style[property.Key] = property.Value;
        }

        float opacity = parentOpacity * Opacity(Value(declared, "opacity"));
        Matrix? transform = SvgTransform.Read((string?)element.Attribute("transform"));
        string? clip = Value(declared, "clip-path");
        VectorPath? clipPath = clip is null ? null : ClipPathOf(clip, depth);

        // An element whose transform or clip reaches beyond what a PDF can write is left out.
        if ((transform is { } matrix && !SvgTransform.IsWritable(matrix)) || clipPath is { IsWritable: false })
            return;

        bool scoped = transform is not null || clipPath is not null || name == "svg" && depth > 0 || name == "use";

        if (scoped)
            art.SaveState();

        if (transform is { } writable)
            art.Transform(writable.A, writable.B, writable.C, writable.D, writable.E, writable.F);

        if (clipPath is not null)
            art.Clip(clipPath, Value(style, "clip-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero);

        switch (name)
        {
            case "svg" when depth > 0:
                NestedViewport(element, style, opacity, art, depth);
                break;

            // A symbol is only reached through a use, which draws it as a group.
            case "svg":
            case "symbol":
            case "g":
            case "a":
            case "switch":
                foreach (XElement child in element.Elements())
                    Render(child, style, opacity, art, depth + 1);

                break;

            case "use":
                Use(element, style, opacity, art, depth);
                break;

            case "text":
                Text(element, style, opacity, art);
                break;

            case "image":
                Image(element, art);
                break;

            default:
                if (Shape(element) is { } path)
                    Paint(path, style, opacity, art, open: name is "line" or "polyline");

                break;
        }

        if (scoped)
            art.RestoreState();
    }

    private void NestedViewport(XElement element, Dictionary<string, string> style, float opacity, ArtworkComposer art, int depth)
    {
        float x = SvgLength.Read((string?)element.Attribute("x"), 0, 0);
        float y = SvgLength.Read((string?)element.Attribute("y"), 0, 0);
        float[]? viewBox = ViewBox(element);
        float width = SvgLength.Read((string?)element.Attribute("width"), viewBox?[2] ?? 100, viewBox?[2] ?? 100);
        float height = SvgLength.Read((string?)element.Attribute("height"), viewBox?[3] ?? 100, viewBox?[3] ?? 100);

        art.Translate(x, y);
        art.Clip(new VectorPath().AddRectangle(0, 0, width, height));

        if (!MapViewBox(art, element, viewBox, width, height))
            return;

        foreach (XElement child in element.Elements())
            Render(child, style, opacity, art, depth + 1);
    }

    private void Use(XElement element, Dictionary<string, string> style, float opacity, ArtworkComposer art, int depth)
    {
        if (Referenced(element) is not { } target || !_using.Add(target))
            return;

        art.Translate(SvgLength.Read((string?)element.Attribute("x"), 0, 0), SvgLength.Read((string?)element.Attribute("y"), 0, 0));

        // A symbol is drawn as a group of its children; anything else as itself.
        if (target.Name.LocalName == "symbol")
        {
            if (MapViewBox(art, target, ViewBox(target), SvgLength.Read((string?)element.Attribute("width"), 100, ViewBox(target)?[2] ?? 100), SvgLength.Read((string?)element.Attribute("height"), 100, ViewBox(target)?[3] ?? 100)))
                Render(target, style, opacity, art, depth + 1, children: true);
        }
        else
        {
            Render(target, style, opacity, art, depth + 1);
        }

        _using.Remove(target);
    }

    private void Text(XElement element, Dictionary<string, string> style, float opacity, ArtworkComposer art)
    {
        string content = string.Join(" ", element.DescendantNodes().OfType<XText>().Select(text => text.Value)
            .SelectMany(text => text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)));

        if (content.Length == 0 || Value(style, "visibility") is "hidden" or "collapse")
            return;

        Ink? ink = SolidPaint(Value(style, "fill") ?? "black", style);

        if (ink is not { } fill)
            return;

        float size = SvgLength.Read(Value(style, "font-size"), 16, 16);
        string[] families = (Value(style, "font-family") ?? "sans-serif").Split(',').Select(family => family.Trim(' ', '"', '\'')).Where(family => family.Length > 0).ToArray();
        TypeStyle type = TypeStyle.Default
            .WithTypeface(families.Length > 0 ? families[0] : "sans-serif", families.Skip(1).ToArray())
            .WithPointSize(size)
            .WithInk(fill.WithOpacity(fill.Opacity * opacity * Opacity(Value(style, "fill-opacity"))));

        string weight = Value(style, "font-weight") ?? "normal";
        if (weight is "bold" or "bolder" || (int.TryParse(weight, out int numeric) && numeric >= 600))
            type = type.Bold();

        if (Value(style, "font-style") is "italic" or "oblique")
            type = type.Italic();

        TextAnchor anchor = Value(style, "text-anchor") switch
        {
            "middle" => TextAnchor.Middle,
            "end" => TextAnchor.End,
            _ => TextAnchor.Start,
        };

        art.Text(content, First(element, "x"), First(element, "y"), type, anchor);
    }

    private static void Image(XElement element, ArtworkComposer art)
    {
        string? href = Href(element);
        float width = SvgLength.Read((string?)element.Attribute("width"), 0, 0);
        float height = SvgLength.Read((string?)element.Attribute("height"), 0, 0);

        // Only images carried in the document are drawn; nothing is fetched from elsewhere.
        if (href is null || !href.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || !(width > 0) || !(height > 0))
            return;

        int comma = href.IndexOf(',');
        if (comma < 0 || href.Substring(0, comma).IndexOf(";base64", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        try
        {
            RasterImage image = RasterImage.FromBytes(Convert.FromBase64String(href.Substring(comma + 1).Trim()));
            art.Image(image, SvgLength.Read((string?)element.Attribute("x"), 0, 0), SvgLength.Read((string?)element.Attribute("y"), 0, 0), width, height);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or NotSupportedException or InvalidDataException)
        {
            // An image that cannot be read is left out, as a browser leaves it.
        }
    }

    private void Paint(VectorPath path, Dictionary<string, string> style, float opacity, ArtworkComposer art, bool open)
    {
        // A shape reaching beyond what a PDF can write, as a circle about a centre near that does, is left out.
        if (Value(style, "visibility") is "hidden" or "collapse" || !path.IsWritable)
            return;

        // A line or polyline has no inside to fill.
        if (!open)
        {
            FillRule rule = Value(style, "fill-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero;
            float fillOpacity = opacity * Opacity(Value(style, "fill-opacity"));

            switch (Resolve(Value(style, "fill") ?? "black", style, fillOpacity))
            {
                case Ink ink:
                    art.Fill(path, ink, rule);
                    break;

                case Gradient gradient when ArtworkComposer.CanLay(gradient, path):
                    art.Fill(path, gradient, rule);
                    break;
            }
        }

        float weight = SvgLength.Read(Value(style, "stroke-width"), 100, 1);

        if (!(weight > 0))
            return;

        LineStyle line = new LineStyle(
            weight,
            Value(style, "stroke-linecap") switch { "round" => LineCap.Round, "square" => LineCap.Square, _ => LineCap.Butt },
            Value(style, "stroke-linejoin") switch { "round" => LineJoin.Round, "bevel" => LineJoin.Bevel, _ => LineJoin.Miter },
            Fraction(Value(style, "stroke-miterlimit"), 4),
            Dashes(Value(style, "stroke-dasharray")),
            SvgLength.Read(Value(style, "stroke-dashoffset"), 100, 0));
        float strokeOpacity = opacity * Opacity(Value(style, "stroke-opacity"));

        switch (Resolve(Value(style, "stroke") ?? "none", style, strokeOpacity))
        {
            case Ink ink:
                art.Stroke(path, ink, line);
                break;

            case Gradient gradient when ArtworkComposer.CanLay(gradient, path):
                art.Stroke(path, gradient, line);
                break;
        }
    }

    /// <summary>What a fill or stroke is painted with: an ink, a gradient, or nothing.</summary>
    private object? Resolve(string paint, Dictionary<string, string> style, float opacity)
    {
        string value = paint.Trim();

        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            int close = value.IndexOf(')');
            string id = close < 0 ? string.Empty : value.Substring(4, close - 4).Trim(' ', '"', '\'').TrimStart('#');
            string fallback = close < 0 ? string.Empty : value.Substring(close + 1).Trim();

            if (_byId.TryGetValue(id, out XElement? server))
            {
                object? painted = server.Name.LocalName switch
                {
                    "linearGradient" => Linear(server, opacity),
                    "radialGradient" => Mean(Stops(server, opacity)),
                    _ => null,
                };

                if (painted is not null)
                    return painted;
            }

            return fallback.Length > 0 ? Resolve(fallback, style, opacity) : null;
        }

        return SolidPaint(value, style) is { } ink ? ink.WithOpacity(ink.Opacity * opacity) : null;
    }

    private static Ink? SolidPaint(string paint, Dictionary<string, string> style)
    {
        string value = paint.Trim();

        if (value == "none")
            return null;

        if (value.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            return SvgColour.Read(Value(style, "color")) ?? Ink.Rgb(0, 0, 0);

        return SvgColour.Read(value);
    }

    private object? Linear(XElement element, float opacity)
    {
        List<GradientStop> stops = Stops(element, opacity);

        if (stops.Count == 0)
            return null;

        if (stops.Count == 1)
            return stops[0].Ink;

        bool ofBox = Inherit(element, "gradientUnits") != "userSpaceOnUse";
        Offset start = new Offset(Coordinate(element, "x1", ofBox, 0), Coordinate(element, "y1", ofBox, 0));
        Offset end = new Offset(Coordinate(element, "x2", ofBox, 1), Coordinate(element, "y2", ofBox, 0));

        if (SvgTransform.Read(Inherit(element, "gradientTransform")) is { } matrix)
        {
            start = Apply(matrix, start);
            end = Apply(matrix, end);
        }

        return Gradient.Between(start, end, ofBox, stops);
    }

    private float Coordinate(XElement element, string name, bool ofBox, float fallback) =>
        ofBox ? SvgLength.Fraction(Inherit(element, name), fallback) : SvgLength.Read(Inherit(element, name), 100, fallback * 100);

    /// <summary>A gradient's stops, from itself or the gradient it refers to, in order and at their opacity.</summary>
    private List<GradientStop> Stops(XElement element, float opacity)
    {
        XElement? current = element;

        for (int step = 0; current is not null && step < DeepestNesting; step++)
        {
            List<XElement> stops = current.Elements().Where(child => child.Name.LocalName == "stop").ToList();

            if (stops.Count > 0)
            {
                List<GradientStop> read = [];
                float last = 0;

                foreach (XElement stop in stops)
                {
                    Dictionary<string, string> declared = Declared(stop);
                    float position = Math.Max(last, Math.Min(1, Math.Max(0, SvgLength.Fraction((string?)stop.Attribute("offset"), 0))));
                    Ink ink = SolidPaint(Value(declared, "stop-color") ?? "black", declared) ?? Ink.Rgb(0, 0, 0);
                    read.Add(new GradientStop(position, ink.WithOpacity(ink.Opacity * opacity * Opacity(Value(declared, "stop-opacity")))));
                    last = position;
                }

                return read;
            }

            current = Referenced(current);
        }

        return [];
    }

    private static Ink? Mean(List<GradientStop> stops)
    {
        if (stops.Count == 0)
            return null;

        (float Red, float Green, float Blue)[] colours = stops.Select(stop => stop.Ink.ToRgb()).ToArray();
        return Ink.Rgb(
            (byte)Math.Round(colours.Average(colour => colour.Red) * 255),
            (byte)Math.Round(colours.Average(colour => colour.Green) * 255),
            (byte)Math.Round(colours.Average(colour => colour.Blue) * 255)).WithOpacity(stops.Average(stop => stop.Ink.Opacity));
    }

    /// <summary>An attribute of a gradient, or of the gradient it refers to when it has none of its own.</summary>
    private string? Inherit(XElement element, string name)
    {
        XElement? current = element;

        for (int step = 0; current is not null && step < DeepestNesting; step++)
        {
            if ((string?)current.Attribute(name) is { } value)
                return value;

            current = Referenced(current);
        }

        return null;
    }

    private VectorPath? ClipPathOf(string reference, int depth)
    {
        string trimmed = reference.Trim();

        if (!trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            return null;

        string id = trimmed.Substring(4).TrimEnd(')').Trim(' ', '"', '\'').TrimStart('#');

        if (!_byId.TryGetValue(id, out XElement? clip) || clip.Name.LocalName != "clipPath" || (string?)clip.Attribute("clipPathUnits") == "objectBoundingBox")
            return null;

        VectorPath path = new VectorPath();
        Matrix outer = SvgTransform.Read((string?)clip.Attribute("transform")) ?? SvgTransform.Identity;
        AddClipShapes(clip, outer, path, depth);
        return path;
    }

    private void AddClipShapes(XElement parent, Matrix matrix, VectorPath into, int depth)
    {
        if (depth > DeepestNesting)
            return;

        foreach (XElement child in parent.Elements())
        {
            if (Value(Declared(child), "display") == "none")
                continue;

            Matrix inner = SvgTransform.Multiply(matrix, SvgTransform.Read((string?)child.Attribute("transform")) ?? SvgTransform.Identity);

            if (child.Name.LocalName == "use" && Referenced(child) is { } target && _using.Add(target))
            {
                Matrix moved = SvgTransform.Multiply(inner, (1, 0, 0, 1, SvgLength.Read((string?)child.Attribute("x"), 0, 0), SvgLength.Read((string?)child.Attribute("y"), 0, 0)));
                AddClipShapes(new XElement("g", target), moved, into, depth + 1);
                _using.Remove(target);
            }
            else if (child.Name.LocalName == "g")
            {
                AddClipShapes(child, inner, into, depth + 1);
            }
            else if (Shape(child) is { } shape)
            {
                into.AddTransformed(shape, inner.A, inner.B, inner.C, inner.D, inner.E, inner.F);
            }
        }
    }

    /// <summary>The path of a basic shape or path element, or null for any other element or a shape of no size.</summary>
    private static VectorPath? Shape(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "path":
                return (string?)element.Attribute("d") is { } data ? SvgPathData.Read(data) : null;

            case "rect":
            {
                float x = Length(element, "x"), y = Length(element, "y");
                float width = Length(element, "width"), height = Length(element, "height");

                if (!(width > 0) || !(height > 0))
                    return null;

                string? rxText = (string?)element.Attribute("rx"), ryText = (string?)element.Attribute("ry");
                float rx = SvgLength.Read(rxText ?? ryText, width, 0);
                float ry = SvgLength.Read(ryText ?? rxText, height, 0);
                rx = Math.Min(Math.Max(0, rx), width / 2);
                ry = Math.Min(Math.Max(0, ry), height / 2);

                if (rx == 0 || ry == 0)
                    return new VectorPath().AddRectangle(x, y, width, height);

                return new VectorPath()
                    .MoveTo(x + rx, y).LineTo(x + width - rx, y).ArcTo(rx, ry, 0, false, true, x + width, y + ry)
                    .LineTo(x + width, y + height - ry).ArcTo(rx, ry, 0, false, true, x + width - rx, y + height)
                    .LineTo(x + rx, y + height).ArcTo(rx, ry, 0, false, true, x, y + height - ry)
                    .LineTo(x, y + ry).ArcTo(rx, ry, 0, false, true, x + rx, y).Close();
            }

            case "circle":
            {
                float radius = Length(element, "r");
                return radius > 0 ? new VectorPath().AddCircle(Length(element, "cx"), Length(element, "cy"), radius) : null;
            }

            case "ellipse":
            {
                float rx = Length(element, "rx"), ry = Length(element, "ry");
                return rx > 0 && ry > 0 ? new VectorPath().AddEllipse(Length(element, "cx"), Length(element, "cy"), rx, ry) : null;
            }

            case "line":
                return new VectorPath().MoveTo(Length(element, "x1"), Length(element, "y1")).LineTo(Length(element, "x2"), Length(element, "y2"));

            case "polyline":
            case "polygon":
            {
                List<float> points = new SvgNumbers((string?)element.Attribute("points") ?? string.Empty).Rest();

                if (points.Count < 4)
                    return null;

                VectorPath path = new VectorPath().MoveTo(points[0], points[1]);
                for (int index = 2; index + 1 < points.Count; index += 2)
                    path.LineTo(points[index], points[index + 1]);

                return element.Name.LocalName == "polygon" ? path.Close() : path;
            }

            default:
                return null;
        }
    }

    /// <summary>The properties an element declares: presentation attributes, then style sheet rules, then its style.</summary>
    private Dictionary<string, string> Declared(XElement element)
    {
        Dictionary<string, string> declared = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (XAttribute attribute in element.Attributes())
        {
            if (Properties.Contains(attribute.Name.LocalName))
                declared[attribute.Name.LocalName] = attribute.Value.Trim();
        }

        foreach (KeyValuePair<string, string> rule in _sheet.For(element))
            declared[rule.Key] = rule.Value;

        if ((string?)element.Attribute("style") is { } inline)
        {
            foreach (KeyValuePair<string, string> property in SvgStyleSheet.Declarations(inline))
                declared[property.Key] = property.Value;
        }

        return declared;
    }

    private XElement? Referenced(XElement element) =>
        Href(element) is { } href && href.StartsWith("#", StringComparison.Ordinal) && _byId.TryGetValue(href.Substring(1), out XElement? target)
            ? target
            : null;

    private static string? Href(XElement element) =>
        (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "href");

    private static float[]? ViewBox(XElement element)
    {
        List<float> values = new SvgNumbers((string?)element.Attribute("viewBox") ?? string.Empty).Rest();
        return values.Count == 4 && values[2] > 0 && values[3] > 0 ? values.ToArray() : null;
    }

    /// <summary>
    /// Maps a view box into a viewport of <paramref name="width"/> by <paramref name="height"/>, as its aspect ratio
    /// setting says: false, with nothing mapped, when the view box is too small beside the viewport for a PDF to write
    /// the scale, and what it holds is left out.
    /// </summary>
    private static bool MapViewBox(ArtworkComposer art, XElement element, float[]? viewBox, float width, float height)
    {
        if (viewBox is null)
            return true;

        string[] aspect = ((string?)element.Attribute("preserveAspectRatio") ?? "xMidYMid meet").Split([' '], StringSplitOptions.RemoveEmptyEntries);
        float scaleX = width / viewBox[2];
        float scaleY = height / viewBox[3];

        if (aspect.Length > 0 && aspect[0] == "none")
        {
            if (!Writable.Is(scaleX) || !Writable.Is(scaleY))
                return false;

            art.Scale(scaleX, scaleY);
            art.Translate(-viewBox[0], -viewBox[1]);
            return true;
        }

        string align = aspect.Length > 0 ? aspect[0] : "xMidYMid";
        bool slice = aspect.Length > 1 && aspect[1] == "slice";
        float scale = slice ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        float spareX = width - (viewBox[2] * scale);
        float spareY = height - (viewBox[3] * scale);
        float alignX = align.Contains("xMid") ? 0.5f : align.Contains("xMax") ? 1 : 0;
        float alignY = align.Contains("YMid") ? 0.5f : align.Contains("YMax") ? 1 : 0;

        if (!Writable.Is(scale) || !Writable.Is(spareX * alignX) || !Writable.Is(spareY * alignY))
            return false;

        art.Translate(spareX * alignX, spareY * alignY);
        art.Scale(scale, scale);
        art.Translate(-viewBox[0], -viewBox[1]);
        return true;
    }

    private static List<float>? Dashes(string? text)
    {
        if (text is null || text == "none")
            return null;

        List<float> dashes = new SvgNumbers(text).Rest();
        return dashes.Count == 0 || dashes.Any(dash => dash < 0) ? null : dashes;
    }

    private static float Length(XElement element, string name) => SvgLength.Read((string?)element.Attribute(name), 100, 0);

    private static float First(XElement element, string name)
    {
        List<float> values = new SvgNumbers((string?)element.Attribute(name) ?? string.Empty).Rest();
        return values.Count > 0 ? values[0] : 0;
    }

    private static float Fraction(string? text, float fallback) => Math.Max(0, SvgLength.Fraction(text, fallback));

    /// <summary>An opacity, fully opaque when none is given, and clamped to the range from 0 to 1 as SVG clamps it.</summary>
    private static float Opacity(string? text) => Math.Min(1, Fraction(text, 1));

    private static string? Value(Dictionary<string, string> style, string name) =>
        style.TryGetValue(name, out string? value) ? value : null;

    private static Offset Apply(Matrix matrix, Offset point) =>
        new Offset((matrix.A * point.X) + (matrix.C * point.Y) + matrix.E, (matrix.B * point.X) + (matrix.D * point.Y) + matrix.F);
}
