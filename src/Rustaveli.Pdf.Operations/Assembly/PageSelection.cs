using System.Globalization;

namespace Rustaveli.Pdf.Operations.Assembly;

/// <summary>
/// Which pages a list such as <c>1-3, 5, 8-last</c> names, as a print dialog takes it: numbers from 1, ranges that
/// may run backwards, ranges open at either end, and <c>last</c> for the last page.
/// </summary>
internal static class PageSelection
{
    /// <summary>The zero-based indexes of the pages <paramref name="text"/> names, in its order; every page for null.</summary>
    public static List<int> Parse(string? text, int count, string parameter)
    {
        if (text is null)
            return Enumerable.Range(0, count).ToList();

        List<int> pages = [];

        foreach (string part in text.Split(','))
        {
            string item = part.Trim();

            if (item.Length == 0)
                throw Malformed(text, parameter);

            int dash = item.IndexOf('-');

            if (dash < 0)
            {
                pages.Add(Page(item, count, text, parameter) - 1);
                continue;
            }

            string from = item.Substring(0, dash).Trim();
            string to = item.Substring(dash + 1).Trim();
            int first = from.Length == 0 ? 1 : Page(from, count, text, parameter);
            int last = to.Length == 0 ? count : Page(to, count, text, parameter);
            int step = first <= last ? 1 : -1;

            for (int page = first; page != last + step; page += step)
                pages.Add(page - 1);
        }

        return pages;
    }

    private static int Page(string item, int count, string text, string parameter)
    {
        if (item.Equals("last", StringComparison.OrdinalIgnoreCase))
            return count;

        if (!int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out int page))
            throw Malformed(text, parameter);

        if (page < 1 || page > count)
            throw new ArgumentOutOfRangeException(parameter, text, $"Page {page} is not among the {count} pages.");

        return page;
    }

    private static ArgumentException Malformed(string text, string parameter) =>
        new ArgumentException($"'{text}' is not a list of pages, such as \"1-3, 5, 8-last\".", parameter);
}
