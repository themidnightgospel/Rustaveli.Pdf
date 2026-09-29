using System.Text;
using Rustaveli.Pdf.Security;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Security;

/// <summary>
/// The standard security handler: keys from passwords, what the owner and user entries let a reader check, and
/// strings and streams encrypted object by object.
/// </summary>
public class EncryptionTests
{
    private static readonly byte[] Text = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");

    public static TheoryData<EncryptionLevel> Levels => new TheoryData<EncryptionLevel>
    {
        EncryptionLevel.Rc4With40Bits,
        EncryptionLevel.Rc4With128Bits,
        EncryptionLevel.AesWith128Bits,
        EncryptionLevel.AesWith256Bits,
    };

    private static PdfEncryption? Reopen(PdfEncryption written, string password) =>
        PdfEncryption.Open(written.Dictionary, written.DocumentId, password, value => value);

    [Fact]
    public void Rc4MatchesItsPublishedVector()
    {
        byte[] encrypted = Rc4.Transform("Key"u8, "Plaintext"u8);

        Assert.Equal("BBF316E8D940AF0AD3", BitConverter.ToString(encrypted).Replace("-", string.Empty));
        Assert.Equal("Plaintext"u8.ToArray(), Rc4.Transform("Key"u8, encrypted));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void EitherPasswordOpensWhatWasProtectedAndDecryptsIt(EncryptionLevel level)
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", OwnerPassword = "owner", Encryption = level });

        foreach (string password in new[] { "user", "owner" })
        {
            PdfEncryption opened = Reopen(written, password)!;

            Assert.NotNull(opened);
            Assert.Equal(Text, opened.DecryptString(7, written.EncryptString(7, Text)));
            Assert.Equal(Text, opened.DecryptStream(12, written.EncryptStream(12, Text)));
            Assert.Equal(Text, written.DecryptStream(12, opened.EncryptStream(12, Text)));
        }

        Assert.Null(Reopen(written, "neither"));
        Assert.Null(Reopen(written, string.Empty));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void EachObjectIsEncryptedDifferentlyAndNeverAsItWas(EncryptionLevel level)
    {
        PdfEncryption encryption = PdfEncryption.Create(new Protection { Encryption = level });

        byte[] first = encryption.EncryptString(1, Text);
        byte[] second = encryption.EncryptString(2, Text);

        Assert.NotEqual(Text, first.Take(Text.Length));
        Assert.NotEqual(first, second);
        Assert.Equal(Text, encryption.DecryptString(1, first));

        // Below 256-bit AES each object has its own key; at 256 bits the file key serves every object.
        Assert.Equal(level == EncryptionLevel.AesWith256Bits, Text.AsSpan().SequenceEqual(encryption.DecryptString(2, first)));
    }

    [Fact]
    public void AnEmptyUserPasswordOpensTheFileWithoutAsking()
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { AllowCopying = false });

        Assert.NotNull(Reopen(written, string.Empty));
        Assert.Null(Reopen(written, "anything"));
    }

    [Theory]
    [InlineData(EncryptionLevel.Rc4With40Bits, 1, 2, 40)]
    [InlineData(EncryptionLevel.Rc4With128Bits, 2, 3, 128)]
    [InlineData(EncryptionLevel.AesWith128Bits, 4, 4, 128)]
    [InlineData(EncryptionLevel.AesWith256Bits, 5, 6, 256)]
    public void TheDictionaryNamesTheHandlerVersionRevisionAndKeyLength(EncryptionLevel level, int version, int revision, int bits)
    {
        PdfDictionary dictionary = PdfEncryption.Create(new Protection { Encryption = level }).Dictionary;

        Assert.Equal("Standard", dictionary[PdfNames.Filter].AsName().Value);
        Assert.Equal(version, dictionary[new PdfName("V")].AsInteger());
        Assert.Equal(revision, dictionary[new PdfName("R")].AsInteger());
        Assert.Equal(bits, dictionary[PdfNames.Length].AsInteger());
        Assert.Equal(version >= 4, dictionary.ContainsKey(new PdfName("CF")));
        Assert.Equal(version == 5, dictionary.ContainsKey(new PdfName("Perms")));
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void CrossReferenceStreamsAreNeverEncryptedAndMetadataOnlyWhenAsked(EncryptionLevel level)
    {
        PdfEncryption encrypted = PdfEncryption.Create(new Protection { Encryption = level });
        PdfEncryption readable = PdfEncryption.Create(new Protection { Encryption = level, EncryptMetadata = false });
        PdfDictionary metadata = new PdfDictionary { [PdfNames.Type] = new PdfName("Metadata") };

        Assert.False(encrypted.Covers(new PdfDictionary { [PdfNames.Type] = PdfNames.XRef }));
        Assert.True(encrypted.Covers(new PdfDictionary()));
        Assert.True(encrypted.Covers(new PdfDictionary { [PdfNames.Type] = PdfNames.XObject }));
        Assert.True(encrypted.Covers(new PdfDictionary { [PdfNames.Type] = 3 }));
        Assert.True(encrypted.Covers(metadata));

        // RC4 below revision 4 cannot leave the metadata readable.
        Assert.Equal(level is EncryptionLevel.Rc4With40Bits or EncryptionLevel.Rc4With128Bits, readable.Covers(metadata));
        Assert.Equal(level is EncryptionLevel.AesWith128Bits or EncryptionLevel.AesWith256Bits, readable.Dictionary.ContainsKey(new PdfName("EncryptMetadata")));
    }

    [Fact]
    public void PermissionsSetTheReservedBitsAndEachAllowedOne()
    {
        Assert.Equal(unchecked((int)0xFFFFFFFC), new Protection().Permissions);
        Assert.Equal(unchecked((int)0xFFFFF0C0), new Protection
        {
            AllowPrinting = false,
            AllowModifying = false,
            AllowCopying = false,
            AllowAnnotating = false,
            AllowFillingForms = false,
            AllowAccessibility = false,
            AllowAssembling = false,
            AllowHighQualityPrinting = false,
        }.Permissions);

        Assert.Equal(1 << 2, new Protection().Permissions & ~new Protection { AllowPrinting = false }.Permissions);
        Assert.Equal(1 << 3, new Protection().Permissions & ~new Protection { AllowModifying = false }.Permissions);
        Assert.Equal(1 << 4, new Protection().Permissions & ~new Protection { AllowCopying = false }.Permissions);
        Assert.Equal(1 << 5, new Protection().Permissions & ~new Protection { AllowAnnotating = false }.Permissions);
        Assert.Equal(1 << 8, new Protection().Permissions & ~new Protection { AllowFillingForms = false }.Permissions);
        Assert.Equal(1 << 9, new Protection().Permissions & ~new Protection { AllowAccessibility = false }.Permissions);
        Assert.Equal(1 << 10, new Protection().Permissions & ~new Protection { AllowAssembling = false }.Permissions);
        Assert.Equal(1 << 11, new Protection().Permissions & ~new Protection { AllowHighQualityPrinting = false }.Permissions);
    }

    [Fact]
    public void ProtectionDefaultsToTheStrongestEncryptionAndEveryPermission()
    {
        Protection protection = new Protection();

        Assert.Equal(EncryptionLevel.AesWith256Bits, protection.Encryption);
        Assert.Equal(string.Empty, protection.UserPassword);
        Assert.Null(protection.OwnerPassword);
        Assert.True(protection.EncryptMetadata);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutAnOwnerPasswordTheRestrictionsCannotBeLifted(string? owner)
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", OwnerPassword = owner, Encryption = EncryptionLevel.AesWith128Bits });

        Assert.NotNull(Reopen(written, "user"));
        Assert.Null(Reopen(written, string.Empty));
    }

    [Theory]
    [InlineData(EncryptionLevel.Rc4With40Bits)]
    [InlineData(EncryptionLevel.Rc4With128Bits)]
    [InlineData(EncryptionLevel.AesWith128Bits)]
    public void AUserEntryTooShortToCheckOpensToNeitherPassword(EncryptionLevel level)
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", OwnerPassword = "owner", Encryption = level });
        written.Dictionary[new PdfName("U")] = new PdfString(new byte[8], PdfStringForm.Hex);

        Assert.Null(Reopen(written, "user"));
        Assert.Null(Reopen(written, "owner"));
    }

    [Theory]
    [InlineData("Length", 4096)]
    [InlineData("Length", 8)]
    [InlineData("O", 8)]
    public void AKeyLengthOrOwnerEntryNoPasswordCanBeCheckedWithIsDamage(string entry, int value)
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", OwnerPassword = "owner", Encryption = EncryptionLevel.Rc4With128Bits });
        written.Dictionary[new PdfName(entry)] = entry == "O" ? new PdfString(new byte[value], PdfStringForm.Hex) : value;

        Assert.Throws<InvalidDataException>(() => Reopen(written, "user"));
    }

    [Theory]
    [InlineData("OE", "owner")]
    [InlineData("UE", "user")]
    public void AKeyWrappedInTheWrongLengthIsDamage(string entry, string password)
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", OwnerPassword = "owner", Encryption = EncryptionLevel.AesWith256Bits });
        written.Dictionary[new PdfName(entry)] = new PdfString(new byte[31], PdfStringForm.Hex);

        Assert.Throws<InvalidDataException>(() => Reopen(written, password));
    }

    [Fact]
    public void AVersion4FileWithoutItsStandardCryptFilterTakesTheKeyLengthFromItsDictionary()
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "user", Encryption = EncryptionLevel.AesWith128Bits });
        written.Dictionary[new PdfName("CF")] = new PdfDictionary();

        PdfEncryption opened = Reopen(written, "user")!;

        Assert.NotNull(opened);
        Assert.Equal(Text, opened.DecryptString(7, written.EncryptString(7, Text)));
        Assert.Equal(Text, opened.DecryptStream(12, written.EncryptStream(12, Text)));
    }

    [Fact]
    public void EncryptionKeepsTheIdentifierItWasGiven()
    {
        byte[] id = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();

        Assert.Equal(id, PdfEncryption.Create(new Protection(), id).DocumentId);
        Assert.Equal(16, PdfEncryption.Create(new Protection()).DocumentId.Length);
    }

    [Fact]
    public void OnlyTheStandardHandlerIsOpened()
    {
        PdfDictionary other = new PdfDictionary { [PdfNames.Filter] = new PdfName("Adobe.PubSec") };
        PdfDictionary spelled = new PdfDictionary { [PdfNames.Filter] = PdfString.FromText("Standard") };

        Assert.Throws<NotSupportedException>(() => PdfEncryption.Open(other, [], string.Empty, value => value));
        Assert.Throws<NotSupportedException>(() => PdfEncryption.Open(spelled, [], string.Empty, value => value));
        Assert.Throws<NotSupportedException>(() => PdfEncryption.Open(new PdfDictionary(), [], string.Empty, value => value));
    }

    [Fact]
    public void IdentityCryptFiltersLeaveDataAsItIs()
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { Encryption = EncryptionLevel.AesWith128Bits });
        written.Dictionary[new PdfName("StmF")] = new PdfName("Identity");
        written.Dictionary[new PdfName("StrF")] = new PdfName("Identity");

        PdfEncryption opened = Reopen(written, string.Empty)!;

        Assert.Equal(PdfEncryption.Cipher.None, opened.Streams);
        Assert.Equal(PdfEncryption.Cipher.None, opened.Strings);
        Assert.False(opened.Covers(new PdfDictionary()));
        Assert.Equal(Text, opened.EncryptString(3, Text));
        Assert.Equal(Text, opened.DecryptStream(3, Text));
    }

    [Fact]
    public void AesDataTooShortToHoldAVectorIsLeftAsItIs()
    {
        PdfEncryption encryption = PdfEncryption.Create(new Protection { Encryption = EncryptionLevel.AesWith128Bits });

        Assert.Equal([], encryption.DecryptString(1, new byte[16]));
        Assert.Equal(new byte[5], encryption.DecryptString(1, new byte[5]));
    }

    [Theory]
    [InlineData(EncryptionLevel.Rc4With40Bits)]
    [InlineData(EncryptionLevel.Rc4With128Bits)]
    [InlineData(EncryptionLevel.AesWith128Bits)]
    public void APasswordBelowAes256IsRefusedWhereItsCharactersWouldBeLost(EncryptionLevel level)
    {
        // Below revision 5 a password is PDFDocEncoding, and every character outside it would become the same '?', so
        // "пароль" would open a file protected with "секрет".
        ArgumentException user = Assert.Throws<ArgumentException>(() => PdfEncryption.Create(new Protection { UserPassword = "секрет", Encryption = level }));
        ArgumentException owner = Assert.Throws<ArgumentException>(() => PdfEncryption.Create(new Protection { OwnerPassword = "секрет", Encryption = level }));

        Assert.Contains(nameof(EncryptionLevel.AesWith256Bits), user.Message);
        Assert.Contains("user", user.Message);
        Assert.Contains("owner", owner.Message);

        // Latin-1 letters are in PDFDocEncoding, and are kept.
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "café", OwnerPassword = "naïve", Encryption = level });
        Assert.NotNull(Reopen(written, "café"));
        Assert.NotNull(Reopen(written, "naïve"));
        Assert.Null(Reopen(written, "cafe"));
    }

    [Fact]
    public void Aes256KeepsAPasswordInAnyScript()
    {
        PdfEncryption written = PdfEncryption.Create(new Protection { UserPassword = "секрет", OwnerPassword = "ქართული" });

        Assert.NotNull(Reopen(written, "секрет"));
        Assert.NotNull(Reopen(written, "ქართული"));
        Assert.Null(Reopen(written, "пароль"));
    }

    [Fact]
    public void ThePasswordIsPaddedOrCutTo32Bytes()
    {
        Assert.Equal(32, StandardSecurity.Pad(string.Empty).Length);
        Assert.Equal(0x28, StandardSecurity.Pad(string.Empty)[0]);
        Assert.Equal(Encoding.ASCII.GetBytes(new string('x', 32)), StandardSecurity.Pad(new string('x', 40)));
        Assert.Equal((byte)'a', StandardSecurity.Pad("a")[0]);
        Assert.Equal(0x28, StandardSecurity.Pad("a")[1]);
        Assert.Equal((byte)'?', StandardSecurity.Pad("一")[0]);
        Assert.Equal(127, StandardSecurity.Utf8(new string('x', 200)).Length);
    }
}
