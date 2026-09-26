using System.Runtime.InteropServices;
using SkiaSharp;

namespace Rustaveli.Pdf.ConformanceTests.Rendering;

/// <summary>
/// Rasterises every page of a PDF with PDFium, an independent renderer: what a snapshot shows is what a viewer that
/// shares no code with this library sees.
/// </summary>
internal static class PageRenderer
{
    // PDFium is not thread-safe, and xUnit runs test classes in parallel.
    private static readonly object Gate = new object();
    private static bool _initialised;

    public static List<SKBitmap> Render(byte[] pdf, float dotsPerInch)
    {
        lock (Gate)
        {
            if (!_initialised)
            {
                PdfiumNative.InitLibrary();
                _initialised = true;
            }

            // PDFium reads the buffer lazily for as long as the document is open, so it must not move.
            GCHandle pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
            try
            {
                IntPtr document = PdfiumNative.LoadMemDocument(pinned.AddrOfPinnedObject(), pdf.Length, null);
                if (document == IntPtr.Zero)
                    throw new InvalidOperationException($"PDFium could not open the document (error {PdfiumNative.GetLastError().Value}).");

                try
                {
                    int count = PdfiumNative.GetPageCount(document);
                    List<SKBitmap> pages = new List<SKBitmap>(count);
                    for (int index = 0; index < count; index++)
                        pages.Add(RenderPage(document, index, dotsPerInch));
                    return pages;
                }
                finally
                {
                    PdfiumNative.CloseDocument(document);
                }
            }
            finally
            {
                pinned.Free();
            }
        }
    }

    private static SKBitmap RenderPage(IntPtr document, int index, float dotsPerInch)
    {
        IntPtr page = PdfiumNative.LoadPage(document, index);
        if (page == IntPtr.Zero)
            throw new InvalidOperationException($"PDFium could not load page {index + 1}.");

        try
        {
            int width = (int)Math.Round(PdfiumNative.GetPageWidth(page) * dotsPerInch / 72f);
            int height = (int)Math.Round(PdfiumNative.GetPageHeight(page) * dotsPerInch / 72f);

            IntPtr bitmap = PdfiumNative.CreateBitmap(width, height, 0);
            try
            {
                PdfiumNative.FillRect(bitmap, 0, 0, width, height, new CULong(0xFFFFFFFF));
                PdfiumNative.RenderPageBitmap(bitmap, page, 0, 0, width, height, 0, PdfiumNative.RenderAnnotations);

                // BGRx from PDFium maps onto Skia's BGRA with the alpha byte ignored.
                SKBitmap result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
                int stride = PdfiumNative.GetStride(bitmap);
                IntPtr source = PdfiumNative.GetBuffer(bitmap);
                IntPtr target = result.GetPixels();

                for (int row = 0; row < height; row++)
                {
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (byte*)source + (row * stride),
                            (byte*)target + (row * result.RowBytes),
                            result.RowBytes,
                            width * 4);
                    }
                }

                return result;
            }
            finally
            {
                PdfiumNative.DestroyBitmap(bitmap);
            }
        }
        finally
        {
            PdfiumNative.ClosePage(page);
        }
    }
}
