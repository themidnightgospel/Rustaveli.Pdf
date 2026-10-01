using System.Text.Json;
using System.Text.RegularExpressions;
using Rustaveli.Pdf.ConformanceTests.Validation;
using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Text in the font corpus's formats — a variable TrueType font, a TrueType collection and a font kerned by the older
/// <c>kern</c> table — embeds a subset that qpdf and veraPDF accept and that is a plain TrueType font of its own.
/// </summary>
/// <remarks>The fonts, where they come from and how they were made are listed in tests/assets/fonts/corpus.</remarks>
public class CorpusFontEmbeddingTests
{
    private const string Sentence = "Sphinx of black quartz, judge my vow: AVATAR Today, fifty offices, 1234.";

    /// <summary>File, typeface, whether bold, and the PostScript name of the face that sets it.</summary>
    public static TheoryData<string, string, bool, string> Faces { get; } = new TheoryData<string, string, bool, string>
    {
        { "SourceSans3VF-Upright.ttf", "SourceSans3VF", false, "SourceSans3VF-ExtraLight" },
        { "NotoSans-Collection.ttc", "Noto Sans", false, "NotoSans-Regular" },
        { "NotoSans-Collection.ttc", "Noto Sans", true, "NotoSans-Bold" },
        { "KernTableTest-Regular.ttf", "Kern Table Test", false, "KernTableTest-Regular" },
    };

    private static string PathOf(string file) => Path.Combine(AppContext.BaseDirectory, "assets", "fonts", "corpus", file);

    private static byte[] Export(string file, string typeface, bool bold, PdfExportOptions options)
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.RegisterFile(PathOf(file));
        options.Typefaces = library;

        TypeStyle style = TypeStyle.Default.WithTypeface(typeface).WithPointSize(12);
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.DefaultType = bold ? style.Bold() : style;
            section.Body().Text(Sentence);
        }));
        document.Info.Title = typeface;
        document.Info.Language = "en";

        return document.ExportPdf(options);
    }

    /// <summary>Each font descriptor's font name and its decoded font program, read through qpdf's JSON view.</summary>
    private static List<(string Name, string Key, byte[] Program)> EmbeddedFonts(byte[] pdf)
    {
        (int code, string output, _) = Qpdf.Run(
            pdf, "--json=2", "--json-key=qpdf", "--json-stream-data=inline", "--decode-level=generalized", "{input}");
        Assert.True(code == 0, output);

        using JsonDocument json = JsonDocument.Parse(output);
        JsonElement objects = json.RootElement.GetProperty("qpdf")[1];
        List<(string Name, string Key, byte[] Program)> fonts = [];

        foreach (JsonProperty entry in objects.EnumerateObject())
        {
            if (!entry.Value.TryGetProperty("value", out JsonElement value) || value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("/Type", out JsonElement type) || type.GetString() != "/FontDescriptor")
            {
                continue;
            }

            foreach (string key in new[] { "/FontFile", "/FontFile2", "/FontFile3" })
            {
                if (!value.TryGetProperty(key, out JsonElement reference))
                    continue;

                JsonElement stream = objects.GetProperty("obj:" + reference.GetString()).GetProperty("stream");
                byte[] program = Convert.FromBase64String(stream.GetProperty("data").GetString()!);
                fonts.Add((value.GetProperty("/FontName").GetString()![1..], key, program));
            }
        }

        return fonts;
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void EachFormatEmbedsAPlainTrueTypeSubsetThatQpdfAccepts(string file, string typeface, bool bold, string postScriptName)
    {
        byte[] pdf = Export(file, typeface, bold, new PdfExportOptions());

        Assert.Contains("No syntax or stream encoding errors found", Qpdf.Check(pdf), StringComparison.Ordinal);

        (string name, string key, byte[] program) = Assert.Single(EmbeddedFonts(pdf));
        Assert.Matches("^[A-Z]{6}\\+" + Regex.Escape(postScriptName) + "$", name);
        Assert.Equal("/FontFile2", key);

        // A face of a collection comes out as a font of its own, and a variable font as its default instance alone.
        OpenTypeFont subset = OpenTypeFont.Load(program);
        OpenTypeFont source = OpenTypeFont.LoadFile(PathOf(file), bold ? 1 : 0);

        Assert.Equal(TableDirectory.TrueTypeVersion, subset.Tables.SfntVersion);
        Assert.Equal(OutlineFormat.TrueType, subset.Outlines);
        Assert.InRange(subset.GlyphCount, Sentence.Distinct().Count() - 1, 64);
        Assert.True(source.GlyphCount > 1000, "The source holds far more glyphs than the subset.");

        foreach (string tag in new[] { "fvar", "gvar", "HVAR", "MVAR", "avar", "GPOS", "GSUB", "kern" })
            Assert.False(subset.Tables.Contains(TableTag.FromString(tag)), $"The subset carries a '{tag}' table.");

        // Every glyph shown keeps the outline and advance it has in the source.
        foreach (char character in Sentence.Distinct().Where(character => character != ' '))
        {
            ushort shown = source.GetGlyphId(character);
            ushort kept = subset.GetGlyphId(character);

            Assert.True(kept != 0, $"The subset lacks '{character}'.");
            Assert.Equal(source.GetAdvance(shown), subset.GetAdvance(kept));
            Assert.True(source.TryGetGlyphBounds(shown, out GlyphBounds shownBounds));
            Assert.True(subset.TryGetGlyphBounds(kept, out GlyphBounds keptBounds), $"The subset has no outline for '{character}'.");
            Assert.Equal(shownBounds, keptBounds);
        }
    }

    [Fact]
    public void EachFormatMeetsPdfAAsVeraPdfReadsIt()
    {
        // PDF/A checks every embedded font program against the widths and glyphs the PDF says it has.
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (object[] face in Faces)
        {
            string name = $"{Path.GetFileNameWithoutExtension((string)face[0])}-{((bool)face[2] ? "bold" : "regular")}";
            PdfExportOptions options = new PdfExportOptions { Conformance = PdfAConformance.PdfA2U };
            files.Add(name, Export((string)face[0], (string)face[1], (bool)face[2], options));
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> failures = VeraPdf.Validate(files);

        Assert.Equal(files.Keys.Order(StringComparer.Ordinal), failures.Keys.Order(StringComparer.Ordinal));
        string[] broken = failures.SelectMany(file => file.Value.Select(rule => $"{file.Key}: {rule}")).ToArray();
        Assert.True(broken.Length == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }
}
