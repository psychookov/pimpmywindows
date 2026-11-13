using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Tweaks;

public enum TweakCategory
{
    Privacy,
    Performance,
    Interface,
    Gaming,
}

/// <summary>
/// Nível de risco mostrado ao usuário.
/// Seguro: só muda aparência/comportamento de conforto e é totalmente reversível.
/// Moderado: afeta o comportamento do sistema; exige confirmação.
/// Avançado: só pode existir no Modo Técnico; nunca aparece no Modo Básico.
/// </summary>
public enum RiskLevel
{
    Safe,
    Moderate,
    Advanced,
}

public enum RestartRequirement
{
    None,
    SignOut,
    Restart,
}

public enum TweakState
{
    Unknown,
    NotApplied,
    Applied,
    Partial,
}

/// <summary>
/// Uma alteração em um valor do registro: o valor otimizado e o padrão do Windows
/// (<c>null</c> em <see cref="DefaultValue"/> = o valor normalmente não existe).
/// </summary>
public sealed record RegistryChange(
    RegistryLocation Location,
    RegistryData OptimizedValue,
    RegistryData? DefaultValue);

/// <summary>
/// Descreve um ajuste de forma declarativa. Todo texto é em português e escrito
/// para quem não tem experiência técnica.
/// <see cref="MinBuild"/>/<see cref="MaxBuild"/> limitam o ajuste a uma faixa de builds do Windows
/// (nulo = sem limite); <see cref="Mode"/> define em qual modo ele aparece.
/// </summary>
public sealed record TweakDefinition(
    string Id,
    string Name,
    string Summary,
    string Details,
    TweakCategory Category,
    RiskLevel Risk,
    bool RequiresAdmin,
    RestartRequirement Restart,
    IReadOnlyList<RegistryChange> Changes,
    string? Warning = null,
    UserMode Mode = UserMode.Basic,
    int? MinBuild = null,
    int? MaxBuild = null);
