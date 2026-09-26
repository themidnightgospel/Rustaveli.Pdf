using System.Text;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Turns a generated tree into a real document through the public fluent API, and records every character of text
/// it wrote so a render can be checked for lost or duplicated content.
/// </summary>
public sealed class TreeComposer
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz";

    private readonly StringBuilder _written = new StringBuilder();
    private int _word;

    /// <summary>Every non-space character composed into the document, in composition order.</summary>
    public string WrittenText => _written.ToString();

    public void Compose(IContainer container, TreeNode node)
    {
        switch (node.Kind)
        {
            case NodeKind.Text:
                container.Text(Words(node.Amount));
                break;

            case NodeKind.Box:
                Composition.Attach(container, new FixedElement(20, node.Amount));
                break;

            case NodeKind.Column:
                container.Column(column =>
                {
                    column.Spacing(node.Amount);
                    foreach (TreeNode child in node.Children)
                        Compose(column.Item(), child);
                });
                break;

            case NodeKind.Row:
                container.Row(row =>
                {
                    for (int index = 0; index < node.Children.Count; index++)
                        Compose(node.Sizes[index] == 0 ? row.RelativeItem() : row.ConstantItem(node.Sizes[index]), node.Children[index]);
                });
                break;

            case NodeKind.Table:
                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (int index = 0; index < node.Sizes[0]; index++)
                            columns.RelativeColumn();
                    });
                    foreach (TreeNode cell in node.Children)
                        Compose(table.Cell(), cell);
                });
                break;

            case NodeKind.Padding:
                Compose(container.Padding(node.Amount), node.Children[0]);
                break;

            case NodeKind.Background:
                Compose(container.Background(TestInks.GreyLighten3), node.Children[0]);
                break;

            case NodeKind.Border:
                Compose(container.Border(1).BorderColor(TestInks.Grey), node.Children[0]);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(node), node.Kind, null);
        }
    }

    // Short, distinct words of three letters, so any word fits the narrowest generated column unbroken.
    private string Words(int count)
    {
        StringBuilder text = new StringBuilder();
        for (int index = 0; index < count; index++)
        {
            int word = _word++;
            string token = new string([Alphabet[word % 26], Alphabet[(word / 26) % 26], Alphabet[(word / 676) % 26]]);
            if (index > 0)
                text.Append(' ');
            text.Append(token);
            _written.Append(token);
        }

        return text.ToString();
    }
}
