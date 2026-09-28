using System.Xml.Linq;

namespace Rustaveli.Pdf.Svg;

/// <summary>
/// The rules of an SVG's <c>&lt;style&gt;</c> elements, for the selectors drawing tools write: an element name, a
/// class, an id, or those joined, as <c>path.outline</c>, and lists of them. Rules with any other selector are left
/// out.
/// </summary>
internal sealed class SvgStyleSheet
{
    private readonly List<Rule> _rules = [];

    public void Add(string css)
    {
        string text = StripComments(css);
        int position = 0;

        while (position < text.Length)
        {
            int open = text.IndexOf('{', position);
            if (open < 0)
                return;

            int close = text.IndexOf('}', open);
            if (close < 0)
                return;

            string selectors = text.Substring(position, open - position);
            Dictionary<string, string> declarations = Declarations(text.Substring(open + 1, close - open - 1));
            position = close + 1;

            foreach (string selector in selectors.Split(','))
            {
                if (Selector.Read(selector.Trim()) is { } simple)
                    _rules.Add(new Rule(simple, declarations, _rules.Count));
            }
        }
    }

    /// <summary>Every declaration of the rules <paramref name="element"/> matches, weaker rules first.</summary>
    public IEnumerable<KeyValuePair<string, string>> For(XElement element) =>
        _rules.Where(rule => rule.Selector.Matches(element))
            .OrderBy(rule => rule.Selector.Specificity)
            .ThenBy(rule => rule.Order)
            .SelectMany(rule => rule.Declarations);

    /// <summary>The <c>name: value</c> pairs of a declaration block or a <c>style</c> attribute.</summary>
    public static Dictionary<string, string> Declarations(string block)
    {
        Dictionary<string, string> declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string declaration in StripComments(block).Split(';'))
        {
            int colon = declaration.IndexOf(':');
            if (colon <= 0)
                continue;

            string value = declaration.Substring(colon + 1).Replace("!important", string.Empty).Trim();
            declarations[declaration.Substring(0, colon).Trim()] = value;
        }

        return declarations;
    }

    private static string StripComments(string css)
    {
        int start;

        while ((start = css.IndexOf("/*", StringComparison.Ordinal)) >= 0)
        {
            int end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            css = end < 0 ? css.Substring(0, start) : css.Remove(start, end + 2 - start);
        }

        return css;
    }

    private sealed record Rule(Selector Selector, Dictionary<string, string> Declarations, int Order);

    /// <summary>An element name, an id and classes, any of which may be absent.</summary>
    private sealed record Selector(string? Name, string? Id, IReadOnlyList<string> Classes)
    {
        public int Specificity => (Id is null ? 0 : 100) + (Classes.Count * 10) + (Name is null ? 0 : 1);

        public static Selector? Read(string text)
        {
            if (text.Length == 0 || text.IndexOfAny([' ', '>', '+', '~', ':', '[']) >= 0)
                return null;

            string? name = null, id = null;
            List<string> classes = [];
            int position = 0;

            while (position < text.Length)
            {
                char marker = text[position];
                int end = text.IndexOfAny(['.', '#'], position + 1);
                if (end < 0)
                    end = text.Length;

                string part = marker is '.' or '#' ? text.Substring(position + 1, end - position - 1) : text.Substring(position, end - position);

                if (marker == '.')
                    classes.Add(part);
                else if (marker == '#')
                    id = part;
                else if (part != "*")
                    name = part;

                position = end;
            }

            return new Selector(name, id, classes);
        }

        public bool Matches(XElement element)
        {
            if (Name is not null && element.Name.LocalName != Name)
                return false;

            if (Id is not null && (string?)element.Attribute("id") != Id)
                return false;

            if (Classes.Count == 0)
                return true;

            string[] own = ((string?)element.Attribute("class") ?? string.Empty).Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            return Classes.All(own.Contains);
        }
    }
}
