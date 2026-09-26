namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>A stream claiming twice the bytes it holds, like a file truncated after its size was read.</summary>
internal sealed class ShrinkingStream(byte[] data) : MemoryStream(data)
{
    public override long Length => base.Length * 2;
}
