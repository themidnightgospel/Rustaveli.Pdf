using System.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// A page being written: its content stream, the resources that stream names, and its annotations. Begun by
/// <see cref="PdfDocumentWriter.BeginPage(PdfRectangle)"/> and written out by <see cref="PdfDocumentWriter.EndPage"/>.
/// </summary>
internal sealed class PdfPage
{
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
    public void AddUriLink(PdfRectangle area, string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        AddLink(area, new PdfDictionary
        {
            [PdfNames.S] = PdfNames.URI,
            [PdfNames.URI] = new PdfString(EncodeUri(uri)),
        });
    }

    /// <summary>Makes <paramref name="area"/> a link to the named destination <paramref name="destination"/>.</summary>
    public void AddDestinationLink(PdfRectangle area, string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        AddLink(area, new PdfDictionary
        {
            [PdfNames.S] = PdfNames.GoTo,
            [PdfNames.D] = PdfString.FromText(destination),
        });
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

    private void AddLink(PdfRectangle area, PdfDictionary action)
    {
        // A zero-width border: without it, viewers following the specification's default draw a black box
        // around every link.
        _annotations.Add(_file.Write(new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Annot,
            [PdfNames.Subtype] = PdfNames.Link,
            [PdfNames.Rect] = area.ToArray(),
            [PdfNames.Border] = new PdfArray { 0, 0, 0 },
            [PdfNames.A] = action,
        }));
    }
}
