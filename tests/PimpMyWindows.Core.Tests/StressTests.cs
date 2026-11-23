using PimpMyWindows.Core.Benchmarks;

namespace PimpMyWindows.Core.Tests;

public class StressTests
{
    private static List<double> Flat(int seconds, double value = 100) => Enumerable.Repeat(value, seconds).ToList();

    [Fact]
    public void Steady_Performance_IsStable()
    {
        var result = StressAnalysis.Evaluate(TimeSpan.FromSeconds(60), Flat(60), 0, completed: true);
        Assert.Equal(StressVerdict.Stable, result.Verdict);
        Assert.Equal(0, result.DropPercent, 3);
    }

    [Fact]
    public void BigDropAtTheEnd_IsThrottling()
    {
        var samples = Flat(40).Concat(Flat(20, 70)).ToList();
        var result = StressAnalysis.Evaluate(TimeSpan.FromSeconds(60), samples, 0, completed: true);
        Assert.Equal(StressVerdict.Throttled, result.Verdict);
        Assert.InRange(result.DropPercent, 29, 31);
    }

    [Fact]
    public void WarmUpSeconds_AreIgnored()
    {
        var samples = new List<double> { 10, 20 };
        samples.AddRange(Flat(30));
        Assert.Equal(0, StressAnalysis.DropPercent(samples), 3);
    }

    [Fact]
    public void FasterAtTheEnd_IsNotADrop()
    {
        var samples = Flat(30).Concat(Flat(10, 130)).ToList();
        Assert.Equal(0, StressAnalysis.DropPercent(samples));
    }

    [Fact]
    public void MemoryErrors_WinOverEverythingElse()
    {
        var samples = Flat(40).Concat(Flat(20, 50)).ToList();
        var result = StressAnalysis.Evaluate(TimeSpan.FromSeconds(60), samples, 3, completed: true);
        Assert.Equal(StressVerdict.MemoryErrors, result.Verdict);
        Assert.Contains("3", result.Text);
    }

    [Fact]
    public void Cancelled_NeverClaimsAConclusion()
    {
        var result = StressAnalysis.Evaluate(TimeSpan.FromSeconds(60), Flat(5), 0, completed: false);
        Assert.Equal(StressVerdict.Cancelled, result.Verdict);
    }

    [Fact]
    public void TooFewSamples_GivesNoDrop() =>
        Assert.Equal(0, StressAnalysis.DropPercent(Flat(4)));

    [Fact]
    public void RealRun_ShortTest_CompletesWithoutMemoryErrors()
    {
        var reports = 0;
        var result = StressTest.Run(TimeSpan.FromSeconds(2), 8 * 1024 * 1024, (_, _) => reports++, CancellationToken.None);
        Assert.Equal(0, result.MemoryErrors);
        Assert.NotEqual(StressVerdict.Cancelled, result.Verdict);
        Assert.True(reports >= 1);
        Assert.All(result.OperationsPerSecond, v => Assert.True(v > 0));
    }

    [Fact]
    public void RealRun_CancelledEarly_ReportsCancelled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var result = StressTest.Run(TimeSpan.FromSeconds(30), 4 * 1024 * 1024, null, cts.Token);
        Assert.Equal(StressVerdict.Cancelled, result.Verdict);
    }
}
