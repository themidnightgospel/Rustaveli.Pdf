using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Tagging;

/// <summary>
/// The structure elements a render pass is drawing inside, innermost last: blocks enter and leave them as they draw,
/// and the surface is told which element what it draws next belongs to.
/// </summary>
/// <remarks>
/// When the export is not tagged nothing is created and the surface is never told anything, so tagged content costs
/// nothing there. Inside untagged content — running heads, repeated table headings, decoration — nothing is created
/// either, and what is drawn is marked as outside the structure.
/// </remarks>
internal sealed class TagStack
{
    private readonly ISurface _surface;
    private int _untagged;
    private string? _language;

    /// <summary>
    /// Draws onto <paramref name="surface"/> inside <paramref name="root"/>, the document every element descends from,
    /// shared by every section; without a root nothing is tagged.
    /// </summary>
    public TagStack(ISurface surface, StructureElement? root)
    {
        _surface = surface;
        Enabled = root is not null;
        Current = root ?? new StructureElement("Document", null);

        if (root is not null)
            surface.Tag(root);
    }

    public bool Enabled { get; }

    /// <summary>The innermost element being drawn inside.</summary>
    public StructureElement Current { get; private set; }

    /// <summary>Whether content is being drawn outside the structure.</summary>
    public bool IsUntagged => _untagged > 0;

    /// <summary>
    /// A new element, the last child of the current one, in the language in force; null when not tagging or while
    /// untagged.
    /// </summary>
    public StructureElement? Create(string role)
    {
        if (!Enabled || _untagged > 0)
            return null;

        StructureElement element = new StructureElement(role, Current);

        if (_language is not null && !string.Equals(_language, Current.EffectiveLanguage, StringComparison.Ordinal))
            element.Language = _language;

        Current.Kids.Add(element);
        return element;
    }

    /// <summary>An element for <paramref name="tag"/>, as <see cref="Create(string)"/> makes one.</summary>
    public StructureElement? Create(ContentTag tag)
    {
        StructureElement? element = Create(tag.Role);

        if (element is not null)
        {
            element.AlternateText = tag.AlternateText;
            element.Expansion = tag.Expansion;
        }

        return element;
    }

    /// <summary>
    /// Draws inside <paramref name="element"/> until the scope ends; a null element, or any when not tagging, changes
    /// nothing.
    /// </summary>
    public Scope Enter(StructureElement? element)
    {
        if (element is null || !Enabled)
            return default;

        Scope scope = new Scope(this, Current, _untagged, _language);
        Current = element;
        _surface.Tag(Mark);
        return scope;
    }

    /// <summary>Draws outside the structure until the scope ends.</summary>
    public Scope Untag()
    {
        if (!Enabled)
            return default;

        Scope scope = new Scope(this, Current, _untagged, _language);
        _untagged++;
        _surface.Tag(null);
        return scope;
    }

    /// <summary>Creates elements in <paramref name="language"/> until the scope ends.</summary>
    public Scope Speak(string language)
    {
        if (!Enabled)
            return default;

        Scope scope = new Scope(this, Current, _untagged, _language);
        _language = language;
        return scope;
    }

    private StructureElement? Mark => _untagged > 0 ? null : Current;

    private void Restore(StructureElement current, int untagged, string? language)
    {
        Current = current;
        _untagged = untagged;
        _language = language;
        _surface.Tag(Mark);
    }

    /// <summary>Returns to the element, and the untagged state and language, in force before it began.</summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly TagStack? _stack;
        private readonly StructureElement? _current;
        private readonly int _untagged;
        private readonly string? _language;

        public Scope(TagStack stack, StructureElement current, int untagged, string? language)
        {
            _stack = stack;
            _current = current;
            _untagged = untagged;
            _language = language;
        }

        public void Dispose() => _stack?.Restore(_current!, _untagged, _language);
    }
}
