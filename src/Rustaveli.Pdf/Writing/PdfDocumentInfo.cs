namespace Rustaveli.Pdf.Writing;

/// <summary>The document information dictionary (ISO 32000-1, 14.3.3). Unset entries are left out.</summary>
internal sealed class PdfDocumentInfo
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    /// <summary>The application that created the original content.</summary>
    public string? Creator { get; set; }

    /// <summary>The software that converted it to PDF.</summary>
    public string? Producer { get; set; }

    public DateTimeOffset? CreationDate { get; set; }

    public DateTimeOffset? ModificationDate { get; set; }

    public bool IsEmpty =>
        Title == null && Author == null && Subject == null && Keywords == null && Creator == null && Producer == null
        && CreationDate == null && ModificationDate == null;

    public PdfDictionary ToDictionary()
    {
        PdfDictionary dictionary = new PdfDictionary(8);
        AddText(dictionary, PdfNames.Title, Title);
        AddText(dictionary, PdfNames.Author, Author);
        AddText(dictionary, PdfNames.Subject, Subject);
        AddText(dictionary, PdfNames.Keywords, Keywords);
        AddText(dictionary, PdfNames.Creator, Creator);
        AddText(dictionary, PdfNames.Producer, Producer);
        AddDate(dictionary, PdfNames.CreationDate, CreationDate);
        AddDate(dictionary, PdfNames.ModDate, ModificationDate);
        return dictionary;
    }

    private static void AddText(PdfDictionary dictionary, PdfName key, string? value)
    {
        if (value != null)
            dictionary.Add(key, PdfString.FromText(value));
    }

    private static void AddDate(PdfDictionary dictionary, PdfName key, DateTimeOffset? value)
    {
        if (value is DateTimeOffset date)
            dictionary.Add(key, PdfDate.Format(date));
    }
}
