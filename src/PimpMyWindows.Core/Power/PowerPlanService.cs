using System.Text.Json;
using System.Text.RegularExpressions;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Power;

public enum PowerPlanKind
{
    Other,
    Balanced,
    HighPerformance,
    PowerSaver,
    Ultimate,
}

public sealed record PowerPlan(Guid Id, string Name, bool IsActive)
{
    public static readonly Guid BalancedId = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformanceId = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid PowerSaverId = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid UltimateTemplateId = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    /// <summary>Os planos de fábrica têm o mesmo identificador em qualquer idioma do Windows; o nome é que muda.</summary>
    public PowerPlanKind Kind => Id == BalancedId ? PowerPlanKind.Balanced
        : Id == HighPerformanceId ? PowerPlanKind.HighPerformance
        : Id == PowerSaverId ? PowerPlanKind.PowerSaver
        : PowerPlanKind.Other;
}

public sealed record PowerResult(bool Success, string Message);

/// <summary>
/// Planos de energia pelo <c>powercfg</c>, que é a ferramenta do próprio Windows. Trocar de plano é reversível:
/// o plano anterior fica guardado e dá para voltar com um clique, até depois de fechar o programa.
/// </summary>
public sealed partial class PowerPlanService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly IProcessRunner _runner;
    private readonly string _statePath;
    private readonly IActionLog _log;

    public PowerPlanService(IProcessRunner runner, string statePath, IActionLog log)
    {
        _runner = runner;
        _statePath = statePath;
        _log = log;
    }

    public async Task<IReadOnlyList<PowerPlan>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync("powercfg.exe", ["/list"], Timeout, cancellationToken);
        return result.ExitCode == 0 ? Parse(result.Output) : [];
    }

    /// <summary>Lê a saída de <c>powercfg /list</c>, que muda de texto conforme o idioma mas sempre traz GUID, nome entre parênteses e * no ativo.</summary>
    public static IReadOnlyList<PowerPlan> Parse(string output)
    {
        var plans = new List<PowerPlan>();
        foreach (var line in output.Split('\n'))
        {
            var match = PlanLine().Match(line);
            if (match.Success && Guid.TryParse(match.Groups[1].Value, out var id))
            {
                plans.Add(new PowerPlan(id, match.Groups[2].Value.Trim(), match.Groups[3].Success));
            }
        }

        return plans;
    }

    public async Task<PowerResult> ActivateAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plans = await ListAsync(cancellationToken);
        var target = plans.FirstOrDefault(p => p.Id == planId);
        if (target is null)
        {
            return new PowerResult(false, "Esse plano de energia não existe mais neste computador.");
        }

        var current = plans.FirstOrDefault(p => p.IsActive);
        if (current is not null && current.Id == planId)
        {
            return new PowerResult(true, $"“{target.Name}” já é o plano ativo.");
        }

        if (current is not null)
        {
            SavePrevious(current.Id);
        }

        var result = await _runner.RunAsync("powercfg.exe", ["/setactive", planId.ToString("D")], Timeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            return new PowerResult(false, "O Windows não aceitou a troca de plano. " + Tail(result.Output));
        }

        _log.Write($"Plano de energia: {current?.Name ?? "?"} -> {target.Name}");
        return new PowerResult(true, $"Plano “{target.Name}” ativado.");
    }

    /// <summary>Cria o plano “Desempenho máximo” (modelo oculto do Windows) e o ativa. Não mexe nos planos que já existem.</summary>
    public async Task<PowerResult> AddUltimatePerformanceAsync(CancellationToken cancellationToken = default)
    {
        var newId = Guid.NewGuid();
        var result = await _runner.RunAsync(
            "powercfg.exe",
            ["/duplicatescheme", PowerPlan.UltimateTemplateId.ToString("D"), newId.ToString("D")],
            Timeout,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            return new PowerResult(
                false,
                "Este Windows não oferece o plano Desempenho máximo (comum em edições mais simples). " + Tail(result.Output));
        }

        _log.Write($"Plano de energia criado a partir do modelo Desempenho máximo ({newId:D})");
        var activated = await ActivateAsync(newId, cancellationToken);
        return activated.Success
            ? new PowerResult(true, "Plano Desempenho máximo criado e ativado. Para voltar, use “Voltar ao plano anterior”.")
            : activated;
    }

    public Guid? PreviousPlanId()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<PowerState>(File.ReadAllText(_statePath));
            return state?.Previous;
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is JsonException)
        {
            return null;
        }
    }

    public async Task<PowerResult> RestorePreviousAsync(CancellationToken cancellationToken = default)
    {
        var previous = PreviousPlanId();
        if (previous is null)
        {
            return new PowerResult(false, "Não há plano anterior guardado.");
        }

        var plans = await ListAsync(cancellationToken);
        if (plans.All(p => p.Id != previous))
        {
            ClearPrevious();
            return new PowerResult(false, "O plano anterior foi removido do Windows. Escolha outro na lista.");
        }

        var result = await _runner.RunAsync("powercfg.exe", ["/setactive", previous.Value.ToString("D")], Timeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            return new PowerResult(false, "O Windows não aceitou a troca de plano. " + Tail(result.Output));
        }

        ClearPrevious();
        _log.Write($"Plano de energia restaurado: {previous:D}");
        return new PowerResult(true, "Plano anterior restaurado.");
    }

    private void SavePrevious(Guid id)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(new PowerState(id)));
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            // Sem o arquivo só perde o atalho de voltar; a troca em si continua valendo.
        }
    }

    private void ClearPrevious()
    {
        try
        {
            File.Delete(_statePath);
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            // Nada a fazer.
        }
    }

    private static string Tail(string output)
    {
        var text = output.Trim();
        return text.Length > 200 ? text[..200] : text;
    }

    [GeneratedRegex(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\((.+?)\)(\s*(\*))?\s*$")]
    private static partial Regex PlanLine();

    private sealed record PowerState(Guid Previous);
}
