namespace Rustaveli.Pdf.Images;

/// <summary>The PDF colour space family an <see cref="ImageColorSpace"/> becomes.</summary>
internal enum ImageColorSpaceKind
{
    /// <summary>/DeviceGray.</summary>
    DeviceGray,

    /// <summary>/DeviceRGB.</summary>
    DeviceRgb,

    /// <summary>/DeviceCMYK.</summary>
    DeviceCmyk,

    /// <summary>[/Indexed base hival lookup].</summary>
    Indexed,

    /// <summary>[/ICCBased stream], the stream carrying the profile and /N.</summary>
    IccBased,
}
