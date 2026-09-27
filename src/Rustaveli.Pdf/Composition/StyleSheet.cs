namespace Rustaveli.Pdf;

/// <summary>
/// A document's named styles: type styles for words, paragraph styles for blocks of text, and frame styles for the
/// frames content sits in — each defined once, able to build on another, and applied by name.
/// </summary>
/// <remarks>
/// <para>
/// A style based on another applies the other first and then its own, so <c>Subheading</c> based on <c>Heading</c>
/// takes the heading's typeface and weight and sets only its own size. Styles are applied where they are named, as the
/// document is composed, so a style is defined before content names it.
/// </para>
/// <para>
/// Named styles and inline settings mix as the calls do: <c>Style("Emphasis").Bold()</c> makes emphasis bold, and a
/// setting made before a named style is overridden by what the style sets.
/// </para>
/// </remarks>
public sealed class StyleSheet
{
    [ThreadStatic]
    private static StyleSheet? _inForce;

    private readonly Dictionary<string, (string? BasedOn, Func<TypeStyle, TypeStyle> Style)> _types = new Dictionary<string, (string?, Func<TypeStyle, TypeStyle>)>(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? BasedOn, Action<TextComposer> Format)> _paragraphs = new Dictionary<string, (string?, Action<TextComposer>)>(StringComparer.Ordinal);
    private readonly Dictionary<string, (string? BasedOn, Func<IFrame, IFrame> Frame)> _frames = new Dictionary<string, (string?, Func<IFrame, IFrame>)>(StringComparer.Ordinal);

    /// <summary>The style sheet of the document being composed or set on this thread.</summary>
    internal static StyleSheet InForce =>
        _inForce ?? throw new CompositionException("Named styles are applied while a document is composed, from the document's Styles.");

    /// <summary>Defines a type style: how words named with it are set, such as <c>style => style.Italic()</c>.</summary>
    public StyleSheet DefineType(string name, Func<TypeStyle, TypeStyle> style, string? basedOn = null) =>
        Define(_types, name, style, basedOn);

    /// <summary>
    /// Defines a paragraph style: how a block of text named with it is set — its alignment, indent and spacing, and
    /// the type its words take — such as <c>text => { text.Justified(); text.FirstLineIndent(12); }</c>.
    /// </summary>
    public StyleSheet DefineParagraph(string name, Action<TextComposer> format, string? basedOn = null) =>
        Define(_paragraphs, name, format, basedOn);

    /// <summary>
    /// Defines a frame style: the frame settings a frame named with it takes, such as
    /// <c>frame => frame.Inset(8).Fill(Ink.Hex("#EEEEEE"))</c>, returning the frame content goes in.
    /// </summary>
    public StyleSheet DefineFrame(string name, Func<IFrame, IFrame> frame, string? basedOn = null) =>
        Define(_frames, name, frame, basedOn);

    /// <summary>Makes this the style sheet names are looked up in on this thread until the scope ends.</summary>
    internal Scope Use()
    {
        Scope scope = new Scope(_inForce);
        _inForce = this;
        return scope;
    }

    internal Func<TypeStyle, TypeStyle> Type(string name)
    {
        List<Func<TypeStyle, TypeStyle>> chain = Chain(_types, name, "type");
        return style => chain.Aggregate(style, (current, step) => step(current));
    }

    internal Action<TextComposer> Paragraph(string name)
    {
        List<Action<TextComposer>> chain = Chain(_paragraphs, name, "paragraph");
        return text => chain.ForEach(step => step(text));
    }

    internal Func<IFrame, IFrame> Frame(string name)
    {
        List<Func<IFrame, IFrame>> chain = Chain(_frames, name, "frame");
        return frame => chain.Aggregate(frame, (current, step) => step(current));
    }

    private StyleSheet Define<T>(Dictionary<string, (string? BasedOn, T Style)> styles, string name, T style, string? basedOn)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(style);

        if (basedOn is not null && string.IsNullOrWhiteSpace(basedOn))
            throw new ArgumentException("A style is based on another by that style's name.", nameof(basedOn));

        styles[name] = (basedOn, style);
        return this;
    }

    /// <summary>The steps a style takes: those of the style it is based on, and theirs, first.</summary>
    private static List<T> Chain<T>(Dictionary<string, (string? BasedOn, T Style)> styles, string name, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        List<T> chain = [];
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        for (string? current = name; current is not null;)
        {
            if (!seen.Add(current))
                throw new CompositionException($"The {kind} style '{name}' is based, in the end, on itself.");

            if (!styles.TryGetValue(current, out (string? BasedOn, T Style) found))
            {
                throw new CompositionException(current == name
                    ? $"No {kind} style is named '{name}'."
                    : $"The {kind} style '{name}' is based on '{current}', and no {kind} style is named that.");
            }

            chain.Insert(0, found.Style);
            current = found.BasedOn;
        }

        return chain;
    }

    /// <summary>Restores the style sheet in force before.</summary>
    internal readonly struct Scope(StyleSheet? previous) : IDisposable
    {
        public void Dispose() => _inForce = previous;
    }
}
