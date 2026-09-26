using System.Globalization;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// The resource dictionary of one content stream: the fonts, XObjects, graphics states and colour spaces it uses,
/// each under a short name the stream's operators refer to.
/// </summary>
/// <remarks>
/// Asking for the same object twice returns the same name, so callers can ask on every use instead of keeping their
/// own map. Names are assigned in order of first use per category — <c>/F1</c>, <c>/F2</c> for fonts, <c>/X1</c>
/// for XObjects, <c>/GS1</c> for graphics states, <c>/CS1</c> for colour spaces — and are local to this dictionary:
/// the same font may be <c>/F1</c> on one page and <c>/F2</c> on another.
/// </remarks>
internal sealed class PdfResources
{
    private readonly Category _fonts = new Category(PdfNames.Font, "F");
    private readonly Category _xObjects = new Category(PdfNames.XObject, "X");
    private readonly Category _graphicsStates = new Category(PdfNames.ExtGState, "GS");
    private readonly Category _colorSpaces = new Category(PdfNames.ColorSpace, "CS");

    public bool IsEmpty => _fonts.IsEmpty && _xObjects.IsEmpty && _graphicsStates.IsEmpty && _colorSpaces.IsEmpty;

    public PdfName GetFontName(PdfReference font) => _fonts.NameFor(font);

    /// <summary>The name for an image or form XObject.</summary>
    public PdfName GetXObjectName(PdfReference xObject) => _xObjects.NameFor(xObject);

    /// <summary>The name for an extended graphics state, such as the document's shared opacity states.</summary>
    public PdfName GetExtGStateName(PdfReference graphicsState) => _graphicsStates.NameFor(graphicsState);

    /// <summary>The name for a colour space written as its own object — a separation for a spot colour, say.</summary>
    public PdfName GetColorSpaceName(PdfReference colorSpace) => _colorSpaces.NameFor(colorSpace);

    /// <summary>The <c>/Resources</c> dictionary, with a sub-dictionary for each category in use.</summary>
    public PdfDictionary ToDictionary()
    {
        PdfDictionary resources = new PdfDictionary(4);
        _fonts.AddTo(resources);
        _xObjects.AddTo(resources);
        _graphicsStates.AddTo(resources);
        _colorSpaces.AddTo(resources);
        return resources;
    }

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
            _entries.Add(name, reference);
            return name;
        }

        public void AddTo(PdfDictionary resources)
        {
            if (!IsEmpty)
                resources.Add(key, _entries);
        }
    }
}
