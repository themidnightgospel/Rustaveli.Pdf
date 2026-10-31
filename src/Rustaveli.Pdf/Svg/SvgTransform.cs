using Matrix = (float A, float B, float C, float D, float E, float F);

namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads an SVG transform list — <c>matrix</c>, <c>translate</c>, <c>scale</c>, <c>rotate</c>, <c>skewX</c>,
/// <c>skewY</c>, in any number — into the one matrix they make, as (a, b, c, d, e, f).
/// </summary>
internal static class SvgTransform
{
    public static readonly Matrix Identity = (1, 0, 0, 1, 0, 0);

    /// <summary>
    /// Whether every entry of <paramref name="matrix"/> can be written to a PDF: steps that each can may multiply into
    /// a matrix that cannot.
    /// </summary>
    public static bool IsWritable(Matrix matrix) =>
        Writable.Is(matrix.A) && Writable.Is(matrix.B) && Writable.Is(matrix.C) && Writable.Is(matrix.D) && Writable.Is(matrix.E) && Writable.Is(matrix.F);

    /// <summary>The matrix of <paramref name="list"/>, or null when it holds nothing that can be read.</summary>
    public static Matrix? Read(string? list)
    {
        if (string.IsNullOrWhiteSpace(list))
            return null;

        Matrix matrix = Identity;
        bool any = false;
        int position = 0;

        while (position < list!.Length)
        {
            int open = list.IndexOf('(', position);
            if (open < 0)
                break;

            int close = list.IndexOf(')', open);
            if (close < 0)
                break;

            string name = list.Substring(position, open - position).Trim(' ', ',', '\t', '\n', '\r');
            List<float> values = new SvgNumbers(list.Substring(open + 1, close - open - 1)).Rest();
            position = close + 1;

            if (Step(name, values) is { } step)
            {
                // Each step applies within the space of those before it.
                matrix = Multiply(matrix, step);
                any = true;
            }
        }

        return any ? matrix : null;
    }

    /// <summary>The matrix applying <paramref name="inner"/> first, then <paramref name="outer"/>.</summary>
    public static Matrix Multiply(Matrix outer, Matrix inner) =>
        ((outer.A * inner.A) + (outer.C * inner.B),
         (outer.B * inner.A) + (outer.D * inner.B),
         (outer.A * inner.C) + (outer.C * inner.D),
         (outer.B * inner.C) + (outer.D * inner.D),
         (outer.A * inner.E) + (outer.C * inner.F) + outer.E,
         (outer.B * inner.E) + (outer.D * inner.F) + outer.F);

    /// <summary>The matrix moving by <paramref name="x"/> across and <paramref name="y"/> down.</summary>
    public static Matrix Translation(float x, float y) => (1, 0, 0, 1, x, y);

    private static Matrix? Step(string name, List<float> values)
    {
        switch (name)
        {
            case "matrix" when values.Count >= 6:
                return (values[0], values[1], values[2], values[3], values[4], values[5]);

            case "translate" when values.Count >= 1:
                return Translation(values[0], values.Count > 1 ? values[1] : 0);

            case "scale" when values.Count >= 1:
                return (values[0], 0, 0, values.Count > 1 ? values[1] : values[0], 0, 0);

            case "rotate" when values.Count >= 1:
            {
                double radians = Radians(values[0]);
                float cos = (float)Math.Cos(radians);
                float sin = (float)Math.Sin(radians);
                Matrix turn = (cos, sin, -sin, cos, 0, 0);

                if (values.Count < 3)
                    return turn;

                // About a point: there, turn, and back.
                float x = values[1], y = values[2];
                return Multiply(Multiply(Translation(x, y), turn), Translation(-x, -y));
            }

            case "skewX" when values.Count >= 1:
                return (1, 0, (float)Math.Tan(Radians(values[0])), 1, 0, 0);

            case "skewY" when values.Count >= 1:
                return (1, (float)Math.Tan(Radians(values[0])), 0, 1, 0, 0);

            default:
                return null;
        }
    }

    private static double Radians(float degrees) => degrees * Math.PI / 180;
}
