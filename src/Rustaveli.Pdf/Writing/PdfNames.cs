namespace Rustaveli.Pdf.Writing;

/// <summary>The names the writer itself emits, created once rather than on every use.</summary>
internal static class PdfNames
{
    /// <summary>The resource category of patterns, and the colour space that paints with one.</summary>
    public static readonly PdfName Pattern = new PdfName("Pattern");

    public static readonly PdfName A = new PdfName("A");
    public static readonly PdfName Annot = new PdfName("Annot");
    public static readonly PdfName Annots = new PdfName("Annots");
    public static readonly PdfName Author = new PdfName("Author");
    public static readonly PdfName Border = new PdfName("Border");
    public static readonly PdfName Catalog = new PdfName("Catalog");
    public static readonly PdfName ColorSpace = new PdfName("ColorSpace");
    public static readonly PdfName Columns = new PdfName("Columns");
    public static readonly PdfName Contents = new PdfName("Contents");
    public static readonly PdfName Count = new PdfName("Count");
    public static readonly PdfName CreationDate = new PdfName("CreationDate");
    public static readonly PdfName Creator = new PdfName("Creator");
    public static readonly PdfName D = new PdfName("D");
    public static readonly PdfName DecodeParms = new PdfName("DecodeParms");
    public static readonly PdfName Dests = new PdfName("Dests");
    public static readonly PdfName ExtGState = new PdfName("ExtGState");
    public static readonly PdfName Filter = new PdfName("Filter");
    public static readonly PdfName First = new PdfName("First");
    public static readonly PdfName FlateDecode = new PdfName("FlateDecode");
    public static readonly PdfName Font = new PdfName("Font");
    public static readonly PdfName GoTo = new PdfName("GoTo");
    public static readonly PdfName ID = new PdfName("ID");
    public static readonly PdfName Info = new PdfName("Info");
    public static readonly PdfName Keywords = new PdfName("Keywords");
    public static readonly PdfName Kids = new PdfName("Kids");
    public static readonly PdfName Length = new PdfName("Length");
    public static readonly PdfName Limits = new PdfName("Limits");
    public static readonly PdfName Link = new PdfName("Link");
    public static readonly PdfName MediaBox = new PdfName("MediaBox");
    public static readonly PdfName ModDate = new PdfName("ModDate");
    public static readonly PdfName N = new PdfName("N");
    public static readonly PdfName Names = new PdfName("Names");

    /// <summary><c>/ca</c>: the constant opacity for filling and other non-stroking operations.</summary>
    public static readonly PdfName NonStrokingAlpha = new PdfName("ca");

    public static readonly PdfName ObjStm = new PdfName("ObjStm");
    public static readonly PdfName Page = new PdfName("Page");
    public static readonly PdfName Pages = new PdfName("Pages");
    public static readonly PdfName Parent = new PdfName("Parent");
    public static readonly PdfName Predictor = new PdfName("Predictor");
    public static readonly PdfName Producer = new PdfName("Producer");
    public static readonly PdfName Rect = new PdfName("Rect");
    public static readonly PdfName Resources = new PdfName("Resources");
    public static readonly PdfName Root = new PdfName("Root");
    public static readonly PdfName S = new PdfName("S");
    public static readonly PdfName Size = new PdfName("Size");

    /// <summary><c>/CA</c>: the constant opacity for stroking operations.</summary>
    public static readonly PdfName StrokingAlpha = new PdfName("CA");

    public static readonly PdfName Subject = new PdfName("Subject");
    public static readonly PdfName Subtype = new PdfName("Subtype");
    public static readonly PdfName Title = new PdfName("Title");
    public static readonly PdfName Type = new PdfName("Type");
    public static readonly PdfName URI = new PdfName("URI");
    public static readonly PdfName W = new PdfName("W");
    public static readonly PdfName XObject = new PdfName("XObject");
    public static readonly PdfName XRef = new PdfName("XRef");
    public static readonly PdfName XYZ = new PdfName("XYZ");
}
