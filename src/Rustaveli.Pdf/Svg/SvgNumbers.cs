using System.Globalization;

namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads the numbers of SVG attributes one at a time, however they are separated: by spaces, commas, or nothing at
/// all where a sign or a second decimal point starts the next.
/// </summary>
internal sealed class SvgNumbers(string text)
{
    private int _position;

    public bool AtEnd => _position >= text.Length;

    public char Peek() => text[_position];

    public void Advance() => _position++;

    public void SkipSeparators()
    {
        while (_position < text.Length && (char.IsWhiteSpace(text[_position]) || text[_position] == ','))
            _position++;
    }

    /// <summary>The next number.</summary>
    /// <exception cref="FormatException">No number comes next.</exception>
    public float Next()
    {
        SkipSeparators();
        int start = _position;

        if (_position < text.Length && text[_position] is '+' or '-')
            _position++;

        bool digits = false;
        bool point = false;

        while (_position < text.Length)
        {
            char character = text[_position];

            if (char.IsDigit(character))
            {
                digits = true;
            }
            else if (character == '.' && !point)
            {
                point = true;
            }
            else
            {
                break;
            }

            _position++;
        }

        if (digits && _position < text.Length && text[_position] is 'e' or 'E')
        {
            int exponent = _position;
            _position++;

            if (_position < text.Length && text[_position] is '+' or '-')
                _position++;

            // Not an exponent after all, such as the "e" of a unit: leave it for whoever reads on.
            if (_position >= text.Length || !char.IsDigit(text[_position]))
            {
                _position = exponent;
            }
            else
            {
                while (_position < text.Length && char.IsDigit(text[_position]))
                    _position++;
            }
        }

        if (!digits)
        {
            _position = start;
            throw new FormatException($"A number was expected at {start} in \"{text}\".");
        }

        // Too large for a PDF, or even a float: .NET Framework fails to parse the second, later runtimes read it as infinity.
        if (!float.TryParse(text.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !Writable.Is(number))
        {
            _position = start;
            throw new FormatException($"The number at {start} in \"{text}\" is too large to draw with.");
        }

        return number;
    }

    /// <summary>An arc's flag: a single 0 or 1, which may run straight into what follows it.</summary>
    /// <exception cref="FormatException">No flag comes next.</exception>
    public bool NextFlag()
    {
        SkipSeparators();

        if (_position < text.Length && text[_position] is '0' or '1')
            return text[_position++] == '1';

        throw new FormatException($"An arc flag was expected at {_position} in \"{text}\".");
    }

    /// <summary>Every number left, stopping at the first that is not one.</summary>
    public List<float> Rest()
    {
        List<float> numbers = [];

        while (true)
        {
            SkipSeparators();

            if (AtEnd)
                return numbers;

            try
            {
                numbers.Add(Next());
            }
            catch (FormatException)
            {
                return numbers;
            }
        }
    }
}
