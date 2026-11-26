using System.Collections.ObjectModel;
using System.Windows.Input;
using PimpMyWindows.Core.Components;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class ComponentItemViewModel : ObservableObject
{
    private ComponentStatus _status = new(ComponentState.Unknown);
    private bool _isExpanded;

    public ComponentItemViewModel(ManagedComponent component)
    {
        Component = component;
    }

    public ManagedComponent Component { get; }

    public string Name => Component.Name;

    public string Summary => Component.Summary;

    public string Details => Component.Details;

    public string? Warning => Component.Warning;

    public bool HasWarning => !string.IsNullOrEmpty(Component.Warning);

    public bool IsTechnical => Component.Mode == UserMode.Technical;

    public string RiskText => Component.Risk == RiskLevel.Safe ? "Seguro" : "Moderado";

    public string RiskKey => Component.Risk.ToString();

    public bool IsService => Component.Kind == ComponentKind.Service;

    public ComponentStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsOptimized));
                OnPropertyChanged(nameof(IsDefault));
                OnPropertyChanged(nameof(IsAvailable));
            }
        }
    }

    public bool IsOptimized => _status.State == ComponentState.Optimized;

    public bool IsDefault => _status.State == ComponentState.Default;

    public bool IsAvailable => _status.State is ComponentState.Default or ComponentState.Optimized;

    public string StateText => _status.State switch
    {
        ComponentState.Optimized => "Desativado",
        ComponentState.Default => "Como o Windows entrega",
        ComponentState.NotPresent => "Não existe neste Windows",
        _ => "Estado desconhecido",
    };

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

/// <summary>Serviços e tarefas agendadas do Windows que podem ser desativados com segurança, um a um e reversíveis.</summary>
public sealed class ServicesPageViewModel : PageViewModel
{
    private readonly ComponentService _service;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly List<ComponentItemViewModel> _all;
    private bool _isBusy;

    public ServicesPageViewModel(ComponentService service, IUserPrompt prompt, AppState state)
    {
        _service = service;
        _prompt = prompt;
        _state = state;
        _all = ComponentCatalog.All.Select(c => new ComponentItemViewModel(c)).ToList();
        ApplyCommand = new AsyncRelayCommand(p => ApplyAsync((ComponentItemViewModel)p!), _ => !_isBusy);
        RevertCommand = new AsyncRelayCommand(p => RevertAsync((ComponentItemViewModel)p!), _ => !_isBusy);
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !_isBusy);
        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.IsTechnicalMode))
            {
                Rebuild();
            }
        };
    }

    public override string Key => "services";

    public ObservableCollection<ComponentItemViewModel> Services { get; } = [];

    public ObservableCollection<ComponentItemViewModel> Tasks { get; } = [];

    public ICommand ApplyCommand { get; }

    public ICommand RevertCommand { get; }

    public ICommand RefreshCommand { get; }

    public override void OnShown()
    {
        Rebuild();
        _ = LoadAsync();
    }

    private void Rebuild()
    {
        Services.Clear();
        Tasks.Clear();
        foreach (var item in _all.Where(i => _state.IsTechnicalMode || !i.IsTechnical))
        {
            (item.IsService ? Services : Tasks).Add(item);
        }
    }

    private async Task LoadAsync()
    {
        _isBusy = true;
        try
        {
            foreach (var item in _all)
            {
                item.Status = await _service.GetStatusAsync(item.Component);
            }
        }
        finally
        {
            _isBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ApplyAsync(ComponentItemViewModel item)
    {
        var restart = item.IsService ? "\n\nO serviço continua rodando até reiniciar o computador." : string.Empty;
        var warning = item.HasWarning ? $"\n\nAtenção: {item.Warning}" : string.Empty;
        if (!_prompt.Confirm(
                "Desativar",
                $"{item.Name}\n\n{item.Summary}{warning}{restart}\n\nVocê pode restaurar a qualquer momento nesta página. Continuar?"))
        {
            return;
        }

        await RunAsync(item, () => _service.ApplyAsync(item.Component));
    }

    private async Task RevertAsync(ComponentItemViewModel item) =>
        await RunAsync(item, () => _service.RevertAsync(item.Component));

    private async Task RunAsync(ComponentItemViewModel item, Func<Task<ComponentResult>> action)
    {
        _isBusy = true;
        CommandManager.InvalidateRequerySuggested();
        ComponentResult result;
        try
        {
            result = await action();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException or InvalidOperationException)
        {
            result = new ComponentResult(false, ex.Message);
        }
        finally
        {
            _isBusy = false;
        }

        if (result.Success)
        {
            _state.Say($"{item.Name}: {result.Message}");
        }
        else
        {
            _prompt.ShowError(item.Name, result.Message);
        }

        item.Status = await _service.GetStatusAsync(item.Component);
        CommandManager.InvalidateRequerySuggested();
        _state.NotifyStateChanged();
    }
}
