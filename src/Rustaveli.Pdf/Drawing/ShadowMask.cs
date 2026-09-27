namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// How much of a blurred shadow covers each point around its shape, sampled on a grid: a soft mask for renderers
/// that cannot blur, such as PDF.
/// </summary>
/// <remarks>
/// The shape is sampled with a margin of three deviations on every side, beyond which a Gaussian leaves nothing, and
/// blurred in two passes, across and down. A blur is smooth, so the grid can be coarse where the blur is wide: it
/// is sampled at about four pixels per deviation, and a viewer interpolates between them.
/// </remarks>
internal sealed class ShadowMask
{
    /// <summary>The most pixels a mask is given, however large its shape; beyond it the grid coarsens.</summary>
    internal const int MaximumPixels = 1 << 21;

    private ShadowMask(int width, int height, float pixelsPerPoint, float margin, byte[] coverage)
    {
        Width = width;
        Height = height;
        PixelsPerPoint = pixelsPerPoint;
        Margin = margin;
        Coverage = coverage;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>How many of the grid's pixels span one point.</summary>
    public float PixelsPerPoint { get; }

    /// <summary>How far the grid reaches beyond the shape on every side, in points.</summary>
    public float Margin { get; }

    /// <summary>Coverage from 0 to 255, a row at a time from the top.</summary>
    public byte[] Coverage { get; }

    /// <summary>The grid's size in points.</summary>
    public Extent Size => new Extent(Width / PixelsPerPoint, Height / PixelsPerPoint);

    /// <summary>The blurred coverage of a rectangle of <paramref name="size"/> with <paramref name="corners"/>.</summary>
    public static ShadowMask Create(Extent size, Corners corners, float deviation)
    {
        float margin = deviation * 3;
        float spanWidth = size.Width + (2 * margin);
        float spanHeight = size.Height + (2 * margin);
        float pixelsPerPoint = Math.Min(2f, Math.Max(0.25f, 4f / deviation));

        if (spanWidth * spanHeight * pixelsPerPoint * pixelsPerPoint > MaximumPixels)
            pixelsPerPoint = (float)Math.Sqrt(MaximumPixels / (spanWidth * spanHeight));

        int width = Math.Max(1, (int)Math.Ceiling(spanWidth * pixelsPerPoint));
        int height = Math.Max(1, (int)Math.Ceiling(spanHeight * pixelsPerPoint));
        float[] field = new float[width * height];

        for (int row = 0; row < height; row++)
        {
            float y = ((row + 0.5f) / pixelsPerPoint) - margin;

            for (int column = 0; column < width; column++)
            {
                float x = ((column + 0.5f) / pixelsPerPoint) - margin;
                field[(row * width) + column] = Inside(x, y, size, corners) ? 1f : 0f;
            }
        }

        float[] kernel = Kernel(deviation * pixelsPerPoint);
        float[] across = new float[field.Length];
        Blur(field, across, width, height, kernel, horizontal: true);
        Blur(across, field, width, height, kernel, horizontal: false);

        byte[] coverage = new byte[field.Length];
        for (int index = 0; index < coverage.Length; index++)
            coverage[index] = (byte)Math.Round(Math.Min(1f, Math.Max(0f, field[index])) * 255);

        return new ShadowMask(width, height, pixelsPerPoint, margin, coverage);
    }

    /// <summary>
    /// Whether a point lies inside the rounded rectangle: inside the rectangle, and not cut off by any corner's arc.
    /// Fitted radii never overlap, so each corner is checked on its own.
    /// </summary>
    internal static bool Inside(float x, float y, Extent size, Corners corners)
    {
        if (x < 0 || y < 0 || x > size.Width || y > size.Height)
            return false;

        float right = size.Width;
        float bottom = size.Height;

        return !CutOff(corners.TopLeft - x, corners.TopLeft - y, corners.TopLeft)
            && !CutOff(x - (right - corners.TopRight), corners.TopRight - y, corners.TopRight)
            && !CutOff(x - (right - corners.BottomRight), y - (bottom - corners.BottomRight), corners.BottomRight)
            && !CutOff(corners.BottomLeft - x, y - (bottom - corners.BottomLeft), corners.BottomLeft);

        // Within a corner's square, beyond its arc.
        static bool CutOff(float dx, float dy, float radius) =>
            dx > 0 && dy > 0 && (dx * dx) + (dy * dy) > radius * radius;
    }

    /// <summary>A normalised Gaussian reaching three deviations either side of its centre.</summary>
    private static float[] Kernel(float deviation)
    {
        int reach = Math.Max(1, (int)Math.Ceiling(deviation * 3));
        float[] kernel = new float[(2 * reach) + 1];
        float sum = 0;

        for (int index = 0; index < kernel.Length; index++)
        {
            float distance = index - reach;
            kernel[index] = (float)Math.Exp(-(distance * distance) / (2 * deviation * deviation));
            sum += kernel[index];
        }

        for (int index = 0; index < kernel.Length; index++)
            kernel[index] /= sum;

        return kernel;
    }

    /// <summary>One pass of the blur, along rows or down columns; beyond the grid there is nothing.</summary>
    private static void Blur(float[] source, float[] target, int width, int height, float[] kernel, bool horizontal)
    {
        int reach = kernel.Length / 2;
        int length = horizontal ? width : height;
        int lines = horizontal ? height : width;
        int step = horizontal ? 1 : width;

        for (int line = 0; line < lines; line++)
        {
            int start = horizontal ? line * width : line;

            for (int position = 0; position < length; position++)
            {
                float sum = 0;
                int from = Math.Max(0, position - reach);
                int to = Math.Min(length - 1, position + reach);

                for (int sample = from; sample <= to; sample++)
                    sum += source[start + (sample * step)] * kernel[sample - position + reach];

                target[start + (position * step)] = sum;
            }
        }
    }
}
