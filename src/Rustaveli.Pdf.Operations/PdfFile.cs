using Rustaveli.Pdf.Operations.Assembly;
using Rustaveli.Pdf.Operations.Reading;

namespace Rustaveli.Pdf;

/// <summary>
/// A PDF file being put together from others: its pages kept, reordered or added to from other files, and pages of
/// other files laid beneath or over them, like a letterhead or a stamp. Nothing is written until it is saved.
/// </summary>
/// <remarks>
/// <para>
/// Pages are named as a print dialog names them: <c>"1-3, 5, 8-last"</c>, from 1, ranges running either way and open
/// at either end. Each operation works on the pages as they are when it is called.
/// </para>
/// <para>
/// Saving writes a new file. What the first file says of the document as a whole — its information, language,
/// metadata and colour intents — is kept; its outline, named destinations and structure are kept too while its pages
/// all are, in their order, at the start of the file. The layers and form fields of every file are kept with its pages,
/// and links lead where they did in their own file.
/// </para>
/// </remarks>
public sealed class PdfFile
{
    private readonly PdfSource _first;
    private readonly List<PageEntry> _pages;
    private readonly SaveSettings _settings = new SaveSettings();

    private PdfFile(PdfSource source)
    {
        _first = source;
        _pages = source.Pages.Select(page => new PageEntry(page)).ToList();
    }

    /// <summary>How many pages the file has now.</summary>
    public int PageCount => _pages.Count;

    /// <summary>
    /// Opens the PDF file at <paramref name="path"/>, with <paramref name="password"/> — the owner's or the user's —
    /// if it is protected.
    /// </summary>
    /// <exception cref="IncorrectPasswordException">The file is protected, and the password does not open it.</exception>
    /// <exception cref="UnreadableFileException">The file is not a PDF, or is damaged past repair.</exception>
    /// <exception cref="NotSupportedException">
    /// The file is protected by a security handler other than the standard, password one, such as a certificate's.
    /// </exception>
    public static PdfFile Open(string path, string? password = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Open(File.ReadAllBytes(path), password);
    }

    /// <inheritdoc cref="Open(string, string?)"/>
    public static PdfFile Open(byte[] data, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new PdfFile(PdfSource.Open(data, password));
    }

    /// <summary>Opens a PDF file read from <paramref name="stream"/>, which is read to its end and not closed.</summary>
    /// <inheritdoc cref="Open(string, string?)"/>
    public static PdfFile Open(Stream stream, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return Open(copy.ToArray(), password);
    }

    /// <summary>Whether the file was protected by a password when it was opened.</summary>
    public bool WasProtected => _first.Encryption is not null;

    /// <summary>Keeps only the pages <paramref name="pages"/> names, in the order it names them.</summary>
    public PdfFile KeepPages(string pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        List<PageEntry> kept = PageSelection.Parse(pages, _pages.Count, nameof(pages)).Select(index => _pages[index].Clone()).ToList();
        _pages.Clear();
        _pages.AddRange(kept);
        return this;
    }

    /// <summary>Adds the pages of the PDF file at <paramref name="path"/> after these — those <paramref name="pages"/> names, or all.</summary>
    public PdfFile Append(string path, string? pages = null) => Append(Open(path), pages);

    /// <summary>Adds the pages of <paramref name="other"/>, as they are now, after these — those <paramref name="pages"/> names, or all.</summary>
    public PdfFile Append(PdfFile other, string? pages = null)
    {
        ArgumentNullException.ThrowIfNull(other);

        _pages.AddRange(PageSelection.Parse(pages, other._pages.Count, nameof(pages)).Select(index => other._pages[index].Clone()));
        return this;
    }

    /// <summary>
    /// Draws pages of the PDF file at <paramref name="path"/> over these, as a stamp: over the pages
    /// <paramref name="onto"/> names, or all, the pages <paramref name="from"/> names, or all, one each in turn and
    /// starting again when they run out — so a one-page stamp goes on every page.
    /// </summary>
    public PdfFile Overlay(string path, string? onto = null, string? from = null) => Overlay(Open(path), onto, from);

    /// <inheritdoc cref="Overlay(string, string?, string?)"/>
    public PdfFile Overlay(PdfFile layer, string? onto = null, string? from = null) => Lay(layer, onto, from, over: true);

    /// <summary>
    /// Draws pages of the PDF file at <paramref name="path"/> beneath these, as a letterhead: beneath the pages
    /// <paramref name="onto"/> names, or all, the pages <paramref name="from"/> names, or all, one each in turn and
    /// starting again when they run out.
    /// </summary>
    public PdfFile Underlay(string path, string? onto = null, string? from = null) => Underlay(Open(path), onto, from);

    /// <inheritdoc cref="Underlay(string, string?, string?)"/>
    public PdfFile Underlay(PdfFile layer, string? onto = null, string? from = null) => Lay(layer, onto, from, over: false);

    /// <summary>
    /// Attaches a file, listed among the attachments readers show and, for PDF/A-3, associated with the document by
    /// its relationship. The file's own attachments are kept whatever pages are.
    /// </summary>
    public PdfFile Attach(FileAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _settings.Attachments.Add(attachment);
        return this;
    }

    /// <summary>
    /// Adds to the file's XMP metadata: each <c>rdf:Description</c> in <paramref name="xmp"/> — the description of an
    /// electronic invoice, say, with the schema PDF/A needs to know it by — joins those already there.
    /// </summary>
    public PdfFile AddMetadata(string xmp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmp);
        _settings.Metadata.Add(xmp);
        return this;
    }

    /// <summary>Saves the file protected as <paramref name="protection"/> says, in place of any protection it had.</summary>
    public PdfFile Protect(Protection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        _settings.Protection = protection;
        _settings.KeepProtection = false;
        return this;
    }

    /// <summary>
    /// Saves the file without protection. A protected file keeps its protection, as it had it, unless this or
    /// <see cref="Protect"/> says otherwise.
    /// </summary>
    public PdfFile Unprotect()
    {
        _settings.Protection = null;
        _settings.KeepProtection = false;
        return this;
    }

    /// <summary>Drops the restrictions a signature places on the file — what its <c>/Perms</c> allow — so it can be changed freely.</summary>
    public PdfFile LiftRestrictions()
    {
        _settings.LiftRestrictions = true;
        return this;
    }

    /// <summary>
    /// Saves the file for viewing over the web as it downloads — Acrobat's fast web view — its first page first, then
    /// each other page with what it alone needs, with hints saying where everything lies.
    /// </summary>
    public PdfFile OptimizeForWeb()
    {
        _settings.Linearize = true;
        return this;
    }

    /// <summary>Writes the file, returning its bytes.</summary>
    public byte[] ToArray()
    {
        using MemoryStream output = new MemoryStream();
        Save(output);
        return output.ToArray();
    }

    /// <summary>Writes the file to <paramref name="stream"/>, which is not closed.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        FileAssembler.Write(_pages, _first, stream, _settings);
    }

    /// <summary>
    /// Writes the file at <paramref name="path"/>, beside it first and then moved into place, so a failure leaves
    /// whatever was there — the file this one was opened from, say — as it was.
    /// </summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string target = Path.GetFullPath(path);
        string partial = target + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".partial";

        try
        {
            using (FileStream stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                Save(stream);

            if (File.Exists(target))
                File.Replace(partial, target, null);
            else
                File.Move(partial, target);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    private PdfFile Lay(PdfFile layer, string? onto, string? from, bool over)
    {
        ArgumentNullException.ThrowIfNull(layer);

        List<int> targets = PageSelection.Parse(onto, _pages.Count, nameof(onto));
        List<int> sources = PageSelection.Parse(from, layer._pages.Count, nameof(from));

        if (sources.Count == 0)
            return this;

        for (int index = 0; index < targets.Count; index++)
        {
            PageEntry target = _pages[targets[index]];
            SourcePage source = layer._pages[sources[index % sources.Count]].Page;
            (over ? target.Over : target.Beneath).Add(source);
        }

        return this;
    }
}
