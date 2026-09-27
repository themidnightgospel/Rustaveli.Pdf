using System.Security.Cryptography;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>Keeps every row a decoder hands over, for comparison.</summary>
internal sealed class RowCollector : IPngRowSink
{
    public List<byte[]> Rows { get; } = new List<byte[]>();

    public static RowCollector Decode(PngFile png)
    {
        RowCollector collector = new RowCollector();
        PngScanlineDecoder.Decode(png, collector);
        return collector;
    }

    public void Accept(ReadOnlySpan<byte> row) => Rows.Add(row.ToArray());

    public byte[] Concatenated() => Rows.SelectMany(row => row).ToArray();

    /// <summary>SHA-256 of the concatenated rows, lower-case hex, as the reference decoder printed them.</summary>
    public string Sha256()
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Concatenated()).Select(value => value.ToString("x2")));
    }
}
