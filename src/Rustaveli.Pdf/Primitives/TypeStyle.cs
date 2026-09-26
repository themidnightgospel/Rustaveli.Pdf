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

    public string Typeface { get; init; } = "Helvetica";

    public float PointSize { get; init; } = 12f;

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

    /// <summary>How underlines, strike-throughs and overlines are drawn.</summary>
    public StrokeStyle StrokeStyle { get; init; } = StrokeStyle.Solid;

    /// <summary>The ink of underlines, strike-throughs and overlines; the text's own ink when not set.</summary>
    public Ink? StrokeInk { get; init; }

    /// <summary>The weight of underlines, strike-throughs and overlines, in points; the font's own when not set.</summary>
    public float? StrokeWeight { get; init; }

    /// <summary>Multiplier applied to the font's natural line height.</summary>
    public float Leading { get; init; } = 1f;

    /// <summary>Additional space inserted between characters, in points.</summary>
    public float Tracking { get; init; }

    /// <summary>Additional space added to each space between words, in points; negative tightens.</summary>
    public float WordSpacing { get; init; }

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

    public TypeStyle WithTypeface(string fontFamily)
    {
        return this with
        {
            Typeface = fontFamily
        };
    }

    public TypeStyle WithPointSize(float size)
    {
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
        ArgumentOutOfRangeException.ThrowIfNegative(weight);

        return this with
        {
            StrokeWeight = weight
        };
    }

    public TypeStyle WithLeading(float multiplier)
    {
        return this with
        {
            Leading = multiplier
        };
    }

    public TypeStyle WithTracking(float spacing)
    {
        return this with
        {
            Tracking = spacing
        };
    }

    public TypeStyle WithWordSpacing(float spacing)
    {
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
}
