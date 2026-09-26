using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfByteWriterTests
{
    private static PdfArray Nested(int depth)
    {
        PdfArray outer = new PdfArray();
        PdfArray current = outer;
        for (int level = 1; level < depth; level++)
        {
            PdfArray inner = new PdfArray();
            current.Add(inner);
            current = inner;
        }

        return outer;
    }

    [Fact]
    public void EscapesParenthesesBackslashesAndNamedControlCharacters()
    {
        string written = Latin1.Written(writer => writer.WriteLiteralString(Latin1.Bytes("()\\\n\r\t\b\f")));

        Assert.Equal("(\\(\\)\\\\\\n\\r\\t\\b\\f)", written);
    }

    [Theory]
    [InlineData(0x00, "(\\000)")]
    [InlineData(0x01, "(\\001)")]
    [InlineData(0x07, "(\\007)")]
    [InlineData(0x0B, "(\\013)")]
    [InlineData(0x1F, "(\\037)")]
    [InlineData(0x7F, "(\\177)")]
    public void WritesOtherControlCharactersAsThreeOctalDigits(int value, string expected)
    {
        Assert.Equal(expected, Latin1.Written(writer => writer.WriteLiteralString(new[] { (byte)value })));
    }

    [Theory]
    [InlineData(0x20)]
    [InlineData(0x41)]
    [InlineData(0x7E)]
    [InlineData(0x80)]
    [InlineData(0xFF)]
    public void WritesPrintableAndHighBytesAsThemselves(int value)
    {
        Assert.Equal("(" + (char)value + ")", Latin1.Written(writer => writer.WriteLiteralString(new[] { (byte)value })));
    }

    [Fact]
    public void KeepsAnOctalEscapeFromSwallowingAFollowingDigit()
    {
        byte[] bytes = { 0x01, (byte)'7' };

        string written = Latin1.Written(writer => writer.WriteLiteralString(bytes));

        Assert.Equal("(\\0017)", written);
        Assert.Equal(bytes, PdfSyntaxParser.ParseSingle(Latin1.Bytes(written)));
    }

    [Fact]
    public void WritesHexStringsInUppercaseWithTwoDigitsPerByte()
    {
        Assert.Equal("<0009ABFF1F>", Latin1.Written(writer => writer.WriteHexString(new byte[] { 0x00, 0x09, 0xAB, 0xFF, 0x1F })));
        Assert.Equal("<>", Latin1.Written(writer => writer.WriteHexString(ReadOnlySpan<byte>.Empty)));
        Assert.Equal("()", Latin1.Written(writer => writer.WriteLiteralString(ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void WritesStringsInTheirOwnForm()
    {
        PdfString literal = new PdfString(Latin1.Bytes("ab"));
        PdfString hex = new PdfString(Latin1.Bytes("ab"), PdfStringForm.Hex);

        Assert.Equal("(ab)", Latin1.Written(writer => writer.WriteString(literal)));
        Assert.Equal("<6162>", Latin1.Written(writer => writer.WriteString(hex)));
    }

    [Fact]
    public void PredictsTheExactLengthOfEveryLiteralString()
    {
        for (int value = 0; value < 256; value++)
        {
            byte[] bytes = { (byte)value };
            int expected = Latin1.Written(writer => writer.WriteLiteralString(bytes)).Length;

            Assert.Equal(expected, PdfByteWriter.LiteralLength(bytes));
        }
    }

    [Theory]
    [InlineData(new byte[] { }, 2)]
    [InlineData(new byte[] { 0x61, 0x62 }, 4)]
    [InlineData(new byte[] { 0x28, 0x29, 0x5C, 0x0A, 0x0D, 0x09, 0x08, 0x0C }, 18)]
    [InlineData(new byte[] { 0x00, 0x1F, 0x7F }, 14)]
    [InlineData(new byte[] { 0x80, 0xFF }, 4)]
    public void CountsOneTwoOrFourBytesPerCharacter(byte[] bytes, int expected)
    {
        Assert.Equal(expected, PdfByteWriter.LiteralLength(bytes));
    }

    [Theory]
    [InlineData(new byte[] { }, true)]
    [InlineData(new byte[] { 0x61 }, true)]
    [InlineData(new byte[] { 0x28 }, true)]
    [InlineData(new byte[] { 0x28, 0x00, 0x61 }, false)]
    [InlineData(new byte[] { 0x00, 0x61, 0x61 }, true)]
    [InlineData(new byte[] { 0x00 }, false)]
    [InlineData(new byte[] { 0x00, 0x10, 0x00, 0x20 }, false)]
    public void ChoosesTheShorterFormAndPrefersLiteralOnATie(byte[] bytes, bool literal)
    {
        Assert.Equal(literal ? PdfStringForm.Literal : PdfStringForm.Hex, PdfByteWriter.CompactForm(bytes));
    }

    [Theory]
    [InlineData(nameof(PdfValueKind.Null), "null")]
    [InlineData(nameof(PdfValueKind.Boolean), "true")]
    [InlineData(nameof(PdfValueKind.Integer), "-17")]
    [InlineData(nameof(PdfValueKind.Real), "2.5")]
    [InlineData(nameof(PdfValueKind.Name), "/Page")]
    [InlineData(nameof(PdfValueKind.String), "(x)")]
    [InlineData(nameof(PdfValueKind.Array), "[]")]
    [InlineData(nameof(PdfValueKind.Dictionary), "<<>>")]
    [InlineData(nameof(PdfValueKind.Reference), "12 0 R")]
    public void WritesEachKindOfValue(string kindName, string expected)
    {
        PdfValueKind kind = (PdfValueKind)Enum.Parse(typeof(PdfValueKind), kindName);
        PdfValue value = kind switch
        {
            PdfValueKind.Null => PdfValue.Null,
            PdfValueKind.Boolean => true,
            PdfValueKind.Integer => -17,
            PdfValueKind.Real => 2.5,
            PdfValueKind.Name => new PdfName("Page"),
            PdfValueKind.String => new PdfString(Latin1.Bytes("x")),
            PdfValueKind.Array => new PdfArray(),
            PdfValueKind.Dictionary => new PdfDictionary(),
            _ => new PdfReference(12),
        };

        Assert.Equal(expected, Latin1.Written(writer => writer.WriteValue(value)));
    }

    [Fact]
    public void WritesFalse()
    {
        Assert.Equal("false", Latin1.Written(writer => writer.WriteValue(false)));
    }

    [Fact]
    public void SeparatesTokensOnlyWhereTheyWouldOtherwiseMerge()
    {
        PdfDictionary dictionary = new PdfDictionary
        {
            [new PdfName("Type")] = new PdfName("Page"),
            [new PdfName("Kids")] = new PdfArray { new PdfReference(1), new PdfReference(22) },
            [new PdfName("Count")] = 2,
            [new PdfName("Box")] = new PdfArray { 0, -0.5, 595.25, 842 },
            [new PdfName("Flags")] = new PdfArray { true, false, PdfValue.Null, new PdfName("N"), 7 },
            [new PdfName("Text")] = new PdfString(Latin1.Bytes("a")),
            [new PdfName("After")] = 1,
            [new PdfName("Inner")] = new PdfDictionary { [new PdfName("A")] = new PdfString(Latin1.Bytes("b"), PdfStringForm.Hex) },
        };

        string written = Latin1.Written(writer => writer.WriteDictionary(dictionary));

        Assert.Equal(
            "<</Type/Page/Kids[1 0 R 22 0 R]/Count 2/Box[0 -0.5 595.25 842]/Flags[true false null/N 7]"
            + "/Text(a)/After 1/Inner<</A<62>>>>>",
            written);
    }

    [Fact]
    public void SeparatesAKeywordFromAPrecedingRegularToken()
    {
        string written = Latin1.Written(writer =>
        {
            writer.WriteKeyword("q"u8);
            writer.WriteByte((byte)'\n');
            writer.WriteName(new PdfName("GS1"));
            writer.WriteKeyword("gs"u8);
            writer.WriteByte((byte)']');
            writer.WriteKeyword("TJ"u8);
            writer.WriteInteger(1);
            writer.WriteReal(0.5);
            writer.WriteLiteralString(Latin1.Bytes("x"));
            writer.WriteReal(3);
        });

        Assert.Equal("q\n/GS1 gs]TJ 1 0.5(x)3", written);
    }

    [Fact]
    public void KeepsTheEmptyNameFromAbsorbingTheNextToken()
    {
        PdfName empty = new PdfName(string.Empty);
        ParsedName parsedEmpty = new ParsedName(string.Empty);
        PdfArray array = new PdfArray { empty, new PdfReference(21), empty, 2.5, empty, true, empty, empty, empty, new PdfName("A") };
        PdfDictionary dictionary = new PdfDictionary { [empty] = 5 };

        string written = Latin1.Written(writer =>
        {
            writer.WriteArray(array);
            writer.WriteDictionary(dictionary);
        });

        Assert.Equal("[/ 21 0 R/ 2.5/ true////A]<</ 5>>", written);
        Assert.Equal(
            new List<object?>
            {
                parsedEmpty, new ParsedReference(21, 0), parsedEmpty, 2.5, parsedEmpty, true,
                parsedEmpty, parsedEmpty, parsedEmpty, new ParsedName("A"),
            },
            PdfSyntaxParser.ParseSingle(Latin1.Bytes(written.Substring(0, written.IndexOf(']') + 1))));
    }

    [Fact]
    public void WritesDictionaryEntriesWithoutTheBrackets()
    {
        PdfDictionary dictionary = new PdfDictionary { [new PdfName("A")] = 1, [new PdfName("B")] = new PdfArray { 2 } };

        Assert.Equal("/A 1/B[2]", Latin1.Written(writer => writer.WriteDictionaryEntries(dictionary)));
    }

    [Fact]
    public void WritesReferences()
    {
        Assert.Equal("7 0 R 8 0 R", Latin1.Written(writer =>
        {
            writer.WriteReference(new PdfReference(7));
            writer.WriteReference(new PdfReference(8));
        }));
    }

    [Fact]
    public void LeavesTheBufferUntouchedWhenARealIsRejected()
    {
        using PdfByteWriter writer = new PdfByteWriter();
        writer.WriteInteger(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteReal(double.NaN));

        Assert.Equal("1", Latin1.Text(writer.WrittenSpan));
    }

    [Fact]
    public void WritesNestingUpToTheLimit()
    {
        string written = Latin1.Written(writer => writer.WriteArray(Nested(PdfByteWriter.MaxNestingDepth)));

        Assert.Equal(new string('[', PdfByteWriter.MaxNestingDepth) + new string(']', PdfByteWriter.MaxNestingDepth), written);
    }

    [Fact]
    public void RefusesNestingBeyondTheLimit()
    {
        using PdfByteWriter writer = new PdfByteWriter();

        Assert.Throws<InvalidOperationException>(() => writer.WriteArray(Nested(PdfByteWriter.MaxNestingDepth + 1)));
    }

    [Fact]
    public void RefusesDictionariesNestedBeyondTheLimit()
    {
        PdfDictionary outer = new PdfDictionary();
        PdfDictionary current = outer;
        for (int level = 1; level <= PdfByteWriter.MaxNestingDepth; level++)
        {
            PdfDictionary inner = new PdfDictionary();
            current[new PdfName("D")] = inner;
            current = inner;
        }

        using PdfByteWriter writer = new PdfByteWriter();

        Assert.Throws<InvalidOperationException>(() => writer.WriteDictionary(outer));
    }

    [Fact]
    public void RefusesAnArrayThatContainsItselfInsteadOfOverflowingTheStack()
    {
        PdfArray array = new PdfArray();
        array.Add(array);
        using PdfByteWriter writer = new PdfByteWriter();

        Assert.Throws<InvalidOperationException>(() => writer.WriteValue(array));
    }

    [Fact]
    public void GrowsPastItsInitialCapacity()
    {
        byte[] data = Enumerable.Range(0, 10_000).Select(index => (byte)index).ToArray();
        using PdfByteWriter writer = new PdfByteWriter(1);

        writer.WriteByte(0xAA);
        writer.Write(data);

        Assert.Equal(10_001, writer.Length);
        Assert.Equal(0xAA, writer.WrittenSpan[0]);
        Assert.Equal(data, writer.WrittenSpan.Slice(1).ToArray());
    }

    [Fact]
    public void CommitsBytesWrittenThroughASpan()
    {
        using PdfByteWriter writer = new PdfByteWriter(2);
        writer.WriteByte((byte)'a');

        Span<byte> span = writer.GetSpan(3);
        span[0] = (byte)'b';
        span[1] = (byte)'c';
        writer.Advance(2);

        Assert.True(span.Length >= 3);
        Assert.Equal("abc", Latin1.Text(writer.WrittenSpan));
    }

    [Fact]
    public void ExposesTheWrittenBytesAsASegment()
    {
        using PdfByteWriter writer = new PdfByteWriter();
        writer.Write("xyz"u8);

        ArraySegment<byte> segment = writer.WrittenSegment;

        Assert.Equal(0, segment.Offset);
        Assert.Equal("xyz", Latin1.Text(segment.AsSpan()));
    }

    [Fact]
    public void StartsAfreshAfterClear()
    {
        using PdfByteWriter writer = new PdfByteWriter();
        writer.WriteInteger(5);

        writer.Clear();
        writer.WriteInteger(6);

        Assert.Equal("6", Latin1.Text(writer.WrittenSpan));
    }

    [Fact]
    public void KeepsWorkingAfterDispose()
    {
        PdfByteWriter writer = new PdfByteWriter();
        writer.Write("abc"u8);

        writer.Dispose();
        writer.Dispose();

        Assert.Equal(0, writer.Length);
        writer.Write("d"u8);
        Assert.Equal("d", Latin1.Text(writer.WrittenSpan));
        writer.Dispose();
    }
}
