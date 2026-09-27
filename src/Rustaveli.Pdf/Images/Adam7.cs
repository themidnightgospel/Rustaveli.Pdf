namespace Rustaveli.Pdf.Images;

/// <summary>
/// The seven passes of Adam7 interlacing: each pass samples the pixels at a fixed offset and spacing within every
/// 8 × 8 block, and is stored as a small image of its own, filtered independently.
/// </summary>
internal static class Adam7
{
    public const int PassCount = 7;

    private static readonly int[] StartX = [0, 4, 0, 2, 0, 1, 0];
    private static readonly int[] StartY = [0, 0, 4, 0, 2, 0, 1];
    private static readonly int[] StepX = [8, 8, 4, 4, 2, 2, 1];
    private static readonly int[] StepY = [8, 8, 8, 4, 4, 2, 2];

    public static int ColumnStart(int pass) => StartX[pass];

    public static int RowStart(int pass) => StartY[pass];

    public static int ColumnStep(int pass) => StepX[pass];

    public static int RowStep(int pass) => StepY[pass];

    /// <summary>Pixels per row in <paramref name="pass"/>; zero when the image is too narrow to reach it.</summary>
    public static int Columns(int pass, int width) => Count(width, StartX[pass], StepX[pass]);

    /// <summary>Rows in <paramref name="pass"/>; zero when the image is too short to reach it.</summary>
    public static int Rows(int pass, int height) => Count(height, StartY[pass], StepY[pass]);

    private static int Count(int size, int start, int step) => size <= start ? 0 : ((size - start - 1) / step) + 1;
}
