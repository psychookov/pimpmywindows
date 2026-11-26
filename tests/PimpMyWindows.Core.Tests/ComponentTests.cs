using PimpMyWindows.Core.Components;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

public class ComponentTests
{
    private sealed class FakeWindows : IProcessRunner
    {
        public Dictionary<string, string> ServiceStart { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> TaskEnabled { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Delayed { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string[]> Calls { get; } = [];

        public bool RefuseChanges { get; set; }

        public bool IgnoreChanges { get; set; }

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Calls.Add([fileName, .. args]);
            if (fileName == "sc.exe")
            {
                if (args[0] == "qc")
                {
                    if (!ServiceStart.TryGetValue(args[1], out var start))
                    {
                        return Task.FromResult(new ProcessResult(1060, "[SC] OpenService FAILED 1060"));
                    }

                    var text = start switch { "auto" => "2   AUTO_START", "demand" => "3   DEMAND_START", _ => "4   DISABLED" };
                    if (start == "auto" && Delayed.Contains(args[1]))
                    {
                        text += "  (DELAYED)";
                    }

                    return Task.FromResult(new ProcessResult(0, $"SERVICE_NAME: {args[1]}\r\n        TYPE               : 20  WIN32_SHARE_PROCESS\r\n        START_TYPE         : {text}\r\n"));
                }

                if (RefuseChanges)
                {
                    return Task.FromResult(new ProcessResult(5, "Acesso negado."));
                }

                if (!IgnoreChanges)
                {
                    // args: config <name> start= <value>
                    ServiceStart[args[1]] = args[3] == "delayed-auto" ? "auto" : args[3];
                    if (args[3] == "delayed-auto")
                    {
                        Delayed.Add(args[1]);
                    }
                    else
                    {
                        Delayed.Remove(args[1]);
                    }
                }

                return Task.FromResult(new ProcessResult(0, "[SC] ChangeServiceConfig SUCCESS"));
            }

            if (args[0] == "/Query")
            {
                if (!TaskEnabled.TryGetValue(args[2], out var enabled))
                {
                    return Task.FromResult(new ProcessResult(1, string.Empty));
                }

                var xml = "<Task><Triggers><LogonTrigger><Enabled>false</Enabled></LogonTrigger></Triggers><Settings>" +
                          (enabled ? string.Empty : "<Enabled>false</Enabled>") + "<Hidden>true</Hidden></Settings></Task>";
                return Task.FromResult(new ProcessResult(0, xml));
            }

            if (RefuseChanges)
            {
                return Task.FromResult(new ProcessResult(1, "Acesso negado."));
            }

            TaskEnabled[args[2]] = args[3] == "/ENABLE";
            return Task.FromResult(new ProcessResult(0, "SUCCESS"));
        }
    }

    private static string TempState() => Path.Combine(Path.GetTempPath(), "pmw-comp-" + Guid.NewGuid().ToString("N"), "c.json");

    private static ComponentService Service(FakeWindows windows, bool admin = true, string? state = null) =>
        new(windows, state ?? TempState(), () => admin, new NullActionLog());

    private static ManagedComponent Svc(string id) => ComponentCatalog.All.Single(c => c.Id == id);

    [Fact]
    public async Task Service_Apply_DisablesAndRevertRestoresTheRealOriginal()
    {
        var windows = new FakeWindows();
        windows.ServiceStart["SysMain"] = "demand";
        var service = Service(windows);
        var component = Svc("svc.sysmain");

        Assert.Equal(ComponentState.Default, (await service.GetStatusAsync(component)).State);
        Assert.True((await service.ApplyAsync(component)).Success);
        Assert.Equal("disabled", windows.ServiceStart["SysMain"]);
        Assert.Equal(ComponentState.Optimized, (await service.GetStatusAsync(component)).State);

        Assert.True((await service.RevertAsync(component)).Success);
        Assert.Equal("demand", windows.ServiceStart["SysMain"]);
    }

    [Fact]
    public async Task Service_DelayedAutoStart_ComesBackDelayed()
    {
        var windows = new FakeWindows();
        windows.ServiceStart["MapsBroker"] = "auto";
        windows.Delayed.Add("MapsBroker");
        var service = Service(windows);
        var component = Svc("svc.mapsbroker");

        Assert.True((await service.ApplyAsync(component)).Success);
        Assert.True((await service.RevertAsync(component)).Success);

        Assert.Equal("auto", windows.ServiceStart["MapsBroker"]);
        Assert.Contains("MapsBroker", windows.Delayed);
    }

    [Fact]
    public async Task Original_SurvivesRestartingTheProgram()
    {
        var windows = new FakeWindows();
        windows.ServiceStart["lfsvc"] = "demand";
        var state = TempState();
        var component = Svc("svc.geolocation");

        await Service(windows, state: state).ApplyAsync(component);
        // "Reabre" o programa: outra instância, mesmo arquivo de estado.
        Assert.True((await Service(windows, state: state).RevertAsync(component)).Success);
        Assert.Equal("demand", windows.ServiceStart["lfsvc"]);
    }

    [Fact]
    public async Task Apply_WithoutAdmin_ChangesNothing()
    {
        var windows = new FakeWindows();
        windows.ServiceStart["SysMain"] = "auto";
        var result = await Service(windows, admin: false).ApplyAsync(Svc("svc.sysmain"));

        Assert.False(result.Success);
        Assert.DoesNotContain(windows.Calls, c => c.Contains("config"));
        Assert.Equal("auto", windows.ServiceStart["SysMain"]);
    }

    [Fact]
    public async Task Missing_Service_IsReportedAndNotTouched()
    {
        var windows = new FakeWindows();
        var service = Service(windows);
        Assert.Equal(ComponentState.NotPresent, (await service.GetStatusAsync(Svc("svc.retaildemo"))).State);
        Assert.False((await service.ApplyAsync(Svc("svc.retaildemo"))).Success);
        Assert.DoesNotContain(windows.Calls, c => c.Contains("config"));
    }

    [Fact]
    public async Task Windows_Refusing_LeavesNoSavedOriginal_AndReportsFailure()
    {
        var windows = new FakeWindows { RefuseChanges = true };
        windows.ServiceStart["DiagTrack"] = "auto";
        var state = TempState();

        var result = await Service(windows, state: state).ApplyAsync(Svc("svc.diagtrack"));

        Assert.False(result.Success);
        Assert.False(File.Exists(state) && File.ReadAllText(state).Contains("svc.diagtrack"));
    }

    [Fact]
    public async Task Apply_DetectsWhenTheStateDidNotReallyChange()
    {
        var windows = new FakeWindows { IgnoreChanges = true };
        windows.ServiceStart["DiagTrack"] = "auto";
        Assert.False((await Service(windows).ApplyAsync(Svc("svc.diagtrack"))).Success);
    }

    [Fact]
    public async Task Task_ApplyAndRevert_UsesDisableThenEnable()
    {
        var windows = new FakeWindows();
        var component = Svc("task.compat-appraiser");
        windows.TaskEnabled[component.Target] = true;
        var service = Service(windows);

        Assert.Equal(ComponentState.Default, (await service.GetStatusAsync(component)).State);
        Assert.True((await service.ApplyAsync(component)).Success);
        Assert.False(windows.TaskEnabled[component.Target]);
        Assert.Equal(ComponentState.Optimized, (await service.GetStatusAsync(component)).State);

        Assert.True((await service.RevertAsync(component)).Success);
        Assert.True(windows.TaskEnabled[component.Target]);
    }

    [Fact]
    public async Task Task_ThatDoesNotExist_IsNotPresent()
    {
        var service = Service(new FakeWindows());
        Assert.Equal(ComponentState.NotPresent, (await service.GetStatusAsync(Svc("task.maps-update"))).State);
    }

    [Fact]
    public async Task Unsafe_Targets_NeverReachTheProcessRunner()
    {
        var windows = new FakeWindows();
        var service = Service(windows);
        var bad1 = new ManagedComponent("x.a", ComponentKind.Service, "Foo & calc", "n", "s", "d", RiskLevel.Safe, UserMode.Basic);
        var bad2 = new ManagedComponent("x.b", ComponentKind.ScheduledTask, @"\..\Windows\Evil", "n", "s", "d", RiskLevel.Safe, UserMode.Basic);

        Assert.False((await service.ApplyAsync(bad1)).Success);
        Assert.False((await service.ApplyAsync(bad2)).Success);
        Assert.Equal(ComponentState.Unknown, (await service.GetStatusAsync(bad1)).State);
        Assert.Empty(windows.Calls);
    }

    [Fact]
    public void Catalog_IsConsistentAndSafe()
    {
        var all = ComponentCatalog.All;
        Assert.Equal(all.Count, all.Select(c => c.Id).Distinct().Count());
        Assert.Equal(all.Count, all.Select(c => c.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(all, c => Assert.Matches("^[a-z0-9][a-z0-9.-]*$", c.Id));

        foreach (var c in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Summary) || string.IsNullOrWhiteSpace(c.Details), c.Id);
            Assert.True(c.Kind == ComponentKind.Service ? ComponentCatalog.IsSafeServiceName(c.Target) : ComponentCatalog.IsSafeTaskPath(c.Target), c.Id);
            Assert.NotEqual(RiskLevel.Advanced, c.Risk);
            if (c.Risk == RiskLevel.Moderate)
            {
                Assert.True(c.Warning is not null || c.Mode == UserMode.Basic, c.Id);
            }
        }

        Assert.Contains(all, c => c.Kind == ComponentKind.Service);
        Assert.Contains(all, c => c.Kind == ComponentKind.ScheduledTask);
    }

    [Fact]
    public void Catalog_NeverIncludesSecurityOrUpdateComponents()
    {
        var forbiddenWords = new[]
        {
            "WinDefend", "wuauserv", "mpssvc", "SecurityHealth", "Sense", "BITS", "EventLog", "RpcSs", "wscsvc", "WdNisSvc",
            "Windows Defender", "WindowsUpdate", "UpdateOrchestrator", "Windows Update", "Firewall", "BitLocker",
        };
        Assert.All(ComponentCatalog.All, c =>
            Assert.DoesNotContain(forbiddenWords, w => c.Target.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("DiagTrack", true)]
    [InlineData("Foo Bar", false)]
    [InlineData("a&b", false)]
    [InlineData("", false)]
    public void ServiceNameValidation(string name, bool ok) => Assert.Equal(ok, ComponentCatalog.IsSafeServiceName(name));

    [Theory]
    [InlineData(@"\Microsoft\Windows\Maps\MapsUpdateTask", true)]
    [InlineData(@"\..\x\y", false)]
    [InlineData(@"Microsoft\x", false)]
    [InlineData(@"\Só", false)]
    [InlineData(@"\a\b""; calc", false)]
    public void TaskPathValidation(string path, bool ok) => Assert.Equal(ok, ComponentCatalog.IsSafeTaskPath(path));
}
