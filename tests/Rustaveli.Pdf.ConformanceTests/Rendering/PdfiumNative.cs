using System.Runtime.InteropServices;

namespace Rustaveli.Pdf.ConformanceTests.Rendering;

/// <summary>
/// The handful of PDFium entry points needed to rasterise a page. Declared by hand rather than through a wrapper
/// package so that the renderer's only dependency is PDFium itself.
/// </summary>
internal static partial class PdfiumNative
{
    private const string Library = "pdfium";

    /// <summary>Render annotations too, so link borders and form appearances show up in snapshots.</summary>
    public const int RenderAnnotations = 0x01;

    [LibraryImport(Library, EntryPoint = "FPDF_InitLibrary")]
    public static partial void InitLibrary();

    [LibraryImport(Library, EntryPoint = "FPDF_LoadMemDocument", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr LoadMemDocument(IntPtr data, int size, string? password);

    [LibraryImport(Library, EntryPoint = "FPDF_CloseDocument")]
    public static partial void CloseDocument(IntPtr document);

    [LibraryImport(Library, EntryPoint = "FPDF_GetLastError")]
    public static partial CULong GetLastError();

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageCount")]
    public static partial int GetPageCount(IntPtr document);

    [LibraryImport(Library, EntryPoint = "FPDF_LoadPage")]
    public static partial IntPtr LoadPage(IntPtr document, int index);

    [LibraryImport(Library, EntryPoint = "FPDF_ClosePage")]
    public static partial void ClosePage(IntPtr page);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageWidthF")]
    public static partial float GetPageWidth(IntPtr page);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageHeightF")]
    public static partial float GetPageHeight(IntPtr page);

    /// <summary>Creates a 32-bit BGRx (alpha = 0) or BGRA (alpha = 1) bitmap.</summary>
    [LibraryImport(Library, EntryPoint = "FPDFBitmap_Create")]
    public static partial IntPtr CreateBitmap(int width, int height, int alpha);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_FillRect")]
    public static partial int FillRect(IntPtr bitmap, int left, int top, int width, int height, CULong color);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_GetBuffer")]
    public static partial IntPtr GetBuffer(IntPtr bitmap);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_GetStride")]
    public static partial int GetStride(IntPtr bitmap);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_Destroy")]
    public static partial void DestroyBitmap(IntPtr bitmap);

    [LibraryImport(Library, EntryPoint = "FPDF_RenderPageBitmap")]
    public static partial void RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);
}
