using System.Buffers;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Receives decoded PNG rows and compresses them again in the form PDF needs: colour samples in one zlib stream
/// and opacity in another, each row PNG-filtered so the result is both compact and readable with /Predictor 15.
/// </summary>
/// <remarks>
/// PDF has no interleaved alpha. An image with an alpha channel therefore becomes a colour image plus a separate
/// /DeviceGray soft mask of the same size, and a palette image with partial transparency gains a soft mask built
/// from its palette's alpha. Samples keep their bit depth: nothing is reduced or rounded.
/// </remarks>
internal sealed class PngReencoder : IPngRowSink, IDisposable
{
    private readonly PngHeader _header;
    private readonly byte[]? _paletteAlpha;
    private readonly int _colorBytes;
    private readonly int _alphaBytes;
    private readonly ZlibWriter _color = new ZlibWriter();
    private readonly ZlibWriter? _alpha;
    private readonly byte[] _filtered;
    private byte[] _colorRow;
    private byte[] _previousColorRow;
    private byte[]? _alphaRow;
    private byte[]? _previousAlphaRow;
    private bool _translucent;

    /// <param name="header">The image being decoded.</param>
    /// <param name="paletteAlpha">The palette image's tRNS entries, when <paramref name="paletteMask"/> is set.</param>
    /// <param name="paletteMask">True to build a soft mask by looking each palette index up in its alpha.</param>
    public PngReencoder(PngHeader header, ReadOnlySpan<byte> paletteAlpha, bool paletteMask)
    {
        _header = header;
        _colorBytes = (int)RowBytes(header.Width, header.ColorChannels, header.BitDepth);
        _colorRow = Rent(_colorBytes);
        _previousColorRow = Rent(_colorBytes);
        int filtered = _colorBytes;

        if (header.HasAlphaChannel || paletteMask)
        {
            if (paletteMask)
            {
                // Entries beyond the tRNS chunk, and indices beyond the palette, are opaque.
                _paletteAlpha = new byte[256];
                _paletteAlpha.AsSpan().Fill(0xFF);
                paletteAlpha.CopyTo(_paletteAlpha);
            }

            _alphaBytes = (int)RowBytes(header.Width, 1, AlphaBitDepth);
            _alpha = new ZlibWriter();
            _alphaRow = Rent(_alphaBytes);
            _previousAlphaRow = Rent(_alphaBytes);
            filtered = Math.Max(filtered, _alphaBytes);
        }

        _filtered = ArrayPool<byte>.Shared.Rent(filtered + 1);
    }

    /// <summary>Bits per sample of the soft mask: the alpha channel's depth, or 8 for palette alpha.</summary>
    public int AlphaBitDepth => _header.HasAlphaChannel ? _header.BitDepth : 8;

    public void Accept(ReadOnlySpan<byte> row)
    {
        if (_header.HasAlphaChannel)
            SplitAlpha(row);
        else
            row.CopyTo(_colorRow);

        if (_paletteAlpha != null)
            LookUpAlpha(row);

        Write(_color, _colorRow, _previousColorRow, _colorBytes, _header.BitDepth, _header.ColorChannels);
        (_colorRow, _previousColorRow) = (_previousColorRow, _colorRow);

        if (_alpha != null)
        {
            _translucent |= _alphaRow!.AsSpan(0, _alphaBytes).IndexOfAnyExcept((byte)0xFF) >= 0;
            Write(_alpha, _alphaRow!, _previousAlphaRow!, _alphaBytes, AlphaBitDepth, 1);
            (_alphaRow, _previousAlphaRow) = (_previousAlphaRow, _alphaRow);
        }
    }

    /// <summary>Completes the colour stream.</summary>
    public byte[] FinishColor() => _color.Finish();

    /// <summary>
    /// Completes the opacity stream, or returns null if there is none or every pixel turned out fully opaque — an
    /// RGBA image with an unused alpha channel is common, and a mask that masks nothing only costs the reader time.
    /// </summary>
    public byte[]? FinishAlpha() => _alpha != null && _translucent ? _alpha.Finish() : null;

    public void Dispose()
    {
        _color.Dispose();
        _alpha?.Dispose();
        ArrayPool<byte>.Shared.Return(_colorRow);
        ArrayPool<byte>.Shared.Return(_previousColorRow);
        ArrayPool<byte>.Shared.Return(_filtered);
        if (_alphaRow != null)
        {
            ArrayPool<byte>.Shared.Return(_alphaRow);
            ArrayPool<byte>.Shared.Return(_previousAlphaRow!);
        }
    }

    private static long RowBytes(long pixels, int channels, int bitDepth) => ((pixels * channels * bitDepth) + 7) / 8;

    // A cleared buffer, because the first row of an image is filtered against a row of zeros.
    private static byte[] Rent(int length)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        Array.Clear(buffer, 0, length);
        return buffer;
    }

    private void SplitAlpha(ReadOnlySpan<byte> row)
    {
        int sampleBytes = _header.BitDepth / 8;
        int colorBytes = _header.ColorChannels * sampleBytes;
        byte[] color = _colorRow;
        byte[] alpha = _alphaRow!;
        int source = 0;
        int colorIndex = 0;
        int alphaIndex = 0;

        for (int pixel = 0; pixel < _header.Width; pixel++)
        {
            for (int index = 0; index < colorBytes; index++)
                color[colorIndex++] = row[source++];
            for (int index = 0; index < sampleBytes; index++)
                alpha[alphaIndex++] = row[source++];
        }
    }

    private void LookUpAlpha(ReadOnlySpan<byte> row)
    {
        byte[] table = _paletteAlpha!;
        byte[] alpha = _alphaRow!;
        int bits = _header.BitDepth;
        int mask = (1 << bits) - 1;

        for (int pixel = 0; pixel < _header.Width; pixel++)
        {
            int bit = pixel * bits;
            int index = (row[bit >> 3] >> (8 - bits - (bit & 7))) & mask;
            alpha[pixel] = table[index];
        }
    }

    // Paeth suits continuous-tone samples; below 8 bits (bilevel, and palette indices) filtering rarely pays, which
    // is also libpng's default, so those rows go out unfiltered.
    private void Write(ZlibWriter writer, byte[] row, byte[] previous, int length, int bitDepth, int channels)
    {
        if (bitDepth < 8)
        {
            _filtered[0] = PngFilters.None;
            Array.Copy(row, 0, _filtered, 1, length);
        }
        else
        {
            int step = channels * bitDepth / 8;
            PngFilters.ApplyPaeth(
                row.AsSpan(0, length), previous.AsSpan(0, length), _filtered.AsSpan(0, length + 1), step);
        }

        writer.Write(_filtered, 0, length + 1);
    }
}
