namespace Rustaveli.Pdf.Images;

/// <summary>
/// The colour space of an <see cref="EncodedImage"/>, described in PDF terms. Switch on <see cref="Kind"/>; the
/// members that do not apply to a kind are empty or null.
/// </summary>
internal sealed class ImageColorSpace
{
    private ImageColorSpace(
        ImageColorSpaceKind kind,
        int componentCount,
        ImageColorSpace? baseSpace,
        ReadOnlyMemory<byte> palette,
        IccProfile? profile)
    {
        Kind = kind;
        ComponentCount = componentCount;
        Base = baseSpace;
        Palette = palette;
        Profile = profile;
    }

    public static ImageColorSpace DeviceGray { get; } = Device(ImageColorSpaceKind.DeviceGray, 1);

    public static ImageColorSpace DeviceRgb { get; } = Device(ImageColorSpaceKind.DeviceRgb, 3);

    public static ImageColorSpace DeviceCmyk { get; } = Device(ImageColorSpaceKind.DeviceCmyk, 4);

    public ImageColorSpaceKind Kind { get; }

    /// <summary>
    /// The number of samples per pixel in image data using this space: 1 for <see cref="ImageColorSpaceKind.Indexed"/>
    /// (the index), otherwise the number of colour components.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>For <see cref="ImageColorSpaceKind.Indexed"/>: the space the palette entries are in.</summary>
    public ImageColorSpace? Base { get; }

    /// <summary>
    /// For <see cref="ImageColorSpaceKind.Indexed"/>: the lookup table, <see cref="Base"/>'s component count bytes
    /// per entry, entry 0 first — exactly the bytes of the PDF lookup string.
    /// </summary>
    public ReadOnlyMemory<byte> Palette { get; }

    /// <summary>
    /// For <see cref="ImageColorSpaceKind.Indexed"/>: the highest valid index, the PDF hival operand.
    /// </summary>
    public int HighValue => Base == null ? 0 : (Palette.Length / Base.ComponentCount) - 1;

    /// <summary>For <see cref="ImageColorSpaceKind.IccBased"/>: the profile, whose component count is /N.</summary>
    public IccProfile? Profile { get; }

    /// <summary>
    /// For <see cref="ImageColorSpaceKind.IccBased"/>: the device space a reader falls back to if it cannot use
    /// the profile, the PDF /Alternate.
    /// </summary>
    public ImageColorSpace? Alternate => Profile == null ? null : Device(Profile.ComponentCount);

    /// <summary>An ICC-based space for <paramref name="profile"/>.</summary>
    public static ImageColorSpace Icc(IccProfile profile) =>
        new ImageColorSpace(ImageColorSpaceKind.IccBased, profile.ComponentCount, null, default, profile);

    /// <summary>A palette of colours in <paramref name="baseSpace"/>, the image data holding indices into it.</summary>
    public static ImageColorSpace Indexed(ImageColorSpace baseSpace, ReadOnlyMemory<byte> palette) =>
        new ImageColorSpace(ImageColorSpaceKind.Indexed, 1, baseSpace, palette, null);

    /// <summary>The ICC-based space for <paramref name="profile"/> if there is one, else the device space.</summary>
    public static ImageColorSpace For(int components, IccProfile? profile) =>
        profile == null ? Device(components) : Icc(profile);

    private static ImageColorSpace Device(ImageColorSpaceKind kind, int components) =>
        new ImageColorSpace(kind, components, null, default, null);

    private static ImageColorSpace Device(int components) => components switch
    {
        1 => DeviceGray,
        3 => DeviceRgb,
        _ => DeviceCmyk,
    };
}
