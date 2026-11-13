using System.Security;
using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tweaks;

/// <summary>
/// Aplica e reverte ajustes com segurança:
/// 1) guarda os valores atuais antes de qualquer mudança;
/// 2) se uma gravação falhar no meio, desfaz o que já foi gravado;
/// 3) registra tudo no log;
/// 4) reverter devolve os valores que o usuário tinha (ou o padrão do Windows, se não houver backup).
/// </summary>
public sealed class TweakEngine
{
    private readonly IRegistryAccess _registry;
    private readonly IBackupStore _backups;
    private readonly IActionLog _log;
    private readonly Func<bool> _isAdministrator;

    public TweakEngine(IRegistryAccess registry, IBackupStore backups, IActionLog log, Func<bool> isAdministrator)
    {
        _registry = registry;
        _backups = backups;
        _log = log;
        _isAdministrator = isAdministrator;
    }

    public TweakState GetState(TweakDefinition tweak)
    {
        try
        {
            var applied = tweak.Changes.Count(c => object.Equals(_registry.Read(c.Location), c.OptimizedValue));

            if (applied == tweak.Changes.Count)
            {
                return TweakState.Applied;
            }

            return applied == 0 ? TweakState.NotApplied : TweakState.Partial;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao ler o estado de {tweak.Id}: {ex.Message}");
            return TweakState.Unknown;
        }
    }

    public OperationResult Apply(TweakDefinition tweak)
    {
        if (tweak.RequiresAdmin && !_isAdministrator())
        {
            return OperationResult.Fail(AdminRequiredMessage);
        }

        List<BackedUpValue> previous;
        try
        {
            if (GetState(tweak) == TweakState.Applied)
            {
                return OperationResult.Ok($"“{tweak.Name}” já estava ativo. Nada foi alterado.");
            }

            // O backup vem ANTES de qualquer alteração. Se ele falhar, nada é mexido.
            previous = tweak.Changes
                .Select(c => new BackedUpValue(c.Location, _registry.Read(c.Location)))
                .ToList();
            _backups.Save(new TweakBackup(tweak.Id, DateTimeOffset.UtcNow, previous));
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao guardar backup de {tweak.Id}: {ex.Message}");
            return OperationResult.Fail(
                $"Não foi possível guardar a cópia de segurança, então nada foi alterado. Detalhe: {ex.Message}");
        }

        try
        {
            foreach (var change in tweak.Changes)
            {
                var before = previous.First(p => p.Location == change.Location).Previous;
                _log.Write($"APLICAR {tweak.Id}: {change.Location} = {change.OptimizedValue} (antes: {Describe(before)})");
                _registry.Write(change.Location, change.OptimizedValue);
            }

            _log.Write($"APLICADO {tweak.Id}");
            return OperationResult.Ok($"“{tweak.Name}” foi aplicado." + RestartNote(tweak));
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao aplicar {tweak.Id}: {ex.Message}. Desfazendo.");
            var undone = TryRestore(previous);
            return OperationResult.Fail(undone
                ? $"Não foi possível aplicar “{tweak.Name}”. Tudo o que já tinha sido alterado foi desfeito. Detalhe: {ex.Message}"
                : $"Não foi possível aplicar “{tweak.Name}” e não foi possível desfazer tudo. Use o botão Reverter. Detalhe: {ex.Message}");
        }
    }

    public OperationResult Revert(TweakDefinition tweak)
    {
        if (tweak.RequiresAdmin && !_isAdministrator())
        {
            return OperationResult.Fail(AdminRequiredMessage);
        }

        try
        {
            if (GetState(tweak) == TweakState.NotApplied)
            {
                _backups.Delete(tweak.Id);
                return OperationResult.Ok($"“{tweak.Name}” já estava no padrão do Windows. Nada foi alterado.");
            }

            var backup = _backups.Load(tweak.Id);
            var useBackup = backup is not null && BackupCoversTweak(tweak, backup);

            var target = useBackup
                ? backup!.Values
                : tweak.Changes.Select(c => new BackedUpValue(c.Location, c.DefaultValue)).ToList();

            foreach (var item in target)
            {
                _log.Write($"REVERTER {tweak.Id}: {item.Location} -> {Describe(item.Previous)}");
                Restore(item);
            }

            _backups.Delete(tweak.Id);
            _log.Write($"REVERTIDO {tweak.Id}");

            var origin = useBackup ? "Os valores que você tinha antes foram restaurados." : "O padrão do Windows foi restaurado.";
            return OperationResult.Ok($"“{tweak.Name}” foi revertido. {origin}" + RestartNote(tweak));
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao reverter {tweak.Id}: {ex.Message}");
            return OperationResult.Fail($"Não foi possível reverter “{tweak.Name}”. Detalhe: {ex.Message}");
        }
    }

    private const string AdminRequiredMessage =
        "Este ajuste altera configurações do computador inteiro e precisa que o Pimp my Windows " +
        "seja executado como administrador.";

    private static bool BackupCoversTweak(TweakDefinition tweak, TweakBackup backup) =>
        backup.Values.Count == tweak.Changes.Count
        && tweak.Changes.All(c => backup.Values.Any(v => v.Location == c.Location));

    private bool TryRestore(IEnumerable<BackedUpValue> values)
    {
        var allRestored = true;
        foreach (var value in values)
        {
            try
            {
                Restore(value);
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                allRestored = false;
                _log.Write($"ERRO ao restaurar {value.Location}: {ex.Message}");
            }
        }

        return allRestored;
    }

    private void Restore(BackedUpValue value)
    {
        if (value.Previous is null)
        {
            _registry.Delete(value.Location);
        }
        else
        {
            _registry.Write(value.Location, value.Previous);
        }
    }

    private static string RestartNote(TweakDefinition tweak) => tweak.Restart switch
    {
        RestartRequirement.SignOut => " Para valer, saia e entre novamente no Windows.",
        RestartRequirement.Restart => " Para valer, reinicie o computador.",
        _ => string.Empty,
    };

    private static string Describe(RegistryData? data) => data is null ? "(não existia)" : data.ToString();

    private static bool IsExpectedFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException
            or NotSupportedException or InvalidCastException or InvalidOperationException or ArgumentException;
}
