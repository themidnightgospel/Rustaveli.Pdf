using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Reading;

/// <summary>
/// A PDF file being read: its cross-reference sections followed back through every update, its objects read as they
/// are asked for, and its pages with what they inherit.
/// </summary>
/// <remarks>
/// <para>
/// A file whose cross-reference section is missing, points at the wrong places or cannot be parsed is read as viewers
/// read it: the whole file is scanned for objects, the last of each number kept, and the trailer taken from the last
/// one written.
/// </para>
/// <para>Not thread-safe: objects are read and kept as they are first asked for.</para>
/// </remarks>
internal sealed class PdfSource
{
    private static readonly PdfName Root = new PdfName("Root");
    private static readonly PdfName Prev = new PdfName("Prev");
    private static readonly PdfName XRefStm = new PdfName("XRefStm");
    private static readonly PdfName Index = new PdfName("Index");
    private static readonly PdfName W = new PdfName("W");
    private static readonly PdfName First = new PdfName("First");
    private static readonly PdfName ObjStm = new PdfName("ObjStm");
    private static readonly PdfName XRef = new PdfName("XRef");
    private static readonly PdfName MediaBox = PdfNames.MediaBox;
    private static readonly PdfName CropBox = new PdfName("CropBox");
    private static readonly PdfName Rotate = new PdfName("Rotate");

    /// <summary>What a page takes from the page tree when it does not say for itself (7.7.3.4).</summary>
    private static readonly PdfName[] Inherited = [PdfNames.Resources, MediaBox, CropBox, Rotate];

    /// <summary>An object read from somewhere other than where the cross-reference section said.</summary>
    private static readonly object Misplaced = new object();

    private readonly Dictionary<int, SourceEntry> _entries = [];
    private readonly HashSet<int> _freed = [];
    private readonly Dictionary<int, object> _objects = [];
    private readonly Dictionary<int, (int[] Numbers, int[] Offsets, byte[] Data, int First)> _objectStreams = [];
    private readonly HashSet<int> _loading = [];
    private IReadOnlyList<SourcePage>? _pages;
    private bool _rebuilt;

    private PdfSource(byte[] data)
    {
        Data = data;
        Trailer = new PdfDictionary();
    }

    public byte[] Data { get; }

    /// <summary>The trailer of the file's latest update.</summary>
    public PdfDictionary Trailer { get; private set; }

    /// <summary>Whether the cross-reference section had to be rebuilt by scanning the file.</summary>
    public bool WasRepaired => _rebuilt;

    public PdfDictionary Catalog =>
        Resolve(Trailer.TryGetValue(Root, out PdfValue root) ? root : PdfValue.Null) is { Kind: PdfValueKind.Dictionary } catalog
            ? catalog.AsDictionary()
            : throw new UnreadableFileException("The file is not a PDF this library can read: it has no catalog.");

    /// <summary>The object numbers the file holds, in order.</summary>
    public IEnumerable<int> ObjectNumbers => _entries.Keys.OrderBy(number => number);

    public static PdfSource Open(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.AsSpan(0, Math.Min(data.Length, 1024)).IndexOf("%PDF-"u8) < 0)
            throw new UnreadableFileException("The file is not a PDF: it does not begin with a PDF header.");

        PdfSource source = new PdfSource(data);

        if (!source.TryReadCrossReferences() || !source.Trailer.ContainsKey(Root))
            source.Rebuild();

        _ = source.Catalog;
        return source;
    }

    /// <summary>
    /// The object numbered <paramref name="number"/>: a <see cref="PdfValue"/>, or a <see cref="SourceStream"/> for a
    /// stream. An object the file does not hold is null (7.3.10).
    /// </summary>
    public object GetObject(int number)
    {
        if (_objects.TryGetValue(number, out object? known))
            return known;

        // An object whose length refers back to itself would otherwise be read forever.
        if (!_loading.Add(number))
            return PdfValue.Null;

        try
        {
            object loaded = Load(number);

            if (ReferenceEquals(loaded, Misplaced))
            {
                if (_rebuilt)
                {
                    loaded = PdfValue.Null;
                }
                else
                {
                    Rebuild();
                    _loading.Remove(number);
                    return GetObject(number);
                }
            }

            _objects[number] = loaded;
            return loaded;
        }
        finally
        {
            _loading.Remove(number);
        }
    }

    /// <summary>A value with references followed; a stream gives its dictionary.</summary>
    public PdfValue Resolve(PdfValue value)
    {
        for (int hops = 0; value.Kind == PdfValueKind.Reference && hops < 32; hops++)
        {
            object target = GetObject(value.AsReference().ObjectNumber);
            value = target is SourceStream stream ? stream.Dictionary : (PdfValue)target;
        }

        return value.Kind == PdfValueKind.Reference ? PdfValue.Null : value;
    }

    /// <summary>The stream a value refers to, or null if it refers to something else.</summary>
    public SourceStream? Stream(PdfValue value) =>
        value.Kind == PdfValueKind.Reference ? GetObject(value.AsReference().ObjectNumber) as SourceStream : null;

    /// <summary>A stream's data with its filters undone.</summary>
    public byte[] Decode(SourceStream stream) => StreamDecoder.Decode(stream.Dictionary, stream.Data, Resolve);

    /// <summary>The pages, in order, with what each inherits from the page tree.</summary>
    public IReadOnlyList<SourcePage> Pages => _pages ??= ReadPages();

    private List<SourcePage> ReadPages()
    {
        List<SourcePage> pages = [];
        HashSet<int> visited = [];

        if (!Catalog.TryGetValue(PdfNames.Pages, out PdfValue tree) || tree.Kind != PdfValueKind.Reference)
            throw new UnreadableFileException("The file is not a PDF this library can read: its catalog has no page tree.");

        Visit(tree.AsReference().ObjectNumber, new PdfDictionary());
        return pages;

        void Visit(int number, PdfDictionary inherited)
        {
            if (!visited.Add(number) || Resolve(new PdfReference(number)) is not { Kind: PdfValueKind.Dictionary } found)
                return;

            PdfDictionary node = found.AsDictionary();

            if (node.TryGetValue(PdfNames.Kids, out PdfValue kids) && Resolve(kids) is { Kind: PdfValueKind.Array } children)
            {
                PdfDictionary passed = new PdfDictionary();

                foreach (PdfName key in Inherited)
                {
                    if (node.TryGetValue(key, out PdfValue own))
                        passed[key] = own;
                    else if (inherited.TryGetValue(key, out PdfValue before))
                        passed[key] = before;
                }

                foreach (PdfValue kid in children.AsArray())
                {
                    if (kid.Kind == PdfValueKind.Reference)
                        Visit(kid.AsReference().ObjectNumber, passed);
                }

                return;
            }

            PdfDictionary page = new PdfDictionary(node.Count + Inherited.Length);

            foreach (KeyValuePair<PdfName, PdfValue> entry in node)
                page[entry.Key] = entry.Value;

            foreach (PdfName key in Inherited)
            {
                if (!page.ContainsKey(key) && inherited.TryGetValue(key, out PdfValue value))
                    page[key] = value;
            }

            // A page with no media box anywhere above it is taken to be US Letter, as viewers take it.
            if (!page.ContainsKey(MediaBox))
                page[MediaBox] = new PdfArray(4) { 0, 0, 612, 792 };

            pages.Add(new SourcePage(this, number, page));
        }
    }

    private object Load(int number)
    {
        if (!_entries.TryGetValue(number, out SourceEntry entry))
            return PdfValue.Null;

        return entry.IsCompressed ? LoadCompressed(number, entry) : ReadAt(entry.Offset, number);
    }

    /// <summary>The object at <paramref name="offset"/>, which must say it is <paramref name="number"/>.</summary>
    private object ReadAt(long offset, int number)
    {
        if (offset < 0 || offset >= Data.Length)
            return Misplaced;

        PdfParser parser = new PdfParser(Data, (int)offset);

        if (!parser.TryReadInteger(out long found) || found != number || !parser.TryReadInteger(out _) || !parser.TryReadKeyword("obj"u8))
            return Misplaced;

        PdfValue value = parser.ReadValue();

        if (value.Kind == PdfValueKind.Dictionary && parser.TryReadKeyword("stream"u8))
            return new SourceStream(value.AsDictionary(), StreamData(parser.Position, value.AsDictionary()));

        return value;
    }

    /// <summary>
    /// A stream's data, from just past its <c>stream</c> keyword: as long as its dictionary says, or up to
    /// <c>endstream</c> where the length is wrong.
    /// </summary>
    private byte[] StreamData(int position, PdfDictionary dictionary)
    {
        // The keyword is followed by a line feed, or a carriage return and line feed; a lone carriage return is tolerated.
        if (position < Data.Length && Data[position] == '\r')
            position++;

        if (position < Data.Length && Data[position] == '\n')
            position++;

        if (dictionary.TryGetValue(PdfNames.Length, out PdfValue given)
            && Resolve(given) is { Kind: PdfValueKind.Integer } length
            && length.AsInteger() >= 0
            && position + length.AsInteger() <= Data.Length)
        {
            int end = position + (int)length.AsInteger();
            PdfParser after = new PdfParser(Data, end);

            if (after.TryReadKeyword("endstream"u8))
                return Data.AsSpan(position, end - position).ToArray();
        }

        int found = Data.AsSpan(position).IndexOf("endstream"u8);

        if (found < 0)
            throw new UnreadableFileException("The file is not a PDF this library can read: a stream has no end.");

        int stop = position + found;

        if (stop > position && Data[stop - 1] == '\n')
            stop--;

        if (stop > position && Data[stop - 1] == '\r')
            stop--;

        return Data.AsSpan(position, stop - position).ToArray();
    }

    private object LoadCompressed(int number, SourceEntry entry)
    {
        if (!_objectStreams.TryGetValue(entry.Stream, out (int[] Numbers, int[] Offsets, byte[] Data, int First) held))
        {
            if (GetObject(entry.Stream) is not SourceStream stream)
                return Misplaced;

            byte[] data = Decode(stream);
            int count = Resolve(stream.Dictionary.TryGetValue(PdfNames.N, out PdfValue n) ? n : PdfValue.Null) is { Kind: PdfValueKind.Integer } total
                ? (int)total.AsInteger()
                : 0;
            int first = Resolve(stream.Dictionary.TryGetValue(First, out PdfValue f) ? f : PdfValue.Null) is { Kind: PdfValueKind.Integer } start
                ? (int)start.AsInteger()
                : 0;

            PdfParser header = new PdfParser(data);
            List<int> numbers = [];
            List<int> offsets = [];

            for (int index = 0; index < count && header.TryReadInteger(out long contained) && header.TryReadInteger(out long offset); index++)
            {
                numbers.Add((int)contained);
                offsets.Add((int)offset);
            }

            held = (numbers.ToArray(), offsets.ToArray(), data, first);
            _objectStreams[entry.Stream] = held;
        }

        int position = entry.Index < held.Numbers.Length && held.Numbers[entry.Index] == number
            ? entry.Index
            : Array.IndexOf(held.Numbers, number);

        if (position < 0 || held.First + held.Offsets[position] >= held.Data.Length)
            return Misplaced;

        return new PdfParser(held.Data, held.First + held.Offsets[position]).ReadValue();
    }

    private bool TryReadCrossReferences()
    {
        try
        {
            int keyword = Data.AsSpan(Math.Max(0, Data.Length - 4096)).LastIndexOf("startxref"u8);

            if (keyword < 0)
                return false;

            PdfParser parser = new PdfParser(Data, Math.Max(0, Data.Length - 4096) + keyword + "startxref".Length);

            if (!parser.TryReadInteger(out long offset))
                return false;

            HashSet<long> visited = [];
            bool latest = true;

            while (offset > 0 && offset < Data.Length && visited.Add(offset))
            {
                PdfDictionary trailer = ReadSection(offset);

                if (latest)
                {
                    Trailer = trailer;
                    latest = false;
                }

                offset = trailer.TryGetValue(Prev, out PdfValue previous) && previous.Kind == PdfValueKind.Integer ? previous.AsInteger() : -1;
            }

            return !latest;
        }
        catch (Exception exception) when (exception is UnreadableFileException or NotSupportedException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            _entries.Clear();
            _freed.Clear();
            return false;
        }
    }

    /// <summary>
    /// Reads the cross-reference section at <paramref name="offset"/> — a table or a stream — keeping only entries
    /// no later section gave, and returns its trailer.
    /// </summary>
    private PdfDictionary ReadSection(long offset)
    {
        PdfParser parser = new PdfParser(Data, (int)offset);

        if (!parser.TryReadKeyword("xref"u8))
            return ReadStreamSection(offset);

        List<(int Number, SourceEntry? Entry)> table = [];

        while (parser.TryReadInteger(out long start) && parser.TryReadInteger(out long count))
        {
            for (long index = 0; index < count; index++)
            {
                if (!parser.TryReadInteger(out long position) || !parser.TryReadInteger(out _))
                    throw parser.Damaged("a cross-reference entry is not an offset and a generation");

                ReadOnlySpan<byte> type = parser.ReadToken();

                // A common mistake numbers the first subsection from 1 though it begins with object 0, the free head.
                if (index == 0 && start == 1 && position == 0 && type.SequenceEqual("f"u8))
                    start = 0;

                int number = (int)(start + index);

                if (type.SequenceEqual("n"u8))
                    table.Add((number, SourceEntry.At(position)));
                else if (type.SequenceEqual("f"u8))
                    table.Add((number, null));
                else
                    throw parser.Damaged("a cross-reference entry is neither in use nor free");
            }
        }

        if (!parser.TryReadKeyword("trailer"u8))
            throw parser.Damaged("a cross-reference table has no trailer");

        PdfDictionary trailer = parser.ReadValue().AsDictionary();

        // A hybrid file lists in a stream the objects its table marks free for older readers (7.5.8.4).
        if (trailer.TryGetValue(XRefStm, out PdfValue hybrid) && hybrid.Kind == PdfValueKind.Integer)
            ReadStreamSection(hybrid.AsInteger());

        foreach ((int number, SourceEntry? entry) in table)
            Record(number, entry);

        return trailer;
    }

    private PdfDictionary ReadStreamSection(long offset)
    {
        PdfParser parser = new PdfParser(Data, (int)offset);

        if (!parser.TryReadInteger(out long number) || !parser.TryReadInteger(out _) || !parser.TryReadKeyword("obj"u8))
            throw parser.Damaged("a cross-reference section is neither a table nor a stream");

        PdfValue value = parser.ReadValue();

        if (value.Kind != PdfValueKind.Dictionary || !parser.TryReadKeyword("stream"u8))
            throw parser.Damaged("a cross-reference stream is not a stream");

        PdfDictionary dictionary = value.AsDictionary();

        if (!dictionary.TryGetValue(PdfNames.Type, out PdfValue type) || type.Kind != PdfValueKind.Name || !type.AsName().Equals(XRef))
            throw parser.Damaged("a cross-reference stream is not of type XRef");

        byte[] data = StreamDecoder.Decode(dictionary, StreamData(parser.Position, dictionary), value => value);
        PdfArray widths = dictionary[W].AsArray();
        int[] width = [(int)widths[0].AsInteger(), (int)widths[1].AsInteger(), (int)widths[2].AsInteger()];
        int row = width[0] + width[1] + width[2];
        long size = dictionary[PdfNames.Size].AsInteger();
        PdfArray ranges = dictionary.TryGetValue(Index, out PdfValue given) ? given.AsArray() : new PdfArray(2) { 0, size };
        int position = 0;

        for (int range = 0; range + 1 < ranges.Count; range += 2)
        {
            long start = ranges[range].AsInteger();
            long count = ranges[range + 1].AsInteger();

            for (long index = 0; index < count && position + row <= data.Length; index++, position += row)
            {
                long kind = width[0] == 0 ? 1 : Field(data, position, width[0]);
                long second = Field(data, position + width[0], width[1]);
                long third = Field(data, position + width[0] + width[1], width[2]);

                switch (kind)
                {
                    case 0:
                        Record((int)(start + index), null);
                        break;
                    case 1:
                        Record((int)(start + index), SourceEntry.At(second));
                        break;
                    case 2:
                        Record((int)(start + index), SourceEntry.InStream((int)second, (int)third));
                        break;
                }
            }
        }

        // The stream itself is found by where it was read, whether or not it lists itself.
        Record((int)number, SourceEntry.At(offset));
        return dictionary;
    }

    private static long Field(byte[] data, int position, int width)
    {
        long value = 0;

        for (int index = 0; index < width; index++)
            value = (value << 8) | data[position + index];

        return value;
    }

    /// <summary>Keeps an entry unless a later section already said where the object is, or that it was freed.</summary>
    private void Record(int number, SourceEntry? entry)
    {
        if (number <= 0 || _entries.ContainsKey(number) || _freed.Contains(number))
            return;

        if (entry is { } found)
            _entries[number] = found;
        else
            _freed.Add(number);
    }

    /// <summary>Finds every object by scanning the file, as viewers do when its cross-reference section fails them.</summary>
    private void Rebuild()
    {
        _entries.Clear();
        _freed.Clear();
        _objects.Clear();
        _objectStreams.Clear();
        _pages = null;
        _rebuilt = true;

        for (int at = Data.AsSpan().IndexOf("obj"u8); at >= 0; at = Next(at + 3))
        {
            if (at + 3 < Data.Length && PdfParser.IsRegular(Data[at + 3]))
                continue;

            int start = ObjectStart(at);

            if (start >= 0)
            {
                PdfParser parser = new PdfParser(Data, start);
                parser.TryReadInteger(out long number);

                if (number is > 0 and <= int.MaxValue)
                    _entries[(int)number] = SourceEntry.At(start);
            }
        }

        // Objects kept in object streams are found through the streams the scan found; the catalog may be among them.
        foreach (int number in _entries.Keys.ToList())
        {
            if (GetObject(number) is not SourceStream stream
                || !stream.Dictionary.TryGetValue(PdfNames.Type, out PdfValue type)
                || type.Kind != PdfValueKind.Name
                || !type.AsName().Equals(ObjStm))
            {
                continue;
            }

            try
            {
                PdfParser header = new PdfParser(Decode(stream));
                long count = stream.Dictionary.TryGetValue(PdfNames.N, out PdfValue n) && n.Kind == PdfValueKind.Integer ? n.AsInteger() : 0;

                for (int index = 0; index < count && header.TryReadInteger(out long contained) && header.TryReadInteger(out _); index++)
                {
                    if (contained is > 0 and <= int.MaxValue && !_entries.ContainsKey((int)contained))
                        _entries[(int)contained] = SourceEntry.InStream(number, index);
                }
            }
            catch (Exception exception) when (exception is NotSupportedException or UnreadableFileException)
            {
                // An object stream that cannot be read holds nothing that can be recovered.
            }
        }

        Trailer = RebuiltTrailer();

        int Next(int from) => from >= Data.Length ? -1 : Data.AsSpan(from).IndexOf("obj"u8) is int found and >= 0 ? from + found : -1;
    }

    /// <summary>Where "<c>n g obj</c>" begins, for the keyword at <paramref name="keyword"/>, or -1 if it is not one.</summary>
    private int ObjectStart(int keyword)
    {
        int position = keyword;

        if (!SkipBack(ref position, whitespace: true) || !SkipBack(ref position, whitespace: false))
            return -1;

        if (!SkipBack(ref position, whitespace: true) || !SkipBack(ref position, whitespace: false))
            return -1;

        return position == 0 || !PdfParser.IsRegular(Data[position - 1]) ? position : -1;

        bool SkipBack(ref int at, bool whitespace)
        {
            int start = at;

            while (at > 0 && (whitespace ? PdfParser.IsWhitespace(Data[at - 1]) : Data[at - 1] is >= (byte)'0' and <= (byte)'9'))
                at--;

            return at < start;
        }
    }

    /// <summary>The trailer of a file being rebuilt: its last, or the catalog found by its type.</summary>
    private PdfDictionary RebuiltTrailer()
    {
        PdfDictionary trailer = new PdfDictionary();

        for (int at = Data.AsSpan().IndexOf("trailer"u8); at >= 0;)
        {
            try
            {
                PdfValue found = new PdfParser(Data, at + "trailer".Length).ReadValue();

                if (found.Kind == PdfValueKind.Dictionary)
                {
                    foreach (KeyValuePair<PdfName, PdfValue> entry in found.AsDictionary())
                        trailer[entry.Key] = entry.Value;
                }
            }
            catch (UnreadableFileException)
            {
                // A damaged trailer adds nothing.
            }

            int next = Data.AsSpan(at + 1).IndexOf("trailer"u8);
            at = next < 0 ? -1 : at + 1 + next;
        }

        if (trailer.ContainsKey(Root))
            return trailer;

        foreach (int number in _entries.Keys.OrderByDescending(number => number))
        {
            PdfValue value = GetObject(number) is SourceStream stream ? stream.Dictionary : (PdfValue)GetObject(number);

            if (value.Kind != PdfValueKind.Dictionary || !value.AsDictionary().TryGetValue(PdfNames.Type, out PdfValue type) || type.Kind != PdfValueKind.Name)
                continue;

            if (type.AsName().Equals(XRef) || type.AsName().Equals(PdfNames.Catalog))
            {
                PdfDictionary dictionary = value.AsDictionary();

                if (type.AsName().Equals(PdfNames.Catalog))
                    trailer[Root] = new PdfReference(number);
                else if (dictionary.TryGetValue(Root, out PdfValue root))
                    trailer[Root] = root;

                if (trailer.ContainsKey(Root))
                    return trailer;
            }
        }

        throw new UnreadableFileException("The file is not a PDF this library can read: no catalog could be found in it.");
    }
}
