using System.Collections.ObjectModel;
using System.Windows.Input;
using PimpMyWindows.Core.Benchmarks;
using PimpMyWindows.Core.Power;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class PowerPlanItemViewModel
{
    public PowerPlanItemViewModel(PowerPlan plan)
    {
        Plan = plan;
    }

    public PowerPlan Plan { get; }

    public string Name => Plan.Name;

    public bool IsActive => Plan.IsActive;

    public bool CanActivate => !Plan.IsActive;

    public string Hint => Plan.Kind switch
    {
        PowerPlanKind.Balanced => "O recomendado para o dia a dia: acelera quando precisa e economiza quando está parado.",
        PowerPlanKind.HighPerformance => "Mantém o processador mais acelerado. Gasta mais energia e esquenta mais.",
        PowerPlanKind.PowerSaver => "Reduz a velocidade para economizar bateria. Bom longe da tomada.",
        _ => "Plano personalizado.",
    };
}

/// <summary>Planos de energia do Windows: ver, trocar com volta garantida e criar o Desempenho máximo.</summary>
public sealed class PowerPageViewModel : PageViewModel
{
    private readonly PowerPlanService _service;
    private readonly BatteryService _battery;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private bool _isBusy;
    private bool _canRestore;
    private string _notebookText = string.Empty;

    public PowerPageViewModel(PowerPlanService service, BatteryService battery, IUserPrompt prompt, AppState state)
    {
        _service = service;
        _battery = battery;
        _prompt = prompt;
        _state = state;
        ActivateCommand = new AsyncRelayCommand(p => ActivateAsync((PowerPlanItemViewModel)p!), _ => !_isBusy);
        UltimateCommand = new AsyncRelayCommand(_ => UltimateAsync(), _ => !_isBusy);
        RestoreCommand = new AsyncRelayCommand(_ => RestoreAsync(), _ => !_isBusy && CanRestore);
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !_isBusy);
    }

    public override string Key => "power";

    public ObservableCollection<PowerPlanItemViewModel> Plans { get; } = [];

    public ICommand ActivateCommand { get; }

    public ICommand UltimateCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand RefreshCommand { get; }

    public bool CanRestore
    {
        get => _canRestore;
        private set => SetProperty(ref _canRestore, value);
    }

    public string NotebookText
    {
        get => _notebookText;
        private set
        {
            if (SetProperty(ref _notebookText, value))
            {
                OnPropertyChanged(nameof(HasNotebookText));
            }
        }
    }

    public bool HasNotebookText => _notebookText.Length > 0;

    public override void OnShown() => _ = LoadAsync();

    private async Task LoadAsync()
    {
        _isBusy = true;
        try
        {
            IReadOnlyList<PowerPlan> plans;
            try
            {
                plans = await _service.ListAsync();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException or InvalidOperationException)
            {
                _state.Say("Não foi possível ler os planos de energia: " + ex.Message);
                return;
            }

            Plans.Clear();
            foreach (var plan in plans.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name))
            {
                Plans.Add(new PowerPlanItemViewModel(plan));
            }

            CanRestore = _service.PreviousPlanId() is not null;
            var battery = await Task.Run(() => _battery.Read());
            NotebookText = battery.HasBattery
                ? "Este computador tem bateria (notebook). Dica: use Equilibrado ou Economia longe da tomada; Desempenho máximo gasta a bateria rápido e esquenta mais, então deixe para quando estiver ligado na energia."
                : string.Empty;
        }
        finally
        {
            _isBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ActivateAsync(PowerPlanItemViewModel item)
    {
        await RunAsync(() => _service.ActivateAsync(item.Plan.Id));
    }

    private async Task UltimateAsync()
    {
        if (!_prompt.Confirm(
                "Desempenho máximo",
                "Vai criar o plano “Desempenho máximo” do Windows e ativá-lo. Ele mantém o processador sempre pronto, " +
                "o que gasta mais energia e esquenta mais (em notebook, só vale na tomada). Seus planos atuais não são alterados " +
                "e você volta ao plano de hoje com “Voltar ao plano anterior”.\n\nContinuar?"))
        {
            return;
        }

        await RunAsync(() => _service.AddUltimatePerformanceAsync());
    }

    private async Task RestoreAsync() => await RunAsync(() => _service.RestorePreviousAsync());

    private async Task RunAsync(Func<Task<PowerResult>> action)
    {
        _isBusy = true;
        CommandManager.InvalidateRequerySuggested();
        PowerResult result;
        try
        {
            result = await action();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or TimeoutException or InvalidOperationException)
        {
            result = new PowerResult(false, ex.Message);
        }
        finally
        {
            _isBusy = false;
        }

        if (result.Success)
        {
            _state.Say(result.Message);
        }
        else
        {
            _prompt.ShowError("Plano de energia", result.Message);
        }

        await LoadAsync();
        _state.NotifyStateChanged();
    }
}
