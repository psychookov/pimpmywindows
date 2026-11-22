using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Desktop.Services;

public sealed class PlanPreviewItem
{
    public required string Title { get; init; }

    public required string ActionText { get; init; }

    public required string RiskKey { get; init; }

    public required string RiskText { get; init; }

    public required IReadOnlyList<string> Lines { get; init; }

    public string? Blocker { get; init; }

    public string? Warning { get; init; }

    public bool IsBlocked => Blocker is not null;

    public bool HasWarning => !string.IsNullOrEmpty(Warning);
}

/// <summary>O que será mostrado na janela de conferência antes de aplicar um lote de ajustes.</summary>
public sealed class PlanPreviewModel
{
    public required IReadOnlyList<PlanPreviewItem> Items { get; init; }

    public required string SummaryText { get; init; }

    public string RestartText { get; init; } = string.Empty;

    /// <summary>Marcado pelo usuário na janela: criar ponto de restauração antes de aplicar.</summary>
    public bool CreateRestorePoint { get; set; }

    /// <summary>Falso quando quem chama não cria ponto de restauração (some a caixa na janela).</summary>
    public bool OfferRestorePoint { get; init; } = true;

    public bool HasRestartText => RestartText.Length > 0;

    public static PlanPreviewModel From(
        IReadOnlyList<TweakPlan> plans,
        bool createRestorePointDefault,
        bool offerRestorePoint = true)
    {
        var items = plans.Select(ToItem).ToList();
        var runnable = plans.Count(p => !p.IsBlocked && !p.IsNoOp);
        var blocked = plans.Count(p => p.IsBlocked);

        var summary = runnable == 1 ? "1 ajuste será alterado." : $"{runnable} ajustes serão alterados.";
        if (blocked > 0)
        {
            summary += blocked == 1 ? " 1 não pode ser feito agora." : $" {blocked} não podem ser feitos agora.";
        }

        var restart = plans
            .Where(p => !p.IsBlocked && !p.IsNoOp)
            .Select(p => p.Tweak.Restart)
            .DefaultIfEmpty(RestartRequirement.None)
            .Max();

        return new PlanPreviewModel
        {
            Items = items,
            SummaryText = summary,
            RestartText = restart switch
            {
                RestartRequirement.Restart => "Para valer por completo, será preciso reiniciar o computador.",
                RestartRequirement.SignOut => "Para valer por completo, será preciso sair e entrar no Windows.",
                _ => string.Empty,
            },
            CreateRestorePoint = createRestorePointDefault && offerRestorePoint,
            OfferRestorePoint = offerRestorePoint,
        };
    }

    private static PlanPreviewItem ToItem(TweakPlan plan)
    {
        var risk = plan.Tweak.Risk;
        return new PlanPreviewItem
        {
            Title = plan.Tweak.Name,
            ActionText = plan.Action == TweakAction.Apply ? "Ativar" : "Reverter",
            RiskKey = risk.ToString(),
            RiskText = risk switch
            {
                RiskLevel.Safe => "Seguro",
                RiskLevel.Moderate => "Moderado",
                _ => "Avançado",
            },
            Lines = LinesFor(plan),
            Blocker = plan.Blocker is null ? null : $"Não dá para fazer: {plan.Blocker}.",
            Warning = plan.Action == TweakAction.Apply ? plan.Tweak.Warning : null,
        };
    }

    private static IReadOnlyList<string> LinesFor(TweakPlan plan)
    {
        if (plan.IsNoOp)
        {
            return new List<string> { "Já está assim. Nada a alterar." };
        }

        return plan.Changes.Select(Describe).ToList();
    }

    private static string Describe(PlannedChange change) =>
        $"{change.Location}:  {Show(change.Current, "(não existe)")}  →  {Show(change.Target, "(será removido)")}";

    private static string Show(RegistryData? data, string whenNull) => data is null ? whenNull : data.ToString();
}
