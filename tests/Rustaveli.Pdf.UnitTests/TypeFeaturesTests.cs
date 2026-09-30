using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.UnitTests;

public class TypeFeaturesTests
{
    private static readonly TypeStyle Base = TypeStyle.Default;

    [Fact]
    public void AStyleTurnsNoFeaturesOnOrOffUntilAsked()
    {
        Assert.Same(TypeFeatures.None, Base.Features);
        Assert.Empty(Base.Features.Settings);
        Assert.Equal("none", Base.Features.ToString());
    }

    [Fact]
    public void WithFeatureSetsTheFeatureToTheValue()
    {
        TypeStyle style = Base.WithFeature("salt", 3);

        FeatureSetting setting = Assert.Single(style.Features.Settings);
        Assert.Equal(FeatureTag.StylisticAlternates, setting.Tag);
        Assert.Equal(3, setting.Value);
    }

    [Fact]
    public void AFeatureIsOnUnlessGivenAValue() =>
        Assert.Equal(1, Assert.Single(Base.WithFeature("smcp").Features.Settings).Value);

    [Fact]
    public void ALaterSettingOfAFeatureReplacesTheEarlier()
    {
        TypeStyle style = Base.WithFeature("liga", 0).WithFeature("liga");

        Assert.Equal(1, Assert.Single(style.Features.Settings).Value);
    }

    [Fact]
    public void StylesAskingForTheSameFeaturesAreEqualWhateverTheOrder()
    {
        TypeStyle first = Base.WithFeature("smcp").WithFeature("onum");
        TypeStyle second = Base.WithFeature("onum").WithFeature("smcp");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal("onum=1 smcp=1", first.Features.ToString());
    }

    [Fact]
    public void StylesAskingForDifferentFeaturesDiffer()
    {
        Assert.NotEqual(Base.WithFeature("smcp"), Base.WithFeature("onum"));
        Assert.NotEqual(Base.WithFeature("salt", 1), Base.WithFeature("salt", 2));
        Assert.NotEqual(Base, Base.WithFeature("smcp"));
        Assert.False(Base.WithFeature("smcp").Features.Equals((object?)null));
        Assert.False(Base.WithFeature("smcp").Features.Equals("smcp"));
    }

    [Fact]
    public void ChangingFeaturesLeavesTheOriginalAlone()
    {
        TypeStyle original = Base.WithFeature("smcp");

        original.WithFeature("onum");

        Assert.Single(original.Features.Settings);
    }

    [Theory]
    [InlineData("lig")]
    [InlineData("ligat")]
    [InlineData("liça")]
    [InlineData("li\ta")]
    [InlineData("")]
    public void AFeatureTagIsFourPrintableAsciiCharacters(string tag)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Base.WithFeature(tag));

        Assert.Equal("tag", exception.ParamName);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("~")]
    public void TheEdgesOfPrintableAsciiAreAllowed(string edge) =>
        Assert.Single(Base.WithFeature("ab" + edge + edge).Features.Settings);

    [Fact]
    public void AFeatureTagCannotBeNull() =>
        Assert.Equal("tag", Assert.Throws<ArgumentNullException>(() => Base.WithFeature(null!)).ParamName);

    [Fact]
    public void AFeatureValueCannotBeNegative() =>
        Assert.Equal("value", Assert.Throws<ArgumentOutOfRangeException>(() => Base.WithFeature("salt", -1)).ParamName);

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void LigaturesSetsTheStandardAndContextualOnes(bool on, int value)
    {
        TypeStyle style = Base.Ligatures(on);

        Assert.Equal(
            [(FeatureTag.ContextualLigatures, value), (FeatureTag.StandardLigatures, value)],
            style.Features.Settings.Select(setting => (setting.Tag, setting.Value)));
    }

    [Theory]
    [InlineData("smcp", true)]
    [InlineData("onum", true)]
    [InlineData("tnum", true)]
    [InlineData("smcp", false)]
    [InlineData("onum", false)]
    [InlineData("tnum", false)]
    public void TheNamedFeaturesSetTheirTags(string tag, bool on)
    {
        TypeStyle style = tag switch
        {
            "smcp" => Base.SmallCapitals(on),
            "onum" => Base.OldstyleFigures(on),
            _ => Base.TabularFigures(on),
        };

        Assert.Equal(Base.WithFeature(tag, on ? 1 : 0), style);
    }

    [Fact]
    public void TheNamedFeaturesAreOnUnlessTold()
    {
        Assert.Equal(Base.WithFeature("smcp"), Base.SmallCapitals());
        Assert.Equal(Base.WithFeature("onum"), Base.OldstyleFigures());
        Assert.Equal(Base.WithFeature("tnum"), Base.TabularFigures());
        Assert.Equal(Base.WithFeature("liga").WithFeature("clig"), Base.Ligatures());
    }

    [Fact]
    public void TheRunComposerSetsFeaturesOnTheRun()
    {
        Block root = LayoutHarness.Build(frame => frame.Text(text =>
        {
            text.Run("a").Feature("ss01", 2);
            text.Run("b").Ligatures(false);
            text.Run("c").SmallCapitals();
            text.Run("d").OldstyleFigures();
            text.Run("e").TabularFigures();
        }));

        List<TypeStyle> styles = LayoutHarness.Render(root, new Extent(200, 200)).Texts.Select(text => text.Style).ToList();

        Assert.Equal(Base.WithFeature("ss01", 2).Features, styles[0].Features);
        Assert.Equal(Base.Ligatures(false).Features, styles[1].Features);
        Assert.Equal(Base.SmallCapitals().Features, styles[2].Features);
        Assert.Equal(Base.OldstyleFigures().Features, styles[3].Features);
        Assert.Equal(Base.TabularFigures().Features, styles[4].Features);
    }

    [Theory]
    [InlineData(null, "a", "a")]
    [InlineData(null, "", "")]
    [InlineData(null, null, null)]
    [InlineData("", "a", "a")]
    [InlineData("", null, "")]
    [InlineData("a", "b", "a")]
    [InlineData("a", "", "a")]
    [InlineData("a", null, "a")]
    public void AGlyphKeepsTheFirstTextItShows(string? recorded, string? offered, string? kept) =>
        Assert.Equal(kept, GlyphSubset.Keep(recorded, offered));
}
