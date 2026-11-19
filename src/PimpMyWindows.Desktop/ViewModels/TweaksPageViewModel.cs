using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class TweaksPageViewModel : PageViewModel
{
    private readonly IReadOnlyList<TweakDefinition> _catalog;
    private readonly TweakEngine _engine;
    private readonly TweakPlanner _planner;
    private readonly TweakBatchRunner _runner;
    private readonly RestorePointCoordinator _restore;
    private readonly IUserPrompt _prompt;
    private readonly IFileDialogs _dialogs;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private readonly int _windowsBuild;
    private readonly List<TweakItemViewModel> _items = [];
    private string _searchText = string.Empty;
    private int _pendingCount;
    private string _summaryText = string.Empty;
    private string _filterInfoText = string.Empty;

    public TweaksPageViewModel(
        IReadOnlyList<TweakDefinition> catalog,
        TweakEngine engine,
        TweakPlanner planner,
        TweakBatchRunner runner,
        RestorePointCoordinator restore,
        IUserPrompt prompt,
        IFileDialogs dialogs,
        AppState state,
        IActionLog log,
        int windowsBuild)
    {
        _catalog = catalog;
        _engine = engine;
        _planner = planner;
        _runner = runner;
        _restore = restore;
        _prompt = prompt;
        _dialogs = dialogs;
        _state = state;
        _log = log;
        _windowsBuild = windowsBuild;

        ApplyCommand = new AsyncRelayCommand(_ => ApplyAsync(), _ => PendingCount > 0);
        DiscardCommand = new RelayCommand(_ => DiscardPending(), _ => PendingCount > 0);
        MarkCommand = new RelayCommand(p => Mark((string)p!));
        ProfileCommand = new RelayCommand(p => MarkProfile((TweakProfile)p!));
        ExportCommand = new RelayCommand(_ => Export());
        ImportCommand = new RelayCommand(_ => Import());

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.IsTechnicalMode))
            {
                OnModeChanged();
            }
        };

        LoadItems();
    }

    public override string Key => "tweaks";

    public ObservableCollection<TweakGroupViewModel> Groups { get; } = [];

    public IReadOnlyList<TweakProfile> Profiles => TweakProfiles.All;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebuildGroups();
            }
        }
    }

    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (SetProperty(ref _pendingCount, value))
            {
                OnPropertyChanged(nameof(PendingText));
            }
        }
    }

    public string PendingText => PendingCount == 1
        ? "1 mudança aguardando você"
        : $"{PendingCount} mudanças aguardando você";

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    /// <summary>Quantos ajustes estão escondidos e por quê (modo ou versão do Windows).</summary>
    public string FilterInfoText
    {
        get => _filterInfoText;
        private set => SetProperty(ref _filterInfoText, value);
    }

    public ICommand ApplyCommand { get; }

    public ICommand DiscardCommand { get; }

    public ICommand MarkCommand { get; }

    public ICommand ProfileCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand ImportCommand { get; }

    public override void OnShown() => ReloadStates();

    /// <summary>Marca os ajustes recomendados (usado pelo Painel).</summary>
    public void MarkRecommended() => Mark("recommended");

    private void LoadItems()
    {
        _items.Clear();
        foreach (var tweak in _catalog.Where(t => TweakApplicability.IsApplicable(t, _windowsBuild)))
        {
            var item = new TweakItemViewModel(tweak, _engine.GetState(tweak));
            item.PendingChanged += UpdateCounters;
            _items.Add(item);
        }

        RebuildGroups();
    }

    private void ReloadStates()
    {
        foreach (var item in _items.Where(i => !i.IsPending))
        {
            item.Accept(_engine.GetState(item.Definition));
        }

        UpdateCounters();
    }

    private IEnumerable<TweakItemViewModel> VisibleByMode =>
        _items.Where(i => _state.IsTechnicalMode || !i.IsTechnical);

    private void OnModeChanged()
    {
        if (!_state.IsTechnicalMode)
        {
            // Ajustes técnicos escondidos não podem ficar pendentes sem o usuário ver.
            foreach (var item in _items.Where(i => i.IsTechnical))
            {
                item.ResetDesired();
            }
        }

        RebuildGroups();
    }

    private void RebuildGroups()
    {
        var query = TweakItemViewModel.Normalize(SearchText.Trim());
        var visible = VisibleByMode
            .Where(i => query.Length == 0 || i.SearchIndex.Contains(query, StringComparison.Ordinal))
            .ToList();

        Groups.Clear();
        foreach (var group in visible.GroupBy(i => i.Definition.Category))
        {
            Groups.Add(new TweakGroupViewModel(CategoryTitle(group.Key), group.ToList()));
        }

        var hiddenByWindows = _catalog.Count(t => !TweakApplicability.IsApplicable(t, _windowsBuild));
        var hiddenByMode = _items.Count - VisibleByMode.Count();
        var parts = new List<string>();
        if (hiddenByMode > 0)
        {
            parts.Add($"{hiddenByMode} {(hiddenByMode == 1 ? "ajuste avançado" : "ajustes avançados")} no Modo Técnico");
        }

        if (hiddenByWindows > 0)
        {
            parts.Add(
                $"{hiddenByWindows} {(hiddenByWindows == 1 ? "ajuste oculto" : "ajustes ocultos")} " +
                $"por não valer para o seu Windows (build {_windowsBuild})");
        }

        if (query.Length > 0 && visible.Count == 0)
        {
            parts.Add("nenhum ajuste combina com a busca");
        }

        FilterInfoText = string.Join(" · ", parts);
        UpdateCounters();
    }

    private void UpdateCounters()
    {
        PendingCount = _items.Count(i => i.IsPending);
        var shown = VisibleByMode.ToList();
        var active = shown.Count(i => i.IsApplied);
        SummaryText = $"{active} de {shown.Count} ajustes ativos";
    }

    private void Mark(string kind)
    {
        var visible = VisibleByMode.Where(i => i.CanToggle).ToList();
        switch (kind)
        {
            case "recommended":
                SetDesired(visible.Where(i => i.IsRecommended));
                _state.Say("Recomendados marcados. Revise e clique em Aplicar.");
                break;
            case "safe":
                SetDesired(visible.Where(i => i.Definition.Risk == RiskLevel.Safe));
                _state.Say("Ajustes seguros marcados. Revise e clique em Aplicar.");
                break;
            case "all":
                SetDesired(visible);
                _state.Say("Todos os ajustes visíveis foram marcados. Revise com atenção antes de aplicar.");
                break;
            default:
                DiscardPending();
                break;
        }
    }

    private static void SetDesired(IEnumerable<TweakItemViewModel> items)
    {
        foreach (var item in items.ToList())
        {
            item.Desired = true;
        }
    }

    private void MarkProfile(TweakProfile profile)
    {
        var ids = profile.TweakIds.ToHashSet();
        var matching = VisibleByMode.Where(i => i.CanToggle && ids.Contains(i.Definition.Id)).ToList();
        SetDesired(matching);
        _state.Say($"Perfil “{profile.Name}”: {matching.Count} ajustes marcados. Revise e clique em Aplicar.");
    }

    private void DiscardPending()
    {
        foreach (var item in _items.Where(i => i.IsPending).ToList())
        {
            item.ResetDesired();
        }

        _state.Say("Seleção limpa. Nada foi alterado.");
    }

    private async Task ApplyAsync()
    {
        var pending = _items.Where(i => i.IsPending).ToList();
        if (pending.Count == 0)
        {
            _state.Say("Nada pendente para aplicar.");
            return;
        }

        var plans = pending
            .Select(i => _planner.Plan(i.Definition, i.Desired ? TweakAction.Apply : TweakAction.Revert))
            .ToList();

        var runnable = plans.Where(p => !p.IsBlocked && !p.IsNoOp).ToList();
        if (runnable.Count == 0)
        {
            ShowNothingToDo(plans);
            foreach (var item in pending)
            {
                item.Accept(_engine.GetState(item.Definition));
            }

            return;
        }

        var model = PlanPreviewModel.From(plans, _restore.CreateBeforeApplyDefault);
        if (!_prompt.ShowPlanPreview(model))
        {
            _state.Say("Cancelado. Nada foi alterado.");
            return;
        }

        _restore.RememberPreference(model.CreateRestorePoint);
        if (model.CreateRestorePoint)
        {
            var point = await _restore.CreateAsync();
            if (!point.Success && !_prompt.Confirm(
                    "Ponto de restauração",
                    $"{point.Message}\n\nQuer continuar mesmo assim, sem o ponto de restauração? " +
                    "Os ajustes continuam podendo ser desfeitos pela Central Desfazer."))
            {
                _state.Say("Cancelado. Nada foi alterado.");
                return;
            }
        }

        _state.Say($"Aplicando {runnable.Count} ajuste(s)…");
        var batch = await Task.Run(() => _runner.Run(plans));

        foreach (var item in pending)
        {
            item.Accept(_engine.GetState(item.Definition));
        }

        ReportBatch(batch);
        _state.NotifyStateChanged();
    }

    private void ShowNothingToDo(IReadOnlyList<TweakPlan> plans)
    {
        var blocked = plans.Where(p => p.IsBlocked).ToList();
        if (blocked.Count == 0)
        {
            _state.Say("Tudo já estava como você escolheu. Nada foi alterado.");
            return;
        }

        var lines = string.Join("\n", blocked.Select(p => $"• {p.Tweak.Name}: {p.Blocker}"));
        _state.Say("Nenhum ajuste pôde ser feito. Nada foi alterado.");
        _prompt.ShowError(
            "Nada foi alterado",
            $"Não foi possível fazer o que você marcou:\n\n{lines}\n\n" +
            "Se o motivo for “precisa do programa executado como administrador”, use o botão do menu lateral.");
    }

    private void ReportBatch(BatchResult batch)
    {
        var message = batch.SucceededCount == 1 ? "1 ajuste concluído." : $"{batch.SucceededCount} ajustes concluídos.";
        message += batch.HighestRestart switch
        {
            RestartRequirement.Restart => " Reinicie o computador para valer por completo.",
            RestartRequirement.SignOut => " Saia e entre no Windows para valer por completo.",
            _ => string.Empty,
        };

        if (batch.FailedCount > 0)
        {
            message += $" {batch.FailedCount} com problema (veja o aviso).";
        }

        _state.Say(message);

        if (batch.FailedCount > 0)
        {
            _prompt.ShowError(
                "Alguns ajustes não foram concluídos",
                string.Join("\n\n", batch.Failures.Select(f => $"• {f.Tweak.Name}\n  {f.Result.Message}")));
        }
    }

    private void Export()
    {
        var chosen = _items.Where(i => i.Desired).Select(i => i.Definition.Id).ToList();
        if (chosen.Count == 0)
        {
            _prompt.ShowInfo("Exportar seleção", "Marque pelo menos um ajuste (ou aplique alguns) para exportar.");
            return;
        }

        var path = _dialogs.PickSavePath("Exportar seleção de ajustes", "meus-ajustes.json");
        if (path is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(path, TweakSelection.Export(chosen));
            _state.Say($"Seleção exportada ({chosen.Count} ajustes). O arquivo só guarda os nomes internos dos ajustes.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _prompt.ShowError("Exportar seleção", $"Não foi possível salvar o arquivo. Detalhe: {ex.Message}");
        }
    }

    private void Import()
    {
        var path = _dialogs.PickOpenPath("Importar seleção de ajustes");
        if (path is null)
        {
            return;
        }

        try
        {
            var valid = TweakSelection.Import(File.ReadAllText(path), _items.Select(i => i.Definition.Id), out var unknown);
            var visibleIds = VisibleByMode.Where(i => i.CanToggle).Select(i => i.Definition.Id).ToHashSet();
            var usable = valid.Where(visibleIds.Contains).ToList();
            SetDesired(_items.Where(i => usable.Contains(i.Definition.Id)));

            var skipped = unknown.Count + (valid.Count - usable.Count);
            _log.Write($"IMPORTAR seleção: {usable.Count} marcados, {skipped} ignorados");
            _state.Say(
                $"Seleção importada: {usable.Count} ajustes marcados" +
                (skipped > 0 ? $"; {skipped} ignorados (não existem aqui, são do Modo Técnico ou não valem para o seu Windows)" : string.Empty) +
                ". Revise e clique em Aplicar.");
        }
        catch (FormatException ex)
        {
            _prompt.ShowError("Importar seleção", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _prompt.ShowError("Importar seleção", $"Não foi possível ler o arquivo. Detalhe: {ex.Message}");
        }
    }

    private static string CategoryTitle(TweakCategory category) => category switch
    {
        TweakCategory.Privacy => "Privacidade",
        TweakCategory.Performance => "Desempenho",
        TweakCategory.Interface => "Interface e conforto",
        TweakCategory.Gaming => "Jogos",
        _ => category.ToString(),
    };
}
