using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PimpMyWindows.Core;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IReadOnlyList<TweakDefinition> _catalog;
    private readonly TweakEngine _engine;
    private readonly StartupService _startup;
    private readonly RestorePointService _restorePoints;
    private readonly IUserPrompt _prompt;
    private readonly IActionLog _log;
    private string _statusText = "Pronto. Nada é alterado até você clicar em Aplicar.";

    public MainViewModel(
        IReadOnlyList<TweakDefinition> catalog,
        TweakEngine engine,
        StartupService startup,
        RestorePointService restorePoints,
        IUserPrompt prompt,
        IActionLog log,
        bool isAdministrator,
        string osDescription)
    {
        _catalog = catalog;
        _engine = engine;
        _startup = startup;
        _restorePoints = restorePoints;
        _prompt = prompt;
        _log = log;
        IsAdministrator = isAdministrator;
        OsDescription = osDescription;

        ToggleTweakCommand = new RelayCommand(p => ToggleTweak((TweakItemViewModel)p!), p => p is TweakItemViewModel { CanApply: true });
        ToggleStartupCommand = new RelayCommand(p => ToggleStartup((StartupItemViewModel)p!), p => p is StartupItemViewModel);
        RestorePointCommand = new AsyncRelayCommand(_ => CreateRestorePointAsync());
        ElevateCommand = new RelayCommand(_ => Elevate(), _ => !IsAdministrator);
        RefreshCommand = new RelayCommand(_ => Refresh());

        Refresh();
    }

    public string OsDescription { get; }

    public bool IsAdministrator { get; }

    public bool ShowAdminBanner => !IsAdministrator;

    public string AdminStatusText => IsAdministrator
        ? "Executando como administrador"
        : "Modo normal (alguns ajustes exigem administrador)";

    public ObservableCollection<TweakGroupViewModel> Groups { get; } = [];

    public ObservableCollection<StartupItemViewModel> StartupItems { get; } = [];

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand ToggleTweakCommand { get; }

    public ICommand ToggleStartupCommand { get; }

    public ICommand RestorePointCommand { get; }

    public ICommand ElevateCommand { get; }

    public ICommand RefreshCommand { get; }

    private void Refresh()
    {
        Groups.Clear();
        foreach (var group in _catalog.GroupBy(t => t.Category))
        {
            var items = group.Select(t => new TweakItemViewModel(t, _engine.GetState(t))).ToList();
            Groups.Add(new TweakGroupViewModel(CategoryTitle(group.Key), items));
        }

        LoadStartup();
    }

    private void LoadStartup()
    {
        StartupItems.Clear();
        foreach (var entry in _startup.List())
        {
            StartupItems.Add(new StartupItemViewModel(entry));
        }
    }

    private void ToggleTweak(TweakItemViewModel item)
    {
        var tweak = item.Definition;
        var reverting = item.State is TweakState.Applied or TweakState.Partial;

        if (!reverting && NeedsConfirmation(tweak) && !_prompt.Confirm(tweak.Name, BuildConfirmation(tweak)))
        {
            StatusText = "Cancelado. Nada foi alterado.";
            return;
        }

        var result = reverting ? _engine.Revert(tweak) : _engine.Apply(tweak);
        item.State = _engine.GetState(tweak);
        StatusText = result.Message;

        if (!result.Success)
        {
            _prompt.ShowError(tweak.Name, result.Message);
        }
    }

    private void ToggleStartup(StartupItemViewModel item)
    {
        var result = _startup.SetEnabled(item.Entry, !item.IsEnabled);
        StatusText = result.Message;

        if (!result.Success)
        {
            _prompt.ShowError(item.Name, result.Message);
        }

        LoadStartup();
    }

    private async Task CreateRestorePointAsync()
    {
        StatusText = "Criando ponto de restauração… isso pode levar um minuto.";
        var result = await _restorePoints.CreateAsync("Pimp my Windows - antes de otimizar");
        StatusText = result.Message;

        if (result.Success)
        {
            _prompt.ShowInfo("Ponto de restauração", result.Message);
        }
        else
        {
            _prompt.ShowError("Ponto de restauração", result.Message);
        }
    }

    private void Elevate()
    {
        if (!_prompt.Confirm(
                "Executar como administrador",
                "O programa será fechado e aberto novamente com permissão de administrador. Continuar?"))
        {
            return;
        }

        if (ElevationHelper.TryStartElevatedInstance())
        {
            _log.Write("Reiniciando como administrador a pedido do usuário.");
            Application.Current.Shutdown();
        }
        else
        {
            StatusText = "A permissão de administrador não foi concedida. Continuamos no modo normal.";
        }
    }

    private static bool NeedsConfirmation(TweakDefinition tweak) =>
        tweak.Risk != RiskLevel.Safe || !string.IsNullOrEmpty(tweak.Warning);

    private static string BuildConfirmation(TweakDefinition tweak)
    {
        var message = $"{tweak.Summary}\n\n{tweak.Details}";
        if (!string.IsNullOrEmpty(tweak.Warning))
        {
            message += $"\n\nAtenção: {tweak.Warning}";
        }

        return message + "\n\nVocê pode reverter depois pelo botão Reverter. Aplicar agora?";
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
