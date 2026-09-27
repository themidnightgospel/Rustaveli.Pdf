using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.UnitTests.Images;

public class IccProfileTests
{
    [Theory]
    [InlineData("GRAY", 1)]
    [InlineData("RGB ", 3)]
    [InlineData("CMYK", 4)]
    public void AcceptsAProfileWhoseColourSpaceMatches(string colorSpace, int components)
    {
        byte[] data = TestJpeg.Profile(colorSpace);

        IccProfile? profile = IccProfile.TryCreate(data, components);

        Assert.NotNull(profile);
        Assert.Equal(components, profile.ComponentCount);
        Assert.Equal(data, profile.Data.ToArray());
        Assert.Equal(ImageContentHash.Of(data), profile.Hash);
    }

    [Theory]
    [InlineData("GRAY", 3)]
    [InlineData("RGB ", 1)]
    [InlineData("RGB ", 4)]
    [InlineData("CMYK", 3)]
    [InlineData("Lab ", 3)]
    [InlineData("XYZ ", 3)]
    public void RejectsAProfileForADifferentColourSpace(string colorSpace, int components)
    {
        Assert.Null(IccProfile.TryCreate(TestJpeg.Profile(colorSpace), components));
    }

    [Fact]
    public void TrimsTrailingBytesBeyondTheDeclaredSize()
    {
        byte[] data = TestJpeg.Profile("RGB ", size: 300, declaredSize: 250);

        IccProfile? profile = IccProfile.TryCreate(data, 3);

        Assert.NotNull(profile);
        Assert.Equal(data.AsSpan(0, 250).ToArray(), profile.Data.ToArray());
        Assert.Equal(ImageContentHash.Of(data.AsSpan(0, 250)), profile.Hash);
    }

    [Fact]
    public void AcceptsTheSmallestPossibleProfile()
    {
        Assert.NotNull(IccProfile.TryCreate(TestJpeg.Profile("RGB ", size: 132), 3));
    }

    [Fact]
    public void RejectsDataShorterThanAHeaderAndTagCount()
    {
        Assert.Null(IccProfile.TryCreate(TestJpeg.Profile("RGB ", size: 131, declaredSize: 131), 3));
        Assert.Null(IccProfile.TryCreate(ReadOnlyMemory<byte>.Empty, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(131)]
    [InlineData(201)]
    public void RejectsADeclaredSizeOutsideTheData(int declaredSize)
    {
        Assert.Null(IccProfile.TryCreate(TestJpeg.Profile("RGB ", size: 200, declaredSize: declaredSize), 3));
    }

    [Fact]
    public void RejectsAHugeDeclaredSize()
    {
        byte[] data = TestJpeg.Profile("RGB ");
        TestPng.WriteUInt32(data, 0, 0xFFFFFFF0u);

        Assert.Null(IccProfile.TryCreate(data, 3));
    }

    [Fact]
    public void RequiresTheProfileSignature()
    {
        byte[] data = TestJpeg.Profile("RGB ");
        data[39] = (byte)'q';

        Assert.Null(IccProfile.TryCreate(data, 3));
    }
}
