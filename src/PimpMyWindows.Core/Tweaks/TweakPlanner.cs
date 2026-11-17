using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tweaks;

public enum TweakAction
{
    Apply,
    Revert,
}

/// <summary>Uma mudança concreta: o que está no registro agora e o que ficará depois.</summary>
public sealed record PlannedChange(RegistryLocation Location, RegistryData? Current, RegistryData? Target);

/// <summary>
/// O que vai acontecer com um ajuste, calculado SEM alterar nada. <see cref="Blocker"/> preenchido = não pode ser feito.
/// </summary>
public sealed record TweakPlan(
    TweakDefinition Tweak,
    TweakAction Action,
    IReadOnlyList<PlannedChange> Changes,
    string? Blocker)
{
    public bool IsBlocked => Blocker is not null;

    /// <summary>Nada a fazer: já está como o usuário quer.</summary>
    public bool IsNoOp => !IsBlocked && Changes.Count == 0;
}

/// <summary>Calcula a prévia das mudanças, para o usuário conferir antes de confirmar.</summary>
public sealed class TweakPlanner
{
    private readonly IRegistryAccess _registry;
    private readonly IBackupStore _backups;
    private readonly Func<bool> _isAdministrator;
    private readonly Func<int>? _getWindowsBuild;

    public TweakPlanner(
        IRegistryAccess registry,
        IBackupStore backups,
        Func<bool> isAdministrator,
        Func<int>? getWindowsBuild = null)
    {
        _registry = registry;
        _backups = backups;
        _isAdministrator = isAdministrator;
        _getWindowsBuild = getWindowsBuild;
    }

    public TweakPlan Plan(TweakDefinition tweak, TweakAction action)
    {
        if (action == TweakAction.Apply
            && _getWindowsBuild is not null
            && !TweakApplicability.IsApplicable(tweak, _getWindowsBuild()))
        {
            return Blocked(tweak, action, "não vale para a sua versão do Windows");
        }

        if (tweak.RequiresAdmin && !_isAdministrator())
        {
            return Blocked(tweak, action, "precisa do programa executado como administrador");
        }

        try
        {
            var targets = action == TweakAction.Apply ? ApplyTargets(tweak) : RevertTargets(tweak);
            var changes = new List<PlannedChange>();
            foreach (var (location, target) in targets)
            {
                var current = _registry.Read(location);
                if (!object.Equals(current, target))
                {
                    changes.Add(new PlannedChange(location, current, target));
                }
            }

            return new TweakPlan(tweak, action, changes, null);
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return Blocked(tweak, action, $"não foi possível ler o estado atual ({ex.Message})");
        }
    }

    private static IEnumerable<(RegistryLocation, RegistryData?)> ApplyTargets(TweakDefinition tweak) =>
        tweak.Changes.Select(c => (c.Location, (RegistryData?)c.OptimizedValue));

    private IEnumerable<(RegistryLocation, RegistryData?)> RevertTargets(TweakDefinition tweak)
    {
        var backup = _backups.Load(tweak.Id);
        if (backup is not null && BackupRules.Covers(tweak, backup))
        {
            return backup.Values.Select(v => (v.Location, v.Previous));
        }

        return tweak.Changes.Select(c => (c.Location, c.DefaultValue));
    }

    private static TweakPlan Blocked(TweakDefinition tweak, TweakAction action, string reason) =>
        new(tweak, action, [], reason);
}
