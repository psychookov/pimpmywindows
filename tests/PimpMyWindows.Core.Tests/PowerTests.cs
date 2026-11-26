using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Power;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tests;

public class PowerTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string Saver = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string Custom = "11111111-2222-3333-4444-555555555555";

    private sealed class FakePowercfg : IProcessRunner
    {
        public string ActiveId { get; set; } = Balanced;

        public List<string> Plans { get; } = [Balanced, Saver, Custom];

        public Dictionary<string, string> Names { get; } = new()
        {
            [Balanced] = "Equilibrado",
            [Saver] = "Economia de energia",
            [Custom] = "Meu plano",
        };

        public bool FailDuplicate { get; set; }

        public List<string[]> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Calls.Add(args);
            switch (args[0])
            {
                case "/list":
                    var lines = Plans.Select(
                        id => $"GUID do Esquema de Energia: {id}  ({Names.GetValueOrDefault(id, "Novo")}){(id == ActiveId ? " *" : string.Empty)}");
                    return Task.FromResult(new ProcessResult(0, "Esquemas de energia existentes (* Ativo)\r\n-----\r\n" + string.Join("\r\n", lines)));
                case "/setactive":
                    ActiveId = args[1];
                    return Task.FromResult(new ProcessResult(0, string.Empty));
                case "/duplicatescheme":
                    if (FailDuplicate)
                    {
                        return Task.FromResult(new ProcessResult(1, "Parâmetro inválido."));
                    }

                    Plans.Add(args[2]);
                    Names[args[2]] = "Desempenho máximo";
                    return Task.FromResult(new ProcessResult(0, string.Empty));
                default:
                    return Task.FromResult(new ProcessResult(1, "?"));
            }
        }
    }

    private static string TempState() =>
        Path.Combine(Path.GetTempPath(), "pmw-power-" + Guid.NewGuid().ToString("N"), "power.json");

    [Fact]
    public void Parse_ReadsGuidNameAndActiveFlag_InAnyLanguage()
    {
        var plans = PowerPlanService.Parse(
            "Power Scheme GUID: " + Balanced + "  (Balanced) *\r\nPower Scheme GUID: " + Saver + "  (Power saver)\r\nlixo qualquer\r\n");
        Assert.Equal(2, plans.Count);
        Assert.True(plans[0].IsActive);
        Assert.Equal(PowerPlanKind.Balanced, plans[0].Kind);
        Assert.False(plans[1].IsActive);
        Assert.Equal("Power saver", plans[1].Name);
        Assert.Equal(PowerPlanKind.PowerSaver, plans[1].Kind);
    }

    [Fact]
    public void Parse_NamesWithParentheses_KeepTheWholeName()
    {
        var plans = PowerPlanService.Parse("GUID: " + Custom + "  (Jogos (alto desempenho)) *");
        Assert.Equal("Jogos (alto desempenho)", Assert.Single(plans).Name);
    }

    [Fact]
    public async Task Activate_SavesPreviousAndRestoreGoesBack()
    {
        var runner = new FakePowercfg();
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());

        Assert.True((await service.ActivateAsync(Guid.Parse(Saver))).Success);
        Assert.Equal(Saver, runner.ActiveId);
        Assert.Equal(Guid.Parse(Balanced), service.PreviousPlanId());

        Assert.True((await service.RestorePreviousAsync()).Success);
        Assert.Equal(Balanced, runner.ActiveId);
        Assert.Null(service.PreviousPlanId());
    }

    [Fact]
    public async Task Activate_AlreadyActive_DoesNothingAndKeepsNoPrevious()
    {
        var runner = new FakePowercfg();
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());

        var result = await service.ActivateAsync(Guid.Parse(Balanced));

        Assert.True(result.Success);
        Assert.DoesNotContain(runner.Calls, c => c[0] == "/setactive");
        Assert.Null(service.PreviousPlanId());
    }

    [Fact]
    public async Task Activate_UnknownPlan_IsRefusedWithoutCallingSetActive()
    {
        var runner = new FakePowercfg();
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());

        var result = await service.ActivateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.DoesNotContain(runner.Calls, c => c[0] == "/setactive");
    }

    [Fact]
    public async Task Restore_WhenPreviousPlanWasDeleted_FailsClearly()
    {
        var runner = new FakePowercfg();
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());
        await service.ActivateAsync(Guid.Parse(Custom));
        runner.Plans.Remove(Balanced);

        var result = await service.RestorePreviousAsync();

        Assert.False(result.Success);
        Assert.Equal(Custom, runner.ActiveId);
        Assert.Null(service.PreviousPlanId());
    }

    [Fact]
    public async Task Restore_WithoutSavedPrevious_Fails()
    {
        var service = new PowerPlanService(new FakePowercfg(), TempState(), new NullActionLog());
        Assert.False((await service.RestorePreviousAsync()).Success);
    }

    [Fact]
    public async Task AddUltimate_DuplicatesTemplateWithOurGuid_ThenActivates()
    {
        var runner = new FakePowercfg();
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());

        var result = await service.AddUltimatePerformanceAsync();

        Assert.True(result.Success);
        var duplicate = Assert.Single(runner.Calls, c => c[0] == "/duplicatescheme");
        Assert.Equal(PowerPlan.UltimateTemplateId.ToString("D"), duplicate[1]);
        Assert.Equal(duplicate[2], runner.ActiveId);
        Assert.Equal(Guid.Parse(Balanced), service.PreviousPlanId());
    }

    [Fact]
    public async Task AddUltimate_WhenWindowsRefuses_ChangesNothing()
    {
        var runner = new FakePowercfg { FailDuplicate = true };
        var service = new PowerPlanService(runner, TempState(), new NullActionLog());

        var result = await service.AddUltimatePerformanceAsync();

        Assert.False(result.Success);
        Assert.Equal(Balanced, runner.ActiveId);
        Assert.DoesNotContain(runner.Calls, c => c[0] == "/setactive");
    }
}
