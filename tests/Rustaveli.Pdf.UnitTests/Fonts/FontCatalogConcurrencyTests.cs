using System.Collections.Concurrent;
using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Documents generated in parallel share one catalog, with no lock across the process: every thread must get the
/// same answers a single thread would, while fonts are being registered underneath them.
/// </summary>
public class FontCatalogConcurrencyTests
{
    private static readonly (FontRequest Request, string Expected)[] Matches =
    [
        (new FontRequest("Noto Sans"), "NotoSans-Regular"),
        (new FontRequest("Noto Sans", 700), "NotoSans-Bold"),
        (new FontRequest("noto sans", 400, FontSlant.Italic), "NotoSans-Italic"),
        (new FontRequest("Specimen Sans", 600), "SpecimenSans-SemiBold"),
        (new FontRequest("Noto Sans Georgian"), "NotoSansGeorgian-Regular")
    ];

    [Fact]
    public void MatchesMeasuresAndFallsBackConsistentlyFromManyThreads()
    {
        FontCatalog catalog = new FontCatalog(new SystemFontIndex([TestFonts.Directory]));
        ConcurrentBag<string> failures = [];
        ConcurrentBag<OpenTypeFont> georgianFonts = [];

        Parallel.For(0, 400, new ParallelOptions { MaxDegreeOfParallelism = 16 }, iteration =>
        {
            (FontRequest request, string expected) = Matches[iteration % Matches.Length];
            OpenTypeFont? font = catalog.Match(request);

            if (font?.Names.PostScriptName != expected)
                failures.Add($"{request} gave {font?.Names.PostScriptName}");

            // Measuring from many threads at once shares the lazily parsed tables.
            if (font is not null && font.MeasureWidthInUnits("AVATAR") <= 0)
                failures.Add($"{expected} measured nothing");

            OpenTypeFont? fallback = catalog.FindFallback(0x10D0 + (iteration % 40), new FontRequest("Noto Sans"));

            if (fallback is null)
                failures.Add($"No fallback for U+{0x10D0 + (iteration % 40):X4}");
            else
                georgianFonts.Add(fallback);

            if (iteration % 50 == 0)
                catalog.Register(SyntheticFont.Named("Registered " + iteration).Build());
        });

        Assert.Empty(failures);

        // Loaded once, however many threads asked.
        Assert.Single(georgianFonts.Distinct());
        Assert.Equal(8, catalog.RegisteredFaces.Count);
        Assert.Equal(8, catalog.RegisteredFaces.Select(face => face.Names.Family).Distinct().Count());
    }

    [Fact]
    public void ScansTheSystemOnceWhenManyThreadsAskAtOnce()
    {
        SystemFontIndex index = new SystemFontIndex([TestFonts.Directory]);
        ConcurrentBag<IReadOnlyList<FontFaceInfo>> seen = [];

        Parallel.For(0, 32, _ => seen.Add(index.Faces));

        Assert.Single(seen.Distinct());
    }
}
