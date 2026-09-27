using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Writes a PDF file's physical structure: the header, numbered objects and streams, the cross-reference section
/// and the trailer. Objects go to the output as soon as they are written, so memory stays flat however long the
/// document grows.
/// </summary>
/// <remarks>
/// <para>
/// Object numbers come from <see cref="Reserve"/> — or from the <c>Write</c> overloads that reserve for you — and
/// each number must be written exactly once before <see cref="Finish"/>. Reserving first lets objects refer to each
/// other in any order: a page names its parent before the parent is written, and a font can be referenced from
/// every page and written only at the end, once its subset is known.
/// </para>
/// <para>
/// In <see cref="PdfCrossReferenceFormat.Stream"/> form, non-stream objects are gathered into object streams of
/// <see cref="ObjectsPerStream"/> and written when one fills, so they reach the output in batches rather than
/// immediately.
/// </para>
/// <para>
/// Not thread-safe: one writer produces one file, from one thread at a time. After an exception the output is
/// unusable; <see cref="Finish"/> refuses to complete a file with an object missing.
/// </para>
/// </remarks>
internal sealed class PdfFileWriter : IDisposable
{
    /// <summary>
    /// Streams shorter than this are never compressed: zlib's six bytes of framing plus deflate's block overhead
    /// outweigh anything so little data could save.
    /// </summary>
    public const int MinimumCompressibleLength = 32;

    /// <summary>
    /// Objects per object stream. Large enough that similar dictionaries share a compression window, small enough
    /// that a reader need not inflate much to reach any one object.
    /// </summary>
    public const int ObjectsPerStream = 100;

    /// <summary>Pending output is handed to the stream once it passes this size.</summary>
    private const int FlushThreshold = 64 * 1024;

    private readonly Stream _output;
    private readonly CompressionLevel _compressionLevel;
    private readonly byte[]? _fixedId;
    private readonly IncrementalHash? _hash;
    private readonly PdfByteWriter _pending = new PdfByteWriter(2 * FlushThreshold);
    private readonly PdfByteWriter _compressed = new PdfByteWriter(4096);
    private readonly List<CrossReferenceEntry> _entries = [CrossReferenceEntry.FreeHead];
    private readonly ObjectStreamBuilder? _objectStream;
    private PdfReference _objectStreamReference;
    private long _flushed;
    private bool _finished;

    public PdfFileWriter(Stream output, PdfWriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
            throw new ArgumentException("The output stream is not writable.", nameof(output));

        options ??= new PdfWriterOptions();
        if (options.DocumentId is { Length: not 16 })
            throw new ArgumentException("A document ID is 16 bytes long.", nameof(options));

        _output = output;
        _compressionLevel = options.CompressionLevel;
        _fixedId = options.DocumentId?.ToArray();
        _hash = _fixedId == null ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        _objectStream = options.CrossReferenceFormat == PdfCrossReferenceFormat.Stream
            ? new ObjectStreamBuilder()
            : null;

        // The comment's four bytes above 127 tell transfer tools the file is binary (ISO 32000-1, 7.5.2).
        _pending.Write("%PDF-1.7\n%"u8);
        _pending.Write([0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
    }

    /// <summary>Allocates the next object number. It must be written before <see cref="Finish"/>.</summary>
    public PdfReference Reserve()
    {
        ThrowIfFinished();
        _entries.Add(default);
        return new PdfReference(_entries.Count - 1);
    }

    /// <summary>Writes <paramref name="value"/> as a new indirect object and returns its reference.</summary>
    public PdfReference Write(PdfValue value)
    {
        PdfReference reference = Reserve();
        Write(reference, value);
        return reference;
    }

    /// <summary>Writes <paramref name="value"/> as the reserved object <paramref name="reference"/>.</summary>
    public void Write(PdfReference reference, PdfValue value)
    {
        CheckUnwritten(reference);

        // "4 0 obj 2 0 R endobj" is not an object but an alias, and strict readers such as qpdf reject it.
        if (value.Kind == PdfValueKind.Reference)
        {
            throw new ArgumentException(
                "An indirect object cannot consist of a reference; refer to the target directly.", nameof(value));
        }

        if (_objectStream != null)
        {
            int index = _objectStream.Add(reference.ObjectNumber, value);
            if (index == 0)
                _objectStreamReference = Reserve();

            _entries[reference.ObjectNumber] =
                CrossReferenceEntry.InObjectStream(_objectStreamReference.ObjectNumber, index);

            if (_objectStream.Count == ObjectsPerStream)
                WriteObjectStream();

            return;
        }

        long offset = BeginObject(reference);
        _pending.WriteValue(value);
        _pending.Write("\nendobj\n"u8);
        _entries[reference.ObjectNumber] = CrossReferenceEntry.AtOffset(offset);
        FlushIfFull();
    }

    /// <summary>Writes a stream as a new indirect object and returns its reference.</summary>
    public PdfReference WriteStream(
        PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfStreamCompression compression = PdfStreamCompression.Auto)
    {
        PdfReference reference = Reserve();
        WriteStream(reference, dictionary, data, compression);
        return reference;
    }

    /// <summary>
    /// Writes a stream as the reserved object <paramref name="reference"/>. The writer adds <c>/Length</c>, and
    /// <c>/Filter</c> when it compresses; <paramref name="dictionary"/> is not modified.
    /// </summary>
    public void WriteStream(
        PdfReference reference,
        PdfDictionary dictionary,
        ReadOnlySpan<byte> data,
        PdfStreamCompression compression = PdfStreamCompression.Auto)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        CheckUnwritten(reference);
        if (dictionary.ContainsKey(PdfNames.Length))
            throw new ArgumentException("The writer sets /Length itself.", nameof(dictionary));

        if (compression == PdfStreamCompression.Auto && dictionary.ContainsKey(PdfNames.Filter))
        {
            throw new ArgumentException(
                "A stream that already names a filter must be written with PdfStreamCompression.None.",
                nameof(dictionary));
        }

        bool compress = compression == PdfStreamCompression.Auto && ShouldCompress(data.Length);
        if (compress)
        {
            _compressed.Clear();
            ZlibEncoder.Compress(data, _compressionLevel, _compressed);
            compress = _compressed.Length < data.Length;
        }

        WriteStreamObject(reference, dictionary, compress ? _compressed.WrittenSpan : data, compress);
    }

    /// <summary>
    /// Completes the file: flushes any pending object stream, then writes the cross-reference section and the
    /// trailer naming <paramref name="root"/> as the catalog and <paramref name="info"/> as the information
    /// dictionary. Every reserved object must have been written by now.
    /// </summary>
    public void Finish(PdfReference root, PdfReference? info = null)
    {
        ThrowIfFinished();
        if (_objectStream is { Count: > 0 })
            WriteObjectStream();

        for (int number = 1; number < _entries.Count; number++)
        {
            if (!_entries[number].IsWritten)
                throw new InvalidOperationException($"Object {number} was reserved but never written.");
        }

        CheckReference(root);
        if (info is PdfReference infoReference)
            CheckReference(infoReference);

        byte[] id = DocumentId();
        if (_objectStream != null)
            WriteCrossReferenceStream(root, info, id);
        else
            WriteCrossReferenceTable(root, info, id);

        Flush();
        _output.Flush();
        _finished = true;
    }

    /// <summary>
    /// Releases the writer's buffers. Does not finish the file, and does not close the output stream.
    /// </summary>
    public void Dispose()
    {
        _pending.Dispose();
        _compressed.Dispose();
        _objectStream?.Dispose();
        _hash?.Dispose();
    }

    private static int ByteWidth(long value)
    {
        int width = 1;
        while (width < 8 && value >> (8 * width) != 0)
            width++;

        return width;
    }

    private static PdfDictionary Trailer(int size, PdfReference root, PdfReference? info, byte[] id)
    {
        PdfDictionary trailer = new PdfDictionary
        {
            [PdfNames.Size] = size,
            [PdfNames.Root] = root,
        };

        if (info is PdfReference infoReference)
            trailer[PdfNames.Info] = infoReference;

        PdfString idString = new PdfString(id, PdfStringForm.Hex);
        trailer[PdfNames.ID] = new PdfArray { idString, idString };
        return trailer;
    }

    private bool CompressionEnabled => _compressionLevel != CompressionLevel.NoCompression;

    private bool ShouldCompress(int length) => CompressionEnabled && length >= MinimumCompressibleLength;

    private void WriteStreamObject(
        PdfReference reference, PdfDictionary dictionary, ReadOnlySpan<byte> body, bool deflated)
    {
        long offset = BeginObject(reference);
        _pending.Write("<<"u8);
        _pending.WriteDictionaryEntries(dictionary);
        if (deflated)
        {
            _pending.WriteName(PdfNames.Filter);
            _pending.WriteName(PdfNames.FlateDecode);
        }

        _pending.WriteName(PdfNames.Length);
        _pending.WriteInteger(body.Length);
        _pending.Write(">>\nstream\n"u8);

        while (body.Length > 0)
        {
            int count = Math.Min(body.Length, FlushThreshold);
            _pending.Write(body.Slice(0, count));
            FlushIfFull();
            body = body.Slice(count);
        }

        _pending.Write("\nendstream\nendobj\n"u8);
        _entries[reference.ObjectNumber] = CrossReferenceEntry.AtOffset(offset);
        FlushIfFull();
    }

    private void WriteObjectStream()
    {
        ReadOnlySpan<byte> content = _objectStream!.Build(out int first);
        PdfDictionary dictionary = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.ObjStm,
            [PdfNames.N] = _objectStream.Count,
            [PdfNames.First] = first,
        };

        WriteStream(_objectStreamReference, dictionary, content);
        _objectStream.Clear();
    }

    private void WriteCrossReferenceTable(PdfReference root, PdfReference? info, byte[] id)
    {
        long start = Position();
        _pending.Write("xref\n0 "u8);
        _pending.WriteInteger(_entries.Count);
        _pending.Write("\n0000000000 65535 f \n"u8);

        for (int number = 1; number < _entries.Count; number++)
        {
            // Each line is exactly twenty bytes; readers seek to an entry by multiplying.
            Span<byte> line = _pending.GetSpan(20);
            _ = Utf8Formatter.TryFormat(_entries[number].Field2, line, out _, new StandardFormat('D', 10));
            " 00000 n \n"u8.CopyTo(line.Slice(10));
            _pending.Advance(20);
            FlushIfFull();
        }

        _pending.Write("trailer\n"u8);
        _pending.WriteDictionary(Trailer(_entries.Count, root, info, id));
        _pending.WriteByte((byte)'\n');
        WriteEnd(start);
    }

    private void WriteCrossReferenceStream(PdfReference root, PdfReference? info, byte[] id)
    {
        PdfReference self = Reserve();
        long start = Position();
        _entries[self.ObjectNumber] = CrossReferenceEntry.AtOffset(start);

        long largest = 0;
        foreach (CrossReferenceEntry entry in _entries)
            largest = Math.Max(largest, entry.Field2);

        int width = ByteWidth(largest);
        int rowLength = 1 + width + 2;
        bool compress = CompressionEnabled;

        PdfDictionary dictionary = Trailer(_entries.Count, root, info, id);
        dictionary[PdfNames.Type] = PdfNames.XRef;
        dictionary[PdfNames.W] = new PdfArray { 1, width, 2 };

        using PdfByteWriter rows = new PdfByteWriter(_entries.Count * (rowLength + 1));
        byte[] previous = new byte[rowLength];
        byte[] current = new byte[rowLength];
        foreach (CrossReferenceEntry entry in _entries)
        {
            current[0] = entry.Type;
            for (int index = 0; index < width; index++)
                current[1 + index] = (byte)(entry.Field2 >> (8 * (width - 1 - index)));

            BinaryPrimitives.WriteUInt16BigEndian(current.AsSpan(1 + width), (ushort)entry.Field3);

            if (compress)
            {
                // The PNG "Up" predictor: each byte minus the one above it. Consecutive rows differ in a byte or two,
                // so the result is mostly zeros and deflates to a fraction of the plain rows.
                rows.WriteByte(2);
                for (int index = 0; index < rowLength; index++)
                    rows.WriteByte((byte)(current[index] - previous[index]));

                (previous, current) = (current, previous);
            }
            else
            {
                rows.Write(current);
            }
        }

        ReadOnlySpan<byte> body = rows.WrittenSpan;
        if (compress)
        {
            dictionary[PdfNames.DecodeParms] = new PdfDictionary
            {
                [PdfNames.Columns] = rowLength,
                [PdfNames.Predictor] = 12,
            };

            _compressed.Clear();
            ZlibEncoder.Compress(body, _compressionLevel, _compressed);
            body = _compressed.WrittenSpan;
        }

        WriteStreamObject(self, dictionary, body, compress);
        WriteEnd(start);
    }

    private void WriteEnd(long crossReferenceOffset)
    {
        _pending.Write("startxref\n"u8);
        _pending.WriteInteger(crossReferenceOffset);
        _pending.Write("\n%%EOF\n"u8);
    }

    private byte[] DocumentId()
    {
        if (_fixedId != null)
            return _fixedId;

        // Everything written so far goes into the hash, so the ID identifies this content and nothing else — no
        // clock, no random bytes — and reproducible builds stay reproducible.
        Flush();
        return _hash!.GetHashAndReset().AsSpan(0, 16).ToArray();
    }

    private long BeginObject(PdfReference reference)
    {
        long offset = Position();
        _pending.WriteInteger(reference.ObjectNumber);
        _pending.Write(" 0 obj\n"u8);
        return offset;
    }

    private long Position() => _flushed + _pending.Length;

    private void FlushIfFull()
    {
        if (_pending.Length >= FlushThreshold)
            Flush();
    }

    private void Flush()
    {
        ArraySegment<byte> written = _pending.WrittenSegment;
        _hash?.AppendData(written.Array!, written.Offset, written.Count);
        _output.Write(written.Array!, written.Offset, written.Count);
        _flushed += written.Count;
        _pending.Clear();
    }

    private void CheckUnwritten(PdfReference reference)
    {
        CheckReference(reference);
        if (_entries[reference.ObjectNumber].IsWritten)
            throw new InvalidOperationException($"Object {reference.ObjectNumber} has already been written.");
    }

    private void CheckReference(PdfReference reference)
    {
        ThrowIfFinished();
        if (reference.ObjectNumber < 1 || reference.ObjectNumber >= _entries.Count)
        {
            throw new ArgumentException(
                $"Object {reference.ObjectNumber} was not reserved by this writer.", nameof(reference));
        }
    }

    private void ThrowIfFinished()
    {
        if (_finished)
            throw new InvalidOperationException("The file has been finished.");
    }
}
