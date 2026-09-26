using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// The complete set of attributes governing how a run of text is measured and drawn.
/// </summary>
/// <remarks>
/// Instances are immutable; the fluent mutators return modified copies. This lets a style be shared safely as a
/// default across a document while individual spans derive their own variants from it.
/// </remarks>
public sealed record TextStyle
{
    public static TextStyle Default { get; } = new TextStyle();

    public string FontFamily { get; init; } = "Helvetica";

    public float FontSize { get; init; } = 12f;

    public FontWeight Weight { get; init; } = FontWeight.Normal;

    public bool IsItalic { get; init; }

    public Ink Color { get; init; } = Ink.Black;

    public Ink BackgroundColor { get; init; } = Ink.Transparent;

    public bool HasUnderline { get; init; }

    public bool HasStrikethrough { get; init; }

    /// <summary>Multiplier applied to the font's natural line height.</summary>
    public float LineHeight { get; init; } = 1f;

    /// <summary>Additional space inserted between characters, in points.</summary>
    public float LetterSpacing { get; init; }

    public FontPosition Position { get; init; } = FontPosition.Normal;

    /// <summary>
    /// The font size actually rendered, shrunk for sub- and superscript runs.
    /// </summary>
    public float EffectiveFontSize => Position == FontPosition.Normal ? FontSize : FontSize * SubscriptScale;

    /// <summary>
    /// How far the baseline shifts for this run, positive downwards.
    /// </summary>
    public float BaselineOffset => Position switch
    {
        FontPosition.Subscript => FontSize * SubscriptOffsetRatio,
        FontPosition.Superscript => -FontSize * SuperscriptOffsetRatio,
        _ => 0f
    };

    /// <summary>Sub- and superscript runs are set at this fraction of the surrounding font size.</summary>
    private const float SubscriptScale = 0.58f;

    private const float SubscriptOffsetRatio = 0.16f;

    private const float SuperscriptOffsetRatio = 0.33f;

    public TextStyle FontFamilyOf(string fontFamily)
    {
        return this with
        {
            FontFamily = fontFamily
        };
    }

    public TextStyle FontSizeOf(float size)
    {
        return this with
        {
            FontSize = size
        };
    }

    public TextStyle WeightOf(FontWeight weight)
    {
        return this with
        {
            Weight = weight
        };
    }

    public TextStyle Bold()
    {
        return this with
        {
            Weight = FontWeight.Bold
        };
    }

    public TextStyle Italic(bool value = true)
    {
        return this with
        {
            IsItalic = value
        };
    }

    public TextStyle ColorOf(Ink color)
    {
        return this with
        {
            Color = color
        };
    }

    public TextStyle BackgroundColorOf(Ink color)
    {
        return this with
        {
            BackgroundColor = color
        };
    }

    public TextStyle Underline(bool value = true)
    {
        return this with
        {
            HasUnderline = value
        };
    }

    public TextStyle Strikethrough(bool value = true)
    {
        return this with
        {
            HasStrikethrough = value
        };
    }

    public TextStyle LineHeightOf(float multiplier)
    {
        return this with
        {
            LineHeight = multiplier
        };
    }

    public TextStyle LetterSpacingOf(float spacing)
    {
        return this with
        {
            LetterSpacing = spacing
        };
    }

    public TextStyle Subscript()
    {
        return this with
        {
            Position = FontPosition.Subscript
        };
    }

    public TextStyle Superscript()
    {
        return this with
        {
            Position = FontPosition.Superscript
        };
    }
}
