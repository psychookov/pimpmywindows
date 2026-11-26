using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Components;

public enum ComponentKind
{
    Service,
    ScheduledTask,
}

/// <summary>Como o serviço inicia, com os mesmos números que o Windows usa.</summary>
public enum ServiceStartType
{
    Auto = 2,
    Manual = 3,
    Disabled = 4,
}

/// <summary>
/// Um serviço ou tarefa agendada que o programa sabe desativar com segurança.
/// Para serviço, <see cref="Target"/> é o nome interno; para tarefa, o caminho (ex.: \Microsoft\Windows\...\Nome).
/// </summary>
public sealed record ManagedComponent(
    string Id,
    ComponentKind Kind,
    string Target,
    string Name,
    string Summary,
    string Details,
    RiskLevel Risk,
    UserMode Mode,
    ServiceStartType DefaultStart = ServiceStartType.Auto,
    string? Warning = null,
    RestartRequirement Restart = RestartRequirement.None);

public enum ComponentState
{
    /// <summary>Não deu para ler o estado.</summary>
    Unknown,

    /// <summary>Não existe neste Windows (comum: depende da versão e da edição).</summary>
    NotPresent,

    /// <summary>Como o Windows entrega.</summary>
    Default,

    /// <summary>Desativado pelo programa (ou por outro meio).</summary>
    Optimized,
}

public sealed record ComponentStatus(ComponentState State, string? Detail = null);

public sealed record ComponentResult(bool Success, string Message);
