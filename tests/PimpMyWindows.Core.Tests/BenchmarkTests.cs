using System.Net;
using PimpMyWindows.Core.Benchmarks;
using PimpMyWindows.Core.Diagnostics;

namespace PimpMyWindows.Core.Tests;

public class BenchmarkTests
{
    [Fact]
    public void Comparison_ComputesPercent_AndDescribesDirection()
    {
        Assert.Null(BenchmarkComparison.PercentChange(null, 10));
        Assert.Null(BenchmarkComparison.PercentChange(0, 10));
        Assert.Equal(10, BenchmarkComparison.PercentChange(100, 110)!.Value, 6);

        Assert.Equal("primeiro teste", BenchmarkComparison.Describe(null, true));
        Assert.Equal("igual ao anterior", BenchmarkComparison.Describe(2, true));
        Assert.Equal("melhorou 10%", BenchmarkComparison.Describe(10, true));
        Assert.Equal("piorou 10%", BenchmarkComparison.Describe(10, false));
        Assert.Equal("melhorou 20%", BenchmarkComparison.Describe(-20, false));
    }

    [Fact]
    public void History_StoresResults_ReturnsLatest_AndSurvivesCorruption()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmw-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "history.json");
        try
        {
            var store = new BenchmarkHistoryStore(path);
            Assert.Empty(store.Load());

            store.Add([new BenchmarkResult("cpu.single", "CPU", 100, "x", true)], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            store.Add([new BenchmarkResult("cpu.single", "CPU", 120, "x", true)], new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal(2, store.Load().Count);
            Assert.Equal(120, store.Latest()["cpu.single"]);

            File.WriteAllText(path, "{corrompido");
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void NetworkStats_ComputesAverageJitterAndLoss()
    {
        var stats = NetworkStats.Compute([10, 20, null, 10]);

        Assert.Equal(4, stats.Sent);
        Assert.Equal(3, stats.Received);
        Assert.Equal(25, stats.LossPercent, 6);
        Assert.Equal(40.0 / 3, stats.AverageMs!.Value, 6);
        Assert.Equal(10, stats.MinMs);
        Assert.Equal(20, stats.MaxMs);
        Assert.Equal(10, stats.JitterMs!.Value, 6);

        var none = NetworkStats.Compute([null, null]);
        Assert.Null(none.AverageMs);
        Assert.Equal(100, none.LossPercent);
    }

    private sealed class FakeProbe : INetworkProbe
    {
        public IPAddress? Gateway { get; init; }

        public List<IPAddress> Pinged { get; } = [];

        public IPAddress? DefaultGateway() => Gateway;

        public Task<long?> PingAsync(IPAddress address, int timeoutMs, CancellationToken cancellationToken)
        {
            Pinged.Add(address);
            return Task.FromResult<long?>(12);
        }

        public Task<long?> ResolveAsync(string host, CancellationToken cancellationToken) => Task.FromResult<long?>(30);
    }

    [Fact]
    public async Task NetworkTester_TestsGatewayPublicServersAndDns()
    {
        var probe = new FakeProbe { Gateway = IPAddress.Parse("192.168.0.1") };

        var report = await new NetworkTester(probe).RunAsync(null, CancellationToken.None);

        Assert.NotNull(report.Gateway);
        Assert.Equal(2, report.Targets.Count);
        Assert.Equal(3, report.Dns.Count);
        Assert.All(report.Targets, t => Assert.Equal(0, t.Stats.LossPercent));
        Assert.Equal(3 * NetworkTester.Samples, probe.Pinged.Count);
    }

    [Fact]
    public async Task NetworkTester_SkipsGatewayWhenThereIsNone()
    {
        var report = await new NetworkTester(new FakeProbe()).RunAsync(null, CancellationToken.None);

        Assert.Null(report.Gateway);
        Assert.Equal(2, report.Targets.Count);
    }

    [Fact]
    public void Cpu_RunsForAShortTime_AndReportsPositiveResults()
    {
        var results = CpuMemoryBenchmark.RunCpu(TimeSpan.FromMilliseconds(200), CancellationToken.None);

        Assert.Equal(new[] { "cpu.single", "cpu.multi" }, results.Select(r => r.Id).ToArray());
        Assert.All(results, r => Assert.True(r.Value > 0));
    }

    [Fact]
    public void Cpu_StopsWhenCancelled()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => CpuMemoryBenchmark.RunCpu(TimeSpan.FromSeconds(5), cancel.Token));
    }

    [Fact]
    public void Memory_ReportsBandwidthAndLatency()
    {
        var results = CpuMemoryBenchmark.RunMemory(TimeSpan.FromMilliseconds(200), CancellationToken.None, bufferBytes: 8 * 1024 * 1024);

        Assert.Equal(new[] { "mem.copy", "mem.latency" }, results.Select(r => r.Id).ToArray());
        Assert.All(results, r => Assert.True(r.Value > 0));
    }

    [Fact]
    public void Disk_MeasuresWithATemporaryFile_AndCleansUp()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var result = DiskBenchmark.Run(8L * 1024 * 1024, TimeSpan.FromMilliseconds(200), CancellationToken.None, folder);

            Assert.True(result.Success, result.Message);
            Assert.Equal(4, result.Value!.Count);
            Assert.All(result.Value, r => Assert.True(r.Value > 0, r.Id));
            Assert.False(Directory.Exists(Path.Combine(folder, "PimpMyWindows-bench")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Disk_RejectsAnInvalidSize()
    {
        var result = DiskBenchmark.Run(10, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(result.Success);
    }

    private sealed class FakeQuery : ISystemQuery
    {
        public Dictionary<string, List<Dictionary<string, object?>>> Rows { get; } = [];

        public HashSet<string> Failing { get; } = [];

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string scope, string wql)
        {
            if (Failing.Any(f => wql.Contains(f, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("sem resposta");
            }

            var key = Rows.Keys.FirstOrDefault(k => wql.Contains(k, StringComparison.Ordinal));
            return key is null ? [] : Rows[key].Cast<IReadOnlyDictionary<string, object?>>().ToList();
        }
    }

    [Fact]
    public void Battery_ReportsNoBatteryOnDesktops()
    {
        Assert.False(new BatteryService(new FakeQuery()).Read().HasBattery);
    }

    [Fact]
    public void Battery_ComputesWearFromDesignedAndFullCapacity()
    {
        var query = new FakeQuery();
        query.Rows["Win32_Battery"] = [new() { ["EstimatedChargeRemaining"] = 80u, ["BatteryStatus"] = 2u }];
        query.Rows["BatteryStaticData"] = [new() { ["DesignedCapacity"] = 50000u }];
        query.Rows["BatteryFullChargedCapacity"] = [new() { ["FullChargedCapacity"] = 40000u }];
        query.Rows["BatteryCycleCount"] = [new() { ["CycleCount"] = 0u }];

        var info = new BatteryService(query).Read();

        Assert.True(info.HasBattery);
        Assert.Equal(80, info.ChargePercent);
        Assert.Equal("Na tomada", info.Status);
        Assert.Equal(20, info.WearPercent!.Value, 6);
        Assert.Null(info.Cycles);
    }

    [Fact]
    public void Battery_SurvivesMissingCapacityData()
    {
        var query = new FakeQuery();
        query.Rows["Win32_Battery"] = [new() { ["EstimatedChargeRemaining"] = 50u, ["BatteryStatus"] = 1u }];
        query.Failing.Add("BatteryStaticData");

        var info = new BatteryService(query).Read();

        Assert.True(info.HasBattery);
        Assert.Null(info.WearPercent);
        Assert.NotEmpty(info.Notes);
    }
}
