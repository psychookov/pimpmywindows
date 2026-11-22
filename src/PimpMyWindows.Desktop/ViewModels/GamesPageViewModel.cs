using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using PimpMyWindows.Core.Games;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class GameItemViewModel : ObservableObject
{
    public GameItemViewModel(GameInfo game, TweakItemViewModel gpu, TweakItemViewModel? fullscreen)
    {
        Game = game;
        Gpu = gpu;
        Fullscreen = fullscreen;
    }

    public GameInfo Game { get; }

    public TweakItemViewModel Gpu { get; }

    /// <summary>Ajuste técnico; sempre existe, mas só aparece no Modo Técnico.</summary>
    public TweakItemViewModel? Fullscreen { get; }

    public string Name => Game.Name;

    public string Subtitle => $"{Game.Launcher} · {Game.ExecutablePath}";

    public IEnumerable<TweakItemViewModel> All => Fullscreen is null ? [Gpu] : [Gpu, Fullscreen];

    private bool _showTechnical;

    public bool ShowTechnical
    {
        get => _showTechnical;
        set => SetProperty(ref _showTechnical, value);
    }
}

/// <summary>Biblioteca de jogos: acha os jogos instalados e aplica ajustes só para cada um, com prévia e desfazer.</summary>
public sealed class GamesPageViewModel : PageViewModel
{
    private readonly GameDetector _detector;
    private readonly ManagedGameStore _store;
    private readonly TweakEngine _engine;
    private readonly TweakPlanner _planner;
    private readonly TweakBatchRunner _runner;
    private readonly IFileDialogs _dialogs;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private readonly int _windowsBuild;
    private readonly List<GameItemViewModel> _items = [];
    private bool _isLoading;
    private bool _loadedOnce;
    private int _pendingCount;
    private string _infoText = string.Empty;

    public GamesPageViewModel(
        GameDetector detector,
        ManagedGameStore store,
        TweakEngine engine,
        TweakPlanner planner,
        TweakBatchRunner runner,
        IFileDialogs dialogs,
        IUserPrompt prompt,
        AppState state,
        IActionLog log,
        int windowsBuild)
    {
        _detector = detector;
        _store = store;
        _engine = engine;
        _planner = planner;
        _runner = runner;
        _dialogs = dialogs;
        _prompt = prompt;
        _state = state;
        _log = log;
        _windowsBuild = windowsBuild;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsLoading);
        AddCommand = new RelayCommand(_ => AddManually());
        ApplyCommand = new AsyncRelayCommand(_ => ApplyAsync(), _ => PendingCount > 0);
        DiscardCommand = new RelayCommand(_ => Discard(), _ => PendingCount > 0);

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.IsTechnicalMode))
            {
                OnModeChanged();
            }
        };
    }

    public override string Key => "games";

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand AddCommand { get; }

    public ICommand ApplyCommand { get; }

    public ICommand DiscardCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
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

    public string InfoText
    {
        get => _infoText;
        private set => SetProperty(ref _infoText, value);
    }

    public override void OnShown()
    {
        if (!_loadedOnce)
        {
            _ = LoadAsync();
        }
        else
        {
            foreach (var item in _items.SelectMany(i => i.All).Where(t => !t.IsPending))
            {
                item.Accept(_engine.GetState(item.Definition));
            }

            UpdatePending();
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        InfoText = string.Empty;
        try
        {
            var detected = await Task.Run(() => _detector.DetectAll());
            var remembered = await Task.Run(() => _store.Load());
            var all = detected
                .Concat(remembered)
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _items.Clear();
            foreach (var game in all)
            {
                _items.Add(Build(game));
            }

            _loadedOnce = true;
            Rebuild();
            InfoText = all.Count == 0
                ? "Nenhum jogo da Steam, Epic ou GOG foi encontrado. Use “Adicionar jogo” para escolher o programa (.exe) do seu jogo."
                : $"{all.Count} {(all.Count == 1 ? "jogo encontrado" : "jogos encontrados")}. Nada foi alterado: marque o que quiser e clique em Aplicar.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private GameItemViewModel Build(GameInfo game)
    {
        var gpuDefinition = GameTweaks.HighPerformanceGpu(game);
        var gpu = new TweakItemViewModel(gpuDefinition, _engine.GetState(gpuDefinition));
        var fullscreenDefinition = GameTweaks.DisableFullscreenOptimizations(game);
        var fullscreen = new TweakItemViewModel(fullscreenDefinition, _engine.GetState(fullscreenDefinition));
        gpu.PendingChanged += UpdatePending;
        fullscreen.PendingChanged += UpdatePending;
        return new GameItemViewModel(game, gpu, fullscreen) { ShowTechnical = _state.IsTechnicalMode };
    }

    private void Rebuild()
    {
        Games.Clear();
        foreach (var item in _items)
        {
            Games.Add(item);
        }

        UpdatePending();
    }

    private void OnModeChanged()
    {
        foreach (var item in _items)
        {
            item.ShowTechnical = _state.IsTechnicalMode;
            if (!_state.IsTechnicalMode)
            {
                item.Fullscreen?.ResetDesired();
            }
        }

        UpdatePending();
    }

    private void UpdatePending() => PendingCount = _items.SelectMany(i => i.All).Count(t => t.IsPending);

    private void AddManually()
    {
        var path = _dialogs.PickExecutable("Escolha o programa (.exe) do jogo");
        if (path is null)
        {
            return;
        }

        var game = GameDetector.FromExecutable(path);
        if (_items.Any(i => i.Game.Id == game.Id))
        {
            _state.Say("Esse jogo já está na lista.");
            return;
        }

        try
        {
            _store.Remember(game);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _prompt.ShowError("Adicionar jogo", $"Não foi possível salvar o jogo na lista. Detalhe: {ex.Message}");
            return;
        }

        _items.Add(Build(game));
        _items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        Rebuild();
        _state.Say($"“{game.Name}” foi adicionado à lista.");
    }

    private void Discard()
    {
        foreach (var tweak in _items.SelectMany(i => i.All).Where(t => t.IsPending).ToList())
        {
            tweak.ResetDesired();
        }

        _state.Say("Seleção limpa. Nada foi alterado.");
    }

    private async Task ApplyAsync()
    {
        var pending = _items
            .SelectMany(i => i.All.Where(t => t.IsPending).Select(t => (Game: i.Game, Tweak: t)))
            .ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var plans = pending
            .Select(p => _planner.Plan(p.Tweak.Definition, p.Tweak.Desired ? TweakAction.Apply : TweakAction.Revert))
            .ToList();

        var runnable = plans.Where(p => !p.IsBlocked && !p.IsNoOp).ToList();
        if (runnable.Count == 0)
        {
            var blocked = plans.Where(p => p.IsBlocked).ToList();
            if (blocked.Count > 0)
            {
                _prompt.ShowError(
                    "Nada foi alterado",
                    string.Join("\n", blocked.Select(p => $"• {p.Tweak.Name}: {p.Blocker}")));
            }

            _state.Say("Nada foi alterado.");
            foreach (var item in pending)
            {
                item.Tweak.Accept(_engine.GetState(item.Tweak.Definition));
            }

            return;
        }

        if (!_prompt.ShowPlanPreview(PlanPreviewModel.From(plans, createRestorePointDefault: false, offerRestorePoint: false)))
        {
            _state.Say("Cancelado. Nada foi alterado.");
            return;
        }

        // O jogo é guardado antes de alterar, para a Central Desfazer reconhecer o backup mesmo se o programa fechar.
        foreach (var game in pending.Select(p => p.Game).DistinctBy(g => g.Id))
        {
            try
            {
                _store.Remember(game);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _prompt.ShowError(
                    "Não foi possível salvar o jogo",
                    $"Para você poder desfazer depois, o jogo precisa ser guardado na lista, e isso falhou. " +
                    $"Nada foi alterado. Detalhe: {ex.Message}");
                return;
            }
        }

        _state.Say($"Aplicando {runnable.Count} ajuste(s)…");
        var batch = await Task.Run(() => _runner.Run(plans));

        foreach (var item in pending)
        {
            item.Tweak.Accept(_engine.GetState(item.Tweak.Definition));
        }

        _state.Say(batch.FailedCount == 0
            ? $"{batch.SucceededCount} ajuste(s) de jogo concluído(s). Feche e abra o jogo para valer."
            : $"{batch.SucceededCount} concluído(s), {batch.FailedCount} com problema.");
        if (batch.FailedCount > 0)
        {
            _prompt.ShowError(
                "Alguns ajustes não foram concluídos",
                string.Join("\n\n", batch.Failures.Select(f => $"• {f.Tweak.Name}\n  {f.Result.Message}")));
        }

        _log.Write($"Jogos: {batch.SucceededCount} ok, {batch.FailedCount} falhas");
        _state.NotifyStateChanged();
    }
}
