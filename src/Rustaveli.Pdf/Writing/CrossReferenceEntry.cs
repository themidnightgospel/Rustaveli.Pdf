namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Where one object lives, in the three fields of a cross-reference stream row (ISO 32000-1, table 18). The
/// default value marks an object number that has been reserved but not yet written.
/// </summary>
internal readonly struct CrossReferenceEntry
{
    private CrossReferenceEntry(byte type, long field2, int field3)
    {
        Type = type;
        Field2 = field2;
        Field3 = field3;
    }

    /// <summary>The head of the free list, entry zero of every cross-reference section.</summary>
    public static CrossReferenceEntry FreeHead => new CrossReferenceEntry(0, 0, 65535);

    /// <summary>0 for free (or not yet written), 1 for an object at a byte offset, 2 for one inside an object stream.</summary>
    public byte Type { get; }

    /// <summary>The byte offset, or the object number of the containing object stream.</summary>
    public long Field2 { get; }

    /// <summary>The generation, or the index within the containing object stream.</summary>
    public int Field3 { get; }

    public bool IsWritten => Type != 0;

    public static CrossReferenceEntry AtOffset(long offset) => new CrossReferenceEntry(1, offset, 0);

    public static CrossReferenceEntry InObjectStream(int objectStreamNumber, int index) =>
        new CrossReferenceEntry(2, objectStreamNumber, index);
}
