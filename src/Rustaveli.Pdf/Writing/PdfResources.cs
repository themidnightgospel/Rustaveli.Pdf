using System.Globalization;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// The resource dictionary of one content stream: the fonts, XObjects, graphics states, colour spaces and patterns it uses,
/// each under a short name the stream's operators refer to.
/// </summary>
/// <remarks>
/// Asking for the same object twice returns the same name, so callers can ask on every use instead of keeping their
/// own map. Names are assigned in order of first use per category — <c>/F1</c>, <c>/F2</c> for fonts, <c>/X1</c>
/// for XObjects, <c>/GS1</c> for graphics states, <c>/CS1</c> for colour spaces, <c>/P1</c> for patterns — and are local to this dictionary:
/// the same font may be <c>/F1</c> on one page and <c>/F2</c> on another.
/// </remarks>
internal sealed class PdfResources
{
    // Each category is made when it is first asked for, so a page pays only for the ones it uses.
    private Category? _fonts;
    private Category? _xObjects;
    private Category? _graphicsStates;
    private Category? _colorSpaces;
    private Category? _patterns;

    public bool IsEmpty =>
        IsUnused(_fonts)
        && IsUnused(_xObjects)
        && IsUnused(_graphicsStates)
        && IsUnused(_colorSpaces)
        && IsUnused(_patterns);

    public PdfName GetFontName(PdfReference font) => (_fonts ??= new Category(PdfNames.Font, "F")).NameFor(font);

    /// <summary>The name for an image or form XObject.</summary>
    public PdfName GetXObjectName(PdfReference xObject) =>
        (_xObjects ??= new Category(PdfNames.XObject, "X")).NameFor(xObject);

    /// <summary>The name for an extended graphics state, such as the document's shared opacity states.</summary>
    public PdfName GetExtGStateName(PdfReference graphicsState) =>
        (_graphicsStates ??= new Category(PdfNames.ExtGState, "GS")).NameFor(graphicsState);

    /// <summary>
    /// The name for a colour space written as its own object — a separation for a spot colour, say.
    /// </summary>
    public PdfName GetColorSpaceName(PdfReference colorSpace) =>
        (_colorSpaces ??= new Category(PdfNames.ColorSpace, "CS")).NameFor(colorSpace);

    /// <summary>The name for a pattern, such as the shading a gradient is painted with.</summary>
    public PdfName GetPatternName(PdfReference pattern) =>
        (_patterns ??= new Category(PdfNames.Pattern, "P")).NameFor(pattern);

    /// <summary>The <c>/Resources</c> dictionary, with a sub-dictionary for each category in use.</summary>
    public PdfDictionary ToDictionary()
    {
        PdfDictionary resources = new PdfDictionary(5);
        _fonts?.AddTo(resources);
        _xObjects?.AddTo(resources);
        _graphicsStates?.AddTo(resources);
        _colorSpaces?.AddTo(resources);
        _patterns?.AddTo(resources);
        return resources;
    }

    private static bool IsUnused(Category? category) => category is null || category.IsEmpty;

    private sealed class Category(PdfName key, string prefix)
    {
        private readonly Dictionary<int, PdfName> _names = new Dictionary<int, PdfName>();
        private readonly PdfDictionary _entries = new PdfDictionary();

        public bool IsEmpty => _entries.Count == 0;

        public PdfName NameFor(PdfReference reference)
        {
            if (reference.ObjectNumber == 0)
                throw new ArgumentException("The reference was never assigned an object number.", nameof(reference));

            if (_names.TryGetValue(reference.ObjectNumber, out PdfName? existing))
                return existing;

            PdfName name = new PdfName(prefix + (_entries.Count + 1).ToString(CultureInfo.InvariantCulture));
            _names.Add(reference.ObjectNumber, name);

            // Numbered in turn, each name is new: a page of thousands of images need not check it against every other.
            _entries.AddUnique(name, reference);
            return name;
        }

        public void AddTo(PdfDictionary resources)
        {
            if (!IsEmpty)
                resources.Add(key, _entries);
        }
    }
}
