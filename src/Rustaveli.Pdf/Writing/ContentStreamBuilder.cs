namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Writes a content stream: the operators that paint a page or a form (ISO 32000-1, 8–9, and annex A). Each method
/// emits one operator with its operands, one operator per line.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates are PDF user space as the current transformation matrix leaves it; the builder does no conversion.
/// Names of fonts, images and graphics states are resource names — ask <see cref="PdfResources"/> for them.
/// </para>
/// <para>
/// The builder checks the nesting that would otherwise produce a stream viewers misrender without complaint:
/// every <c>q</c> needs its <c>Q</c>, text objects neither nest nor contain <c>q</c>/<c>Q</c>, text is positioned
/// and shown only inside one, and a <c>TJ</c> array is closed before anything else is written.
/// <see cref="EnsureComplete"/> confirms the stream ends balanced. After an exception the stream is unusable.
/// </para>
/// </remarks>
internal sealed class ContentStreamBuilder : IDisposable
{
    private readonly PdfByteWriter _writer;
    private bool _inTextArray;

    public ContentStreamBuilder(int initialCapacity = 1024)
    {
        _writer = new PdfByteWriter(initialCapacity);
    }

    public int Length => _writer.Length;

    /// <summary>The stream's bytes so far.</summary>
    public ReadOnlySpan<byte> Content => _writer.WrittenSpan;

    /// <summary>How many <c>q</c> are waiting for their <c>Q</c>.</summary>
    public int StateDepth { get; private set; }

    public bool InTextObject { get; private set; }

    /// <summary>How many marked sequences are waiting for their <c>EMC</c>.</summary>
    public int MarkedDepth { get; private set; }

    /// <summary>Throws unless every saved state has been restored, every text object ended and every marked sequence closed.</summary>
    public void EnsureComplete()
    {
        if (InTextObject)
            throw new InvalidOperationException("A text object is still open; call EndText.");

        if (MarkedDepth != 0)
            throw new InvalidOperationException($"{MarkedDepth} marked sequence(s) were never ended.");

        if (StateDepth != 0)
            throw new InvalidOperationException($"{StateDepth} saved graphics state(s) were never restored.");
    }

    public void Dispose() => _writer.Dispose();

    // Special graphics state.

    /// <summary><c>q</c>: pushes the graphics state.</summary>
    public void SaveState()
    {
        RequireOutsideText("q");
        StateDepth++;
        Operator("q"u8);
    }

    /// <summary><c>Q</c>: pops the graphics state.</summary>
    public void RestoreState()
    {
        RequireOutsideText("Q");
        if (StateDepth == 0)
            throw new InvalidOperationException("Q without a matching q.");

        StateDepth--;
        Operator("Q"u8);
    }

    /// <summary><c>cm</c>: concatenates a matrix onto the current transformation.</summary>
    public void Transform(double a, double b, double c, double d, double e, double f)
    {
        Numbers(a, b, c, d, e, f);
        Operator("cm"u8);
    }

    // General graphics state.

    /// <summary><c>w</c></summary>
    public void SetLineWidth(double width)
    {
        Real(width);
        Operator("w"u8);
    }

    /// <summary><c>J</c></summary>
    public void SetLineCap(PdfLineCap cap)
    {
        Integer((int)cap);
        Operator("J"u8);
    }

    /// <summary><c>j</c></summary>
    public void SetLineJoin(PdfLineJoin join)
    {
        Integer((int)join);
        Operator("j"u8);
    }

    /// <summary><c>M</c></summary>
    public void SetMiterLimit(double limit)
    {
        Real(limit);
        Operator("M"u8);
    }

    /// <summary>
    /// <c>d</c>: alternating dash and gap lengths starting <paramref name="phase"/> into the pattern. An empty array
    /// draws solid lines.
    /// </summary>
    public void SetDashPattern(ReadOnlySpan<double> dashes, double phase)
    {
        bool anyNonZero = false;
        foreach (double length in dashes)
        {
            if (!(length >= 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dashes), length, "Dash and gap lengths cannot be negative.");
            }

            anyNonZero |= length > 0;
        }

        // A pattern of nothing but zeros has no length to repeat; the specification makes it an error.
        if (dashes.Length > 0 && !anyNonZero)
            throw new ArgumentException("A dash pattern needs at least one non-zero length.", nameof(dashes));

        RequireNoTextArray();
        _writer.WriteByte((byte)'[');
        foreach (double length in dashes)
            _writer.WriteReal(length);

        _writer.WriteByte((byte)']');
        Real(phase);
        Operator("d"u8);
    }

    /// <summary><c>gs</c>: applies a named extended graphics state, such as an opacity.</summary>
    public void SetGraphicsState(PdfName name)
    {
        WriteName(name);
        Operator("gs"u8);
    }

    // Path construction.

    /// <summary><c>m</c></summary>
    public void MoveTo(double x, double y)
    {
        Numbers(x, y);
        Operator("m"u8);
    }

    /// <summary><c>l</c></summary>
    public void LineTo(double x, double y)
    {
        Numbers(x, y);
        Operator("l"u8);
    }

    /// <summary>
    /// <c>c</c>: a cubic Bézier curve through two control points to (<paramref name="x3"/>, <paramref name="y3"/>).
    /// </summary>
    public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        Numbers(x1, y1, x2, y2, x3, y3);
        Operator("c"u8);
    }

    /// <summary><c>h</c></summary>
    public void ClosePath() => Operator("h"u8);

    /// <summary><c>re</c>: a closed rectangle from its lower-left corner.</summary>
    public void Rectangle(double x, double y, double width, double height)
    {
        Numbers(x, y, width, height);
        Operator("re"u8);
    }

    // Path painting and clipping.

    /// <summary><c>f</c>: fills using the non-zero winding rule.</summary>
    public void Fill() => Operator("f"u8);

    /// <summary><c>f*</c>: fills using the even-odd rule.</summary>
    public void FillEvenOdd() => Operator("f*"u8);

    /// <summary><c>S</c></summary>
    public void Stroke() => Operator("S"u8);

    /// <summary><c>B</c>: fills (non-zero winding) and then strokes.</summary>
    public void FillAndStroke() => Operator("B"u8);

    /// <summary><c>n</c>: ends the path without painting it, typically after a clip.</summary>
    public void EndPath() => Operator("n"u8);

    /// <summary>
    /// <c>W</c>: intersects the clip with the path (non-zero winding), effective after the next painting operator.
    /// </summary>
    public void Clip() => Operator("W"u8);

    /// <summary><c>W*</c>: as <see cref="Clip"/>, with the even-odd rule.</summary>
    public void ClipEvenOdd() => Operator("W*"u8);

    // Colour.

    /// <summary><c>g</c></summary>
    public void SetFillGray(double gray)
    {
        Real(gray);
        Operator("g"u8);
    }

    /// <summary><c>G</c></summary>
    public void SetStrokeGray(double gray)
    {
        Real(gray);
        Operator("G"u8);
    }

    /// <summary><c>rg</c>: components from 0 to 1.</summary>
    public void SetFillRgb(double red, double green, double blue)
    {
        Numbers(red, green, blue);
        Operator("rg"u8);
    }

    /// <summary><c>RG</c>: components from 0 to 1.</summary>
    public void SetStrokeRgb(double red, double green, double blue)
    {
        Numbers(red, green, blue);
        Operator("RG"u8);
    }

    /// <summary><c>k</c>: components from 0 to 1.</summary>
    public void SetFillCmyk(double cyan, double magenta, double yellow, double black)
    {
        Numbers(cyan, magenta, yellow, black);
        Operator("k"u8);
    }

    /// <summary><c>K</c>: components from 0 to 1.</summary>
    public void SetStrokeCmyk(double cyan, double magenta, double yellow, double black)
    {
        Numbers(cyan, magenta, yellow, black);
        Operator("K"u8);
    }

    /// <summary>
    /// <c>cs</c>: selects a colour space by resource name, or a device space such as <c>/DeviceRGB</c>.
    /// </summary>
    public void SetFillColorSpace(PdfName colorSpace)
    {
        WriteName(colorSpace);
        Operator("cs"u8);
    }

    /// <summary>
    /// <c>CS</c>: selects a colour space by resource name, or a device space such as <c>/DeviceRGB</c>.
    /// </summary>
    public void SetStrokeColorSpace(PdfName colorSpace)
    {
        WriteName(colorSpace);
        Operator("CS"u8);
    }

    /// <summary><c>sc</c>: a colour in the current fill space, for device, CIE-based and indexed spaces.</summary>
    public void SetFillColor(ReadOnlySpan<double> components)
    {
        Numbers(components);
        Operator("sc"u8);
    }

    /// <summary><c>SC</c>: a colour in the current stroke space, for device, CIE-based and indexed spaces.</summary>
    public void SetStrokeColor(ReadOnlySpan<double> components)
    {
        Numbers(components);
        Operator("SC"u8);
    }

    /// <summary>
    /// <c>scn</c>: a colour in the current fill space, which may also be a separation (spot colour), DeviceN,
    /// ICC-based or pattern space; <paramref name="pattern"/> names the pattern for the last.
    /// </summary>
    public void SetFillColorN(ReadOnlySpan<double> components, PdfName? pattern = null)
    {
        Numbers(components);
        if (pattern != null)
            _writer.WriteName(pattern);

        Operator("scn"u8);
    }

    /// <summary><c>SCN</c>: as <see cref="SetFillColorN"/>, for stroking.</summary>
    public void SetStrokeColorN(ReadOnlySpan<double> components, PdfName? pattern = null)
    {
        Numbers(components);
        if (pattern != null)
            _writer.WriteName(pattern);

        Operator("SCN"u8);
    }

    // XObjects.

    /// <summary>
    /// <c>Do</c>: paints an image or form XObject, by resource name, into the unit square of the current matrix.
    /// </summary>
    public void PaintXObject(PdfName name)
    {
        WriteName(name);
        Operator("Do"u8);
    }

    // Text objects.

    /// <summary><c>BT</c>: starts a text object and resets the text matrices.</summary>
    public void BeginText()
    {
        if (InTextObject)
            throw new InvalidOperationException("Text objects cannot nest; call EndText first.");

        Operator("BT"u8);
        InTextObject = true;
    }

    /// <summary><c>ET</c></summary>
    public void EndText()
    {
        RequireText("ET");
        Operator("ET"u8);
        InTextObject = false;
    }

    // Text state. These are graphics state, so they are allowed outside text objects as well.

    /// <summary><c>Tf</c>: selects a font by resource name, at a size in text space units.</summary>
    public void SetFont(PdfName font, double size)
    {
        WriteName(font);
        Real(size);
        Operator("Tf"u8);
    }

    /// <summary><c>Tc</c>: extra space after every glyph, in unscaled text space units.</summary>
    public void SetCharacterSpacing(double spacing)
    {
        Real(spacing);
        Operator("Tc"u8);
    }

    /// <summary><c>Tw</c>: extra space after every single-byte code 32.</summary>
    public void SetWordSpacing(double spacing)
    {
        Real(spacing);
        Operator("Tw"u8);
    }

    /// <summary><c>Tz</c>: horizontal scaling in percent; 100 is normal.</summary>
    public void SetHorizontalScaling(double percent)
    {
        Real(percent);
        Operator("Tz"u8);
    }

    /// <summary><c>TL</c></summary>
    public void SetLeading(double leading)
    {
        Real(leading);
        Operator("TL"u8);
    }

    /// <summary><c>Ts</c>: moves the baseline up (positive) or down, for superscripts and subscripts.</summary>
    public void SetTextRise(double rise)
    {
        Real(rise);
        Operator("Ts"u8);
    }

    /// <summary><c>Tr</c></summary>
    public void SetTextRenderingMode(PdfTextRenderingMode mode)
    {
        Integer((int)mode);
        Operator("Tr"u8);
    }

    // Text positioning and showing, only inside a text object.

    /// <summary><c>Td</c>: starts a new line offset from the start of the current one.</summary>
    public void MoveTextPosition(double x, double y)
    {
        RequireText("Td");
        Numbers(x, y);
        Operator("Td"u8);
    }

    /// <summary><c>Tm</c>: sets the text matrix and the text line matrix outright.</summary>
    public void SetTextMatrix(double a, double b, double c, double d, double e, double f)
    {
        RequireText("Tm");
        Numbers(a, b, c, d, e, f);
        Operator("Tm"u8);
    }

    /// <summary>
    /// <c>Tj</c>: shows a string of character codes, already encoded for the current font — glyph IDs as two bytes
    /// each for an Identity-H font, single bytes for a simple font.
    /// </summary>
    public void ShowText(ReadOnlySpan<byte> codes)
    {
        RequireText("Tj");
        WriteCodes(codes);
        Operator("Tj"u8);
    }

    /// <summary>
    /// Opens a <c>TJ</c> array: follow with <see cref="AppendText"/> and <see cref="AppendAdjustment"/> in any
    /// order, then <see cref="EndTextArray"/>.
    /// </summary>
    public void BeginTextArray()
    {
        RequireText("TJ");
        RequireNoTextArray();
        _writer.WriteByte((byte)'[');
        _inTextArray = true;
    }

    /// <summary>Adds a run of encoded character codes to the open <c>TJ</c> array.</summary>
    public void AppendText(ReadOnlySpan<byte> codes)
    {
        RequireTextArray();
        WriteCodes(codes);
    }

    /// <summary>
    /// Adds a positioning adjustment to the open <c>TJ</c> array, in thousandths of the font size. Positive values
    /// move the next glyph left — kerning pairs together — and negative values move it right.
    /// </summary>
    public void AppendAdjustment(double thousandths)
    {
        RequireTextArray();
        _writer.WriteReal(thousandths);
    }

    /// <summary>Closes the <c>TJ</c> array and shows it.</summary>
    public void EndTextArray()
    {
        RequireTextArray();
        _writer.WriteByte((byte)']');
        _inTextArray = false;
        Operator("TJ"u8);
    }

    // Marked content.

    /// <summary><c>BMC</c>: begins a sequence of content marked with <paramref name="tag"/> alone.</summary>
    public void BeginMarkedContent(PdfName tag)
    {
        WriteName(tag);
        Operator("BMC"u8);
        MarkedDepth++;
    }

    /// <summary>
    /// <c>BDC</c>: begins a sequence of content marked with <paramref name="tag"/> and numbered
    /// <paramref name="identifier"/>, by which the structure tree finds it.
    /// </summary>
    public void BeginMarkedContent(PdfName tag, int identifier)
    {
        WriteName(tag);
        _writer.Write("<</MCID "u8);
        _writer.WriteInteger(identifier);
        _writer.Write(">>"u8);
        Operator("BDC"u8);
        MarkedDepth++;
    }

    /// <summary><c>EMC</c>: ends the marked sequence most recently begun.</summary>
    public void EndMarkedContent()
    {
        if (MarkedDepth == 0)
            throw new InvalidOperationException("EMC without a matching BMC or BDC.");

        MarkedDepth--;
        Operator("EMC"u8);
    }

    private void WriteCodes(ReadOnlySpan<byte> codes)
    {
        if (PdfByteWriter.CompactForm(codes) == PdfStringForm.Literal)
            _writer.WriteLiteralString(codes);
        else
            _writer.WriteHexString(codes);
    }

    private void WriteName(PdfName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        RequireNoTextArray();
        _writer.WriteName(name);
    }

    private void Real(double value)
    {
        RequireNoTextArray();
        _writer.WriteReal(value);
    }

    private void Integer(long value)
    {
        RequireNoTextArray();
        _writer.WriteInteger(value);
    }

    private void Numbers(double first, double second)
    {
        Real(first);
        Real(second);
    }

    private void Numbers(double first, double second, double third)
    {
        Numbers(first, second);
        Real(third);
    }

    private void Numbers(double first, double second, double third, double fourth)
    {
        Numbers(first, second);
        Numbers(third, fourth);
    }

    private void Numbers(double first, double second, double third, double fourth, double fifth, double sixth)
    {
        Numbers(first, second);
        Numbers(third, fourth);
        Numbers(fifth, sixth);
    }

    private void Numbers(ReadOnlySpan<double> values)
    {
        RequireNoTextArray();
        foreach (double value in values)
            _writer.WriteReal(value);
    }

    private void Operator(ReadOnlySpan<byte> name)
    {
        RequireNoTextArray();
        _writer.WriteKeyword(name);
        _writer.WriteByte((byte)'\n');
    }

    private void RequireText(string operatorName)
    {
        if (!InTextObject)
        {
            throw new InvalidOperationException(
                $"{operatorName} is only valid inside a text object; call BeginText first.");
        }
    }

    private void RequireOutsideText(string operatorName)
    {
        if (InTextObject)
        {
            throw new InvalidOperationException(
                $"{operatorName} is not allowed inside a text object; call EndText first.");
        }
    }

    private void RequireTextArray()
    {
        if (!_inTextArray)
            throw new InvalidOperationException("No TJ array is open; call BeginTextArray first.");
    }

    private void RequireNoTextArray()
    {
        if (_inTextArray)
            throw new InvalidOperationException("A TJ array is open; call EndTextArray first.");
    }
}
