using PimpMyWindows.Core.Controllers;

namespace PimpMyWindows.Core.Tests;

public class ControllerTests
{
    [Fact]
    public void Normalize_StaysInRange()
    {
        Assert.Equal(1.0, ControllerAnalysis.Normalize(short.MaxValue));
        Assert.Equal(-1.0, ControllerAnalysis.Normalize(short.MinValue));
        Assert.Equal(0.0, ControllerAnalysis.Normalize(0));
    }

    [Fact]
    public void Deflection_IsDistanceFromCenter_CappedAtOne()
    {
        Assert.Equal(0.0, ControllerAnalysis.Deflection(0, 0));
        Assert.Equal(1.0, ControllerAnalysis.Deflection(short.MaxValue, short.MaxValue));
        Assert.InRange(ControllerAnalysis.Deflection(16384, 0), 0.49, 0.51);
    }

    [Fact]
    public void Drift_QuietStickIsGood()
    {
        var result = ControllerAnalysis.EvaluateDrift([(0, 0), (120, -80), (-200, 150)]);
        Assert.Equal(DriftVerdict.Good, result.Verdict);
    }

    [Fact]
    public void Drift_SmallOffsetIsSlight_AndBigOffsetIsDrifting()
    {
        Assert.Equal(DriftVerdict.Slight, ControllerAnalysis.EvaluateDrift([(0, 0), (3000, 0)]).Verdict);
        Assert.Equal(DriftVerdict.Drifting, ControllerAnalysis.EvaluateDrift([(0, 0), (0, -8000)]).Verdict);
    }

    [Fact]
    public void Drift_UsesTheWorstMomentNotTheAverage()
    {
        var samples = Enumerable.Repeat(((short)0, (short)0), 50).ToList();
        samples.Add(((short)9000, (short)0));
        Assert.Equal(DriftVerdict.Drifting, ControllerAnalysis.EvaluateDrift(samples).Verdict);
    }

    [Fact]
    public void Drift_NoSamplesIsGoodAndDoesNotThrow() =>
        Assert.Equal(DriftVerdict.Good, ControllerAnalysis.EvaluateDrift([]).Verdict);

    [Fact]
    public void PressedNames_ListsOnlyPressedButtons()
    {
        var names = ControllerAnalysis.PressedNames(ControllerButtons.A | ControllerButtons.DPadLeft);
        Assert.Equal(new[] { "A", "Esquerda" }, names.ToArray());
        Assert.Empty(ControllerAnalysis.PressedNames(ControllerButtons.None));
    }

    [Fact]
    public void ButtonValues_MatchXInputBits()
    {
        Assert.Equal(0x1000, (int)ControllerButtons.A);
        Assert.Equal(0x0100, (int)ControllerButtons.LeftBumper);
        Assert.Equal(14, ControllerAnalysis.Names.Count);
    }
}
