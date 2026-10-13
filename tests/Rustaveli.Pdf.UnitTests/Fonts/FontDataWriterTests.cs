using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class FontDataWriterTests
{
    private const int Capacity = 16;

    private static byte[] Ascending() => Enumerable.Range(1, Capacity).Select(value => (byte)value).ToArray();

    /// <summary>A writer whose buffer the bytes written fill exactly.</summary>
    private static FontDataWriter Filled()
    {
        FontDataWriter writer = new FontDataWriter(Capacity);
        writer.Bytes(Ascending());
        return writer;
    }

    [Fact]
    public void DetachesTheBytesWrittenFromALargerBuffer()
    {
        FontDataWriter writer = new FontDataWriter(Capacity);
        writer.UInt32(0x01020304);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, writer.DetachArray());
    }

    [Fact]
    public void DetachesABufferTheBytesFillExactly()
    {
        Assert.Equal(Ascending(), Filled().DetachArray());
    }

#if NET
    [Fact]
    public void HandsOverABufferTheBytesFillExactlyWithoutCopyingIt()
    {
        FontDataWriter writer = Filled();

        long before = GC.GetAllocatedBytesForCurrentThread();
        writer.DetachArray();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
#endif

    [Fact]
    public void StartsEmptyOnceDetachedSoLaterBytesStayItsOwn()
    {
        FontDataWriter writer = Filled();
        byte[] detached = writer.DetachArray();

        writer.UInt16(0xFFFF);

        Assert.Equal(Ascending(), detached);
        Assert.Equal(new byte[] { 0xFF, 0xFF }, writer.ToArray());
    }
}
