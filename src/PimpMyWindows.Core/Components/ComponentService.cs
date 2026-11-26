using System.Text.Json;
using System.Text.RegularExpressions;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Components;

/// <summary>
/// Lê e altera serviços (<c>sc.exe</c>) e tarefas agendadas (<c>schtasks.exe</c>), as ferramentas do próprio Windows.
/// Antes de mexer, guarda como o item estava, para reverter ao estado real do seu computador (e não a um padrão suposto).
/// </summary>
public sealed partial class ComponentService
{
    private const int DelayedAuto = 5;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly IProcessRunner _runner;
    private readonly string _statePath;
    private readonly Func<bool> _isAdmin;
    private readonly IActionLog _log;
    private readonly object _stateLock = new();

    public ComponentService(IProcessRunner runner, string statePath, Func<bool> isAdmin, IActionLog log)
    {
        _runner = runner;
        _statePath = statePath;
        _isAdmin = isAdmin;
        _log = log;
    }

    public async Task<ComponentStatus> GetStatusAsync(ManagedComponent component, CancellationToken cancellationToken = default)
    {
        if (!IsTargetSafe(component))
        {
            return new ComponentStatus(ComponentState.Unknown, "Identificador inválido.");
        }

        try
        {
            return component.Kind == ComponentKind.Service
                ? await ServiceStatusAsync(component, cancellationToken)
                : await TaskStatusAsync(component, cancellationToken);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException or InvalidOperationException)
        {
            return new ComponentStatus(ComponentState.Unknown, ex.Message);
        }
    }

    public async Task<ComponentResult> ApplyAsync(ManagedComponent component, CancellationToken cancellationToken = default)
    {
        if (!IsTargetSafe(component))
        {
            return new ComponentResult(false, "Identificador inválido: nada foi alterado.");
        }

        if (!_isAdmin())
        {
            return new ComponentResult(false, "Abra o programa como administrador para alterar serviços e tarefas.");
        }

        var before = await GetStatusAsync(component, cancellationToken);
        switch (before.State)
        {
            case ComponentState.NotPresent:
                return new ComponentResult(false, "Este item não existe neste Windows. Nada foi alterado.");
            case ComponentState.Unknown:
                return new ComponentResult(false, "Não foi possível ler o estado atual, então nada foi alterado. " + before.Detail);
            case ComponentState.Optimized:
                return new ComponentResult(true, "Já está desativado.");
        }

        var original = component.Kind == ComponentKind.Service ? CurrentStartValue(before) : 1;
        if (original is null)
        {
            return new ComponentResult(false, "Não foi possível ler como o serviço inicia hoje, então nada foi alterado.");
        }

        SaveOriginal(component.Id, original.Value);
        var run = component.Kind == ComponentKind.Service
            ? await _runner.RunAsync("sc.exe", ["config", component.Target, "start=", "disabled"], Timeout, cancellationToken)
            : await _runner.RunAsync("schtasks.exe", ["/Change", "/TN", component.Target, "/DISABLE"], Timeout, cancellationToken);

        if (run.ExitCode != 0)
        {
            ClearOriginal(component.Id);
            return new ComponentResult(false, "O Windows recusou a alteração. " + Tail(run.Output));
        }

        var after = await GetStatusAsync(component, cancellationToken);
        if (after.State != ComponentState.Optimized)
        {
            return new ComponentResult(false, "O Windows aceitou o comando, mas o estado não mudou. Nada mais foi feito.");
        }

        _log.Write($"Componente '{component.Id}' desativado (original guardado: {original})");
        return new ComponentResult(true, component.Kind == ComponentKind.Service
            ? "Desativado. Vale por completo após reiniciar o computador."
            : "Tarefa desativada.");
    }

    public async Task<ComponentResult> RevertAsync(ManagedComponent component, CancellationToken cancellationToken = default)
    {
        if (!IsTargetSafe(component))
        {
            return new ComponentResult(false, "Identificador inválido: nada foi alterado.");
        }

        if (!_isAdmin())
        {
            return new ComponentResult(false, "Abra o programa como administrador para alterar serviços e tarefas.");
        }

        var before = await GetStatusAsync(component, cancellationToken);
        if (before.State == ComponentState.NotPresent)
        {
            ClearOriginal(component.Id);
            return new ComponentResult(true, "Este item não existe neste Windows.");
        }

        if (before.State == ComponentState.Default)
        {
            ClearOriginal(component.Id);
            return new ComponentResult(true, "Já está como o Windows entrega.");
        }

        var saved = ReadOriginal(component.Id);
        ProcessResult run;
        if (component.Kind == ComponentKind.Service)
        {
            var start = saved is 2 or 3 or DelayedAuto ? saved.Value : (int)component.DefaultStart;
            var keyword = start switch { 2 => "auto", DelayedAuto => "delayed-auto", _ => "demand" };
            run = await _runner.RunAsync("sc.exe", ["config", component.Target, "start=", keyword], Timeout, cancellationToken);
        }
        else
        {
            run = await _runner.RunAsync("schtasks.exe", ["/Change", "/TN", component.Target, "/ENABLE"], Timeout, cancellationToken);
        }

        if (run.ExitCode != 0)
        {
            return new ComponentResult(false, "O Windows recusou a alteração. " + Tail(run.Output));
        }

        var after = await GetStatusAsync(component, cancellationToken);
        if (after.State == ComponentState.Optimized)
        {
            return new ComponentResult(false, "O Windows aceitou o comando, mas o estado não mudou.");
        }

        ClearOriginal(component.Id);
        _log.Write($"Componente '{component.Id}' restaurado");
        return new ComponentResult(true, "Restaurado.");
    }

    private async Task<ComponentStatus> ServiceStatusAsync(ManagedComponent component, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync("sc.exe", ["qc", component.Target], Timeout, cancellationToken);
        if (result.ExitCode == 1060)
        {
            return new ComponentStatus(ComponentState.NotPresent);
        }

        if (result.ExitCode != 0)
        {
            return new ComponentStatus(ComponentState.Unknown, Tail(result.Output));
        }

        var match = StartTypeLine().Match(result.Output);
        if (!match.Success)
        {
            return new ComponentStatus(ComponentState.Unknown, "Resposta inesperada do Windows.");
        }

        var start = int.Parse(match.Groups[1].Value);
        // 5 = início automático atrasado (o sc mostra "(DELAYED)"); precisa voltar assim, e não como automático comum.
        var detail = start == (int)ServiceStartType.Auto && match.Groups[2].Value.Contains("DELAYED", StringComparison.OrdinalIgnoreCase)
            ? DelayedAuto
            : start;
        return new ComponentStatus(
            start == (int)ServiceStartType.Disabled ? ComponentState.Optimized : ComponentState.Default,
            detail.ToString());
    }

    private async Task<ComponentStatus> TaskStatusAsync(ManagedComponent component, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync("schtasks.exe", ["/Query", "/TN", component.Target, "/XML"], Timeout, cancellationToken);
        if (result.ExitCode != 0)
        {
            // O schtasks usa código 1 tanto para "não existe" quanto para erro; sem a tarefa, o XML também não vem.
            return result.Output.Contains("<Task", StringComparison.OrdinalIgnoreCase)
                ? new ComponentStatus(ComponentState.Unknown, Tail(result.Output))
                : new ComponentStatus(ComponentState.NotPresent);
        }

        return DisabledTag().IsMatch(result.Output)
            ? new ComponentStatus(ComponentState.Optimized)
            : new ComponentStatus(ComponentState.Default);
    }

    private static int? CurrentStartValue(ComponentStatus status) =>
        int.TryParse(status.Detail, out var value) && value is 2 or 3 or DelayedAuto ? value : null;

    private static bool IsTargetSafe(ManagedComponent component) => component.Kind == ComponentKind.Service
        ? ComponentCatalog.IsSafeServiceName(component.Target)
        : ComponentCatalog.IsSafeTaskPath(component.Target);

    private int? ReadOriginal(string id)
    {
        lock (_stateLock)
        {
            return LoadState().TryGetValue(id, out var value) ? value : null;
        }
    }

    private void SaveOriginal(string id, int value)
    {
        lock (_stateLock)
        {
            var state = LoadState();
            // Só guarda a primeira vez: o original verdadeiro é o de antes de qualquer alteração nossa.
            if (!state.ContainsKey(id))
            {
                state[id] = value;
                WriteState(state);
            }
        }
    }

    private void ClearOriginal(string id)
    {
        lock (_stateLock)
        {
            var state = LoadState();
            if (state.Remove(id))
            {
                WriteState(state);
            }
        }
    }

    private Dictionary<string, int> LoadState()
    {
        try
        {
            return File.Exists(_statePath)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_statePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is JsonException)
        {
            return [];
        }
    }

    private void WriteState(Dictionary<string, int> state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            // Sem o arquivo, a reversão usa o padrão do Windows para o item.
        }
    }

    private static string Tail(string output)
    {
        var text = output.Trim();
        return text.Length > 200 ? text[..200] : text;
    }

    [GeneratedRegex(@"START_TYPE\s*:\s*(\d+)([^\r\n]*)")]
    private static partial Regex StartTypeLine();

    [GeneratedRegex(@"<Settings>(?:(?!</Settings>).)*?<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DisabledTag();
}
