using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tweaks;

/// <summary>Um ajuste que o programa já alterou (existe backup dele).</summary>
public sealed record UndoEntry(
    TweakDefinition Tweak,
    DateTimeOffset AppliedAt,
    TweakState CurrentState)
{
    /// <summary>O Windows (ou o usuário) já desfez isso por fora: só resta o registro do backup.</summary>
    public bool WasUndoneOutside => CurrentState == TweakState.NotApplied;
}

/// <summary>Central de desfazer: lista tudo o que foi alterado e permite reverter com segurança.</summary>
public sealed class UndoCenterService
{
    private readonly IBackupStore _backups;
    private readonly TweakEngine _engine;
    private readonly IReadOnlyList<TweakDefinition> _catalog;
    private readonly IActionLog _log;

    public UndoCenterService(
        IBackupStore backups,
        TweakEngine engine,
        IReadOnlyList<TweakDefinition> catalog,
        IActionLog log)
    {
        _backups = backups;
        _engine = engine;
        _catalog = catalog;
        _log = log;
    }

    public IReadOnlyList<UndoEntry> List()
    {
        IReadOnlyList<TweakBackup> all;
        try
        {
            all = _backups.ListAll();
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            _log.Write($"ERRO ao listar backups: {ex.Message}");
            return [];
        }

        return all
            .Select(b => (Backup: b, Tweak: _catalog.FirstOrDefault(t => t.Id == b.TweakId)))
            .Where(x => x.Tweak is not null)
            .Select(x => new UndoEntry(x.Tweak!, x.Backup.CreatedAt, _engine.GetState(x.Tweak!)))
            .OrderByDescending(e => e.AppliedAt)
            .ToList();
    }

    public OperationResult Revert(TweakDefinition tweak) => _engine.Revert(tweak);

    public OperationResult Reapply(TweakDefinition tweak) => _engine.Apply(tweak);

    /// <summary>Remove só o registro do backup (use quando o ajuste já foi desfeito por fora).</summary>
    public OperationResult Discard(TweakDefinition tweak)
    {
        try
        {
            _backups.Delete(tweak.Id);
            _log.Write($"DESCARTADO registro de {tweak.Id}");
            return OperationResult.Ok($"O registro de “{tweak.Name}” foi removido. Nada no Windows foi alterado.");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return OperationResult.Fail($"Não foi possível remover o registro de “{tweak.Name}”. Detalhe: {ex.Message}");
        }
    }

    public BatchResult RevertMany(IEnumerable<TweakDefinition> tweaks)
    {
        var results = tweaks
            .Select(t => new BatchItemResult(t, TweakAction.Revert, _engine.Revert(t)))
            .ToList();
        return new BatchResult(results);
    }
}
