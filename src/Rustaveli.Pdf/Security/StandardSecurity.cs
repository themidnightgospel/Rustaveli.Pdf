#if !NET
using System.Buffers;
#endif
using System.Security.Cryptography;
using System.Text;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Security;

/// <summary>
/// The algorithms of PDF's standard security handler: the keys, and the owner and user entries that let a reader
/// check a password, at revisions 2 to 4 (ISO 32000-1, 7.6.3) and 5 and 6 (ISO 32000-2, 7.6.4).
/// </summary>
internal static class StandardSecurity
{
    /// <summary>What a password shorter than 32 bytes is padded with, and what an empty one is.</summary>
    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>A password as revisions 2 to 4 take it: in PDFDocEncoding, cut or padded to 32 bytes.</summary>
    public static byte[] Pad(string password)
    {
        byte[] padded = new byte[32];
        int length = Math.Min(password.Length, 32);

        for (int index = 0; index < length; index++)
        {
            int code = PdfDocEncoding.Encode(password[index]);
            padded[index] = (byte)(code < 0 ? '?' : code);
        }

        Padding.AsSpan(0, 32 - length).CopyTo(padded.AsSpan(length));
        return padded;
    }

    /// <summary>Algorithm 2: the file key from the padded user password.</summary>
    public static byte[] FileKey(byte[] paddedUser, byte[] owner, int permissions, byte[] id, int revision, int length, bool encryptMetadata)
    {
        using MD5 md5 = MD5.Create();
        List<byte> input = [.. paddedUser, .. owner.AsSpan(0, 32).ToArray(), .. BitConverter.GetBytes(permissions), .. id];

        if (revision >= 4 && !encryptMetadata)
            input.AddRange([0xFF, 0xFF, 0xFF, 0xFF]);

        byte[] hash = md5.ComputeHash(input.ToArray());

        if (revision >= 3)
        {
            for (int round = 0; round < 50; round++)
                hash = md5.ComputeHash(hash, 0, length);
        }

        return hash.AsSpan(0, length).ToArray();
    }

    /// <summary>Algorithm 3: the owner entry, the padded user password encrypted by a key from the owner password.</summary>
    public static byte[] Owner(string ownerPassword, string userPassword, int revision, int length)
    {
        byte[] key = OwnerKey(ownerPassword.Length > 0 ? ownerPassword : userPassword, revision, length);
        byte[] entry = Rc4.Transform(key, Pad(userPassword));

        if (revision >= 3)
        {
            for (int round = 1; round <= 19; round++)
                entry = Rc4.Transform(Xor(key, round), entry);
        }

        return entry;
    }

    /// <summary>Algorithms 4 and 5: the user entry, what the file key makes of the padding.</summary>
    public static byte[] User(byte[] fileKey, byte[] id, int revision)
    {
        if (revision == 2)
            return Rc4.Transform(fileKey, Padding);

        using MD5 md5 = MD5.Create();
        byte[] entry = Rc4.Transform(fileKey, md5.ComputeHash([.. Padding, .. id]));

        for (int round = 1; round <= 19; round++)
            entry = Rc4.Transform(Xor(fileKey, round), entry);

        // Sixteen bytes are checked; the rest only fill the entry out to its length.
        return [.. entry, .. new byte[16]];
    }

    /// <summary>Algorithm 6: the file key if <paramref name="paddedUser"/> is the user password, else null.</summary>
    public static byte[]? AuthenticateUser(byte[] paddedUser, byte[] owner, byte[] user, int permissions, byte[] id, int revision, int length, bool encryptMetadata)
    {
        byte[] key = FileKey(paddedUser, owner, permissions, id, revision, length, encryptMetadata);
        byte[] expected = User(key, id, revision);
        int compared = revision == 2 ? 32 : 16;

        return user.Length >= compared && expected.AsSpan(0, compared).SequenceEqual(user.AsSpan(0, compared)) ? key : null;
    }

    /// <summary>Algorithm 7: the padded user password the owner password recovers, which then opens the file.</summary>
    public static byte[] RecoverUser(string ownerPassword, byte[] owner, int revision, int length)
    {
        byte[] key = OwnerKey(ownerPassword, revision, length);
        byte[] entry = owner.AsSpan(0, 32).ToArray();

        if (revision == 2)
            return Rc4.Transform(key, entry);

        for (int round = 19; round >= 0; round--)
            entry = Rc4.Transform(Xor(key, round), entry);

        return entry;
    }

    /// <summary>Algorithm 1: the key one object is encrypted with, from the file key and its number.</summary>
    public static byte[] ObjectKey(byte[] fileKey, int objectNumber, bool aes)
    {
        using MD5 md5 = MD5.Create();
        List<byte> input = [.. fileKey, (byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16), 0, 0];

        if (aes)
            input.AddRange("sAlT"u8.ToArray());

        return md5.ComputeHash(input.ToArray()).AsSpan(0, Math.Min(fileKey.Length + 5, 16)).ToArray();
    }

    /// <summary>A password as revisions 5 and 6 take it: UTF-8, at most 127 bytes.</summary>
    public static byte[] Utf8(string password)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length <= 127 ? bytes : bytes.AsSpan(0, 127).ToArray();
    }

    /// <summary>
    /// Algorithm 2.B, the hash revision 6 checks passwords and derives keys with; at revision 5, SHA-256 alone.
    /// </summary>
    public static byte[] Hash(byte[] password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> userEntry, int revision)
    {
        byte[] start = [.. password, .. salt.ToArray(), .. userEntry.ToArray()];
        byte[] key;

        using (SHA256 sha256 = SHA256.Create())
            key = sha256.ComputeHash(start);

        if (revision == 5)
            return key;

        byte[] udata = userEntry.ToArray();
        byte[] encrypted = [0];

        for (int round = 0; round < 64 || encrypted[encrypted.Length - 1] > round - 32; round++)
        {
            byte[] single = [.. password, .. key, .. udata];
            byte[] repeated = new byte[single.Length * 64];

            for (int copy = 0; copy < 64; copy++)
                single.CopyTo(repeated, copy * single.Length);

            encrypted = Aes(key.AsSpan(0, 16).ToArray(), key.AsSpan(16, 16).ToArray(), repeated, encrypt: true, CipherMode.CBC, PaddingMode.None);

            int remainder = 0;
            for (int index = 0; index < 16; index++)
                remainder += encrypted[index];

            using HashAlgorithm next = (remainder % 3) switch
            {
                0 => SHA256.Create(),
                1 => SHA384.Create(),
                _ => SHA512.Create(),
            };

            key = next.ComputeHash(encrypted);
        }

        return key.AsSpan(0, 32).ToArray();
    }

    public static byte[] Aes(byte[] key, byte[]? iv, byte[] data, bool encrypt, CipherMode mode, PaddingMode padding)
    {
        using Aes aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        aes.Mode = mode;
        aes.Padding = padding;

        if (iv is not null)
            aes.IV = iv;

        using ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    /// <summary>
    /// <paramref name="data"/> encrypted with AES in CBC mode and PKCS #7 padding under a random vector, which comes first,
    /// as PDF stores a string or stream (ISO 32000-1, 7.6.2). A stream can be most of a document, so it is encrypted
    /// straight into the one array returned rather than copied on the way.
    /// </summary>
    public static byte[] EncryptCbc(byte[] key, ReadOnlySpan<byte> data)
    {
        // Padding always adds from one byte to a whole block.
        byte[] result = new byte[16 + ((data.Length / 16) + 1) * 16];
        byte[] iv = Random(16);
        iv.CopyTo(result, 0);

        using Aes aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;

#if NET
        aes.EncryptCbc(data, iv, result.AsSpan(16), PaddingMode.PKCS7);
#else
        // Without span overloads the data passes through a small rented buffer, a run of whole blocks at a time.
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = iv;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] chunk = ArrayPool<byte>.Shared.Rent(Math.Min(Math.Max(data.Length, 16), 64 * 1024));
        int run = chunk.Length / 16 * 16;
        int whole = data.Length - (data.Length % 16);
        int read = 0;
        int written = 16;

        try
        {
            while (read < whole)
            {
                int count = Math.Min(run, whole - read);
                data.Slice(read, count).CopyTo(chunk);
                written += encryptor.TransformBlock(chunk, 0, count, result, written);
                read += count;
            }

            data.Slice(read).CopyTo(chunk);
            encryptor.TransformFinalBlock(chunk, 0, data.Length - read).CopyTo(result, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
#endif

        return result;
    }

    public static byte[] Random(int length)
    {
        byte[] bytes = new byte[length];
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return bytes;
    }

    private static byte[] OwnerKey(string password, int revision, int length)
    {
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Pad(password));

        if (revision >= 3)
        {
            for (int round = 0; round < 50; round++)
                hash = md5.ComputeHash(hash);
        }

        return hash.AsSpan(0, revision == 2 ? 5 : length).ToArray();
    }

    private static byte[] Xor(byte[] key, int value)
    {
        byte[] result = new byte[key.Length];

        for (int index = 0; index < key.Length; index++)
            result[index] = (byte)(key[index] ^ value);

        return result;
    }
}
