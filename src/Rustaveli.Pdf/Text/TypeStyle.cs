using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Text;

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

    public string FontFamily { get; init; } = "Helvetica";

    public float FontSize { get; init; } = 12f;

    public TypeWeight Weight { get; init; } = TypeWeight.Normal;

    public bool IsItalic { get; init; }

    public Ink Color { get; init; } = Ink.Black;

    public Ink BackgroundColor { get; init; } = Ink.Transparent;

    public bool HasUnderline { get; init; }

    public bool HasStrikethrough { get; init; }

    /// <summary>Multiplier applied to the font's natural line height.</summary>
    public float LineHeight { get; init; } = 1f;

    /// <summary>Additional space inserted between characters, in points.</summary>
    public float LetterSpacing { get; init; }

    public ScriptPosition Position { get; init; } = ScriptPosition.Normal;

    /// <summary>
    /// The font size actually rendered, shrunk for sub- and superscript runs.
    /// </summary>
    public float EffectiveFontSize => Position == ScriptPosition.Normal ? FontSize : FontSize * SubscriptScale;

    /// <summary>
    /// How far the baseline shifts for this run, positive downwards.
    /// </summary>
    public float BaselineOffset => Position switch
    {
        ScriptPosition.Subscript => FontSize * SubscriptOffsetRatio,
        ScriptPosition.Superscript => -FontSize * SuperscriptOffsetRatio,
        _ => 0f
    };

    /// <summary>Sub- and superscript runs are set at this fraction of the surrounding font size.</summary>
    private const float SubscriptScale = 0.58f;

    private const float SubscriptOffsetRatio = 0.16f;

    private const float SuperscriptOffsetRatio = 0.33f;

    public TypeStyle FontFamilyOf(string fontFamily)
    {
        return this with
        {
            FontFamily = fontFamily
        };
    }

    public TypeStyle FontSizeOf(float size)
    {
        return this with
        {
            FontSize = size
        };
    }

    public TypeStyle WeightOf(TypeWeight weight)
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

    public TypeStyle ColorOf(Ink color)
    {
        return this with
        {
            Color = color
        };
    }

    public TypeStyle BackgroundColorOf(Ink color)
    {
        return this with
        {
            BackgroundColor = color
        };
    }

    public TypeStyle Underline(bool value = true)
    {
        return this with
        {
            HasUnderline = value
        };
    }

    public TypeStyle Strikethrough(bool value = true)
    {
        return this with
        {
            HasStrikethrough = value
        };
    }

    public TypeStyle LineHeightOf(float multiplier)
    {
        return this with
        {
            LineHeight = multiplier
        };
    }

    public TypeStyle LetterSpacingOf(float spacing)
    {
        return this with
        {
            LetterSpacing = spacing
        };
    }

    public TypeStyle Subscript()
    {
        return this with
        {
            Position = ScriptPosition.Subscript
        };
    }

    public TypeStyle Superscript()
    {
        return this with
        {
            Position = ScriptPosition.Superscript
        };
    }
}
