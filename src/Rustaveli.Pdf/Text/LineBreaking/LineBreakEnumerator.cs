using static Rustaveli.Pdf.Text.LineBreaking.LineBreakClass;

namespace Rustaveli.Pdf.Text.LineBreaking;

/// <summary>
/// Walks the line break opportunities of a text in order, ending with the end of the text.
/// </summary>
/// <remarks>
/// <para>
/// The rules of UAX #14 are applied one by one, in the standard's order and with its numbering, to each position
/// between two characters: the first rule that matches decides, so each can be checked against the standard as
/// written. After LB9 the rules no longer see combining marks — a mark joins the character before it and the pair
/// is judged as that character — so the enumerator keeps the last two such items rather than the last two
/// characters. The few rules that reach further back carry what they need along the text; the few that look ahead
/// read ahead.
/// </para>
/// <para>
/// Most positions never reach the rules. Which rule decides depends, for most pairs of classes, on nothing but the
/// two classes, so the decision for every such pair is worked out once, by the rules themselves, and looked up
/// after that — see <see cref="Consult{T}"/>. It allocates nothing: text is measured far more often than it is
/// drawn.
/// </para>
/// </remarks>
internal ref struct LineBreakEnumerator
{
    private const int DottedCircle = 0x25CC;
    private const int Hyphen = 0x2010;

    private const int ClassCount = (int)VI + 1;

    /// <summary>
    /// The first of the table rows for a space. A run of spaces is judged by what came before it as well as by the
    /// space itself, so it has a row for each class that can precede it, and each of those twice: LB15a treats a run
    /// after an initial quotation mark differently depending on what came before the mark.
    /// </summary>
    private const int SpaceRows = ClassCount;

    private const int RowCount = SpaceRows + (2 * ClassCount);

    /// <summary>
    /// The decision for each item class or space run before a position and class after it, or
    /// <see cref="Decision.Contextual"/> where the rules need more than that to decide.
    /// </summary>
    private static readonly Decision[] Decisions = Tabulate();

    private readonly ReadOnlySpan<char> _text;

    /// <summary>Where the next character to be judged starts.</summary>
    private int _next;

    private bool _ended;

    // The item before the position being judged, and the one before that.
    private LineBreakProperties _before;
    private int _beforeCodepoint;
    private LineBreakProperties _twoBefore;
    private int _twoBeforeCodepoint;

    /// <summary>Where the decisions for the item before the position start in <see cref="Decisions"/>.</summary>
    private int _row;

    // LB8a looks at the previous character itself, which may be a joiner that LB9 has attached to its base.
    private bool _afterJoiner;

    // What came before the current run of spaces, for the rules shaped "X SP* ×". Meaningful only while the item
    // before the position is a space.
    private LineBreakClass _beforeSpaces;
    private bool _spacesFollowOpeningQuote;

    // LB25: the items so far end in NU (SY | IS)*, or in NU (SY | IS)* followed by CL or CP.
    private bool _inNumber;
    private bool _afterNumberAndClose;

    // LB30a: the items so far end in an odd number of regional indicators.
    private bool _oddRegionalIndicators;

    // Set only while the table is being worked out: see Consult.
    private bool _probing;
    private bool _consulted;

    internal LineBreakEnumerator(ReadOnlySpan<char> text)
    {
        _text = text;
        _ended = text.IsEmpty;

        // LB2: never break at the start of the text. The first character is not judged, only remembered.
        if (!_ended)
        {
            int codepoint = CodepointAt(0, out _next);
            Advance(LineBreakProperties.Of(codepoint), codepoint);
        }
    }

    private enum Decision : byte
    {
        Prohibited,
        Allowed,
        Mandatory,

        /// <summary>Not a decision: the table's mark for a pair the rules cannot judge by class alone.</summary>
        Contextual
    }

    public LineBreak Current { get; private set; }

    public readonly LineBreakEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        while (_next < _text.Length)
        {
            int position = _next;
            int codepoint = CodepointAt(position, out int length);
            LineBreakProperties properties = LineBreakProperties.Of(codepoint);
            _next += length;

            // The table was worked out with no joiner before the position, so it can answer only when there is none.
            Decision decision = _afterJoiner ? Decision.Contextual : Decisions[_row + (int)properties.Class];

            if (decision == Decision.Contextual)
                decision = Decide(properties, codepoint);

            Advance(properties, codepoint);

            if (decision != Decision.Prohibited)
            {
                Current = new LineBreak(position, decision == Decision.Mandatory);
                return true;
            }
        }

        if (_ended)
            return false;

        // LB3: always break at the end of the text.
        _ended = true;
        Current = new LineBreak(_text.Length, IsMandatory: true);
        return true;
    }

    /// <summary>
    /// Whether a line may end before the character <paramref name="codepoint"/>, which ends where
    /// <see cref="_next"/> now points.
    /// </summary>
    /// <remarks>
    /// Anything read beyond the two classes and the run of spaces the table rows already tell apart goes through
    /// <see cref="Consult{T}"/>, and only once the classes have shown that the rule could apply.
    /// </remarks>
    private Decision Decide(LineBreakProperties properties, int codepoint)
    {
        LineBreakClass before = _before.Class;
        LineBreakClass after = properties.Class;

        // LB4, LB5: always break after hard line breaks, but keep CR LF together.
        if (before is BK or LF or NL)
            return Decision.Mandatory;

        if (before == CR)
            return after == LF ? Decision.Prohibited : Decision.Mandatory;

        // LB6: do not break before hard line breaks. LB7: nor before spaces or zero width space.
        if (after is BK or CR or LF or NL or SP or ZW)
            return Decision.Prohibited;

        // LB8: break before anything that follows a zero width space, even with spaces between.
        if (before == ZW || (before == SP && _beforeSpaces == ZW))
            return Decision.Allowed;

        // LB8a: do not break after a zero width joiner.
        if (_afterJoiner)
            return Decision.Prohibited;

        // LB9: do not break a combining character sequence. A mark attaches to anything but the classes LB4 to LB8
        // have dealt with, which leaves only a space to refuse it. LB10 then makes the mark a letter; Advance records
        // it as one, but here it would change nothing, because every rule left before LB18 breaks after spaces is
        // as indifferent to a letter as to a mark.
        if (after is CM or ZWJ && before != SP)
            return Decision.Prohibited;

        // LB11: do not break before or after a word joiner.
        if (before == WJ || after == WJ)
            return Decision.Prohibited;

        // LB12: do not break after a no-break space or its relatives.
        if (before == GL)
            return Decision.Prohibited;

        // LB12a: nor before one, except after spaces and hyphens.
        if (after == GL && before is not (SP or BA or HY))
            return Decision.Prohibited;

        // LB13: do not break before closing punctuation, exclamation marks or a solidus, even after spaces.
        if (after is CL or CP or EX or SY)
            return Decision.Prohibited;

        // LB14: do not break after opening punctuation, even after spaces.
        if (before == OP || (before == SP && _beforeSpaces == OP))
            return Decision.Prohibited;

        // LB15a: nor after an initial quotation mark that opens a quotation, even after spaces.
        if ((before == QUPi && OpensQuotation(Consult(_twoBefore).Class)) || (before == SP && _spacesFollowOpeningQuote))
            return Decision.Prohibited;

        // LB15b: nor before a final quotation mark that closes one.
        if (after == QUPf && ClosesQuotation(Consult(ItemAfter(_next)).Class))
            return Decision.Prohibited;

        if (after == IS)
        {
            // LB15c: break before a decimal mark that follows a space, as in "subtract .5".
            if (before == SP && Consult(ItemAfter(_next)).Class == NU)
                return Decision.Allowed;

            // LB15d: otherwise do not break before a comma, full stop, colon or semicolon, even after spaces.
            return Decision.Prohibited;
        }

        // LB16: do not break between closing punctuation and a nonstarter, even with spaces between.
        if (after == NS && (before is CL or CP || (before == SP && _beforeSpaces is CL or CP)))
            return Decision.Prohibited;

        // LB17: do not break within a run of em dashes, even with spaces between.
        if (after == B2 && (before == B2 || (before == SP && _beforeSpaces == B2)))
            return Decision.Prohibited;

        // LB18: break after spaces.
        if (before == SP)
            return Decision.Allowed;

        // LB19: do not break before a quotation mark unless it is initial punctuation, nor after one unless it is
        // final punctuation.
        if (after is QU or QUPf || before is QU or QUPi)
            return Decision.Prohibited;

        // LB19a: nor on either side of the rest, unless East Asian text surrounds them. After LB19, the only mark
        // that can follow the position is an initial one and the only one that can precede it is a final one.
        if (after == QUPi && (!Consult(_before).IsEastAsian || !Consult(ItemAfter(_next)).IsEastAsian))
            return Decision.Prohibited;

        if (before == QUPf && (!Consult(properties).IsEastAsian || !Consult(_twoBefore).IsEastAsian))
            return Decision.Prohibited;

        // LB20: break before and after contingent break opportunities.
        if (before == CB || after == CB)
            return Decision.Allowed;

        // LB20a: do not break after a hyphen that starts a word.
        if (after == AL && (before == HY || (before == BA && Consult(_beforeCodepoint) == Hyphen)) &&
            StartsWord(Consult(_twoBefore).Class))
            return Decision.Prohibited;

        // LB21: do not break before hyphens, fixed-width spaces, small kana and other nonstarters, or after acute
        // accents.
        if (after is BA or HY or NS || before == BB)
            return Decision.Prohibited;

        // LB21a: do not break after the hyphen in Hebrew + hyphen + non-Hebrew.
        if (after != HL && (before == HY || (before == BA && !Consult(_before).IsEastAsian)) &&
            Consult(_twoBefore).Class == HL)
            return Decision.Prohibited;

        // LB21b: do not break between a solidus and a Hebrew letter.
        if (before == SY && after == HL)
            return Decision.Prohibited;

        // LB22: do not break before ellipses.
        if (after == IN)
            return Decision.Prohibited;

        // LB23: do not break between digits and letters.
        if ((before is AL or HL && after == NU) || (before == NU && after is AL or HL))
            return Decision.Prohibited;

        // LB23a: nor between numeric prefixes and ideographs, or between ideographs and numeric postfixes.
        if ((before == PR && after is ID or EB or EM) || (before is ID or EB or EM && after == PO))
            return Decision.Prohibited;

        // LB24: nor between numeric prefixes or postfixes and letters, either way round.
        if ((before is PR or PO && after is AL or HL) || (before is AL or HL && after is PR or PO))
            return Decision.Prohibited;

        // LB25: do not break numbers — NU (SY | IS)* (CL | CP)? × (PO | PR), (PO | PR) × OP IS? NU,
        // (PO | PR | HY | IS) × NU and NU (SY | IS)* × NU. A digit always continues a number, so only the
        // separators and closing punctuation need to know whether they follow one.
        if (after is PO or PR &&
            (before == NU ||
             (before is SY or IS && Consult(_inNumber)) ||
             (before is CL or CP && Consult(_afterNumberAndClose))))
            return Decision.Prohibited;

        if (before is PO or PR && after == OP && Consult(OpensNumber()))
            return Decision.Prohibited;

        if (after == NU && (before is PO or PR or HY or IS or NU || (before == SY && Consult(_inNumber))))
            return Decision.Prohibited;

        // LB26: do not break a Korean syllable.
        if ((before == JL && after is JL or JV or H2 or H3) ||
            (before is JV or H2 && after is JV or JT) ||
            (before is JT or H3 && after == JT))
            return Decision.Prohibited;

        // LB27: treat a Korean syllable block the same as an ideograph.
        if ((before is JL or JV or JT or H2 or H3 && after == PO) || (before == PR && after is JL or JV or JT or H2 or H3))
            return Decision.Prohibited;

        // LB28: do not break between letters.
        if (before is AL or HL && after is AL or HL)
            return Decision.Prohibited;

        // LB28a: do not break inside the orthographic syllables of Brahmic scripts.
        // AP × (AK | ◌ | AS)
        if (before == AP && IsAksara(after, codepoint))
            return Decision.Prohibited;

        // (AK | ◌ | AS) × (VF | VI)
        if (after is VF or VI && IsAksara(before, _beforeCodepoint))
            return Decision.Prohibited;

        // (AK | ◌ | AS) VI × (AK | ◌)
        if (before == VI && after != AS && IsAksara(after, codepoint) &&
            IsAksara(Consult(_twoBefore).Class, _twoBeforeCodepoint))
            return Decision.Prohibited;

        // (AK | ◌ | AS) × (AK | ◌ | AS) VF. The dotted circle is a letter, so only letters need their code point read.
        if (before is AK or AS or AL && after is AK or AS or AL &&
            IsAksara(before, _beforeCodepoint) && IsAksara(after, codepoint) && Consult(ItemAfter(_next)).Class == VF)
            return Decision.Prohibited;

        // LB29: do not break between numeric punctuation and letters, as in "e.g.".
        if (before == IS && after is AL or HL)
            return Decision.Prohibited;

        // LB30: do not break between letters, numbers or ordinary symbols and parentheses, as in "person(s)" —
        // unless the parenthesis is East Asian, where the break usually belongs.
        if ((before is AL or HL or NU && after == OP && !Consult(properties).IsEastAsian) ||
            (before == CP && after is AL or HL or NU && !Consult(_before).IsEastAsian))
            return Decision.Prohibited;

        // LB30a: break between regional indicators only after a whole pair, so that a flag stays in one piece.
        if (before == RI && after == RI && Consult(_oddRegionalIndicators))
            return Decision.Prohibited;

        // LB30b: do not break between an emoji base, or what may become one, and a skin tone modifier.
        if (after == EM && (before == EB || Consult(_before).IsUnassignedPictographic))
            return Decision.Prohibited;

        // LB31: break everywhere else.
        return Decision.Allowed;
    }

    /// <summary>Takes a judged character into the state the later rules read.</summary>
    private void Advance(LineBreakProperties properties, int codepoint)
    {
        LineBreakClass kind = properties.Class;
        _afterJoiner = kind == ZWJ;

        if (kind is CM or ZWJ)
        {
            // LB9: attached to the item before, which stays as it was. Nothing precedes the first character, and a
            // hard break, space or zero width space takes no marks.
            if (_before.Class > ZW)
                return;

            // LB10.
            properties = LineBreakProperties.Alphabetic;
            kind = AL;
        }

        if (kind == SP)
        {
            if (_before.Class != SP)
            {
                _beforeSpaces = _before.Class;
                _spacesFollowOpeningQuote = _before.Class == QUPi && OpensQuotation(_twoBefore.Class);
            }

            _row = SpaceRow(_beforeSpaces, _spacesFollowOpeningQuote) * ClassCount;
        }
        else
        {
            _row = (int)kind * ClassCount;
        }

        _afterNumberAndClose = kind is CL or CP && _inNumber;
        _inNumber = kind == NU || (_inNumber && kind is SY or IS);
        _oddRegionalIndicators = kind == RI && !_oddRegionalIndicators;

        _twoBefore = _before;
        _twoBeforeCodepoint = _beforeCodepoint;
        _before = properties;
        _beforeCodepoint = codepoint;
    }

    /// <summary>
    /// Hands a rule something beyond the classes either side of the position — a flag, a code point, an earlier or
    /// later item, the state of a number — and, while the table is being worked out, notes that the rule needed it.
    /// </summary>
    /// <remarks>
    /// <see cref="Tabulate"/> puts every pair of classes through <see cref="Decide"/> with nothing known beyond the
    /// classes. A pair decided without consulting anything is decided the same way whatever the text around it, so
    /// the table can answer for it; a pair that consulted something is left to the rules. The table is therefore
    /// derived from the rules rather than written alongside them, and cannot disagree with them.
    /// </remarks>
    private T Consult<T>(T value)
    {
        _consulted |= _probing;
        return value;
    }

    private static Decision[] Tabulate()
    {
        Decision[] decisions = new Decision[RowCount * ClassCount];
        decisions.AsSpan().Fill(Decision.Contextual);
        LineBreakEnumerator probe = new LineBreakEnumerator { _probing = true };

        for (LineBreakClass before = BK; before <= VI; before++)
        {
            probe._before = LineBreakProperties.From(before);

            if (before != SP)
            {
                probe.ProbeRow(decisions.AsSpan((int)before * ClassCount, ClassCount));
                continue;
            }

            for (LineBreakClass spaced = Sot; spaced <= VI; spaced++)
            {
                probe._beforeSpaces = spaced;

                probe._spacesFollowOpeningQuote = false;
                probe.ProbeRow(decisions.AsSpan(SpaceRow(spaced, false) * ClassCount, ClassCount));

                probe._spacesFollowOpeningQuote = true;
                probe.ProbeRow(decisions.AsSpan(SpaceRow(spaced, true) * ClassCount, ClassCount));
            }
        }

        return decisions;
    }

    /// <summary>Fills the row for the item before the position this probe has been given.</summary>
    private void ProbeRow(Span<Decision> row)
    {
        for (LineBreakClass after = BK; after <= VI; after++)
        {
            _consulted = false;
            Decision decision = Decide(LineBreakProperties.From(after), 0);
            row[(int)after] = _consulted ? Decision.Contextual : decision;
        }
    }

    private static int SpaceRow(LineBreakClass beforeSpaces, bool followOpeningQuote) =>
        SpaceRows + (2 * (int)beforeSpaces) + (followOpeningQuote ? 1 : 0);

    /// <summary>
    /// LB25's (PO | PR) × OP IS? NU, looking ahead from the opening punctuation that follows the position.
    /// </summary>
    private readonly bool OpensNumber()
    {
        int index = _next;
        LineBreakProperties next = NextItem(ref index);

        if (next.Class == IS)
            next = NextItem(ref index);

        return next.Class == NU;
    }

    /// <summary>
    /// The item after the character that ends at <paramref name="index"/>, past the combining marks that attach to
    /// that character; only asked of characters that take marks.
    /// </summary>
    private readonly LineBreakProperties ItemAfter(int index) => NextItem(ref index);

    /// <inheritdoc cref="ItemAfter(int)"/>
    /// <remarks>Leaves <paramref name="index"/> just past the item returned, ready to be asked again.</remarks>
    private readonly LineBreakProperties NextItem(ref int index)
    {
        while (index < _text.Length)
        {
            int codepoint = CodepointAt(index, out int length);
            LineBreakProperties properties = LineBreakProperties.Of(codepoint);
            index += length;

            if (properties.Class is not (CM or ZWJ))
                return properties;
        }

        return LineBreakProperties.EndOfText;
    }

    /// <summary>
    /// The code point at <paramref name="index"/>. A surrogate without its partner stands for itself, which the
    /// table classes as a letter.
    /// </summary>
    private readonly int CodepointAt(int index, out int length)
    {
        char character = _text[index];

        if (char.IsHighSurrogate(character) && index + 1 < _text.Length && char.IsLowSurrogate(_text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(character, _text[index + 1]);
        }

        length = 1;
        return character;
    }

    /// <summary>
    /// LB28a's (AK | ◌ | AS): an aksara, an independent vowel, or the dotted circle standing in for one. The code point
    /// matters only for the dotted circle, which is otherwise an ordinary letter.
    /// </summary>
    private bool IsAksara(LineBreakClass kind, int codepoint) =>
        kind is AK or AS || (kind == AL && Consult(codepoint) == DottedCircle);

    /// <summary>What may precede an initial quotation mark for LB15a to keep it with what follows.</summary>
    private static bool OpensQuotation(LineBreakClass kind) =>
        kind is Sot or BK or CR or LF or NL or OP or QU or QUPi or QUPf or GL or SP or ZW;

    /// <summary>What may follow a final quotation mark for LB15b to keep it with what precedes.</summary>
    private static bool ClosesQuotation(LineBreakClass kind) =>
        kind is SP or GL or WJ or CL or QU or QUPi or QUPf or CP or EX or IS or SY or BK or CR or LF or NL or ZW or Eot;

    /// <summary>What may precede a hyphen for LB20a to count it as starting a word.</summary>
    private static bool StartsWord(LineBreakClass kind) =>
        kind is Sot or BK or CR or LF or NL or SP or ZW or CB or GL;
}
