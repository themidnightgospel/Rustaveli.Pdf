using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// The XXH64 hash (seed 0), as specified at https://github.com/Cyan4973/xxHash/blob/dev/doc/xxhash_spec.md.
/// </summary>
/// <remarks>
/// Written out rather than taken from System.IO.Hashing, which is a separate package the core must not depend on,
/// or from the platform's SHA-256, which on .NET Framework refuses to run on machines that enforce FIPS policy.
/// The hash only picks candidates for de-duplication and a match is confirmed byte for byte
/// (<c>RasterImage.HasSameContent</c>), so collision resistance is not required of it.
/// </remarks>
internal static class XxHash64
{
    private const ulong Prime1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime3 = 0x165667B19E3779F9UL;
    private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime5 = 0x27D4EB2F165667C5UL;

    public static ulong Hash(ReadOnlySpan<byte> data)
    {
        int length = data.Length;
        int offset = 0;
        ulong hash;

        if (length >= 32)
        {
            ulong lane1 = unchecked(Prime1 + Prime2);
            ulong lane2 = Prime2;
            ulong lane3 = 0;
            ulong lane4 = unchecked(0UL - Prime1);

            for (; offset + 32 <= length; offset += 32)
            {
                lane1 = Round(lane1, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8)));
                lane2 = Round(lane2, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 8, 8)));
                lane3 = Round(lane3, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 16, 8)));
                lane4 = Round(lane4, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 24, 8)));
            }

            hash = unchecked(
                RotateLeft(lane1, 1) + RotateLeft(lane2, 7) + RotateLeft(lane3, 12) + RotateLeft(lane4, 18));
            hash = MergeRound(hash, lane1);
            hash = MergeRound(hash, lane2);
            hash = MergeRound(hash, lane3);
            hash = MergeRound(hash, lane4);
        }
        else
        {
            hash = Prime5;
        }

        hash = unchecked(hash + (ulong)length);

        for (; offset + 8 <= length; offset += 8)
        {
            hash ^= Round(0, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8)));
            hash = unchecked((RotateLeft(hash, 27) * Prime1) + Prime4);
        }

        if (offset + 4 <= length)
        {
            hash ^= unchecked(BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4)) * Prime1);
            hash = unchecked((RotateLeft(hash, 23) * Prime2) + Prime3);
            offset += 4;
        }

        for (; offset < length; offset++)
        {
            hash ^= unchecked(data[offset] * Prime5);
            hash = unchecked(RotateLeft(hash, 11) * Prime1);
        }

        hash ^= hash >> 33;
        hash = unchecked(hash * Prime2);
        hash ^= hash >> 29;
        hash = unchecked(hash * Prime3);
        hash ^= hash >> 32;
        return hash;
    }

    private static ulong Round(ulong accumulator, ulong input)
    {
        accumulator = unchecked(accumulator + (input * Prime2));
        accumulator = RotateLeft(accumulator, 31);
        return unchecked(accumulator * Prime1);
    }

    private static ulong MergeRound(ulong hash, ulong lane)
    {
        hash ^= Round(0, lane);
        return unchecked((hash * Prime1) + Prime4);
    }

    private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));
}
