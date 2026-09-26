using System.Buffers;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Inflates a PNG's image data, reverses the row filters and undoes Adam7 interlacing, handing each finished row
/// to an <see cref="IPngRowSink"/>.
/// </summary>
/// <remarks>
/// A non-interlaced image streams through two pooled row buffers and is never held whole. An interlaced one must
/// be: its first pass holds pixels from every eighth row, so no row is complete until the last pass. All buffers
/// come from <see cref="ArrayPool{T}"/>, and nothing is allocated per row or per pixel.
/// </remarks>
internal static class PngScanlineDecoder
{
    // Deflate cannot expand data by more than a factor of 1032 (a 258-byte match coded in two bits), so image data
    // shorter than this fraction of the size the header promises is truncated. Refusing it up front stops a few
    // hundred forged bytes from claiming, and allocating for, a gigapixel image.
    private const long MaxDeflateExpansion = 1032;

    public static void Decode(PngFile png, IPngRowSink sink)
    {
        PngHeader header = png.Header;
        long rowBytes = header.RowBytes(header.Width);
        if (rowBytes >= ImageLimits.MaxArrayLength)
            throw new ImageFormatException($"The PNG's rows, {rowBytes} bytes each, are too long to decode.");

        long imageBytes = rowBytes * header.Height;
        if (header.Interlaced && imageBytes > ImageLimits.MaxArrayLength)
        {
            throw new ImageFormatException(
                $"The interlaced PNG needs {imageBytes} bytes to de-interlace, which is too many.");
        }

        long expected = ExpectedLength(header);
        if (expected > (png.ImageDataLength * MaxDeflateExpansion) + MaxDeflateExpansion)
        {
            throw new ImageFormatException(
                $"The PNG's {png.ImageDataLength} bytes of image data cannot hold the {expected} bytes its " +
                "header requires.");
        }

        byte[]? pooled = null;
        try
        {
            ReadOnlyMemory<byte> zlib;
            if (png.ImageData.Count == 1)
            {
                zlib = png.ConcatenateImageData();
            }
            else
            {
                pooled = ArrayPool<byte>.Shared.Rent((int)png.ImageDataLength);
                png.CopyImageData(pooled);
                zlib = new ReadOnlyMemory<byte>(pooled, 0, (int)png.ImageDataLength);
            }

            using ZlibReader reader = new ZlibReader(zlib);
            if (header.Interlaced)
                DecodeInterlaced(reader, header, (int)rowBytes, sink);
            else
                DecodeSequential(reader, header, (int)rowBytes, sink);

            reader.Finish();
        }
        finally
        {
            if (pooled != null)
                ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    /// <summary>The length of the inflated image data: every row of every pass, with its filter-type byte.</summary>
    public static long ExpectedLength(PngHeader header)
    {
        if (!header.Interlaced)
            return header.Height * (header.RowBytes(header.Width) + 1);

        long total = 0;
        for (int pass = 0; pass < Adam7.PassCount; pass++)
        {
            int columns = Adam7.Columns(pass, header.Width);
            int rows = Adam7.Rows(pass, header.Height);
            if (columns > 0)
                total += rows * (header.RowBytes(columns) + 1);
        }

        return total;
    }

    private static void DecodeSequential(ZlibReader reader, PngHeader header, int rowBytes, IPngRowSink sink)
    {
        byte[] current = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        byte[] previous = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        try
        {
            Array.Clear(previous, 0, rowBytes + 1);
            for (int row = 0; row < header.Height; row++)
            {
                ReadRow(reader, current, previous, rowBytes, header.FilterStep);
                sink.Accept(current.AsSpan(1, rowBytes));
                (current, previous) = (previous, current);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(current);
            ArrayPool<byte>.Shared.Return(previous);
        }
    }

    private static void DecodeInterlaced(ZlibReader reader, PngHeader header, int rowBytes, IPngRowSink sink)
    {
        int imageBytes = rowBytes * header.Height;
        byte[] image = ArrayPool<byte>.Shared.Rent(imageBytes);
        byte[] current = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        byte[] previous = ArrayPool<byte>.Shared.Rent(rowBytes + 1);
        try
        {
            // Pooled memory holds stale data, and below 8 bits pixels are merged into their bytes bit by bit, so
            // the image starts cleared; that also keeps the padding bits at the end of each row zero.
            Array.Clear(image, 0, imageBytes);

            for (int pass = 0; pass < Adam7.PassCount; pass++)
            {
                int columns = Adam7.Columns(pass, header.Width);
                int rows = Adam7.Rows(pass, header.Height);
                if (columns == 0 || rows == 0)
                    continue;

                int passBytes = (int)header.RowBytes(columns);
                Array.Clear(previous, 0, passBytes + 1);
                for (int row = 0; row < rows; row++)
                {
                    ReadRow(reader, current, previous, passBytes, header.FilterStep);
                    int y = Adam7.RowStart(pass) + (row * Adam7.RowStep(pass));
                    Span<byte> target = image.AsSpan(y * rowBytes, rowBytes);
                    Scatter(current.AsSpan(1, passBytes), target, pass, columns, header.BitsPerPixel);
                    (current, previous) = (previous, current);
                }
            }

            for (int y = 0; y < header.Height; y++)
                sink.Accept(image.AsSpan(y * rowBytes, rowBytes));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(image);
            ArrayPool<byte>.Shared.Return(current);
            ArrayPool<byte>.Shared.Return(previous);
        }
    }

    private static void ReadRow(ZlibReader reader, byte[] current, byte[] previous, int rowBytes, int step)
    {
        reader.ReadExactly(current, 0, rowBytes + 1);
        PngFilters.Unfilter(current[0], current.AsSpan(1, rowBytes), previous.AsSpan(1, rowBytes), step);
    }

    // Places the pixels of one pass row at their positions in the full image row.
    private static void Scatter(ReadOnlySpan<byte> source, Span<byte> target, int pass, int columns, int bitsPerPixel)
    {
        int start = Adam7.ColumnStart(pass);
        int step = Adam7.ColumnStep(pass);

        if (bitsPerPixel >= 8)
        {
            int bytes = bitsPerPixel / 8;
            for (int column = 0; column < columns; column++)
            {
                int from = column * bytes;
                int to = (start + (column * step)) * bytes;
                for (int index = 0; index < bytes; index++)
                    target[to + index] = source[from + index];
            }

            return;
        }

        // Below 8 bits there is one sample per pixel, packed from the most significant bit down.
        int mask = (1 << bitsPerPixel) - 1;
        for (int column = 0; column < columns; column++)
        {
            int sourceBit = column * bitsPerPixel;
            int value = (source[sourceBit >> 3] >> (8 - bitsPerPixel - (sourceBit & 7))) & mask;
            int targetBit = (start + (column * step)) * bitsPerPixel;
            target[targetBit >> 3] |= (byte)(value << (8 - bitsPerPixel - (targetBit & 7)));
        }
    }
}
