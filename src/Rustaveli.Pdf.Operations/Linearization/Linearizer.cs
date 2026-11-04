using System.Globalization;
using System.Text;
using Rustaveli.Pdf.Operations.Reading;
using Rustaveli.Pdf.Security;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Operations.Linearization;

/// <summary>
/// Rewrites a file for viewing over the web as it downloads (ISO 32000-1, annex F): the first page and all it needs
/// first, behind a dictionary saying where it ends, then each other page with what it alone needs, then what pages
/// share, then the rest — with hint tables telling a viewer where every page and shared object lies.
/// </summary>
/// <remarks>
/// Objects are renumbered so those of the first page's section come last, and the file is laid out twice or more, until
/// the hint stream, whose contents give offsets of what follows it, stops changing size.
/// </remarks>
internal static class Linearizer
{
    private static readonly PdfName Root = new PdfName("Root");
    private static readonly PdfName ObjStm = new PdfName("ObjStm");
    private static readonly PdfName S = new PdfName("S");
    private static readonly PdfName O = new PdfName("O");
    private static readonly PdfName PageMode = new PdfName("PageMode");
    private static readonly PdfName UseOutlines = new PdfName("UseOutlines");
    private static readonly PdfName Outlines = new PdfName("Outlines");

    /// <summary>Catalog entries a viewer needs before it shows the first page (F.3.4).</summary>
    private static readonly PdfName[] Opening =
    [
        new PdfName("ViewerPreferences"), new PdfName("PageMode"), new PdfName("Threads"), new PdfName("OpenAction"), new PdfName("AcroForm"),
    ];

    /// <summary>The width every offset in the linearization dictionary is written at, so it can be filled in last.</summary>
    private const string Offset = "D10";

    /// <summary>The length of a cross-reference entry: offset, generation, type and end of line (7.5.4).</summary>
    private const int EntryLength = 20;

    public static void Write(byte[] file, PdfEncryption? encryption, Stream output)
    {
        PdfSource source = PdfSource.Open(file);
        Dictionary<int, object> objects = [];

        foreach (int number in source.ObjectNumbers)
        {
            object value = source.GetObject(number);

            if (value is SourceStream stream
                && (PdfSource.IsOfType(stream.Dictionary, PdfNames.XRef)
                    || PdfSource.IsOfType(stream.Dictionary, ObjStm)))
            {
                continue;
            }

            if (value is PdfValue { Kind: PdfValueKind.Null })
                continue;

            objects[number] = value;
        }

        int catalog = source.Trailer[Root].AsReference().ObjectNumber;
        int? info = source.Trailer.TryGetValue(PdfNames.Info, out PdfValue given) && given.Kind == PdfValueKind.Reference ? given.AsReference().ObjectNumber : null;
        List<int> pages = source.Pages.Select(page => page.ObjectNumber).ToList();

        // Pages, the tree above them, the catalog and the information are where reaching stops: none belongs to a page.
        HashSet<int> boundaries = [.. source.PageTree, catalog];

        if (info is int infoNumber)
            boundaries.Add(infoNumber);

        List<List<int>> reached = pages.Select(page => Reach(page, objects, boundaries)).ToList();
        Dictionary<int, int> usage = [];

        foreach (int number in reached.SelectMany(set => set))
            usage[number] = usage.TryGetValue(number, out int count) ? count + 1 : 1;

        HashSet<int> firstPage = [.. reached[0]];
        List<int> opening = [catalog];
        HashSet<int> placed = [catalog, .. firstPage];

        // A document that opens showing its outline needs the outline with the first page (F.3.6).
        List<int> outline = [];
        PdfDictionary root = ((PdfValue)objects[catalog]).AsDictionary();

        // The file was finished plain, so what its encryption needs declared is declared as it is laid out.
        encryption?.DeclareExtension(root);

        if (root.TryGetValue(PageMode, out PdfValue mode) && mode.Kind == PdfValueKind.Name && mode.AsName().Equals(UseOutlines)
            && root.TryGetValue(Outlines, out PdfValue outlines))
        {
            outline = Reach(outlines, objects, boundaries).Where(placed.Add).ToList();
        }

        foreach (PdfName key in Opening)
        {
            if (root.TryGetValue(key, out PdfValue value))
            {
                foreach (int number in Reach(value, objects, boundaries).Where(number => !usage.ContainsKey(number) && placed.Add(number)))
                    opening.Add(number);
            }
        }

        List<List<int>> later = [];

        for (int page = 1; page < pages.Count; page++)
            later.Add(reached[page].Where(number => usage[number] == 1 && placed.Add(number)).ToList());

        List<int> shared = reached.Skip(1).SelectMany(set => set).Where(number => usage[number] > 1 && placed.Add(number)).ToList();
        List<int> rest = objects.Keys.Where(placed.Add).OrderBy(number => number).ToList();

        Layout layout = new Layout(opening, reached, outline, later, shared, rest, usage);
        layout.Write(objects, source, catalog, info, encryption, output);
    }

    /// <summary>The objects reachable from <paramref name="start"/>, in the order reached, stopping at <paramref name="boundaries"/>.</summary>
    private static List<int> Reach(int start, Dictionary<int, object> objects, HashSet<int> boundaries)
    {
        List<int> order = [start];
        HashSet<int> seen = [start];

        for (int index = 0; index < order.Count; index++)
        {
            if (!objects.TryGetValue(order[index], out object? value))
                continue;

            foreach (int number in References(value))
            {
                if (objects.ContainsKey(number) && !boundaries.Contains(number) && seen.Add(number))
                    order.Add(number);
            }
        }

        return order;
    }

    private static List<int> Reach(PdfValue value, Dictionary<int, object> objects, HashSet<int> boundaries)
    {
        List<int> order = [];

        foreach (int number in References(value))
        {
            if (objects.ContainsKey(number) && !boundaries.Contains(number) && !order.Contains(number))
            {
                foreach (int reached in Reach(number, objects, boundaries).Where(found => !order.Contains(found)))
                    order.Add(reached);
            }
        }

        return order;
    }

    private static IEnumerable<int> References(object value) =>
        value is SourceStream stream ? References(stream.Dictionary) : References((PdfValue)value);

    private static IEnumerable<int> References(PdfValue value)
    {
        switch (value.Kind)
        {
            case PdfValueKind.Reference:
                yield return value.AsReference().ObjectNumber;
                break;

            case PdfValueKind.Array:
                foreach (PdfValue item in value.AsArray())
                {
                    foreach (int number in References(item))
                        yield return number;
                }

                break;

            case PdfValueKind.Dictionary:
                foreach (KeyValuePair<PdfName, PdfValue> entry in value.AsDictionary())
                {
                    foreach (int number in References(entry.Value))
                        yield return number;
                }

                break;
        }
    }

    /// <summary>Where each object goes, what it is numbered, and the file laid out from them.</summary>
    private sealed class Layout
    {
        private readonly List<int> _opening;
        private readonly List<int> _firstPage;
        private readonly List<List<int>> _later;
        private readonly List<int> _shared;
        private readonly List<int> _rest;
        private readonly Dictionary<int, int> _usage;
        private readonly List<HashSet<int>> _reached;
        private readonly List<int> _outline;
        private readonly Dictionary<int, int> _numbers = [];

        // What every pass lays out alike, set once by Write: the objects serialized as numbered, the numbers given
        // around them, and the trailer up to its /Prev, which holds the file's identifier.
        private readonly Dictionary<int, byte[]> _serialized = [];
        private PdfEncryption? _encryption;
        private int _linearization;
        private int? _encrypt;
        private int _hint;
        private int _size;
        private int _mainCount;
        private byte[] _trailer = [];

        public Layout(List<int> opening, List<List<int>> reached, List<int> outline, List<List<int>> later, List<int> shared, List<int> rest, Dictionary<int, int> usage)
        {
            _opening = opening;
            _firstPage = reached[0];
            _outline = outline;
            _reached = reached.Select(set => new HashSet<int>(set)).ToList();
            _later = later;
            _shared = shared;
            _rest = rest;
            _usage = usage;
        }

        /// <summary>The objects of the main section, numbered from 1 in the order they are written.</summary>
        private IEnumerable<int> Main => _later.SelectMany(page => page).Concat(_shared).Concat(_rest);

        public void Write(
            Dictionary<int, object> objects,
            PdfSource source,
            int catalog,
            int? info,
            PdfEncryption? encryption,
            Stream output)
        {
            _encryption = encryption;
            int next = 1;

            foreach (int number in Main)
                _numbers[number] = next++;

            _mainCount = next;
            _linearization = next++;

            foreach (int number in _opening.Concat(_firstPage).Concat(_outline))
                _numbers[number] = next++;

            _encrypt = encryption is null ? null : next++;
            _hint = next++;
            _size = next;

            foreach (KeyValuePair<int, int> entry in _numbers)
                _serialized[entry.Value] = Serialize(entry.Value, objects[entry.Key], encryption);

            if (_encrypt is int encryptNumber)
                _serialized[encryptNumber] = Serialize(encryptNumber, encryption!.Dictionary, encryption: null);

            _trailer = Trailer(Identifier(source), catalog, info);

            byte[] hintObject = [];
            byte[] bytes = [];

            // The hint stream gives offsets of what follows it, so the file is laid out until its length settles.
            for (int pass = 0; pass < 8; pass++)
            {
                (byte[] laid, byte[] hints) = Lay(hintObject);
                bytes = laid;

                if (hints.Length == hintObject.Length && hints.AsSpan().SequenceEqual(hintObject))
                    break;

                hintObject = hints;
            }

            output.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// The file's identifier: its encryption's, which the keys depend on, or else the first the file gives, or
        /// else a new one.
        /// </summary>
        private byte[] Identifier(PdfSource source)
        {
            if (_encryption is not null)
                return _encryption.DocumentId;

            return source.Trailer.TryGetValue(PdfNames.ID, out PdfValue ids) && ids.Kind == PdfValueKind.Array
                && ids.AsArray().Count > 0
                ? ids.AsArray()[0].AsString().Bytes.ToArray()
                : StandardSecurity.Random(16);
        }

        /// <summary>The trailer of the first page's cross-reference section, as far as its <c>/Prev</c>.</summary>
        private byte[] Trailer(byte[] id, int catalog, int? info)
        {
            string encrypted = _encrypt is int e ? $"/Encrypt {e} 0 R" : string.Empty;
            string information = info is int i && _numbers.TryGetValue(i, out int infoNumber)
                ? $"/Info {infoNumber} 0 R"
                : string.Empty;
            string identifier = "<" + BitConverter.ToString(id).Replace("-", string.Empty) + ">";

            return Ascii(
                "trailer\n<</Size " + _size + "/Root " + _numbers[catalog] + " 0 R" + information
                + "/ID[" + identifier + identifier + "]" + encrypted + "/Prev ");
        }

        /// <summary>
        /// Lays the file out with <paramref name="hintObject"/> as its hint stream; returns the file, and the hint
        /// stream its offsets call for.
        /// </summary>
        private (byte[] File, byte[] Hints) Lay(byte[] hintObject)
        {
            using MemoryStream file = new MemoryStream();
            Dictionary<int, long> offsets = [];
            Dictionary<int, long> sizes = [];

            void Put(byte[] bytes) => file.Write(bytes, 0, bytes.Length);

            void Place(int number, byte[] bytes)
            {
                offsets[number] = file.Position;
                sizes[number] = bytes.Length;
                Put(bytes);
            }

            Put([.. "%PDF-1.7\n%"u8.ToArray(), 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);

            // The linearization dictionary is written with room for its offsets, filled in once they are known.
            long dictionaryAt = file.Position;
            byte[] dictionary = LinearizationDictionary(_linearization, 0, 0, 0, 0, 0, 0, 0);
            Place(_linearization, dictionary);

            long firstTable = file.Position;
            int firstCount = _size - _linearization;
            Put(Ascii($"xref\n{_linearization} {firstCount}\n"));
            long firstEntries = file.Position;
            Put(new byte[EntryLength * firstCount]);

            Put(_trailer);
            long prevAt = file.Position;
            Put(Ascii(Padded(0) + ">>\nstartxref\n0\n%%EOF\n"));

            foreach (int number in _opening)
                Place(_numbers[number], _serialized[_numbers[number]]);

            if (_encrypt is int encryptNumber)
                Place(encryptNumber, _serialized[encryptNumber]);

            long hintAt = file.Position;
            Place(_hint, hintObject);

            foreach (int number in _firstPage.Concat(_outline))
                Place(_numbers[number], _serialized[_numbers[number]]);

            long firstPageEnd = file.Position;

            foreach (int number in Main)
                Place(_numbers[number], _serialized[_numbers[number]]);

            long mainTable = file.Position;
            Put(Ascii($"xref\n0 {_mainCount}\n"));
            long mainEntries = file.Position;
            Put(Ascii("0000000000 65535 f\r\n"));

            for (int number = 1; number < _mainCount; number++)
                Put(InUse(offsets[number]));

            Put(Ascii($"trailer\n<</Size {_mainCount}>>\nstartxref\n{firstTable}\n%%EOF\n"));

            byte[] bytes = file.ToArray();

            for (int number = _linearization; number < _size; number++)
                InUse(offsets[number]).CopyTo(bytes, firstEntries + (EntryLength * (number - _linearization)));

            Ascii(Padded(mainTable)).CopyTo(bytes, prevAt);

            byte[] filled = LinearizationDictionary(
                _linearization,
                bytes.Length,
                hintAt,
                hintObject.Length,
                _numbers[_firstPage[0]],
                firstPageEnd,
                _later.Count + 1,
                mainEntries);

            filled.CopyTo(bytes, dictionaryAt);

            SourceStream hints = HintStream(offsets, sizes, hintAt, hintObject.Length, firstPageEnd);
            return (bytes, Serialize(_hint, hints, _encryption));
        }

        /// <summary>The hint stream: the page offset hint table, then the shared object hint table (F.4).</summary>
        private SourceStream HintStream(Dictionary<int, long> offsets, Dictionary<int, long> sizes, long hintAt, long hintLength, long firstPageEnd)
        {
            // Locations in hint tables are given as though the hint stream were not there (F.4).
            long Location(int number) => offsets[number] > hintAt ? offsets[number] - hintLength : offsets[number];

            // Every object of the first page's section is an entry of the shared object table, then each shared object.
            List<int> firstSection = [.. _firstPage, .. _outline];
            List<int> entries = [.. firstSection, .. _shared];
            Dictionary<int, int> identifiers = entries.Select((number, index) => (number, index)).ToDictionary(pair => pair.number, pair => pair.index);

            int pageCount = _later.Count + 1;
            long[] objectCounts = new long[pageCount];
            long[] lengths = new long[pageCount];
            List<int>[] references = new List<int>[pageCount];
            int firstObject = _numbers[_firstPage[0]];

            objectCounts[0] = firstSection.Count;
            lengths[0] = firstPageEnd - offsets[firstObject];
            references[0] = [];

            for (int page = 1; page < pageCount; page++)
            {
                List<int> own = _later[page - 1];
                objectCounts[page] = own.Count;
                lengths[page] = own.Count == 0 ? 0 : offsets[_numbers[own[^1]]] + sizes[_numbers[own[^1]]] - offsets[_numbers[own[0]]];

                // A page refers to the shared objects it reaches, whichever section they lie in.
                references[page] = _usage.Where(use => use.Value > 1 && _reached[page].Contains(use.Key)).Select(use => identifiers[use.Key]).OrderBy(identifier => identifier).ToList();
            }

            BitWriter table = new BitWriter();
            long leastObjects = objectCounts.Min();
            long leastLength = lengths.Min();
            int objectBits = BitWriter.Width(objectCounts.Max() - leastObjects);
            int lengthBits = BitWriter.Width(lengths.Max() - leastLength);
            int referenceBits = BitWriter.Width(references.Max(list => list.Count));
            int identifierBits = BitWriter.Width(Math.Max(0, entries.Count - 1));

            table.Write(leastObjects, 32);
            table.Write(Location(firstObject), 32);
            table.Write(objectBits, 16);
            table.Write(leastLength, 32);
            table.Write(lengthBits, 16);

            // Content stream offsets and lengths within pages are not given: nothing reads them.
            table.Write(0, 32);
            table.Write(0, 16);
            table.Write(0, 32);
            table.Write(0, 16);
            table.Write(referenceBits, 16);
            table.Write(identifierBits, 16);
            table.Write(0, 16);
            table.Write(1, 16);

            foreach (long count in objectCounts)
                table.Write(count - leastObjects, objectBits);

            table.Align();

            foreach (long length in lengths)
                table.Write(length - leastLength, lengthBits);

            table.Align();

            foreach (List<int> list in references)
                table.Write(list.Count, referenceBits);

            table.Align();

            foreach (int identifier in references.SelectMany(list => list))
                table.Write(identifier, identifierBits);

            table.Align();
            byte[] pageTable = table.ToArray();

            // Each shared object is a group of its own, and none carries a signature.
            BitWriter sharing = new BitWriter();
            long[] groupLengths = entries.Select(number => sizes[_numbers[number]]).ToArray();
            long leastGroup = groupLengths.Min();
            int groupBits = BitWriter.Width(groupLengths.Max() - leastGroup);

            sharing.Write(_shared.Count > 0 ? _numbers[_shared[0]] : 0, 32);
            sharing.Write(_shared.Count > 0 ? Location(_numbers[_shared[0]]) : 0, 32);
            sharing.Write(firstSection.Count, 32);
            sharing.Write(entries.Count, 32);
            sharing.Write(0, 16);
            sharing.Write(leastGroup, 32);
            sharing.Write(groupBits, 16);

            foreach (long length in groupLengths)
                sharing.Write(length - leastGroup, groupBits);

            sharing.Align();

            foreach (int _ in entries)
                sharing.Write(0, 1);

            byte[] sharedTable = sharing.ToArray();
            PdfDictionary dictionary = new PdfDictionary { [S] = pageTable.Length };
            byte[] data = [.. pageTable, .. sharedTable];

            if (_outline.Count > 0)
            {
                // The outline is one group: where it starts, how many objects and how many bytes.
                BitWriter outline = new BitWriter();
                int start = _numbers[_outline[0]];
                outline.Write(start, 32);
                outline.Write(Location(start), 32);
                outline.Write(_outline.Count, 32);
                outline.Write(_outline.Sum(number => sizes[_numbers[number]]), 32);

                dictionary[O] = data.Length;
                data = [.. data, .. outline.ToArray()];
            }

            return new SourceStream(dictionary, data);
        }

        private byte[] Serialize(int number, object value, PdfEncryption? encryption)
        {
            using PdfByteWriter writer = new PdfByteWriter(256);
            writer.WriteInteger(number);
            writer.Write(" 0 obj\n"u8);
            writer.StringCipher = encryption is null ? null : data => encryption.EncryptString(number, data);

            if (value is SourceStream stream)
            {
                PdfDictionary dictionary = Remap(stream.Dictionary).AsDictionary();
                byte[] data = encryption is not null && encryption.Covers(dictionary) ? encryption.EncryptStream(number, dictionary, stream.Data) : stream.Data;
                dictionary[PdfNames.Length] = data.Length;
                writer.WriteDictionary(dictionary);
                writer.StringCipher = null;
                writer.Write("\nstream\n"u8);
                writer.Write(data);
                writer.Write("\nendstream"u8);
            }
            else
            {
                writer.WriteValue(Remap((PdfValue)value));
                writer.StringCipher = null;
            }

            writer.Write("\nendobj\n"u8);
            return writer.WrittenSpan.ToArray();
        }

        private byte[] Serialize(int number, PdfDictionary dictionary, PdfEncryption? encryption) => Serialize(number, (object)(PdfValue)dictionary, encryption);

        private PdfValue Remap(PdfValue value)
        {
            switch (value.Kind)
            {
                case PdfValueKind.Reference:
                    return _numbers.TryGetValue(value.AsReference().ObjectNumber, out int renumbered) ? new PdfReference(renumbered) : PdfValue.Null;

                case PdfValueKind.Array:
                    PdfArray array = new PdfArray(value.AsArray().Count);
                    foreach (PdfValue item in value.AsArray())
                        array.Add(Remap(item));

                    return array;

                case PdfValueKind.Dictionary:
                    PdfDictionary dictionary = new PdfDictionary(value.AsDictionary().Count);
                    foreach (KeyValuePair<PdfName, PdfValue> entry in value.AsDictionary())
                        dictionary[entry.Key] = Remap(entry.Value);

                    return dictionary;

                default:
                    return value;
            }
        }

        private static byte[] LinearizationDictionary(int number, long length, long hintAt, long hintLength, int firstPage, long firstPageEnd, int pages, long mainEntries) =>
            Ascii(
                $"{number} 0 obj\n<</Linearized 1/L {Padded(length)}/H[{Padded(hintAt)} {Padded(hintLength)}]"
                + $"/O {Padded(firstPage)}/E {Padded(firstPageEnd)}/N {Padded(pages)}/T {Padded(mainEntries)}"
                + ">>\nendobj\n");

        /// <summary>The cross-reference entry of an object in use at <paramref name="offset"/>.</summary>
        private static byte[] InUse(long offset) => Ascii(Padded(offset) + " 00000 n\r\n");

        /// <summary><paramref name="value"/> at the width of an offset, so that it can be filled in last.</summary>
        private static string Padded(long value) => value.ToString(Offset, CultureInfo.InvariantCulture);

        private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
    }
}
