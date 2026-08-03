namespace Rustaveli.Pdf.UnitTests;

public class EdgesTests
{
    [Fact]
    public void SumsOpposingSides()
    {
        Edges edges = new Edges(1, 2, 4, 8);

        Approximately.Equal(5f, edges.Horizontal);
        Approximately.Equal(10f, edges.Vertical);
    }
}
