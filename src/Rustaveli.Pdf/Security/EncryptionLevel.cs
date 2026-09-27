namespace Rustaveli.Pdf;

/// <summary>How strongly a protected PDF is encrypted, from the weakest readers still honour to the strongest.</summary>
public enum EncryptionLevel
{
    /// <summary>RC4 with a 40-bit key: PDF 1.1's, opened by every reader and by anyone who tries.</summary>
    Rc4With40Bits,

    /// <summary>RC4 with a 128-bit key: PDF 1.4's.</summary>
    Rc4With128Bits,

    /// <summary>AES with a 128-bit key: PDF 1.6's.</summary>
    AesWith128Bits,

    /// <summary>AES with a 256-bit key: PDF 2.0's, and the only one still considered strong.</summary>
    AesWith256Bits,
}
