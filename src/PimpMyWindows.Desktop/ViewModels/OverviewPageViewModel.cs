using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

/// <summary>O que o Painel pode pedir ao resto do programa.</summary>
public sealed record OverviewActions(Action<string> Navigate, Action MarkRecommended, Func<Task> CreateRestorePoint);

public sealed class OverviewItemViewModel
{
    public OverviewItemViewModel(string title, string detail, string severityKey, string glyph, string actionText = "", ICommand? action = null)
    {
        Title = title;
        Detail = detail;
        SeverityKey = severityKey;
        Glyph = glyph;
        ActionText = actionText;
        Action = action;
    }

    public string Title { get; }

    public string Detail { get; }

    /// <summary>Ok | Warn | Info (define a cor).</summary>
    public string SeverityKey { get; }

    public string Glyph { get; }

    public string ActionText { get; }

    public ICommand? Action { get; }

    public bool HasAction => Action is not null;
}

public sealed class OverviewPageViewModel : PageViewModel
{
    private const double RingFullDash = 36.13; // (150 - 12) * pi / 12: volta completa do anel, em unidades da espessura.

    private readonly IReadOnlyList<TweakDefinition> _catalog;
    private readonly TweakEngine _engine;
    private readonly StartupService _startup;
    private readonly UndoCenterService _undo;
    private readonly RestorePointCoordinator _restore;
    private readonly OverviewActions _actions;
    private readonly int _windowsBuild;
    private int _percent;
    private string _headline = string.Empty;
    private string _activeText = string.Empty;
    private string _startupText = string.Empty;
    private string _undoText = string.Empty;
    private DoubleCollection _ringDash = new() { 0, 100 };

    public OverviewPageViewModel(
        IReadOnlyList<TweakDefinition> catalog,
        TweakEngine engine,
        StartupService startup,
        UndoCenterService undo,
        RestorePointCoordinator restore,
        AppState state,
        int windowsBuild,
        string osDescription,
        OverviewActions actions)
    {
        _catalog = catalog;
        _engine = engine;
        _startup = startup;
        _undo = undo;
        _restore = restore;
        _actions = actions;
        _windowsBuild = windowsBuild;
        OsDescription = osDescription;
        state.StateChanged += Refresh;
        Refresh();
    }

    public override string Key => "overview";

    public string OsDescription { get; }

    public ObservableCollection<OverviewItemViewModel> Checklist { get; } = [];

    public int Percent
    {
        get => _percent;
        private set
        {
            if (SetProperty(ref _percent, value))
            {
                OnPropertyChanged(nameof(PercentText));
                OnPropertyChanged(nameof(ShowProgress));
            }
        }
    }

    public string PercentText => $"{Percent}%";

    public bool ShowProgress => Percent > 0;

    public DoubleCollection RingDash
    {
        get => _ringDash;
        private set => SetProperty(ref _ringDash, value);
    }

    public string Headline
    {
        get => _headline;
        private set => SetProperty(ref _headline, value);
    }

    public string ActiveText
    {
        get => _activeText;
        private set => SetProperty(ref _activeText, value);
    }

    public string StartupText
    {
        get => _startupText;
        private set => SetProperty(ref _startupText, value);
    }

    public string UndoText
    {
        get => _undoText;
        private set => SetProperty(ref _undoText, value);
    }

    public string PercentExplanation =>
        "Percentual dos ajustes marcados como “recomendados” que já estão ativos. " +
        "Não é uma nota de desempenho: só mostra o quanto do básico seguro você já aplicou.";

    public override void OnShown() => Refresh();

    private void Refresh()
    {
        var applicable = _catalog.Where(t => TweakApplicability.IsApplicable(t, _windowsBuild)).ToList();
        var states = applicable.ToDictionary(t => t.Id, t => _engine.GetState(t));
        var recommended = applicable.Where(t => t.Recommended).ToList();
        var recommendedApplied = recommended.Count(t => states[t.Id] == TweakState.Applied);
        var activeCount = states.Values.Count(s => s == TweakState.Applied);

        Percent = recommended.Count == 0 ? 0 : recommendedApplied * 100 / recommended.Count;
        RingDash = new DoubleCollection { Percent / 100.0 * RingFullDash, 100 };
        ActiveText = $"{activeCount} de {applicable.Count}";

        var entries = _startup.List();
        var enabledStartup = entries.Count(e => e.IsEnabled);
        var missingStartup = entries.Count(e => e.FileMissing);
        StartupText = enabledStartup.ToString(CultureInfo.InvariantCulture);

        var undoEntries = _undo.List();
        var drifted = undoEntries.Count(e => e.WasUndoneOutside);
        UndoText = undoEntries.Count.ToString(CultureInfo.InvariantCulture);

        Headline = Percent switch
        {
            100 => "O essencial já está aplicado",
            0 => "Vamos começar pelo essencial",
            _ => "Você já aplicou parte do essencial",
        };

        Checklist.Clear();

        var pending = recommended.Count - recommendedApplied;
        Checklist.Add(pending > 0
            ? new OverviewItemViewModel(
                pending == 1 ? "1 ajuste recomendado ainda não foi aplicado" : $"{pending} ajustes recomendados ainda não foram aplicados",
                "São ajustes seguros e reversíveis, do Modo Básico. Você confere tudo antes de aplicar.",
                "Warn",
                "",
                "Revisar e aplicar",
                new RelayCommand(_ => _actions.MarkRecommended()))
            : new OverviewItemViewModel(
                "Todos os ajustes recomendados estão aplicados",
                "Quer ir além? Veja os demais ajustes e perfis prontos.",
                "Ok",
                "",
                "Ver ajustes",
                new RelayCommand(_ => _actions.Navigate("tweaks"))));

        if (drifted > 0)
        {
            Checklist.Add(new OverviewItemViewModel(
                drifted == 1 ? "1 ajuste foi desfeito fora do programa" : $"{drifted} ajustes foram desfeitos fora do programa",
                "Normalmente uma atualização do Windows. Você pode aplicar de novo ou limpar o registro.",
                "Warn",
                "",
                "Abrir Central Desfazer",
                new RelayCommand(_ => _actions.Navigate("undo"))));
        }

        Checklist.Add(missingStartup > 0
            ? new OverviewItemViewModel(
                missingStartup == 1
                    ? "1 item de inicialização aponta para um programa que não existe mais"
                    : $"{missingStartup} itens de inicialização apontam para programas que não existem mais",
                "Costumam ser sobras de programas desinstalados. Desativar é seguro e reversível.",
                "Warn",
                "",
                "Ver inicialização",
                new RelayCommand(_ => _actions.Navigate("startup")))
            : new OverviewItemViewModel(
                $"{enabledStartup} programas iniciam com o Windows",
                "Menos programas na inicialização deixam o computador pronto mais rápido.",
                "Info",
                "",
                "Ver inicialização",
                new RelayCommand(_ => _actions.Navigate("startup"))));

        var last = _restore.LastCreatedAt;
        Checklist.Add(last is null
            ? new OverviewItemViewModel(
                "Nenhum ponto de restauração criado por aqui",
                "É uma segurança extra: com ele você volta o Windows inteiro a um momento anterior.",
                "Info",
                "",
                "Criar agora",
                new AsyncRelayCommand(_ => _actions.CreateRestorePoint()))
            : new OverviewItemViewModel(
                "Último ponto de restauração: " + last.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")),
                "Criado por este programa.",
                "Ok",
                "",
                "Criar outro",
                new AsyncRelayCommand(_ => _actions.CreateRestorePoint())));

        Checklist.Add(new OverviewItemViewModel(
            OsDescription,
            $"Build {_windowsBuild}. Só aparecem os ajustes que existem na sua versão do Windows.",
            "Info",
            ""));
    }
}
