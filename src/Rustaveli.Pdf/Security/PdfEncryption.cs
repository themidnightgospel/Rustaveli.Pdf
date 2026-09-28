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

    private readonly byte[] _key;

    private PdfEncryption(byte[] key, Cipher streams, Cipher strings, bool metadata, PdfDictionary dictionary, byte[] documentId)
    {
        _key = key;
        Streams = streams;
        Strings = strings;
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
            [Perms] = new PdfString(StandardSecurity.Aes(key, null, perms, true, CipherMode.ECB, PaddingMode.None), PdfStringForm.Hex),
        };

        if (!metadata)
            dictionary[EncryptMetadata] = false;

        return new PdfEncryption(key, Cipher.Aes256, Cipher.Aes256, metadata, dictionary, id);
    }

    /// <summary>
    /// The encryption a file's <paramref name="dictionary"/> describes, opened with <paramref name="password"/> as the
    /// owner's or the user's; null when it is neither.
    /// </summary>
    /// <exception cref="NotSupportedException">The file is encrypted by a handler other than the standard one.</exception>
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
            return key is null ? null : new PdfEncryption(key, CryptFilter(dictionary, StmF, Cipher.Aes256, resolve), CryptFilter(dictionary, StrF, Cipher.Aes256, resolve), metadata, dictionary, id);
        }

        int length = revision == 2 ? 5 : Integer(dictionary, PdfNames.Length, 40, resolve) / 8;

        if (version == 4 && CryptLength(dictionary, resolve) is int bytes)
            length = bytes;

        byte[]? opened = StandardSecurity.AuthenticateUser(StandardSecurity.Pad(password), owner, user, permissions, id, revision, length, metadata)
            ?? StandardSecurity.AuthenticateUser(StandardSecurity.RecoverUser(password, owner, revision, length), owner, user, permissions, id, revision, length, metadata);

        if (opened is null)
            return null;

        Cipher cipher = version == 4 ? Cipher.Aes128 : Cipher.Rc4;
        Cipher streams = version == 4 ? CryptFilter(dictionary, StmF, cipher, resolve) : Cipher.Rc4;
        Cipher strings = version == 4 ? CryptFilter(dictionary, StrF, cipher, resolve) : Cipher.Rc4;
        return new PdfEncryption(opened, streams, strings, metadata, dictionary, id);
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
                return StandardSecurity.Aes(key, zero, Bytes(dictionary, OE, resolve), false, CipherMode.CBC, PaddingMode.None);
            }

            if (StandardSecurity.Hash(secret, user.AsSpan(32, 8), [], revision).AsSpan().SequenceEqual(user.AsSpan(0, 32)))
            {
                byte[] key = StandardSecurity.Hash(secret, user.AsSpan(40, 8), [], revision);
                return StandardSecurity.Aes(key, zero, Bytes(dictionary, UE, resolve), false, CipherMode.CBC, PaddingMode.None);
            }
        }

        return null;
    }

    /// <summary>Whether a stream with <paramref name="dictionary"/> is encrypted: all but cross-reference streams, and readable metadata.</summary>
    public bool Covers(PdfDictionary dictionary)
    {
        if (Streams == Cipher.None)
            return false;

        if (!dictionary.TryGetValue(PdfNames.Type, out PdfValue type) || type.Kind != PdfValueKind.Name)
            return true;

        PdfName name = type.AsName();
        return !name.Equals(PdfNames.XRef) && (EncryptsMetadata || !name.Equals(Metadata));
    }

    /// <summary>A stream's data, encrypted for object <paramref name="objectNumber"/>.</summary>
    public byte[] EncryptStream(int objectNumber, ReadOnlySpan<byte> data) => Encrypt(Streams, objectNumber, data);

    /// <summary>A string's bytes, encrypted for object <paramref name="objectNumber"/>.</summary>
    public byte[] EncryptString(int objectNumber, ReadOnlySpan<byte> data) => Encrypt(Strings, objectNumber, data);

    public byte[] DecryptStream(int objectNumber, byte[] data) => Decrypt(Streams, objectNumber, data);

    public byte[] DecryptString(int objectNumber, byte[] data) => Decrypt(Strings, objectNumber, data);

    private byte[] Encrypt(Cipher cipher, int objectNumber, ReadOnlySpan<byte> data)
    {
        switch (cipher)
        {
            case Cipher.Rc4:
                return Rc4.Transform(StandardSecurity.ObjectKey(_key, objectNumber, aes: false), data);

            case Cipher.Aes128:
            case Cipher.Aes256:
                byte[] key = cipher == Cipher.Aes256 ? _key : StandardSecurity.ObjectKey(_key, objectNumber, aes: true);
                byte[] iv = StandardSecurity.Random(16);
                return [.. iv, .. StandardSecurity.Aes(key, iv, data.ToArray(), true, CipherMode.CBC, PaddingMode.PKCS7)];

            default:
                return data.ToArray();
        }
    }

    private byte[] Decrypt(Cipher cipher, int objectNumber, byte[] data)
    {
        switch (cipher)
        {
            case Cipher.Rc4:
                return Rc4.Transform(StandardSecurity.ObjectKey(_key, objectNumber, aes: false), data);

            case Cipher.Aes128:
            case Cipher.Aes256:
                // Data too short to hold its vector and a block is left as it is, as readers leave it.
                if (data.Length < 32 || data.Length % 16 != 0)
                    return data.Length == 16 ? [] : data;

                byte[] key = cipher == Cipher.Aes256 ? _key : StandardSecurity.ObjectKey(_key, objectNumber, aes: true);

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

        if (dictionary.TryGetValue(CF, out PdfValue filters) && resolve(filters) is { Kind: PdfValueKind.Dictionary } all
            && all.AsDictionary().TryGetValue(name.AsName(), out PdfValue filter) && resolve(filter) is { Kind: PdfValueKind.Dictionary } found
            && found.AsDictionary().TryGetValue(CFM, out PdfValue method) && resolve(method) is { Kind: PdfValueKind.Name } kind)
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
