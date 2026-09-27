using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfByteWriterStreamTests
{
    [Fact]
    public void IsWriteOnlyAndForwardOnly()
    {
        using PdfByteWriter target = new PdfByteWriter();
        using PdfByteWriterStream stream = new PdfByteWriterStream(target);

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void AppendsWhatIsWrittenToTheTarget()
    {
        using PdfByteWriter target = new PdfByteWriter();
        using PdfByteWriterStream stream = new PdfByteWriterStream(target);

        stream.Write(Latin1.Bytes("xabcx"), 1, 3);
        stream.Write("def"u8);
        stream.Flush();

        Assert.Equal("abcdef", Latin1.Text(target.WrittenSpan));
    }
}
