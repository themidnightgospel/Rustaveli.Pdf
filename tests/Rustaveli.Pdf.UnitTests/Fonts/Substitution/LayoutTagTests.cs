using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class LayoutTagTests
{
    [Fact]
    public void NamesTheRegisteredFeatures()
    {
        (FeatureTag Tag, string Expected)[] features =
        [
            (FeatureTag.RequiredVariationAlternates, "rvrn"),
            (FeatureTag.GlyphComposition, "ccmp"),
            (FeatureTag.LocalizedForms, "locl"),
            (FeatureTag.RequiredLigatures, "rlig"),
            (FeatureTag.ContextualAlternates, "calt"),
            (FeatureTag.ContextualLigatures, "clig"),
            (FeatureTag.StandardLigatures, "liga"),
            (FeatureTag.RequiredContextualAlternates, "rclt"),
            (FeatureTag.DiscretionaryLigatures, "dlig"),
            (FeatureTag.SmallCapitals, "smcp"),
            (FeatureTag.CapitalsToSmallCapitals, "c2sc"),
            (FeatureTag.OldstyleFigures, "onum"),
            (FeatureTag.LiningFigures, "lnum"),
            (FeatureTag.ProportionalFigures, "pnum"),
            (FeatureTag.TabularFigures, "tnum"),
            (FeatureTag.Fractions, "frac"),
            (FeatureTag.Superscript, "sups"),
            (FeatureTag.Subscript, "subs"),
            (FeatureTag.StylisticAlternates, "salt"),
            (FeatureTag.InitialForms, "init"),
            (FeatureTag.MedialForms, "medi"),
            (FeatureTag.TerminalForms, "fina"),
            (FeatureTag.IsolatedForms, "isol")
        ];

        foreach ((FeatureTag tag, string expected) in features)
            Assert.Equal(expected, tag.ToString());
    }

    [Fact]
    public void HoldsATagAsItsBigEndianBytes()
    {
        Assert.Equal(0x6C696761u, FeatureTag.Parse("liga").Value);
        Assert.Equal(0x6C61746Eu, ScriptTag.Parse("latn").Value);
        Assert.Equal(0x53524220u, LanguageTag.Parse("SRB ").Value);
        Assert.Equal(FeatureTag.StandardLigatures, new FeatureTag(0x6C696761));
    }

    [Theory]
    [InlineData(1, "ss01")]
    [InlineData(9, "ss09")]
    [InlineData(20, "ss20")]
    public void NamesStylisticSets(int number, string expected)
    {
        Assert.Equal(expected, FeatureTag.StylisticSet(number).ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void RejectsStylisticSetsThatDoNotExist(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeatureTag.StylisticSet(number));
    }

    [Fact]
    public void RejectsTagsThatAreNotFourCharacters()
    {
        Assert.Throws<ArgumentException>(() => FeatureTag.Parse("lig"));
        Assert.Throws<ArgumentException>(() => ScriptTag.Parse("latin"));
        Assert.Throws<ArgumentException>(() => LanguageTag.Parse("SRB"));
    }

    [Fact]
    public void NamesTheRegisteredScripts()
    {
        (ScriptTag Tag, string Expected)[] scripts =
        [
            (ScriptTag.Default, "DFLT"),
            (ScriptTag.Latin, "latn"),
            (ScriptTag.Greek, "grek"),
            (ScriptTag.Cyrillic, "cyrl"),
            (ScriptTag.Georgian, "geor"),
            (ScriptTag.Armenian, "armn"),
            (ScriptTag.Hebrew, "hebr"),
            (ScriptTag.Arabic, "arab")
        ];

        foreach ((ScriptTag tag, string expected) in scripts)
            Assert.Equal(expected, tag.ToString());
    }

    [Fact]
    public void NamesTheDefaultLanguage()
    {
        Assert.Equal("dflt", LanguageTag.Default.ToString());
        Assert.Equal("SRB ", LanguageTag.Parse("SRB ").ToString());
    }

    [Fact]
    public void TurnsFeaturesOnAndOff()
    {
        FeatureSetting on = FeatureSetting.On(FeatureTag.SmallCapitals);
        FeatureSetting off = FeatureSetting.Off(FeatureTag.SmallCapitals);

        Assert.Equal(new FeatureSetting(FeatureTag.SmallCapitals, 1), on);
        Assert.True(on.IsEnabled);
        Assert.Equal(0, off.Value);
        Assert.False(off.IsEnabled);
        Assert.True(new FeatureSetting(FeatureTag.StylisticAlternates, 3).IsEnabled);
        Assert.True(new FeatureSetting(FeatureTag.StylisticAlternates, -1).IsEnabled);
    }
}
