using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

public class StrokeGeometryTests
{
    private const float Lift = 4f / 3f;

    [Fact]
    public void TheLinesOfADoubleStrokeSitAWeightEitherSideOfAHorizontalLine()
    {
        Approximately.Equal(new Offset(0, 3), StrokeGeometry.DoubleOffset(Offset.Zero, new Offset(10, 0), 3));
    }

    [Fact]
    public void TheLinesOfADoubleStrokeSitAcrossAVerticalLine()
    {
        Approximately.Equal(new Offset(-3, 0), StrokeGeometry.DoubleOffset(Offset.Zero, new Offset(0, 10), 3));
    }

    [Fact]
    public void TheLinesOfADoubleStrokeSitAcrossADiagonalLine()
    {
        Offset shift = StrokeGeometry.DoubleOffset(Offset.Zero, new Offset(3, 4), 1);

        // The unit normal to (3, 4) is (-0.8, 0.6), a weight long.
        Approximately.Equal(new Offset(-0.8f, 0.6f), shift);
    }

    [Fact]
    public void ADoubleStrokeOfNoLengthSplitsDownwards()
    {
        Approximately.Equal(new Offset(0, 6), StrokeGeometry.DoubleOffset(new Offset(5, 5), new Offset(5, 5), 6));
    }

    [Fact]
    public void AWaveOfNoLengthHasNoSegments()
    {
        Assert.Empty(StrokeGeometry.Wave(new Offset(5, 5), new Offset(5, 5), 1));
    }

    [Fact]
    public void AWaveIsMadeOfHalfWavesTwiceTheWeightLong()
    {
        IReadOnlyList<CubicSegment> wave = StrokeGeometry.Wave(Offset.Zero, new Offset(20, 0), 1);

        Assert.Equal(10, wave.Count);
        Approximately.Equal(new Offset(2, 0), wave[0].End);
        Approximately.Equal(new Offset(20, 0), wave[9].End);
    }

    [Fact]
    public void TheFirstArchRisesAndTheNextFalls()
    {
        IReadOnlyList<CubicSegment> wave = StrokeGeometry.Wave(Offset.Zero, new Offset(20, 0), 1);

        Approximately.Equal(new Offset(2f / 3f, -Lift), wave[0].Control1);
        Approximately.Equal(new Offset(4f / 3f, -Lift), wave[0].Control2);
        Approximately.Equal(new Offset(2f + (2f / 3f), Lift), wave[1].Control1);
        Approximately.Equal(new Offset(2f + (4f / 3f), Lift), wave[1].Control2);
    }

    [Fact]
    public void TheArchesStretchToEndExactlyAtTheEndOfTheLine()
    {
        // 21 / 4 is 5.25 arches, so five, each 4.2 long.
        IReadOnlyList<CubicSegment> wave = StrokeGeometry.Wave(new Offset(1, 2), new Offset(22, 2), 2);

        Assert.Equal(5, wave.Count);
        Approximately.Equal(new Offset(5.2f, 2), wave[0].End);
        Approximately.Equal(new Offset(22, 2), wave[4].End);
    }

    [Fact]
    public void HalfAnArchOverRoundsUp()
    {
        Assert.Equal(3, StrokeGeometry.Wave(Offset.Zero, new Offset(10, 0), 2).Count);
    }

    [Fact]
    public void AWaveShorterThanAnArchIsStillOneArch()
    {
        CubicSegment arch = Assert.Single(StrokeGeometry.Wave(Offset.Zero, new Offset(1, 0), 2));

        Approximately.Equal(new Offset(1, 0), arch.End);
    }

    [Fact]
    public void AWaveFollowsAVerticalLine()
    {
        IReadOnlyList<CubicSegment> wave = StrokeGeometry.Wave(Offset.Zero, new Offset(0, 8), 1);

        Assert.Equal(4, wave.Count);
        Approximately.Equal(new Offset(Lift, 2f / 3f), wave[0].Control1);
        Approximately.Equal(new Offset(0, 2), wave[0].End);
        Approximately.Equal(new Offset(0, 8), wave[3].End);
    }
}
