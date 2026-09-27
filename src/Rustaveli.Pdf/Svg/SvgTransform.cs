namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads an SVG transform list — <c>matrix</c>, <c>translate</c>, <c>scale</c>, <c>rotate</c>, <c>skewX</c>,
/// <c>skewY</c>, in any number — into the one matrix they make, as (a, b, c, d, e, f).
/// </summary>
internal static class SvgTransform
{
    public static readonly (float A, float B, float C, float D, float E, float F) Identity = (1, 0, 0, 1, 0, 0);

    /// <summary>The matrix of <paramref name="list"/>, or null when it holds nothing that can be read.</summary>
    public static (float A, float B, float C, float D, float E, float F)? Read(string? list)
    {
        if (string.IsNullOrWhiteSpace(list))
            return null;

        (float, float, float, float, float, float) matrix = Identity;
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
    public static (float A, float B, float C, float D, float E, float F) Multiply(
        (float A, float B, float C, float D, float E, float F) outer,
        (float A, float B, float C, float D, float E, float F) inner) =>
        ((outer.A * inner.A) + (outer.C * inner.B),
         (outer.B * inner.A) + (outer.D * inner.B),
         (outer.A * inner.C) + (outer.C * inner.D),
         (outer.B * inner.C) + (outer.D * inner.D),
         (outer.A * inner.E) + (outer.C * inner.F) + outer.E,
         (outer.B * inner.E) + (outer.D * inner.F) + outer.F);

    private static (float, float, float, float, float, float)? Step(string name, List<float> values)
    {
        switch (name)
        {
            case "matrix" when values.Count >= 6:
                return (values[0], values[1], values[2], values[3], values[4], values[5]);

            case "translate" when values.Count >= 1:
                return (1, 0, 0, 1, values[0], values.Count > 1 ? values[1] : 0);

            case "scale" when values.Count >= 1:
                return (values[0], 0, 0, values.Count > 1 ? values[1] : values[0], 0, 0);

            case "rotate" when values.Count >= 1:
            {
                double radians = values[0] * Math.PI / 180;
                float cos = (float)Math.Cos(radians);
                float sin = (float)Math.Sin(radians);
                (float, float, float, float, float, float) turn = (cos, sin, -sin, cos, 0, 0);

                if (values.Count < 3)
                    return turn;

                // About a point: there, turn, and back.
                float x = values[1], y = values[2];
                return Multiply(Multiply((1, 0, 0, 1, x, y), turn), (1, 0, 0, 1, -x, -y));
            }

            case "skewX" when values.Count >= 1:
                return (1, 0, (float)Math.Tan(values[0] * Math.PI / 180), 1, 0, 0);

            case "skewY" when values.Count >= 1:
                return (1, (float)Math.Tan(values[0] * Math.PI / 180), 0, 1, 0, 0);

            default:
                return null;
        }
    }
}
