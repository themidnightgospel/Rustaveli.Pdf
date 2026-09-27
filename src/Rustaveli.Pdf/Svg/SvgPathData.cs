namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads SVG path data, the <c>d</c> of a path — every command, absolute and relative, with smooth curves and arcs —
/// into a <see cref="VectorPath"/>.
/// </summary>
/// <remarks>
/// As SVG asks, reading stops at the first error and keeps what came before it.
/// </remarks>
internal static class SvgPathData
{
    public static VectorPath Read(string data)
    {
        VectorPath path = new VectorPath();
        SvgNumbers numbers = new SvgNumbers(data);
        float x = 0, y = 0;
        float startX = 0, startY = 0;
        float controlX = 0, controlY = 0;
        char previous = ' ';
        char command = ' ';

        while (true)
        {
            numbers.SkipSeparators();

            if (numbers.AtEnd)
                break;

            char next = numbers.Peek();

            if (char.IsLetter(next) && next is not ('e' or 'E'))
            {
                command = next;
                numbers.Advance();
            }
            else if (command == ' ')
            {
                break;
            }

            bool relative = char.IsLower(command);
            float baseX = relative ? x : 0;
            float baseY = relative ? y : 0;

            try
            {
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.MoveTo(x, y);
                        startX = x;
                        startY = y;

                        // Pairs after the first of a move are lines.
                        command = relative ? 'l' : 'L';
                        previous = 'M';
                        continue;

                    case 'L':
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.LineTo(x, y);
                        break;

                    case 'H':
                        x = baseX + numbers.Next();
                        path.LineTo(x, y);
                        break;

                    case 'V':
                        y = baseY + numbers.Next();
                        path.LineTo(x, y);
                        break;

                    case 'C':
                    {
                        float x1 = baseX + numbers.Next(), y1 = baseY + numbers.Next();
                        float x2 = baseX + numbers.Next(), y2 = baseY + numbers.Next();
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.CurveTo(x1, y1, x2, y2, x, y);
                        (controlX, controlY) = (x2, y2);
                        previous = 'C';
                        continue;
                    }

                    case 'S':
                    {
                        // The first control point mirrors the last one of a curve just before, or is the pen.
                        (float x1, float y1) = previous == 'C' ? ((2 * x) - controlX, (2 * y) - controlY) : (x, y);
                        float x2 = baseX + numbers.Next(), y2 = baseY + numbers.Next();
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.CurveTo(x1, y1, x2, y2, x, y);
                        (controlX, controlY) = (x2, y2);
                        previous = 'C';
                        continue;
                    }

                    case 'Q':
                    {
                        float x1 = baseX + numbers.Next(), y1 = baseY + numbers.Next();
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.QuadraticTo(x1, y1, x, y);
                        (controlX, controlY) = (x1, y1);
                        previous = 'Q';
                        continue;
                    }

                    case 'T':
                    {
                        (float x1, float y1) = previous == 'Q' ? ((2 * x) - controlX, (2 * y) - controlY) : (x, y);
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.QuadraticTo(x1, y1, x, y);
                        (controlX, controlY) = (x1, y1);
                        previous = 'Q';
                        continue;
                    }

                    case 'A':
                    {
                        float radiusX = numbers.Next(), radiusY = numbers.Next(), rotation = numbers.Next();
                        bool largeArc = numbers.NextFlag();
                        bool clockwise = numbers.NextFlag();
                        x = baseX + numbers.Next();
                        y = baseY + numbers.Next();
                        path.ArcTo(radiusX, radiusY, rotation, largeArc, clockwise, x, y);
                        break;
                    }

                    case 'Z':
                        path.Close();
                        (x, y) = (startX, startY);
                        break;

                    default:
                        return path;
                }
            }
            catch (FormatException)
            {
                return path;
            }

            previous = char.ToUpperInvariant(command);
        }

        return path;
    }
}
