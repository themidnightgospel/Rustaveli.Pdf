namespace Rustaveli.Pdf;

/// <summary>
/// What is known about the page being laid out, for content that shows only on some pages.
/// </summary>
/// <param name="Folio">The page's number, counting from one.</param>
/// <param name="PageCount">
/// How many pages the document has, or null while the engine is still counting them. Content whose size depends on
/// it can change the count it depends on, so it is best kept to content of a fixed size, such as a mark in a margin.
/// </param>
public readonly record struct PageFacts(int Folio, int? PageCount)
{
    /// <summary>Whether this is the document's first page.</summary>
    public bool IsFirst => Folio == 1;

    /// <summary>Whether this is the document's last page; false while the page count is not yet known.</summary>
    public bool IsLast => Folio == PageCount;

    /// <summary>Whether the page has an odd number, which is a right-hand page in a book opening on the right.</summary>
    public bool IsOdd => Folio % 2 == 1;
}
