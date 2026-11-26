using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;
using PimpMyWindows.Desktop.Theme;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class NavItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _badgeText = string.Empty;

    public NavItemViewModel(PageViewModel page, string title, string glyph)
    {
        Page = page;
        Title = title;
        Glyph = glyph;
    }

    public PageViewModel Page { get; }

    public string Key => Page.Key;

    public string Title { get; }

    public string Glyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string BadgeText
    {
        get => _badgeText;
        set
        {
            if (SetProperty(ref _badgeText, value))
            {
                OnPropertyChanged(nameof(HasBadge));
            }
        }
    }

    public bool HasBadge => _badgeText.Length > 0;
}

/// <summary>A janela principal: menu lateral, página atual, modo, tema e ações globais.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly IUserPrompt _prompt;
    private readonly IActionLog _log;
    private readonly ThemeController _theme;
    private readonly RestorePointCoordinator _restore;
    private readonly UndoCenterService _undo;
    private readonly NavItemViewModel _undoItem;
    private PageViewModel _currentPage;

    public ShellViewModel(
        OverviewPageViewModel overview,
        TweaksPageViewModel tweaks,
        StartupPageViewModel startup,
        PcPageViewModel pc,
        DriversPageViewModel drivers,
        DisksPageViewModel disks,
        TestsPageViewModel tests,
        StressPageViewModel stress,
        CleanupPageViewModel cleanup,
        GamesPageViewModel games,
        ControllerPageViewModel controller,
        UndoPageViewModel undoPage,
        AboutPageViewModel about,
        UndoCenterService undo,
        AppState state,
        IUserPrompt prompt,
        IActionLog log,
        ThemeController theme,
        RestorePointCoordinator restore,
        bool isAdministrator,
        string osDescription)
    {
        _state = state;
        _prompt = prompt;
        _log = log;
        _theme = theme;
        _restore = restore;
        _undo = undo;
        IsAdministrator = isAdministrator;
        OsDescription = osDescription;

        _undoItem = new NavItemViewModel(undoPage, "Central Desfazer", "");
        NavItems =
        [
            new NavItemViewModel(overview, "Painel", ""),
            new NavItemViewModel(tweaks, "Ajustes", ""),
            new NavItemViewModel(startup, "Inicialização", ""),
            new NavItemViewModel(pc, "Meu PC", "\uE7F8"),
            new NavItemViewModel(drivers, "Drivers", "\uE772"),
            new NavItemViewModel(disks, "Discos", "\uEDA2"),
            new NavItemViewModel(tests, "Testes", "\uE9D9"),
            new NavItemViewModel(stress, "Estresse", "\uE9F5"),
            new NavItemViewModel(cleanup, "Limpeza", "\uE74D"),
            new NavItemViewModel(games, "Jogos", "\uE7FC"),
            new NavItemViewModel(controller, "Teste de controle", "\uE765"),
            _undoItem,
            new NavItemViewModel(about, "Sobre", ""),
        ];

        _currentPage = overview;
        NavItems[0].IsSelected = true;
        UpdateUndoBadge();

        NavigateCommand = new RelayCommand(p => Navigate((string)p!));
        ToggleModeCommand = new RelayCommand(_ => ToggleMode());
        ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        ElevateCommand = new RelayCommand(_ => Elevate(), _ => !IsAdministrator);
        RestorePointCommand = new AsyncRelayCommand(_ => CreateRestorePointAsync());

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.StatusText))
            {
                OnPropertyChanged(nameof(StatusText));
            }
            else if (e.PropertyName == nameof(AppState.IsTechnicalMode))
            {
                OnPropertyChanged(nameof(IsTechnicalMode));
                OnPropertyChanged(nameof(ModeButtonText));
            }
        };
        _state.StateChanged += UpdateUndoBadge;
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string StatusText => _state.StatusText;

    public bool IsTechnicalMode => _state.IsTechnicalMode;

    public string ModeButtonText => IsTechnicalMode ? "Modo Técnico" : "Modo Básico";

    public string ThemeButtonText => _theme.IsLight ? "Tema escuro" : "Tema claro";

    public string OsDescription { get; }

    public bool IsAdministrator { get; }

    public bool ShowAdminBanner => !IsAdministrator;

    public string AdminStatusText => IsAdministrator
        ? "Executando como administrador"
        : "Modo normal (alguns ajustes exigem administrador)";

    public ICommand NavigateCommand { get; }

    public ICommand ToggleModeCommand { get; }

    public ICommand ToggleThemeCommand { get; }

    public ICommand ElevateCommand { get; }

    public ICommand RestorePointCommand { get; }

    public void Navigate(string key)
    {
        var target = NavItems.FirstOrDefault(i => i.Key == key);
        if (target is null)
        {
            return;
        }

        foreach (var item in NavItems)
        {
            item.IsSelected = ReferenceEquals(item, target);
        }

        if (!ReferenceEquals(CurrentPage, target.Page))
        {
            CurrentPage.OnHidden();
        }

        target.Page.OnShown();
        CurrentPage = target.Page;
    }

    private void UpdateUndoBadge()
    {
        var count = _undo.List().Count;
        _undoItem.BadgeText = count > 0 ? count.ToString() : string.Empty;
    }

    private void ToggleMode()
    {
        if (!IsTechnicalMode)
        {
            if (!_prompt.Confirm(
                    "Modo Técnico",
                    "O Modo Técnico mostra ajustes avançados que podem afetar o funcionamento do Windows e que " +
                    "exigem conhecer o que cada um faz.\n\n" +
                    "Continuam valendo as proteções: prévia do que muda, cópia de segurança antes de alterar, " +
                    "Central Desfazer e log. Recomendamos criar um ponto de restauração antes.\n\nAtivar o Modo Técnico?"))
            {
                _state.Say("Continuamos no Modo Básico.");
                return;
            }

            _state.IsTechnicalMode = true;
            _log.Write("Modo Técnico ativado pelo usuário.");
            _state.Say("Modo Técnico ativado. Leia a explicação de cada ajuste antes de aplicar.");
            return;
        }

        _state.IsTechnicalMode = false;
        _log.Write("Modo Básico ativado pelo usuário.");
        _state.Say("Modo Básico ativado.");
    }

    private void ToggleTheme()
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(ThemeButtonText));
    }

    private async Task CreateRestorePointAsync()
    {
        var result = await _restore.CreateAsync();
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
            _state.Say("A permissão de administrador não foi concedida. Continuamos no modo normal.");
        }
    }
}
