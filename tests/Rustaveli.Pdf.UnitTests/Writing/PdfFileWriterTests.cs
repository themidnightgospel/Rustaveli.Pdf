using System.IO.Compression;
using System.Security.Cryptography;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfFileWriterTests
{
    private static readonly byte[] FixedId = Enumerable.Range(0, 16).Select(index => (byte)index).ToArray();

    private static readonly PdfName Type = new PdfName("Type");

    private static readonly PdfName Catalog = new PdfName("Catalog");

    // Theory data is spelled as names: test methods are public and the writer's enums are internal.
    private static PdfCrossReferenceFormat Format(string name) =>
        (PdfCrossReferenceFormat)Enum.Parse(typeof(PdfCrossReferenceFormat), name);

    private static PdfDictionary CatalogDictionary() => new PdfDictionary { [Type] = Catalog };

    private static byte[] Write(
        PdfWriterOptions options,
        Func<PdfFileWriter, PdfReference> build,
        Func<PdfFileWriter, PdfReference?>? info = null)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfFileWriter writer = new PdfFileWriter(output, options))
        {
            PdfReference root = build(writer);
            writer.Finish(root, info?.Invoke(writer));
        }

        return output.ToArray();
    }

    private static PdfWriterOptions Options(
        PdfCrossReferenceFormat format,
        CompressionLevel level = CompressionLevel.Optimal,
        byte[]? id = null) =>
        new PdfWriterOptions { CrossReferenceFormat = format, CompressionLevel = level, DocumentId = id };

    private static byte[] Random(int length, int seed)
    {
        byte[] data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    /// <summary>
    /// Finds data that deflates to exactly <paramref name="saving"/> bytes fewer than it holds, framing included.
    /// Searched for rather than hard-coded because deflate's output differs between runtimes.
    /// </summary>
    private static byte[] DataThatDeflatesBy(int saving)
    {
        for (int length = 40; length < 400; length++)
        {
            for (int zeros = 0; zeros < length; zeros++)
            {
                byte[] data = new byte[length];
                Random(length - zeros, length).CopyTo(data, zeros);
                using PdfByteWriter probe = new PdfByteWriter();
                ZlibEncoder.Compress(data, CompressionLevel.Optimal, probe);
                if (probe.Length == length - saving)
                    return data;
            }
        }

        throw new InvalidOperationException($"No data found that deflates by exactly {saving} bytes.");
    }

    [Fact]
    public void WritesAMinimalTableFileExactly()
    {
        byte[] file = Write(Options(PdfCrossReferenceFormat.Table, id: FixedId), writer => writer.Write(CatalogDictionary()));

        string id = "<000102030405060708090A0B0C0D0E0F>";
        Assert.Equal(
            "%PDF-1.7\n%âãÏÓ\n"
            + "1 0 obj\n<</Type/Catalog>>\nendobj\n"
            + "xref\n0 2\n0000000000 65535 f \n0000000015 00000 n \n"
            + $"trailer\n<</Size 2/Root 1 0 R/ID[{id}{id}]>>\n"
            + "startxref\n48\n%%EOF\n",
            Latin1.Text(file));
    }

    [Fact]
    public void WritesAMinimalStreamFileExactly()
    {
        byte[] file = Write(
            Options(PdfCrossReferenceFormat.Stream, CompressionLevel.NoCompression, FixedId),
            writer => writer.Write(CatalogDictionary()));

        string id = "<000102030405060708090A0B0C0D0E0F>";
        string rows = "\0\0ÿÿ" + "\u0002\u0002\0\0" + "\u0001\u000F\0\0" + "\u0001m\0\0";
        Assert.Equal(
            "%PDF-1.7\n%âãÏÓ\n"
            + "2 0 obj\n<</Type/ObjStm/N 1/First 4/Length 22>>\nstream\n1 0\n<</Type/Catalog>>\n\nendstream\nendobj\n"
            + $"3 0 obj\n<</Size 4/Root 1 0 R/ID[{id}{id}]/Type/XRef/W[1 1 2]/Length 16>>\nstream\n{rows}\nendstream\nendobj\n"
            + "startxref\n109\n%%EOF\n",
            Latin1.Text(file));
    }

    [Theory]
    [InlineData(nameof(PdfCrossReferenceFormat.Table))]
    [InlineData(nameof(PdfCrossReferenceFormat.Stream))]
    public void ReadsBackEveryObjectThroughTheCrossReferenceSection(string formatName)
    {
        PdfCrossReferenceFormat format = Format(formatName);
        byte[] compressible = Enumerable.Range(0, 5000).Select(index => (byte)(index % 7)).ToArray();
        byte[] incompressible = Random(3000, 7);
        PdfReference array = default;
        PdfReference text = default;
        PdfReference deflated = default;
        PdfReference raw = default;
        PdfReference late = default;

        byte[] file = Write(Options(format), writer =>
        {
            late = writer.Reserve();
            array = writer.Write(new PdfArray { 1, 2.5, new PdfName("X"), late });
            text = writer.Write(new PdfString(Latin1.Bytes("hello (world)")));
            deflated = writer.WriteStream(new PdfDictionary { [new PdfName("Kind")] = 1 }, compressible);
            raw = writer.WriteStream(new PdfDictionary(), incompressible);
            writer.Write(late, new PdfDictionary { [new PdfName("Back")] = array });
            return writer.Write(CatalogDictionary());
        });

        PdfFileReader reader = new PdfFileReader(file);
        Assert.Equal(format == PdfCrossReferenceFormat.Stream, reader.HasCrossReferenceStream);
        Assert.Equal(
            new List<object?> { 1L, 2.5, new ParsedName("X"), new ParsedReference(late.ObjectNumber, 0) },
            reader.GetObject(array.ObjectNumber));
        Assert.Equal(Latin1.Bytes("hello (world)"), reader.GetObject(text.ObjectNumber));
        Assert.Equal(new ParsedReference(array.ObjectNumber, 0), reader.Dictionary(new ParsedReference(late.ObjectNumber, 0))["Back"]);

        ParsedStream deflatedStream = (ParsedStream)reader.GetObject(deflated.ObjectNumber)!;
        Assert.Equal(new ParsedName("FlateDecode"), deflatedStream.Dictionary["Filter"]);
        Assert.Equal(1L, deflatedStream.Dictionary["Kind"]);
        Assert.Equal(compressible, PdfFileReader.Decode(deflatedStream));

        ParsedStream rawStream = (ParsedStream)reader.GetObject(raw.ObjectNumber)!;
        Assert.False(rawStream.Dictionary.ContainsKey("Filter"));
        Assert.Equal(incompressible, rawStream.Data);

        Assert.Equal(reader.Size, reader.Entries.Count);
        Assert.Equal(new ParsedName("Catalog"), reader.Dictionary(reader.Trailer["Root"])["Type"]);
    }

    [Fact]
    public void PacksNonStreamObjectsIntoObjectStreamsOfAHundred()
    {
        byte[] file = Write(Options(PdfCrossReferenceFormat.Stream), writer =>
        {
            for (int index = 0; index < 249; index++)
                writer.Write(new PdfArray { index });

            return writer.Write(CatalogDictionary());
        });

        PdfFileReader reader = new PdfFileReader(file);
        List<IGrouping<long, KeyValuePair<int, (int Type, long Field2, int Field3)>>> containers = reader.Entries
            .Where(entry => entry.Value.Type == 2)
            .GroupBy(entry => entry.Value.Field2)
            .ToList();

        Assert.Equal(new[] { 100, 100, 50 }, containers.Select(group => group.Count()));
        foreach (IGrouping<long, KeyValuePair<int, (int Type, long Field2, int Field3)>> container in containers)
        {
            Assert.Equal(1, reader.Entries[(int)container.Key].Type);
            ParsedStream stream = (ParsedStream)reader.GetObject((int)container.Key)!;
            Assert.Equal((long)container.Count(), stream.Dictionary["N"]);
            Assert.Equal(Enumerable.Range(0, container.Count()), container.Select(entry => entry.Value.Field3));
        }

        int value = 0;
        IEnumerable<int> packed = reader.Entries.Where(entry => entry.Value.Type == 2).Select(entry => entry.Key);
        foreach (int number in packed.OrderBy(number => number).Take(249))
            Assert.Equal(new List<object?> { (long)value++ }, reader.GetObject(number));
    }

    [Fact]
    public void WritesEveryObjectInPlainTextInTableForm()
    {
        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            writer.Write(new PdfArray { 1 });
            return writer.Write(CatalogDictionary());
        });

        PdfFileReader reader = new PdfFileReader(file);
        Assert.All(reader.Entries.Where(entry => entry.Key > 0), entry => Assert.Equal(1, entry.Value.Type));
        Assert.Equal((0, 0L, 65535), reader.Entries[0]);
    }

    [Theory]
    [InlineData(PdfFileWriter.MinimumCompressibleLength - 1, false)]
    [InlineData(PdfFileWriter.MinimumCompressibleLength, true)]
    public void CompressesOnlyStreamsLongEnoughToBenefit(int length, bool compressed)
    {
        byte[] data = Enumerable.Repeat((byte)'a', length).ToArray();
        PdfReference stream = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            stream = writer.WriteStream(new PdfDictionary(), data);
            return writer.Write(CatalogDictionary());
        });

        ParsedStream parsed = (ParsedStream)new PdfFileReader(file).GetObject(stream.ObjectNumber)!;
        Assert.Equal(compressed, parsed.Dictionary.ContainsKey("Filter"));
        Assert.Equal(data, PdfFileReader.Decode(parsed));
        Assert.Equal((long)parsed.Data.Length, parsed.Dictionary["Length"]);
    }

    [Fact]
    public void KeepsDataUncompressedWhenDeflateWouldEnlargeIt()
    {
        byte[] data = Random(64, 3);
        PdfReference stream = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            stream = writer.WriteStream(new PdfDictionary(), data);
            return writer.Write(CatalogDictionary());
        });

        ParsedStream parsed = (ParsedStream)new PdfFileReader(file).GetObject(stream.ObjectNumber)!;
        Assert.False(parsed.Dictionary.ContainsKey("Filter"));
        Assert.Equal(data, parsed.Data);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void CompressesOnlyWhenDeflateSavesAtLeastOneByte(int saving, bool compressed)
    {
        byte[] data = DataThatDeflatesBy(saving);
        PdfReference stream = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            stream = writer.WriteStream(new PdfDictionary(), data);
            return writer.Write(CatalogDictionary());
        });

        ParsedStream parsed = (ParsedStream)new PdfFileReader(file).GetObject(stream.ObjectNumber)!;
        Assert.Equal(compressed, parsed.Dictionary.ContainsKey("Filter"));
        Assert.Equal((long)(data.Length - saving), parsed.Dictionary["Length"]);
        Assert.Equal(data, PdfFileReader.Decode(parsed));
    }

    [Fact]
    public void WritesDataAsGivenWhenAskedTo()
    {
        byte[] data = Enumerable.Repeat((byte)'a', 1000).ToArray();
        PdfReference stream = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            PdfDictionary dictionary = new PdfDictionary { [new PdfName("Filter")] = new PdfName("DCTDecode") };
            stream = writer.WriteStream(dictionary, data, PdfStreamCompression.None);
            return writer.Write(CatalogDictionary());
        });

        ParsedStream parsed = (ParsedStream)new PdfFileReader(file).GetObject(stream.ObjectNumber)!;
        Assert.Equal(new ParsedName("DCTDecode"), parsed.Dictionary["Filter"]);
        Assert.Equal(data, parsed.Data);
    }

    [Theory]
    [InlineData(nameof(PdfCrossReferenceFormat.Table))]
    [InlineData(nameof(PdfCrossReferenceFormat.Stream))]
    public void CompressesNothingWhenCompressionIsOff(string formatName)
    {
        PdfCrossReferenceFormat format = Format(formatName);
        byte[] data = Enumerable.Repeat((byte)'a', 10_000).ToArray();

        byte[] file = Write(Options(format, CompressionLevel.NoCompression), writer =>
        {
            writer.WriteStream(new PdfDictionary(), data);
            return writer.Write(CatalogDictionary());
        });

        Assert.DoesNotContain("FlateDecode", Latin1.Text(file), StringComparison.Ordinal);
        Assert.DoesNotContain("DecodeParms", Latin1.Text(file), StringComparison.Ordinal);
        Assert.Contains(new string('a', 10_000), Latin1.Text(file), StringComparison.Ordinal);
        Assert.NotNull(new PdfFileReader(file).GetObject(1));
    }

    [Fact]
    public void UsesTheConfiguredCompressionEffort()
    {
        byte[] data = Enumerable.Repeat((byte)'a', 1000).ToArray();
        PdfReference stream = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table, CompressionLevel.Fastest), writer =>
        {
            stream = writer.WriteStream(new PdfDictionary(), data);
            return writer.Write(CatalogDictionary());
        });

        ParsedStream parsed = (ParsedStream)new PdfFileReader(file).GetObject(stream.ObjectNumber)!;
        Assert.Equal(new byte[] { 0x78, 0x01 }, parsed.Data.Take(2).ToArray());
    }

    [Fact]
    public void LeavesTheCallersDictionaryAsItWas()
    {
        PdfDictionary dictionary = new PdfDictionary { [new PdfName("Kind")] = 1 };

        Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            writer.WriteStream(dictionary, Enumerable.Repeat((byte)'a', 1000).ToArray());
            return writer.Write(CatalogDictionary());
        });

        Assert.Equal("<</Kind 1>>", Latin1.Written(writer => writer.WriteDictionary(dictionary)));
    }

    [Fact]
    public void RefusesADictionaryThatSetsItsOwnLength()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => writer.WriteStream(new PdfDictionary { [new PdfName("Length")] = 3 }, new byte[3], PdfStreamCompression.None));

        Assert.Equal("dictionary", exception.ParamName);
    }

    [Fact]
    public void RefusesToStackCompressionOnAnExistingFilter()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => writer.WriteStream(new PdfDictionary { [new PdfName("Filter")] = new PdfName("DCTDecode") }, new byte[3]));

        Assert.Equal("dictionary", exception.ParamName);
    }

    [Fact]
    public void RefusesANullDictionary()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());

        Assert.Throws<ArgumentNullException>(() => writer.WriteStream(null!, new byte[3]));
    }

    [Fact]
    public void CarriesLargeStreamsThroughIntact()
    {
        byte[] data = Random(200_000, 5);
        PdfReference stream = default;
        PdfReference after = default;

        byte[] file = Write(Options(PdfCrossReferenceFormat.Table), writer =>
        {
            stream = writer.WriteStream(new PdfDictionary(), data);
            after = writer.Write(new PdfArray { 42 });
            return writer.Write(CatalogDictionary());
        });

        PdfFileReader reader = new PdfFileReader(file);
        Assert.Equal(data, ((ParsedStream)reader.GetObject(stream.ObjectNumber)!).Data);
        Assert.Equal(new List<object?> { 42L }, reader.GetObject(after.ObjectNumber));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(300, 2)]
    [InlineData(70_000, 3)]
    public void SizesCrossReferenceStreamFieldsToTheLargestOffset(int padding, int width)
    {
        byte[] file = Write(Options(PdfCrossReferenceFormat.Stream), writer =>
        {
            if (padding > 0)
                writer.WriteStream(new PdfDictionary(), Random(padding, padding));

            return writer.Write(CatalogDictionary());
        });

        PdfFileReader reader = new PdfFileReader(file);
        Assert.Equal(new List<object?> { 1L, (long)width, 2L }, reader.Trailer["W"]);
        Assert.Equal(new ParsedName("Catalog"), reader.Dictionary(reader.Trailer["Root"])["Type"]);
    }

    [Fact]
    public void PredictsCrossReferenceRowsWhenCompressing()
    {
        byte[] file = Write(Options(PdfCrossReferenceFormat.Stream), writer => writer.Write(CatalogDictionary()));

        PdfFileReader reader = new PdfFileReader(file);
        Assert.Equal(new ParsedName("FlateDecode"), reader.Trailer["Filter"]);
        Assert.Equal(
            new Dictionary<string, object?> { ["Columns"] = 4L, ["Predictor"] = 12L },
            (Dictionary<string, object?>)reader.Trailer["DecodeParms"]!);
        Assert.Equal((0, 0L, 65535), reader.Entries[0]);
        Assert.Equal((2, 2L, 0), reader.Entries[1]);
        Assert.Equal((1, 15L, 0), reader.Entries[2]);
    }

    [Theory]
    [InlineData(nameof(PdfCrossReferenceFormat.Table))]
    [InlineData(nameof(PdfCrossReferenceFormat.Stream))]
    public void NamesTheCatalogAndInformationDictionaryInTheTrailer(string formatName)
    {
        PdfCrossReferenceFormat format = Format(formatName);
        PdfReference info = default;

        byte[] file = Write(
            Options(format, id: FixedId),
            writer => writer.Write(CatalogDictionary()),
            writer => info = writer.Write(new PdfDictionary { [new PdfName("Title")] = new PdfString(Latin1.Bytes("T")) }));

        PdfFileReader reader = new PdfFileReader(file);
        Assert.Equal(new ParsedReference(info.ObjectNumber, 0), reader.Trailer["Info"]);
        Assert.Equal(Latin1.Bytes("T"), reader.Dictionary(reader.Trailer["Info"])["Title"]);
        Assert.Equal(new List<object?> { FixedId, FixedId }, reader.Trailer["ID"]);
    }

    [Theory]
    [InlineData(nameof(PdfCrossReferenceFormat.Table))]
    [InlineData(nameof(PdfCrossReferenceFormat.Stream))]
    public void DerivesTheIdFromAHashOfEverythingBeforeTheCrossReferenceSection(string formatName)
    {
        PdfCrossReferenceFormat format = Format(formatName);
        byte[] file = Write(Options(format), writer => writer.Write(CatalogDictionary()));

        PdfFileReader reader = new PdfFileReader(file);
        byte[] expected;
        using (SHA256 sha = SHA256.Create())
            expected = sha.ComputeHash(file, 0, (int)reader.StartXref).Take(16).ToArray();

        Assert.Equal(new List<object?> { expected, expected }, reader.Trailer["ID"]);
    }

    [Fact]
    public void ProducesIdenticalBytesForIdenticalContent()
    {
        static PdfReference Build(PdfFileWriter writer)
        {
            writer.WriteStream(new PdfDictionary(), Enumerable.Repeat((byte)'z', 500).ToArray());
            return writer.Write(CatalogDictionary());
        }

        byte[] first = Write(Options(PdfCrossReferenceFormat.Stream), Build);
        byte[] second = Write(Options(PdfCrossReferenceFormat.Stream), Build);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GivesDifferentContentADifferentId()
    {
        byte[] first = Write(Options(PdfCrossReferenceFormat.Table), writer => writer.Write(CatalogDictionary()));
        byte[] second = Write(
            Options(PdfCrossReferenceFormat.Table),
            writer => writer.Write(new PdfDictionary { [Type] = Catalog, [new PdfName("X")] = 1 }));

        Assert.NotEqual(new PdfFileReader(first).Trailer["ID"], new PdfFileReader(second).Trailer["ID"]);
    }

    [Fact]
    public void CopiesTheFixedIdSoLaterChangesDoNotLeakIn()
    {
        byte[] id = FixedId.ToArray();
        using MemoryStream output = new MemoryStream();
        using (PdfFileWriter writer = new PdfFileWriter(output, Options(PdfCrossReferenceFormat.Table, id: id)))
        {
            id[0] = 0xEE;
            writer.Finish(writer.Write(CatalogDictionary()));
        }

        Assert.Equal(new List<object?> { FixedId, FixedId }, new PdfFileReader(output.ToArray()).Trailer["ID"]);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public void RefusesAnIdThatIsNotSixteenBytes(int length)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new PdfFileWriter(new MemoryStream(), new PdfWriterOptions { DocumentId = new byte[length] }));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void RefusesAnOutputItCannotWriteTo()
    {
        using MemoryStream readOnly = new MemoryStream(new byte[4], writable: false);

        Assert.Throws<ArgumentNullException>(() => new PdfFileWriter(null!));
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new PdfFileWriter(readOnly));
        Assert.Equal("output", exception.ParamName);
    }

    [Fact]
    public void DefaultsToTheCompactFormWithOptimalCompression()
    {
        PdfWriterOptions options = new PdfWriterOptions();

        Assert.Equal(PdfCrossReferenceFormat.Stream, options.CrossReferenceFormat);
        Assert.Equal(CompressionLevel.Optimal, options.CompressionLevel);
        Assert.Null(options.DocumentId);

        using MemoryStream output = new MemoryStream();
        using (PdfFileWriter writer = new PdfFileWriter(output))
            writer.Finish(writer.Write(CatalogDictionary()));

        Assert.True(new PdfFileReader(output.ToArray()).HasCrossReferenceStream);
    }

    [Fact]
    public void StreamsObjectsOutBeforeTheFileIsFinished()
    {
        using MemoryStream output = new MemoryStream();
        using PdfFileWriter writer = new PdfFileWriter(output, Options(PdfCrossReferenceFormat.Table));

        writer.Write(new PdfArray { 1 });
        Assert.Equal(0, output.Length);

        writer.WriteStream(new PdfDictionary(), Random(100_000, 9));
        Assert.True(output.Length > 64 * 1024, $"Only {output.Length} bytes reached the output.");
    }

    [Fact]
    public void FlushesTheOutputWhenFinished()
    {
        using MemoryStream inner = new MemoryStream();
        using BufferedStream buffered = new BufferedStream(inner, 1 << 20);
        using (PdfFileWriter writer = new PdfFileWriter(buffered))
            writer.Finish(writer.Write(CatalogDictionary()));

        Assert.True(inner.Length > 0);
        Assert.Equal("\n%%EOF\n", Latin1.Text(inner.ToArray().AsSpan((int)inner.Length - 7)));
    }

    [Fact]
    public void LeavesTheOutputOpen()
    {
        using MemoryStream output = new MemoryStream();
        using (PdfFileWriter writer = new PdfFileWriter(output))
            writer.Finish(writer.Write(CatalogDictionary()));

        Assert.True(output.CanWrite);
    }

    [Theory]
    [InlineData(nameof(PdfCrossReferenceFormat.Table))]
    [InlineData(nameof(PdfCrossReferenceFormat.Stream))]
    public void RefusesAnObjectThatIsOnlyAReference(string formatName)
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream(), Options(Format(formatName)));
        PdfReference target = writer.Write(new PdfArray());
        PdfReference alias = writer.Reserve();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => writer.Write(alias, target));

        Assert.Equal("value", exception.ParamName);
        writer.Write(alias, new PdfArray { target });
    }

    [Fact]
    public void RefusesToWriteAnObjectTwice()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream(), Options(PdfCrossReferenceFormat.Table));
        PdfReference reference = writer.Write(new PdfArray());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => writer.Write(reference, 1));
        Assert.Equal($"Object {reference.ObjectNumber} has already been written.", exception.Message);
        Assert.Throws<InvalidOperationException>(() => writer.WriteStream(reference, new PdfDictionary(), new byte[1]));
    }

    [Fact]
    public void RefusesToWriteAnObjectStreamEntryTwice()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream(), Options(PdfCrossReferenceFormat.Stream));
        PdfReference reference = writer.Write(new PdfArray());

        Assert.Throws<InvalidOperationException>(() => writer.Write(reference, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RefusesNumbersItDidNotReserve(int number)
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());
        writer.Reserve();
        PdfReference reference = number == 0 ? default : new PdfReference(number);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => writer.Write(reference, 1));

        Assert.Equal("reference", exception.ParamName);
        Assert.StartsWith($"Object {number} was not reserved by this writer.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToFinishWithAReservedObjectMissing()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());
        PdfReference root = writer.Write(CatalogDictionary());
        PdfReference missing = writer.Reserve();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => writer.Finish(root));

        Assert.Equal($"Object {missing.ObjectNumber} was reserved but never written.", exception.Message);
    }

    [Fact]
    public void RefusesARootOrInformationDictionaryItNeverNumbered()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());
        PdfReference root = writer.Write(CatalogDictionary());

        Assert.Throws<ArgumentException>(() => writer.Finish(default));
        Assert.Throws<ArgumentException>(() => writer.Finish(root, new PdfReference(99)));
    }

    [Fact]
    public void RefusesEverythingOnceFinished()
    {
        using PdfFileWriter writer = new PdfFileWriter(new MemoryStream());
        PdfReference root = writer.Write(CatalogDictionary());
        writer.Finish(root);

        Assert.Throws<InvalidOperationException>(() => writer.Finish(root));
        Assert.Throws<InvalidOperationException>(() => writer.Reserve());
        Assert.Throws<InvalidOperationException>(() => writer.Write(root, 1));
    }
}
