using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tests;

public class DiagnosticsTests
{
    private sealed class FakeQuery : ISystemQuery
    {
        private readonly List<(string Contains, Func<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Rows)> _routes = [];

        public FakeQuery When(string contains, params Dictionary<string, object?>[] rows)
        {
            _routes.Add((contains, () => rows));
            return this;
        }

        public FakeQuery Fail(string contains)
        {
            _routes.Add((contains, () => throw new InvalidOperationException("sem resposta")));
            return this;
        }

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string scope, string wql)
        {
            var route = _routes.FirstOrDefault(r => wql.Contains(r.Contains, StringComparison.Ordinal));
            return route.Rows is null ? [] : route.Rows();
        }
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void QueryRow_ReadsNumbersDatesAndArrays()
    {
        var row = Row(("A", (ushort)7), ("B", "texto"), ("C", new DateTime(2024, 1, 2)), ("D", new ushort[] { 2, 3 }), ("E", true));

        Assert.Equal(7, row.Long("A"));
        Assert.Null(row.Long("B"));
        Assert.Equal(new DateTime(2024, 1, 2), row.Date("C"));
        Assert.Equal(new long[] { 2, 3 }, row.Longs("D"));
        Assert.Equal(true, row.Bool("E"));
        Assert.Equal(string.Empty, row.Text("faltando"));
    }

    [Fact]
    public void Hardware_ParsesTheMachine_AndKeepsGoingWhenASectionFails()
    {
        var query = new FakeQuery()
            .When("Win32_OperatingSystem", Row(("Caption", "Microsoft Windows 11 Pro"), ("Version", "10.0.26100"), ("BuildNumber", "26100"), ("OSArchitecture", "64 bits")))
            .When("Win32_Processor", Row(("Name", "AMD  Ryzen 5   5600"), ("NumberOfCores", 6u), ("NumberOfLogicalProcessors", 12u), ("MaxClockSpeed", 3500u)))
            .When("Win32_PhysicalMemory",
                Row(("Capacity", 8589934592UL), ("Speed", 3200u), ("Manufacturer", "Kingston")),
                Row(("Capacity", 8589934592UL), ("ConfiguredClockSpeed", 3000u), ("Speed", 3200u), ("Manufacturer", "Kingston")))
            .When("Win32_VideoController", Row(("Name", "NVIDIA GeForce RTX 3060"), ("DriverVersion", "31.0.15.5222"), ("DriverDate", new DateTime(2024, 5, 1))))
            .Fail("Win32_BaseBoard");

        var snapshot = new HardwareService(query).Collect();

        Assert.Equal("AMD Ryzen 5 5600", snapshot.CpuName);
        Assert.Equal(6, snapshot.CpuCores);
        Assert.Equal(12, snapshot.CpuThreads);
        Assert.Equal(16L * 1024 * 1024 * 1024, snapshot.RamTotalBytes);
        Assert.Equal(new int?[] { 3200, 3000 }, snapshot.RamModules.Select(m => m.SpeedMhz).ToArray());
        Assert.Single(snapshot.Gpus);
        Assert.Equal(string.Empty, snapshot.Board);
        Assert.Single(snapshot.Errors);
    }

    [Theory]
    [InlineData(512L, "512 B")]
    [InlineData(1024L * 1024 * 1024 * 16, "16 GB")]
    [InlineData(1024L * 1024 * 1024 * 1536, "1,5 TB")]
    public void FormatBytes_UsesReadableUnits(long bytes, string expected)
    {
        var text = HardwareService.FormatBytes(bytes);
        Assert.Equal(expected.Replace(',', '.'), text.Replace(',', '.'));
    }

    [Fact]
    public void Drivers_OldRule_OnlyWatchesRelevantClasses_AndIgnoresMicrosoft()
    {
        var now = new DateTime(2026, 11, 1);
        var old = new DateTime(2020, 1, 1);

        Assert.True(DriverService.IsOldAndWorthWatching(new("Placa", "NVIDIA", "1", old, "DISPLAY", true, false), now));
        Assert.False(DriverService.IsOldAndWorthWatching(new("Placa", "Microsoft", "1", old, "DISPLAY", true, false), now));
        Assert.False(DriverService.IsOldAndWorthWatching(new("Teclado", "Logitech", "1", old, "KEYBOARD", true, false), now));
        Assert.False(DriverService.IsOldAndWorthWatching(new("Placa", "NVIDIA", "1", new DateTime(2026, 1, 1), "DISPLAY", true, false), now));
        Assert.False(DriverService.IsOldAndWorthWatching(new("Placa", "NVIDIA", "1", null, "DISPLAY", true, false), now));
    }

    [Fact]
    public void Drivers_ListsDriversAndProblems_AndReportsQueryFailures()
    {
        var query = new FakeQuery()
            .When("Win32_PnPSignedDriver",
                Row(("DeviceName", "Wi-Fi"), ("Manufacturer", "Intel"), ("DriverVersion", "22.1"), ("DriverDate", new DateTime(2019, 3, 3)), ("DeviceClass", "NET"), ("IsSigned", true)),
                Row(("DeviceName", ""), ("Manufacturer", "x")))
            .When("Win32_PnPEntity", Row(("Name", "Dispositivo desconhecido"), ("ConfigManagerErrorCode", 28u)));

        var report = new DriverService(query, () => new DateTime(2026, 11, 1)).Load();

        var driver = Assert.Single(report.Drivers);
        Assert.True(driver.IsOldWatched);
        var problem = Assert.Single(report.Problems);
        Assert.Equal(28, problem.ErrorCode);
        Assert.Contains("não estão instalados", problem.Description);
        Assert.Empty(report.Errors);

        var broken = new DriverService(new FakeQuery().Fail("Win32_PnPSignedDriver")).Load();
        Assert.Single(broken.Errors);
    }

    [Fact]
    public void DiskHealth_JudgesWearTemperatureAndWindowsStatus()
    {
        var healthy = DiskHealthService.Build(
            Row(("FriendlyName", "Samsung SSD"), ("MediaType", 4u), ("BusType", 17u), ("HealthStatus", 0u), ("Size", 500L * 1000 * 1000 * 1000)),
            Row(("Temperature", 38u), ("Wear", 4u), ("ReadErrorsTotal", 0u), ("WriteErrorsTotal", 0u)));
        Assert.Equal(DiskVerdict.Healthy, healthy.Verdict);
        Assert.Equal("SSD", healthy.MediaType);
        Assert.Equal("NVMe", healthy.Bus);
        Assert.Equal(4, healthy.WearPercent);

        var worn = DiskHealthService.Build(Row(("HealthStatus", 0u)), Row(("Wear", 95u)));
        Assert.Equal(DiskVerdict.Attention, worn.Verdict);
        Assert.NotEmpty(worn.Notes);

        var failing = DiskHealthService.Build(Row(("HealthStatus", 2u)), null);
        Assert.Equal(DiskVerdict.Problem, failing.Verdict);

        var unknown = DiskHealthService.Build(Row(("FriendlyName", "?")), null);
        Assert.Equal(DiskVerdict.Unknown, unknown.Verdict);
        Assert.Null(unknown.TemperatureC);

        var errors = DiskHealthService.Build(Row(("HealthStatus", 0u)), Row(("ReadErrorsTotal", 3u)));
        Assert.Equal(DiskVerdict.Attention, errors.Verdict);
    }

    [Fact]
    public void DiskHealth_MatchesCountersByDeviceId_AndSurvivesMissingCounters()
    {
        var query = new FakeQuery()
            .When("MSFT_PhysicalDisk", Row(("DeviceId", "0"), ("FriendlyName", "Disco A"), ("HealthStatus", 0u)))
            .When("MSFT_StorageReliabilityCounter", Row(("DeviceId", "0"), ("Wear", 12u)));
        Assert.Equal(12, Assert.Single(new DiskHealthService(query).Load().Disks).WearPercent);

        var noCounters = new FakeQuery()
            .When("MSFT_PhysicalDisk", Row(("DeviceId", "0"), ("FriendlyName", "Disco A"), ("HealthStatus", 0u)))
            .Fail("MSFT_StorageReliabilityCounter");
        var report = new DiskHealthService(noCounters).Load();
        Assert.Single(report.Disks);
        Assert.Single(report.Errors);
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public List<(string File, string[] Args)> Calls { get; } = [];

        public int ExitCode { get; set; }

        public Action<string[]>? OnRun { get; set; }

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Calls.Add((fileName, args));
            OnRun?.Invoke(args);
            return Task.FromResult(new ProcessResult(ExitCode, string.Empty));
        }
    }

    private static string TempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task DriverBackup_RequiresAdmin_AndWritesMarkerAndCallsPnpUtil()
    {
        var parent = TempFolder();
        try
        {
            var runner = new FakeRunner { OnRun = args => Directory.CreateDirectory(Path.Combine(args[2], "oem1.inf_amd64_x")) };
            var denied = await new DriverBackupService(runner, () => false, new NullActionLog()).BackupAsync(parent);
            Assert.False(denied.Success);
            Assert.Empty(runner.Calls);

            var service = new DriverBackupService(runner, () => true, new NullActionLog(), () => new DateTime(2026, 11, 20, 14, 30, 0));
            var result = await service.BackupAsync(parent);

            Assert.True(result.Success, result.Message);
            var call = Assert.Single(runner.Calls);
            Assert.Equal("pnputil.exe", call.File);
            Assert.Equal("/export-driver", call.Args[0]);
            Assert.Equal("*", call.Args[1]);
            Assert.EndsWith("Drivers-2026-11-20-1430", call.Args[2]);
            Assert.True(File.Exists(Path.Combine(call.Args[2], DriverBackupService.MarkerFileName)));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task DriverBackup_ReportsPnpUtilFailure()
    {
        var parent = TempFolder();
        try
        {
            var runner = new FakeRunner { ExitCode = 5 };
            var result = await new DriverBackupService(runner, () => true, new NullActionLog()).BackupAsync(parent);
            Assert.False(result.Success);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task DriverRestore_OnlyAcceptsFoldersMadeByTheApp()
    {
        var folder = TempFolder();
        try
        {
            var runner = new FakeRunner();
            var service = new DriverBackupService(runner, () => true, new NullActionLog());

            var rejected = await service.RestoreAsync(folder);
            Assert.False(rejected.Success);
            Assert.Empty(runner.Calls);

            File.WriteAllText(Path.Combine(folder, DriverBackupService.MarkerFileName), "x");
            runner.ExitCode = 3010;
            var accepted = await service.RestoreAsync(folder);

            Assert.True(accepted.Success);
            Assert.Contains("Reinicie", accepted.Message);
            var call = Assert.Single(runner.Calls);
            Assert.Equal(new[] { "/add-driver", Path.Combine(folder, "*.inf"), "/subdirs", "/install" }, call.Args);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SupportReport_ContainsTheEssentials_WithoutPersonalData()
    {
        var snapshot = new HardwareSnapshot(
            "Windows 11 Pro", "10.0.26100", "64 bits", "ASUS B450", "Ryzen 5 5600", 6, 12, 3500,
            16L * 1024 * 1024 * 1024, [new MemoryModule(8L * 1024 * 1024 * 1024, 3200, "Kingston")], [new GpuInfo("RTX 3060", "31.0", null)], []);

        var text = SupportReport.Build(snapshot, new DriverReport([], [new DeviceProblem("Aparelho X", 28, "sem driver")], []), null);

        Assert.Contains("Ryzen 5 5600", text);
        Assert.Contains("RTX 3060", text);
        Assert.Contains("Aparelho X", text);
        Assert.DoesNotContain(Environment.MachineName, text);
        Assert.DoesNotContain(Environment.UserName, text);
    }
}
