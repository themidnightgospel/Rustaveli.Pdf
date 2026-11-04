namespace Rustaveli.Pdf.Writing;

/// <summary>The names used in more than one place, created once rather than on every use.</summary>
internal static class PdfNames
{
    /// <summary>The resource category of patterns, and the colour space that paints with one.</summary>
    public static readonly PdfName Pattern = new PdfName("Pattern");

    public static readonly PdfName A = new PdfName("A");
    public static readonly PdfName AcroForm = new PdfName("AcroForm");
    public static readonly PdfName Annot = new PdfName("Annot");
    public static readonly PdfName Annots = new PdfName("Annots");
    public static readonly PdfName Author = new PdfName("Author");
    public static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");
    public static readonly PdfName Border = new PdfName("Border");
    public static readonly PdfName C0 = new PdfName("C0");
    public static readonly PdfName C1 = new PdfName("C1");
    public static readonly PdfName Catalog = new PdfName("Catalog");
    public static readonly PdfName Colors = new PdfName("Colors");
    public static readonly PdfName ColorSpace = new PdfName("ColorSpace");
    public static readonly PdfName Columns = new PdfName("Columns");
    public static readonly PdfName Contents = new PdfName("Contents");
    public static readonly PdfName Count = new PdfName("Count");
    public static readonly PdfName CreationDate = new PdfName("CreationDate");
    public static readonly PdfName Creator = new PdfName("Creator");
    public static readonly PdfName CropBox = new PdfName("CropBox");
    public static readonly PdfName D = new PdfName("D");
    public static readonly PdfName DecodeParms = new PdfName("DecodeParms");
    public static readonly PdfName Dest = new PdfName("Dest");
    public static readonly PdfName Dests = new PdfName("Dests");
    public static readonly PdfName DeviceCMYK = new PdfName("DeviceCMYK");
    public static readonly PdfName DeviceGray = new PdfName("DeviceGray");
    public static readonly PdfName DeviceRGB = new PdfName("DeviceRGB");
    public static readonly PdfName Domain = new PdfName("Domain");
    public static readonly PdfName EmbeddedFile = new PdfName("EmbeddedFile");
    public static readonly PdfName Encrypt = new PdfName("Encrypt");
    public static readonly PdfName Extensions = new PdfName("Extensions");
    public static readonly PdfName ExtGState = new PdfName("ExtGState");

    /// <summary>
    /// <c>/F</c>, which each dictionary that has it reads its own way: an annotation's flags, a file specification's
    /// file.
    /// </summary>
    public static readonly PdfName F = new PdfName("F");

    public static readonly PdfName Filter = new PdfName("Filter");
    public static readonly PdfName First = new PdfName("First");
    public static readonly PdfName FlateDecode = new PdfName("FlateDecode");
    public static readonly PdfName Font = new PdfName("Font");
    public static readonly PdfName FunctionType = new PdfName("FunctionType");
    public static readonly PdfName GoTo = new PdfName("GoTo");
    public static readonly PdfName Height = new PdfName("Height");
    public static readonly PdfName ID = new PdfName("ID");
    public static readonly PdfName Identity = new PdfName("Identity");
    public static readonly PdfName Image = new PdfName("Image");
    public static readonly PdfName Info = new PdfName("Info");
    public static readonly PdfName Keywords = new PdfName("Keywords");
    public static readonly PdfName Kids = new PdfName("Kids");
    public static readonly PdfName Lang = new PdfName("Lang");
    public static readonly PdfName Length = new PdfName("Length");
    public static readonly PdfName Limits = new PdfName("Limits");
    public static readonly PdfName Link = new PdfName("Link");
    public static readonly PdfName Marked = new PdfName("Marked");
    public static readonly PdfName MarkInfo = new PdfName("MarkInfo");
    public static readonly PdfName MediaBox = new PdfName("MediaBox");
    public static readonly PdfName Metadata = new PdfName("Metadata");
    public static readonly PdfName ModDate = new PdfName("ModDate");
    public static readonly PdfName N = new PdfName("N");
    public static readonly PdfName Name = new PdfName("Name");
    public static readonly PdfName Names = new PdfName("Names");

    /// <summary><c>/ca</c>: the constant opacity for filling and other non-stroking operations.</summary>
    public static readonly PdfName NonStrokingAlpha = new PdfName("ca");

    /// <summary>
    /// <c>/O</c>, which each dictionary that has it reads its own way: an encryption dictionary's owner password
    /// hash, a hint stream's outline table, a structure attribute's owner.
    /// </summary>
    public static readonly PdfName O = new PdfName("O");

    public static readonly PdfName ObjStm = new PdfName("ObjStm");
    public static readonly PdfName Outlines = new PdfName("Outlines");
    public static readonly PdfName OutputIntents = new PdfName("OutputIntents");

    /// <summary>
    /// <c>/P</c>, which each dictionary that has it reads its own way: an encryption dictionary's permissions, the
    /// page an annotation is on, a structure element's parent.
    /// </summary>
    public static readonly PdfName P = new PdfName("P");

    public static readonly PdfName Page = new PdfName("Page");
    public static readonly PdfName PageMode = new PdfName("PageMode");
    public static readonly PdfName Pages = new PdfName("Pages");
    public static readonly PdfName Parent = new PdfName("Parent");
    public static readonly PdfName Perms = new PdfName("Perms");
    public static readonly PdfName Predictor = new PdfName("Predictor");
    public static readonly PdfName Prev = new PdfName("Prev");
    public static readonly PdfName Producer = new PdfName("Producer");
    public static readonly PdfName Rect = new PdfName("Rect");
    public static readonly PdfName Resources = new PdfName("Resources");
    public static readonly PdfName Root = new PdfName("Root");
    public static readonly PdfName Rotate = new PdfName("Rotate");
    public static readonly PdfName S = new PdfName("S");
    public static readonly PdfName Size = new PdfName("Size");
    public static readonly PdfName SMask = new PdfName("SMask");

    /// <summary><c>/CA</c>: the constant opacity for stroking operations.</summary>
    public static readonly PdfName StrokingAlpha = new PdfName("CA");

    public static readonly PdfName StructParent = new PdfName("StructParent");
    public static readonly PdfName StructParents = new PdfName("StructParents");
    public static readonly PdfName Subject = new PdfName("Subject");
    public static readonly PdfName Subtype = new PdfName("Subtype");
    public static readonly PdfName Title = new PdfName("Title");
    public static readonly PdfName Type = new PdfName("Type");
    public static readonly PdfName URI = new PdfName("URI");
    public static readonly PdfName UseOutlines = new PdfName("UseOutlines");
    public static readonly PdfName ViewerPreferences = new PdfName("ViewerPreferences");
    public static readonly PdfName W = new PdfName("W");
    public static readonly PdfName Width = new PdfName("Width");
    public static readonly PdfName XObject = new PdfName("XObject");
    public static readonly PdfName XRef = new PdfName("XRef");
    public static readonly PdfName XYZ = new PdfName("XYZ");
}
