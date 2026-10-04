namespace Rustaveli.Pdf.UnitTests;

/// <summary>Walking the tree of blocks: resetting it between passes reaches every block, and costs nothing.</summary>
public class BlockTreeTests
{
    /// <summary>A tree holding a reset-counting leaf inside every kind of container.</summary>
    private static (Block Root, List<ResetCounted> Leaves) Tree()
    {
        List<ResetCounted> leaves = [];

        void Leaf(IFrame frame)
        {
            ResetCounted leaf = new ResetCounted();
            leaves.Add(leaf);
            frame.Compose(inner => inner.Slot().Child = leaf);
        }

        Block root = LayoutHarness.Build(frame => frame.Stack(stack =>
        {
            Leaf(stack.Add().Inset(2));
            stack.Add().Text("A paragraph of plain words");
            stack.Add().Columns(columns =>
            {
                Leaf(columns.Share());
                Leaf(columns.Fixed(40));
            });
            stack.Add().Layered(layers =>
            {
                Leaf(layers.BaseLayer());
                Leaf(layers.Layer());
            });
            stack.Add().Banded(bands =>
            {
                Leaf(bands.Head());
                Leaf(bands.Body());
                Leaf(bands.Foot());
            });
            stack.Add().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Share();
                    columns.Share();
                });
                table.HeaderRows(header => Leaf(header.Cell()));
                Leaf(table.Cell());
                Leaf(table.Cell());
                table.FooterRows(footer => Leaf(footer.Cell()));
            });
        }));

        return (root, leaves);
    }

    [Fact]
    public void ResettingTheTreeReachesEveryBlockInEveryKindOfContainer()
    {
        (Block root, List<ResetCounted> leaves) = Tree();

        root.ResetState();

        Assert.All(leaves, leaf => Assert.Equal(1, leaf.Resets));
    }

#if NET
    [Fact]
    public void ResettingTheTreeAllocatesNothing()
    {
        // Allocation budget: the engine resets the whole tree before every pass, and the bands that repeat on every
        // page, so walking it must cost nothing. An iterator per container was 6% of what a long table allocated.
        (Block root, _) = Tree();
        root.ResetState();

        long before = GC.GetAllocatedBytesForCurrentThread();
        root.ResetState();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"Resetting the tree allocated {allocated:N0} bytes.");
    }
#endif

    /// <summary>Fixed content that counts how often it is reset.</summary>
    private sealed class ResetCounted : Block
    {
        public int Resets { get; private set; }

        protected override void ResetOwnState() => Resets++;

        protected override Fit PlanCore(Extent availableSpace, PlanContext context) => Fit.Complete(10f, 10f);

        protected override void RenderCore(Extent availableSpace, RenderContext context)
        {
        }
    }
}
