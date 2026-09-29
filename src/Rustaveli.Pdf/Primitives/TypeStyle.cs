using Rustaveli.Pdf.Fonts.Substitution;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// The complete set of attributes governing how a run of text is measured and drawn.
/// </summary>
/// <remarks>
/// Instances are immutable; the fluent mutators return modified copies. This lets a style be shared safely as a
/// default across a document while individual spans derive their own variants from it.
/// </remarks>
public sealed record TypeStyle
{
    public static TypeStyle Default { get; } = new TypeStyle();

    /// <summary>
    /// The typeface text is set in, Noto Sans unless named: the package carries it, so a document that names none is
    /// set the same on every machine, and the Noto faces other scripts fall back to share its design.
    /// </summary>
    public string Typeface { get; init; } = "Noto Sans";

    private readonly float _pointSize = 12f;
    private readonly float? _strokeWeight;
    private readonly float _leading = 1f;
    private readonly float _tracking;
    private readonly float _wordSpacing;

    /// <summary>The size text is set at, in points: zero or more.</summary>
    public float PointSize
    {
        get => _pointSize;
        init => _pointSize = Size(value, nameof(PointSize));
    }

    public TypeWeight Weight { get; init; } = TypeWeight.Normal;

    public bool IsItalic { get; init; }

    public Ink Ink { get; init; } = Ink.Black;

    public Ink Highlight { get; init; } = Ink.Transparent;

    public bool HasUnderline { get; init; }

    public bool HasStrikeThrough { get; init; }

    public bool HasOverline { get; init; }

    /// <summary>
    /// Whether a line may break between any two characters of this type, not only between words, so a long
    /// identifier or address fills each line rather than leaving it short.
    /// </summary>
    public bool BreaksAnywhere { get; init; }

    /// <summary>
    /// The direction the type reads in, set apart from the text around it: a right-to-left name in an English
    /// sentence keeps its own word order and carries its punctuation with it. Null leaves the type to the paragraph,
    /// where each character takes the direction its script gives it.
    /// </summary>
    public ReadingDirection? Direction { get; init; }

    /// <summary>The OpenType features turned on or off beyond the defaults every face is set with.</summary>
    internal TypeFeatures Features { get; init; } = TypeFeatures.None;

    /// <summary>The typefaces tried, in order, for characters <see cref="Typeface"/> lacks.</summary>
    public IReadOnlyList<string> Fallbacks => FallbackTypefaces.Names;

    internal TypefaceFallbacks FallbackTypefaces { get; init; } = TypefaceFallbacks.None;

    /// <summary>How underlines, strike-throughs and overlines are drawn.</summary>
    public StrokeStyle StrokeStyle { get; init; } = StrokeStyle.Solid;

    /// <summary>The ink of underlines, strike-throughs and overlines; the text's own ink when not set.</summary>
    public Ink? StrokeInk { get; init; }

    /// <summary>The weight of underlines, strike-throughs and overlines, in points; the font's own when not set.</summary>
    public float? StrokeWeight
    {
        get => _strokeWeight;
        init => _strokeWeight = value is { } weight ? Size(weight, nameof(StrokeWeight)) : null;
    }

    /// <summary>Multiplier applied to the font's natural line height: zero or more.</summary>
    public float Leading
    {
        get => _leading;
        init => _leading = Size(value, nameof(Leading));
    }

    /// <summary>Additional space inserted between characters, in points; negative tightens.</summary>
    public float Tracking
    {
        get => _tracking;
        init => _tracking = Spacing(value, nameof(Tracking));
    }

    /// <summary>Additional space added to each space between words, in points; negative tightens.</summary>
    public float WordSpacing
    {
        get => _wordSpacing;
        init => _wordSpacing = Spacing(value, nameof(WordSpacing));
    }

    public ScriptPosition Script { get; init; } = ScriptPosition.Normal;

    /// <summary>
    /// The font size actually rendered, shrunk for sub- and superscript runs.
    /// </summary>
    public float EffectivePointSize => Script == ScriptPosition.Normal ? PointSize : PointSize * SubscriptScale;

    /// <summary>
    /// How far the baseline shifts for this run, positive downwards.
    /// </summary>
    public float BaselineOffset => Script switch
    {
        ScriptPosition.Subscript => PointSize * SubscriptOffsetRatio,
        ScriptPosition.Superscript => -PointSize * SuperscriptOffsetRatio,
        _ => 0f
    };

    /// <summary>Sub- and superscript runs are set at this fraction of the surrounding font size.</summary>
    private const float SubscriptScale = 0.58f;

    private const float SubscriptOffsetRatio = 0.16f;

    private const float SuperscriptOffsetRatio = 0.33f;

    /// <summary>
    /// A copy set in <paramref name="fontFamily"/>, falling back to <paramref name="fallbacks"/>, in order, for any
    /// character it lacks — before the library's own fallbacks, and before any installed face that has it.
    /// </summary>
    /// <remarks>Naming the typeface again replaces the fallbacks too: with none given, the style has none.</remarks>
    public TypeStyle WithTypeface(string fontFamily, params string[] fallbacks)
    {
        ArgumentNullException.ThrowIfNull(fallbacks);

        return this with
        {
            Typeface = fontFamily,
            FallbackTypefaces = TypefaceFallbacks.Of(fallbacks)
        };
    }

    public TypeStyle WithPointSize(float size)
    {
        Size(size, nameof(size));

        return this with
        {
            PointSize = size
        };
    }

    public TypeStyle WithWeight(TypeWeight weight)
    {
        return this with
        {
            Weight = weight
        };
    }

    public TypeStyle Bold()
    {
        return this with
        {
            Weight = TypeWeight.Bold
        };
    }

    public TypeStyle Italic(bool value = true)
    {
        return this with
        {
            IsItalic = value
        };
    }

    public TypeStyle WithInk(Ink ink)
    {
        return this with
        {
            Ink = ink
        };
    }

    public TypeStyle WithHighlight(Ink ink)
    {
        return this with
        {
            Highlight = ink
        };
    }

    public TypeStyle Underline(bool value = true)
    {
        return this with
        {
            HasUnderline = value
        };
    }

    public TypeStyle StrikeThrough(bool value = true)
    {
        return this with
        {
            HasStrikeThrough = value
        };
    }

    public TypeStyle Overline(bool value = true)
    {
        return this with
        {
            HasOverline = value
        };
    }

    public TypeStyle BreakAnywhere(bool value = true)
    {
        return this with
        {
            BreaksAnywhere = value
        };
    }

    /// <summary>
    /// A copy with the OpenType feature <paramref name="tag"/> — <c>"smcp"</c>, <c>"onum"</c>, <c>"ss01"</c> — set to
    /// <paramref name="value"/>: 0 turns it off, 1 on, and a higher value chooses among a feature's alternates.
    /// </summary>
    /// <remarks>
    /// Every face is set with composition, localized forms, contextual alternates and standard, contextual and
    /// required ligatures; a feature the face does not have does nothing.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is not four printable ASCII characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public TypeStyle WithFeature(string tag, int value = 1)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (tag.Length != 4 || tag.Any(character => character is < ' ' or > '~'))
            throw new ArgumentException($"An OpenType feature tag is four printable ASCII characters, such as \"liga\"; \"{tag}\" is not.", nameof(tag));

        return this with
        {
            Features = Features.With(FeatureTag.Parse(tag), value)
        };
    }

    /// <summary>A copy with ligatures such as "fi" and "ffl" set, or not: the standard and contextual ones.</summary>
    public TypeStyle Ligatures(bool value = true) =>
        WithFeature("liga", value ? 1 : 0).WithFeature("clig", value ? 1 : 0);

    /// <summary>A copy with lowercase letters set as small capitals, where the face has them.</summary>
    public TypeStyle SmallCapitals(bool value = true) => WithFeature("smcp", value ? 1 : 0);

    /// <summary>A copy with old-style figures, which rise and descend like lowercase letters, where the face has them.</summary>
    public TypeStyle OldstyleFigures(bool value = true) => WithFeature("onum", value ? 1 : 0);

    /// <summary>A copy with figures all one width, so columns of numbers align, where the face has them.</summary>
    public TypeStyle TabularFigures(bool value = true) => WithFeature("tnum", value ? 1 : 0);

    /// <summary>A copy that reads in <paramref name="direction"/>, set apart from the text around it; null to follow it.</summary>
    public TypeStyle WithDirection(ReadingDirection? direction)
    {
        return this with
        {
            Direction = direction
        };
    }

    public TypeStyle WithStrokeStyle(StrokeStyle style)
    {
        return this with
        {
            StrokeStyle = style
        };
    }

    public TypeStyle WithStrokeInk(Ink ink)
    {
        return this with
        {
            StrokeInk = ink
        };
    }

    public TypeStyle WithStrokeWeight(float weight)
    {
        Size(weight, nameof(weight));

        return this with
        {
            StrokeWeight = weight
        };
    }

    public TypeStyle WithLeading(float multiplier)
    {
        Size(multiplier, nameof(multiplier));

        return this with
        {
            Leading = multiplier
        };
    }

    public TypeStyle WithTracking(float spacing)
    {
        Spacing(spacing, nameof(spacing));

        return this with
        {
            Tracking = spacing
        };
    }

    public TypeStyle WithWordSpacing(float spacing)
    {
        Spacing(spacing, nameof(spacing));

        return this with
        {
            WordSpacing = spacing
        };
    }

    public TypeStyle Subscript()
    {
        return this with
        {
            Script = ScriptPosition.Subscript
        };
    }

    public TypeStyle Superscript()
    {
        return this with
        {
            Script = ScriptPosition.Superscript
        };
    }

    /// <summary><paramref name="value"/>, a size or a multiplier: zero or more, and a number a PDF can write.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is negative, NaN, or 10^15 or more.</exception>
    internal static float Size(float value, string name) =>
        value >= 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be zero or more, and less than 10^15, the largest number a PDF can write.");

    /// <summary><paramref name="value"/>, a spacing that may tighten as well as loosen: a number a PDF can write.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is NaN, or 10^15 or more either way.</exception>
    internal static float Spacing(float value, string name) =>
        Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be a number less than 10^15 either way, the largest a PDF can write.");
}
