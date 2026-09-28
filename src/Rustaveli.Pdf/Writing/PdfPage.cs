using System.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A page being written: its content stream, the resources that stream names, and its annotations. Begun by
/// <see cref="PdfDocumentWriter.BeginPage(PdfRectangle)"/> and written out by <see cref="PdfDocumentWriter.EndPage"/>.
/// </summary>
internal sealed class PdfPage
{
    /// <summary>An annotation's flags, <c>/F</c>.</summary>
    private static readonly PdfName Flags = new PdfName("F");

    private readonly PdfFileWriter _file;
    private readonly List<PdfReference> _annotations = new List<PdfReference>();

    internal PdfPage(PdfFileWriter file, PdfReference reference, PdfReference parent, int index, PdfRectangle mediaBox)
    {
        _file = file;
        Reference = reference;
        Parent = parent;
        Index = index;
        MediaBox = mediaBox;
    }

    /// <summary>The page object's reference, for destinations and links that point at this page.</summary>
    public PdfReference Reference { get; }

    /// <summary>Zero-based position of the page in the document.</summary>
    public int Index { get; }

    public PdfRectangle MediaBox { get; }

    public ContentStreamBuilder Content { get; } = new ContentStreamBuilder();

    public PdfResources Resources { get; } = new PdfResources();

    /// <summary>
    /// Further entries for the page dictionary — <c>/Group</c> for a transparency group, <c>/Rotate</c> — beyond
    /// those the writer supplies itself: <c>/Type</c>, <c>/Parent</c>, <c>/MediaBox</c>, <c>/Resources</c>,
    /// <c>/Contents</c> and <c>/Annots</c>.
    /// </summary>
    public PdfDictionary Entries { get; } = new PdfDictionary();

    internal PdfReference Parent { get; }

    internal IReadOnlyList<PdfReference> Annotations => _annotations;

    /// <summary>
    /// Makes <paramref name="area"/> a link that opens <paramref name="uri"/>. PDF requires URIs in 7-bit ASCII, so
    /// anything else — international domain text, spaces — is percent-encoded as UTF-8.
    /// </summary>
    /// <param name="area">Where the link is, in the page's own space.</param>
    /// <param name="uri">What it opens.</param>
    /// <param name="entries">Further entries for the annotation, such as its place in the structure.</param>
    /// <returns>The annotation.</returns>
    public PdfReference AddUriLink(PdfRectangle area, string uri, PdfDictionary? entries = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return AddLink(
            area,
            new PdfDictionary
            {
                [PdfNames.S] = PdfNames.URI,
                [PdfNames.URI] = new PdfString(EncodeUri(uri)),
            },
            entries);
    }

    /// <summary>Makes <paramref name="area"/> a link to the named destination <paramref name="destination"/>.</summary>
    /// <returns>The annotation.</returns>
    public PdfReference AddDestinationLink(PdfRectangle area, string destination, PdfDictionary? entries = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return AddLink(
            area,
            new PdfDictionary
            {
                [PdfNames.S] = PdfNames.GoTo,
                [PdfNames.D] = PdfString.FromText(destination),
            },
            entries);
    }

    /// <summary>Lists an annotation the caller wrote itself in the page's <c>/Annots</c>.</summary>
    public void AddAnnotation(PdfReference annotation)
    {
        if (annotation.ObjectNumber == 0)
            throw new ArgumentException("The reference was never assigned an object number.", nameof(annotation));

        _annotations.Add(annotation);
    }

    private static byte[] EncodeUri(string uri)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(uri);
        List<byte> encoded = new List<byte>(utf8.Length);
        foreach (byte value in utf8)
        {
            if (value is > 0x20 and < 0x7F)
            {
                encoded.Add(value);
            }
            else
            {
                encoded.Add((byte)'%');
                encoded.Add(PdfCharacters.HexDigit(value >> 4));
                encoded.Add(PdfCharacters.HexDigit(value));
            }
        }

        return encoded.ToArray();
    }

    private PdfReference AddLink(PdfRectangle area, PdfDictionary action, PdfDictionary? entries)
    {
        // A zero-width border: without it, viewers following the specification's default draw a black box
        // around every link.
        PdfDictionary annotation = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Annot,
            [PdfNames.Subtype] = PdfNames.Link,
            [PdfNames.Rect] = area.ToArray(),
            [PdfNames.Border] = new PdfArray { 0, 0, 0 },
            [PdfNames.A] = action,

            // Printed with the page, as PDF/A requires of every annotation.
            [Flags] = 4,
        };

        if (entries is not null)
        {
            foreach (KeyValuePair<PdfName, PdfValue> entry in entries)
                annotation[entry.Key] = entry.Value;
        }

        PdfReference reference = _file.Write(annotation);
        _annotations.Add(reference);
        return reference;
    }
}
