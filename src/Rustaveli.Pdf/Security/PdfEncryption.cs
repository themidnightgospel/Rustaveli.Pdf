using System.Security.Cryptography;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Security;

/// <summary>
/// The encryption of one file under the standard security handler: its key and cipher, and the encryption dictionary
/// that lets a reader derive the key from a password.
/// </summary>
/// <remarks>
/// Strings and streams are encrypted object by object: under RC4 and 128-bit AES with a key made from the file key and
/// the object's number, under 256-bit AES with the file key itself. Cross-reference streams are never encrypted, nor the
/// metadata when the dictionary says it is left readable.
/// </remarks>
internal sealed class PdfEncryption
{
    private static readonly PdfName Filter = PdfNames.Filter;
    private static readonly PdfName Standard = new PdfName("Standard");
    private static readonly PdfName V = new PdfName("V");
    private static readonly PdfName R = new PdfName("R");
    private static readonly PdfName O = new PdfName("O");
    private static readonly PdfName U = new PdfName("U");
    private static readonly PdfName OE = new PdfName("OE");
    private static readonly PdfName UE = new PdfName("UE");
    private static readonly PdfName P = new PdfName("P");
    private static readonly PdfName Perms = new PdfName("Perms");
    private static readonly PdfName CF = new PdfName("CF");
    private static readonly PdfName StmF = new PdfName("StmF");
    private static readonly PdfName StrF = new PdfName("StrF");
    private static readonly PdfName StdCF = new PdfName("StdCF");
    private static readonly PdfName CFM = new PdfName("CFM");
    private static readonly PdfName AuthEvent = new PdfName("AuthEvent");
    private static readonly PdfName DocOpen = new PdfName("DocOpen");
    private static readonly PdfName AesV2 = new PdfName("AESV2");
    private static readonly PdfName AesV3 = new PdfName("AESV3");
    private static readonly PdfName V2 = new PdfName("V2");
    private static readonly PdfName Identity = new PdfName("Identity");
    private static readonly PdfName EncryptMetadata = new PdfName("EncryptMetadata");
    private static readonly PdfName Metadata = new PdfName("Metadata");
    private static readonly PdfName Extensions = new PdfName("Extensions");
    private static readonly PdfName Adbe = new PdfName("ADBE");
    private static readonly PdfName BaseVersion = new PdfName("BaseVersion");
    private static readonly PdfName ExtensionLevel = new PdfName("ExtensionLevel");
    private static readonly PdfName Pdf17 = new PdfName("1.7");
    private static readonly PdfName EFF = new PdfName("EFF");
    private static readonly PdfName EFOpen = new PdfName("EFOpen");
    private static readonly PdfName EmbeddedFile = new PdfName("EmbeddedFile");
    private static readonly PdfName Crypt = new PdfName("Crypt");
    private static readonly PdfName Name = new PdfName("Name");

    private readonly byte[] _key;

    /// <summary>Whether the file must declare Adobe's extension level 8, the one 256-bit AES at revision 6 arrived in.</summary>
    private readonly bool _extended;

    private PdfEncryption(
        byte[] key, Cipher streams, Cipher strings, bool metadata, PdfDictionary dictionary, byte[] documentId, Cipher? embeddedFiles = null, bool extended = false)
    {
        _key = key;
        _extended = extended;
        Streams = streams;
        Strings = strings;
        EmbeddedFiles = embeddedFiles ?? streams;
        EncryptsMetadata = metadata;
        Dictionary = dictionary;
        DocumentId = documentId;
    }

    /// <summary>How strings and streams are encrypted.</summary>
    internal enum Cipher
    {
        None,
        Rc4,
        Aes128,
        Aes256,
    }

    public Cipher Streams { get; }

    public Cipher Strings { get; }

    /// <summary>
    /// How embedded files are encrypted: as <c>/EFF</c> says, or else as other streams are. Known even when the file was
    /// opened without its key (see <see cref="HasKey"/>), when they cannot be decrypted.
    /// </summary>
    public Cipher EmbeddedFiles { get; }

    /// <summary>
    /// Whether the file was opened with its key. One whose attachments alone are encrypted opens without a password,
    /// and without the key: its attachments stay encrypted.
    /// </summary>
    public bool HasKey => _key.Length > 0;

    public bool EncryptsMetadata { get; }

    /// <summary>The encryption dictionary, written unencrypted and outside any object stream.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>The file's identifier, whose first half keys RC4 and 128-bit AES: the file must keep it.</summary>
    public byte[] DocumentId { get; }

    /// <summary>Encryption as <paramref name="protection"/> asks, for a file identified by <paramref name="documentId"/>, or by a new one.</summary>
    public static PdfEncryption Create(Protection protection, byte[]? documentId = null)
    {
        ArgumentNullException.ThrowIfNull(protection);

        byte[] id = documentId ?? StandardSecurity.Random(16);
        int permissions = protection.Permissions;
        string owner = protection.OwnerPassword is { Length: > 0 } given ? given : Convert.ToBase64String(StandardSecurity.Random(24));
        bool metadata = protection.EncryptMetadata;

        if (protection.Encryption == EncryptionLevel.AesWith256Bits)
            return CreateAes256(protection.UserPassword, owner, permissions, metadata, id);

        (int version, int revision, int length, Cipher cipher) = protection.Encryption switch
        {
            EncryptionLevel.Rc4With40Bits => (1, 2, 5, Cipher.Rc4),
            EncryptionLevel.Rc4With128Bits => (2, 3, 16, Cipher.Rc4),
            _ => (4, 4, 16, Cipher.Aes128),
        };

        // RC4 below revision 4 always encrypts the metadata.
        metadata |= revision < 4;

        // Below revision 5 a password is PDFDocEncoding, and reading a file maps every character outside it to '?', so
        // two passwords in another script would open each other's files. Only creating is refused; a file already
        // made that way still opens.
        RequireDocEncoding(protection.UserPassword, "user");
        RequireDocEncoding(owner, "owner");

        byte[] ownerEntry = StandardSecurity.Owner(owner, protection.UserPassword, revision, length);
        byte[] key = StandardSecurity.FileKey(StandardSecurity.Pad(protection.UserPassword), ownerEntry, permissions, id, revision, length, metadata);

        PdfDictionary dictionary = new PdfDictionary
        {
            [Filter] = Standard,
            [V] = version,
            [R] = revision,
            [PdfNames.Length] = length * 8,
            [O] = new PdfString(ownerEntry, PdfStringForm.Hex),
            [U] = new PdfString(StandardSecurity.User(key, id, revision), PdfStringForm.Hex),
            [P] = permissions,
        };

        if (version == 4)
        {
            dictionary[CF] = new PdfDictionary { [StdCF] = new PdfDictionary { [CFM] = AesV2, [AuthEvent] = DocOpen, [PdfNames.Length] = 16 } };
            dictionary[StmF] = StdCF;
            dictionary[StrF] = StdCF;

            if (!metadata)
                dictionary[EncryptMetadata] = false;
        }

        return new PdfEncryption(key, cipher, cipher, metadata, dictionary, id);
    }

    private static void RequireDocEncoding(string password, string which)
    {
        foreach (char character in password)
        {
            if (PdfDocEncoding.Encode(character) < 0)
            {
                throw new ArgumentException(
                    $"The {which} password has characters that RC4 and 128-bit AES encryption cannot hold: they take only " +
                    $"PDFDocEncoding, roughly Latin-1. Use {nameof(EncryptionLevel)}.{nameof(EncryptionLevel.AesWith256Bits)}, which takes any password.",
                    "protection");
            }
        }
    }

    private static PdfEncryption CreateAes256(string userPassword, string ownerPassword, int permissions, bool metadata, byte[] id)
    {
        byte[] key = StandardSecurity.Random(32);
        byte[] user = StandardSecurity.Utf8(userPassword);
        byte[] owner = StandardSecurity.Utf8(ownerPassword);
        byte[] zero = new byte[16];

        byte[] userSalts = StandardSecurity.Random(16);
        byte[] userEntry = [.. StandardSecurity.Hash(user, userSalts.AsSpan(0, 8), [], 6), .. userSalts];
        byte[] userKey = StandardSecurity.Aes(StandardSecurity.Hash(user, userSalts.AsSpan(8, 8), [], 6), zero, key, true, CipherMode.CBC, PaddingMode.None);

        byte[] ownerSalts = StandardSecurity.Random(16);
        byte[] ownerEntry = [.. StandardSecurity.Hash(owner, ownerSalts.AsSpan(0, 8), userEntry, 6), .. ownerSalts];
        byte[] ownerKey = StandardSecurity.Aes(StandardSecurity.Hash(owner, ownerSalts.AsSpan(8, 8), userEntry, 6), zero, key, true, CipherMode.CBC, PaddingMode.None);

        byte[] perms = [.. BitConverter.GetBytes(permissions), 0xFF, 0xFF, 0xFF, 0xFF, (byte)(metadata ? 'T' : 'F'), (byte)'a', (byte)'d', (byte)'b', .. StandardSecurity.Random(4)];

        PdfDictionary dictionary = new PdfDictionary
        {
            [Filter] = Standard,
            [V] = 5,
            [R] = 6,
            [PdfNames.Length] = 256,
            [CF] = new PdfDictionary { [StdCF] = new PdfDictionary { [CFM] = AesV3, [AuthEvent] = DocOpen, [PdfNames.Length] = 32 } },
            [StmF] = StdCF,
            [StrF] = StdCF,
            [O] = new PdfString(ownerEntry, PdfStringForm.Hex),
            [U] = new PdfString(userEntry, PdfStringForm.Hex),
            [OE] = new PdfString(ownerKey, PdfStringForm.Hex),
            [UE] = new PdfString(userKey, PdfStringForm.Hex),
            [P] = permissions,
            // ECB because ISO 32000-2 says so (Algorithm 10): /Perms is a single block, a copy of the permissions
            // with four random bytes that a reader decrypts to check nothing altered them. Nothing else is written in ECB.
            [Perms] = new PdfString(StandardSecurity.Aes(key, null, perms, true, CipherMode.ECB, PaddingMode.None), PdfStringForm.Hex),
        };

        if (!metadata)
            dictionary[EncryptMetadata] = false;

        return new PdfEncryption(key, Cipher.Aes256, Cipher.Aes256, metadata, dictionary, id, extended: true);
    }

    /// <summary>
    /// Declares in a file's <paramref name="catalog"/> what this encryption needs beyond PDF 1.7, the version the file's
    /// header gives: 256-bit AES at revision 6 is Adobe's extension level 8 to it (ISO 32000-2, 7.6.4). A catalog that
    /// already declares extensions, one copied from an existing file, keeps its own. Encryption opened from a file needs
    /// nothing declared, since that file's catalog is copied with it.
    /// </summary>
    public void DeclareExtension(PdfDictionary catalog)
    {
        if (_extended && !catalog.ContainsKey(Extensions))
            catalog[Extensions] = new PdfDictionary { [Adbe] = new PdfDictionary { [BaseVersion] = Pdf17, [ExtensionLevel] = 8 } };
    }

    /// <summary>
    /// The encryption a file's <paramref name="dictionary"/> describes, opened with <paramref name="password"/> as the
    /// owner's or the user's; null when it is neither.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is encrypted by a handler other than the standard one.</exception>
    /// <exception cref="InvalidDataException">
    /// The dictionary is damaged: its key length or owner entry leaves no password to check, or the password opens the
    /// file but the key it unwraps is damaged.
    /// </exception>
    public static PdfEncryption? Open(PdfDictionary dictionary, byte[] id, string password, Func<PdfValue, PdfValue> resolve)
    {
        if (!dictionary.TryGetValue(Filter, out PdfValue filter) || resolve(filter) is not { Kind: PdfValueKind.Name } name || !name.AsName().Equals(Standard))
            throw new NotSupportedException("The file is encrypted by a security handler other than the standard, password one.");

        int version = Integer(dictionary, V, 0, resolve);
        int revision = Integer(dictionary, R, 0, resolve);
        int permissions = Integer(dictionary, P, 0, resolve);
        bool metadata = !(dictionary.TryGetValue(EncryptMetadata, out PdfValue flag) && resolve(flag) is { Kind: PdfValueKind.Boolean } encrypt && !encrypt.AsBoolean());
        byte[] owner = Bytes(dictionary, O, resolve);
        byte[] user = Bytes(dictionary, U, resolve);

        if (revision >= 5)
        {
            byte[]? key = OpenAes256(dictionary, password, owner, user, revision, resolve);
            Cipher aes = CryptFilter(dictionary, StmF, Cipher.Aes256, resolve);

            return key is null
                ? AttachmentsOnly(dictionary, id, metadata, resolve)
                : new PdfEncryption(key, aes, CryptFilter(dictionary, StrF, Cipher.Aes256, resolve), metadata, dictionary, id, EmbeddedFileFilter(dictionary, aes, Cipher.Aes256, resolve));
        }

        int length = revision == 2 ? 5 : Integer(dictionary, PdfNames.Length, 40, resolve) / 8;

        if (version == 4 && CryptLength(dictionary, resolve) is int bytes)
            length = bytes;

        // Both passwords are checked through a key of 40 to 128 bits and the 32 bytes of the owner entry: without them
        // no password can be checked at all, which is damage rather than a wrong password.
        if (length is < 5 or > 16 || owner.Length < 32)
            throw new InvalidDataException($"The file's encryption dictionary gives a {length * 8}-bit key and a {owner.Length}-byte /O; RC4 and AES-128 need 40 to 128 bits and 32 bytes.");

        byte[]? opened = StandardSecurity.AuthenticateUser(StandardSecurity.Pad(password), owner, user, permissions, id, revision, length, metadata)
            ?? StandardSecurity.AuthenticateUser(StandardSecurity.RecoverUser(password, owner, revision, length), owner, user, permissions, id, revision, length, metadata);

        if (opened is null)
            return version == 4 ? AttachmentsOnly(dictionary, id, metadata, resolve) : null;

        Cipher cipher = version == 4 ? Cipher.Aes128 : Cipher.Rc4;
        Cipher streams = version == 4 ? CryptFilter(dictionary, StmF, cipher, resolve) : Cipher.Rc4;
        Cipher strings = version == 4 ? CryptFilter(dictionary, StrF, cipher, resolve) : Cipher.Rc4;
        Cipher embedded = version == 4 ? EmbeddedFileFilter(dictionary, streams, cipher, resolve) : Cipher.Rc4;
        return new PdfEncryption(opened, streams, strings, metadata, dictionary, id, embedded);
    }

    /// <summary>The cipher of the embedded files' crypt filter, <c>/EFF</c>, or <paramref name="streams"/> when it names none.</summary>
    private static Cipher EmbeddedFileFilter(PdfDictionary dictionary, Cipher streams, Cipher fallback, Func<PdfValue, PdfValue> resolve) =>
        dictionary.ContainsKey(EFF) ? CryptFilter(dictionary, EFF, fallback, resolve) : streams;

    /// <summary>
    /// A file opened without its password whose strings and streams are left plain, its attachments alone encrypted by a
    /// crypt filter that asks for the password only when an attachment is opened (<c>/AuthEvent /EFOpen</c>, 7.6.5):
    /// viewers open it, and it is opened, its attachments left as they are. Null for any other file.
    /// </summary>
    private static PdfEncryption? AttachmentsOnly(PdfDictionary dictionary, byte[] id, bool metadata, Func<PdfValue, PdfValue> resolve)
    {
        if (CryptFilter(dictionary, StmF, Cipher.Aes128, resolve) != Cipher.None || CryptFilter(dictionary, StrF, Cipher.Aes128, resolve) != Cipher.None
            || !dictionary.TryGetValue(EFF, out PdfValue named) || resolve(named) is not { Kind: PdfValueKind.Name } name
            || Defined(dictionary, name.AsName(), resolve) is not { } filter
            || !filter.TryGetValue(AuthEvent, out PdfValue given) || resolve(given) is not { Kind: PdfValueKind.Name } authEvent || !authEvent.AsName().Equals(EFOpen))
        {
            return null;
        }

        // Without the key the attachments cannot be decrypted, but what they are encrypted with is kept: it decides whether
        // they can be carried as they are into the file written.
        return new PdfEncryption([], Cipher.None, Cipher.None, metadata, dictionary, id, Method(filter, Cipher.Aes128, resolve));
    }

    /// <summary>
    /// The cipher a stream read from the file was encrypted with: that of the crypt filter it names itself, when its first
    /// filter is <c>/Crypt</c> (7.4.10), which <paramref name="named"/> says; for an embedded file, the embedded files';
    /// for any other, the streams'. Cross-reference streams are never encrypted, nor metadata the dictionary leaves readable.
    /// </summary>
    public Cipher StreamCipher(PdfDictionary stream, Func<PdfValue, PdfValue> resolve, out bool named)
    {
        named = false;
        PdfName? type = stream.TryGetValue(PdfNames.Type, out PdfValue given) && resolve(given) is { Kind: PdfValueKind.Name } found ? found.AsName() : null;

        if (type is not null && type.Equals(PdfNames.XRef))
            return Cipher.None;

        if (OwnCryptFilter(stream, resolve) is { } own)
        {
            named = true;

            // Without the key nothing can be decrypted; a name the dictionary does not define is taken as the streams' filter.
            return !HasKey || own.Equals(Identity) ? Cipher.None : Defined(Dictionary, own, resolve) is { } filter ? Method(filter, Streams, resolve) : Streams;
        }

        // Without the key an attachment is left as it was read, encrypted.
        if (type is not null && type.Equals(EmbeddedFile))
            return HasKey ? EmbeddedFiles : Cipher.None;

        return type is not null && type.Equals(Metadata) && !EncryptsMetadata ? Cipher.None : Streams;
    }

    /// <summary>The crypt filter a stream whose first filter is <c>/Crypt</c> names in its parameters, Identity by default; null for any other stream.</summary>
    private static PdfName? OwnCryptFilter(PdfDictionary stream, Func<PdfValue, PdfValue> resolve)
    {
        PdfValue filters = stream.TryGetValue(Filter, out PdfValue given) ? resolve(given) : PdfValue.Null;
        PdfValue first = filters.Kind == PdfValueKind.Array && filters.AsArray().Count > 0 ? resolve(filters.AsArray()[0]) : filters;

        if (first.Kind != PdfValueKind.Name || !first.AsName().Equals(Crypt))
            return null;

        PdfValue parameters = stream.TryGetValue(PdfNames.DecodeParms, out PdfValue held) ? resolve(held) : PdfValue.Null;
        PdfValue own = parameters.Kind == PdfValueKind.Array && parameters.AsArray().Count > 0 ? resolve(parameters.AsArray()[0]) : parameters;

        return own.Kind == PdfValueKind.Dictionary && own.AsDictionary().TryGetValue(Name, out PdfValue name) && resolve(name) is { Kind: PdfValueKind.Name } chosen
            ? chosen.AsName()
            : Identity;
    }

    private static byte[]? OpenAes256(PdfDictionary dictionary, string password, byte[] owner, byte[] user, int revision, Func<PdfValue, PdfValue> resolve)
    {
        byte[] secret = StandardSecurity.Utf8(password);
        byte[] zero = new byte[16];

        if (owner.Length >= 48 && user.Length >= 48)
        {
            byte[] userEntry = user.AsSpan(0, 48).ToArray();

            if (StandardSecurity.Hash(secret, owner.AsSpan(32, 8), userEntry, revision).AsSpan().SequenceEqual(owner.AsSpan(0, 32)))
            {
                byte[] key = StandardSecurity.Hash(secret, owner.AsSpan(40, 8), userEntry, revision);
                return StandardSecurity.Aes(key, zero, WrappedKey(dictionary, OE, resolve), false, CipherMode.CBC, PaddingMode.None);
            }

            if (StandardSecurity.Hash(secret, user.AsSpan(32, 8), [], revision).AsSpan().SequenceEqual(user.AsSpan(0, 32)))
            {
                byte[] key = StandardSecurity.Hash(secret, user.AsSpan(40, 8), [], revision);
                return StandardSecurity.Aes(key, zero, WrappedKey(dictionary, UE, resolve), false, CipherMode.CBC, PaddingMode.None);
            }
        }

        return null;
    }

    /// <summary>The file key, as the /OE or /UE entry <paramref name="key"/> keeps it wrapped: thirty-two bytes.</summary>
    /// <exception cref="InvalidDataException">The entry is missing, or is not thirty-two bytes long.</exception>
    private static byte[] WrappedKey(PdfDictionary dictionary, PdfName key, Func<PdfValue, PdfValue> resolve)
    {
        byte[] wrapped = Bytes(dictionary, key, resolve);

        return wrapped.Length == 32
            ? wrapped
            : throw new InvalidDataException($"The password opens the file, but its /{key.Value} does not hold the 32 bytes of its key.");
    }

    /// <summary>
    /// The cipher a stream with <paramref name="dictionary"/> is written with: an embedded file the embedded files', a
    /// cross-reference stream and metadata left readable none, any other the streams'. Without the key nothing is encrypted:
    /// what a file opened without its password carries encrypted is written as it was read.
    /// </summary>
    private Cipher WrittenCipher(PdfDictionary dictionary)
    {
        if (!HasKey)
            return Cipher.None;

        if (!dictionary.TryGetValue(PdfNames.Type, out PdfValue type) || type.Kind != PdfValueKind.Name)
            return Streams;

        PdfName name = type.AsName();

        if (name.Equals(EmbeddedFile))
            return EmbeddedFiles;

        return name.Equals(PdfNames.XRef) || (name.Equals(Metadata) && !EncryptsMetadata) ? Cipher.None : Streams;
    }

    /// <summary>Whether a stream with <paramref name="dictionary"/> is written encrypted.</summary>
    public bool Covers(PdfDictionary dictionary) => WrittenCipher(dictionary) != Cipher.None;

    /// <summary>A stream's data, encrypted for object <paramref name="objectNumber"/> as a stream with <paramref name="dictionary"/> is.</summary>
    public byte[] EncryptStream(int objectNumber, PdfDictionary dictionary, ReadOnlySpan<byte> data) => Encrypt(WrittenCipher(dictionary), objectNumber, data);

    /// <summary>A string's bytes, encrypted for object <paramref name="objectNumber"/>.</summary>
    public byte[] EncryptString(int objectNumber, ReadOnlySpan<byte> data) => Encrypt(Strings, objectNumber, data);

    /// <summary>
    /// A stream's data, decrypted for object <paramref name="objectNumber"/> of <paramref name="generation"/>: files
    /// that were updated in place reuse numbers, and under RC4 and 128-bit AES the generation is part of the key.
    /// </summary>
    public byte[] DecryptStream(int objectNumber, byte[] data, int generation = 0) => Decrypt(Streams, objectNumber, data, generation);

    /// <summary>A string's bytes, decrypted for object <paramref name="objectNumber"/> of <paramref name="generation"/>.</summary>
    public byte[] DecryptString(int objectNumber, byte[] data, int generation = 0) => Decrypt(Strings, objectNumber, data, generation);

    private byte[] Encrypt(Cipher cipher, int objectNumber, ReadOnlySpan<byte> data)
    {
        switch (cipher)
        {
            case Cipher.Rc4:
                return Rc4.Transform(StandardSecurity.ObjectKey(_key, objectNumber, aes: false), data);

            case Cipher.Aes128:
            case Cipher.Aes256:
                byte[] key = cipher == Cipher.Aes256 ? _key : StandardSecurity.ObjectKey(_key, objectNumber, aes: true);
                return StandardSecurity.EncryptCbc(key, data);

            default:
                return data.ToArray();
        }
    }

    /// <summary>Data decrypted with <paramref name="cipher"/> for object <paramref name="objectNumber"/> of <paramref name="generation"/>.</summary>
    public byte[] Decrypt(Cipher cipher, int objectNumber, byte[] data, int generation)
    {
        switch (cipher)
        {
            case Cipher.Rc4:
                return Rc4.Transform(StandardSecurity.ObjectKey(_key, objectNumber, aes: false, generation), data);

            case Cipher.Aes128:
            case Cipher.Aes256:
                // Data too short to hold its vector and a block is left as it is, as readers leave it.
                if (data.Length < 32 || data.Length % 16 != 0)
                    return data.Length == 16 ? [] : data;

                byte[] key = cipher == Cipher.Aes256 ? _key : StandardSecurity.ObjectKey(_key, objectNumber, aes: true, generation);

                try
                {
                    return StandardSecurity.Aes(key, data.AsSpan(0, 16).ToArray(), data.AsSpan(16).ToArray(), false, CipherMode.CBC, PaddingMode.PKCS7);
                }
                catch (CryptographicException)
                {
                    // Padding that does not check out: the data is kept whole rather than lost.
                    return StandardSecurity.Aes(key, data.AsSpan(0, 16).ToArray(), data.AsSpan(16).ToArray(), false, CipherMode.CBC, PaddingMode.None);
                }

            default:
                return data;
        }
    }

    private static Cipher CryptFilter(PdfDictionary dictionary, PdfName which, Cipher fallback, Func<PdfValue, PdfValue> resolve)
    {
        if (!dictionary.TryGetValue(which, out PdfValue named) || resolve(named) is not { Kind: PdfValueKind.Name } name)
            return Cipher.None;

        if (name.AsName().Equals(Identity))
            return Cipher.None;

        return Defined(dictionary, name.AsName(), resolve) is { } found ? Method(found, fallback, resolve) : fallback;
    }

    /// <summary>The crypt filter the dictionary's <c>/CF</c> defines as <paramref name="name"/>, if it does.</summary>
    private static PdfDictionary? Defined(PdfDictionary dictionary, PdfName name, Func<PdfValue, PdfValue> resolve) =>
        dictionary.TryGetValue(CF, out PdfValue filters) && resolve(filters) is { Kind: PdfValueKind.Dictionary } all
        && all.AsDictionary().TryGetValue(name, out PdfValue filter) && resolve(filter) is { Kind: PdfValueKind.Dictionary } found
            ? found.AsDictionary()
            : null;

    /// <summary>The cipher a crypt filter's <c>/CFM</c> names, or <paramref name="fallback"/> when it names none this library knows.</summary>
    private static Cipher Method(PdfDictionary filter, Cipher fallback, Func<PdfValue, PdfValue> resolve)
    {
        if (filter.TryGetValue(CFM, out PdfValue method) && resolve(method) is { Kind: PdfValueKind.Name } kind)
        {
            PdfName chosen = kind.AsName();

            if (chosen.Equals(V2))
                return Cipher.Rc4;

            if (chosen.Equals(AesV2))
                return Cipher.Aes128;

            if (chosen.Equals(AesV3))
                return Cipher.Aes256;

            if (chosen.Value == "None")
                return Cipher.None;
        }

        return fallback;
    }

    /// <summary>The key length, in bytes, a version 4 dictionary's standard crypt filter gives, if it gives one.</summary>
    private static int? CryptLength(PdfDictionary dictionary, Func<PdfValue, PdfValue> resolve)
    {
        if (dictionary.TryGetValue(CF, out PdfValue filters) && resolve(filters) is { Kind: PdfValueKind.Dictionary } all
            && all.AsDictionary().TryGetValue(StdCF, out PdfValue filter) && resolve(filter) is { Kind: PdfValueKind.Dictionary } found)
        {
            int length = Integer(found.AsDictionary(), PdfNames.Length, 16, resolve);

            // Some writers give the length in bits, as the encryption dictionary does.
            return length > 16 ? length / 8 : length;
        }

        return null;
    }

    private static int Integer(PdfDictionary dictionary, PdfName key, int fallback, Func<PdfValue, PdfValue> resolve) =>
        dictionary.TryGetValue(key, out PdfValue value) && resolve(value) is { Kind: PdfValueKind.Integer } integer ? (int)integer.AsInteger() : fallback;

    private static byte[] Bytes(PdfDictionary dictionary, PdfName key, Func<PdfValue, PdfValue> resolve) =>
        dictionary.TryGetValue(key, out PdfValue value) && resolve(value) is { Kind: PdfValueKind.String } text ? text.AsString().Bytes.ToArray() : [];
}
