using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tweaks;

public sealed record BatchItemResult(TweakDefinition Tweak, TweakAction Action, OperationResult Result);

public sealed record BatchResult(IReadOnlyList<BatchItemResult> Items)
{
    public int SucceededCount => Items.Count(i => i.Result.Success);

    public int FailedCount => Items.Count(i => !i.Result.Success);

    public IEnumerable<BatchItemResult> Failures => Items.Where(i => !i.Result.Success);

    /// <summary>Pior exigência de reinício entre os itens que deram certo.</summary>
    public RestartRequirement HighestRestart => Items
        .Where(i => i.Result.Success)
        .Select(i => i.Tweak.Restart)
        .DefaultIfEmpty(RestartRequirement.None)
        .Max();
}

/// <summary>Aplica/reverte vários ajustes em sequência. Uma falha não interrompe os outros; tudo vai para o log.</summary>
public sealed class TweakBatchRunner
{
    private readonly TweakEngine _engine;
    private readonly IActionLog _log;

    public TweakBatchRunner(TweakEngine engine, IActionLog log)
    {
        _engine = engine;
        _log = log;
    }

    public BatchResult Run(IEnumerable<TweakPlan> plans, Action<int, int>? progress = null)
    {
        var runnable = plans.Where(p => !p.IsBlocked && !p.IsNoOp).ToList();
        var results = new List<BatchItemResult>();
        _log.Write($"LOTE iniciado com {runnable.Count} ajuste(s)");

        for (var i = 0; i < runnable.Count; i++)
        {
            var plan = runnable[i];
            progress?.Invoke(i, runnable.Count);
            var result = plan.Action == TweakAction.Apply ? _engine.Apply(plan.Tweak) : _engine.Revert(plan.Tweak);
            results.Add(new BatchItemResult(plan.Tweak, plan.Action, result));
        }

        progress?.Invoke(runnable.Count, runnable.Count);
        var batch = new BatchResult(results);
        _log.Write($"LOTE concluído: {batch.SucceededCount} ok, {batch.FailedCount} com falha");
        return batch;
    }
}
