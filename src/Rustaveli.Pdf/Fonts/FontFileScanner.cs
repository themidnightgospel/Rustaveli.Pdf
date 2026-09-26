namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Reads what font matching needs from a font file on disk — its faces' names, styles and outline formats — by
/// seeking to the few tables involved instead of loading the file.
/// </summary>
internal static class FontFileScanner
{
    private const int FaceHeaderSize = 12;
    private const int TableRecordSize = 16;

    /// <summary>Describes every face in the file.</summary>
    /// <exception cref="FontFormatException">The file is not a font this library can read.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static IReadOnlyList<FontFaceInfo> Scan(string path)
    {
        FontFileSource source = new FontFileSource(path);
        using FileStream stream = Open(path);
        byte[] header = ReadHeader(stream);
        int count = FontContainer.CountFaces(header);
        List<FontFaceInfo> faces = new List<FontFaceInfo>(count);

        for (int index = 0; index < count; index++)
        {
            TableDirectory directory = ReadDirectory(stream, FontContainer.GetFaceOffset(header, index));

            HeadTable head = new HeadTable(ReadTable(stream, directory, TableTag.Head)
                ?? throw new FontFormatException("The font has no 'head' table."));
            byte[]? name = ReadTable(stream, directory, TableTag.Name);
            byte[]? os2 = ReadTable(stream, directory, TableTag.Os2);
            Os2Table? style = os2 is null ? null : new Os2Table(os2);

            faces.Add(new FontFaceInfo(
                source,
                index,
                name is null ? FontNames.None : NameTable.Read(name),
                FaceStyle.From(style, head),
                directory.Outlines,
                registered: false));
        }

        return faces;
    }

    /// <summary>Reads one face's character map, for asking whether it covers a character, and nothing else.</summary>
    public static CharacterMap ReadCharacterMap(string path, int faceIndex)
    {
        using FileStream stream = Open(path);
        byte[] header = ReadHeader(stream);
        TableDirectory directory = ReadDirectory(stream, FontContainer.GetFaceOffset(header, faceIndex));

        byte[] maxp = ReadTable(stream, directory, TableTag.Maxp)
            ?? throw new FontFormatException("The font has no 'maxp' table.");
        byte[] cmap = ReadTable(stream, directory, TableTag.Cmap)
            ?? throw new FontFormatException("The font has no 'cmap' table.");

        return new CharacterMap(cmap, new MaximumProfileTable(maxp).NumGlyphs);
    }

    private static FileStream Open(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.RandomAccess);

    /// <summary>The file header and, for a collection, the offset array the faces are located through.</summary>
    private static byte[] ReadHeader(FileStream stream)
    {
        byte[] header = ReadAt(stream, 0, FontContainer.CollectionHeaderSize);

        if (!FontContainer.IsCollection(header))
            return header;

        long size = FontContainer.CollectionHeaderSize + (4L * FontContainer.CountFaces(header));
        return ReadAt(stream, 0, size);
    }

    private static TableDirectory ReadDirectory(FileStream stream, int faceOffset)
    {
        byte[] start = ReadAt(stream, faceOffset, FaceHeaderSize);
        int count = BigEndian.UInt16(start, 4);
        byte[] directory = ReadAt(stream, faceOffset, FaceHeaderSize + (count * (long)TableRecordSize));

        return TableDirectory.Read(directory, 0, stream.Length);
    }

    private static byte[]? ReadTable(FileStream stream, TableDirectory directory, uint tag) =>
        directory.TryGet(tag, out TableRecord record) ? ReadAt(stream, record.Offset, record.Length) : null;

    /// <summary>Reads exactly <paramref name="count"/> bytes at <paramref name="offset"/>, which must exist.</summary>
    /// <remarks>
    /// The length is checked against the file before anything is allocated, so a forged table length cannot ask for
    /// more memory than the file has; a file that shrinks while being read is reported the same way.
    /// </remarks>
    internal static byte[] ReadAt(Stream stream, long offset, long count)
    {
        if (offset + count > stream.Length)
            throw FontFormatException.Truncated();

        byte[] buffer = new byte[count];
        stream.Position = offset;
        int total = 0;

        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);

            if (read == 0)
                throw FontFormatException.Truncated();

            total += read;
        }

        return buffer;
    }
}
