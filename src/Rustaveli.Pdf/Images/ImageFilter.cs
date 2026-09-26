namespace Rustaveli.Pdf.Images;

/// <summary>The PDF stream filter that decodes <see cref="EncodedImage.Data"/>.</summary>
internal enum ImageFilter
{
    /// <summary>/DCTDecode: the data is a complete JPEG file.</summary>
    Dct,

    /// <summary>/FlateDecode: the data is a zlib stream (RFC 1950), header and checksum included.</summary>
    Flate,
}
