using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class DriverItemViewModel
{
    public DriverItemViewModel(DriverEntry entry)
    {
        Entry = entry;
        SearchIndex = TweakItemViewModel.Normalize($"{entry.DeviceName} {entry.Manufacturer} {entry.DeviceClass}");
    }

    public DriverEntry Entry { get; }

    public string SearchIndex { get; }

    public string Name => Entry.DeviceName;

    public bool IsOld => Entry.IsOldWatched;

    public bool IsUnsigned => Entry.IsSigned == false;

    public string Meta
    {
        get
        {
            var parts = new List<string>();
            if (Entry.Manufacturer.Length > 0)
            {
                parts.Add(Entry.Manufacturer);
            }

            if (Entry.Version.Length > 0)
            {
                parts.Add("versão " + Entry.Version);
            }

            if (Entry.Date is not null)
            {
                parts.Add(Entry.Date.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")));
            }

            if (Entry.DeviceClass.Length > 0)
            {
                parts.Add(Entry.DeviceClass);
            }

            return string.Join(" · ", parts);
        }
    }
}

public sealed class DriversPageViewModel : PageViewModel
{
    private readonly DriverService _service;
    private readonly DriverBackupService _backup;
    private readonly IFileDialogs _dialogs;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private readonly List<DriverItemViewModel> _all = [];
    private bool _loaded;
    private bool _isLoading;
    private string _filter = "all";
    private string _searchText = string.Empty;
    private string _summaryText = string.Empty;

    public DriversPageViewModel(
        DriverService service,
        DriverBackupService backup,
        IFileDialogs dialogs,
        IUserPrompt prompt,
        AppState state,
        IActionLog log)
    {
        _service = service;
        _backup = backup;
        _dialogs = dialogs;
        _prompt = prompt;
        _state = state;
        _log = log;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsLoading);
        BackupCommand = new AsyncRelayCommand(_ => BackupAsync());
        RestoreCommand = new AsyncRelayCommand(_ => RestoreAsync());
        SetFilterCommand = new RelayCommand(p => Filter = (string)p!);
        OpenWindowsUpdateCommand = new RelayCommand(_ => ShellLauncher.Open("ms-settings:windowsupdate", _prompt));
        OpenDeviceManagerCommand = new RelayCommand(_ => ShellLauncher.Open("devmgmt.msc", _prompt));
    }

    public override string Key => "drivers";

    public ObservableCollection<DeviceProblem> Problems { get; } = [];

    public ObservableCollection<DriverItemViewModel> Items { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string Filter
    {
        get => _filter;
        private set
        {
            if (SetProperty(ref _filter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public int ProblemCount => Problems.Count;

    public ICommand RefreshCommand { get; }

    public ICommand BackupCommand { get; }

    public ICommand RestoreCommand { get; }

    public ICommand SetFilterCommand { get; }

    public ICommand OpenWindowsUpdateCommand { get; }

    public ICommand OpenDeviceManagerCommand { get; }

    public override void OnShown()
    {
        if (!_loaded)
        {
            _ = LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        _state.Say("Lendo os drivers instalados… pode levar alguns segundos.");
        try
        {
            var report = await Task.Run(_service.Load);

            _all.Clear();
            _all.AddRange(report.Drivers.Select(d => new DriverItemViewModel(d)));

            Problems.Clear();
            foreach (var problem in report.Problems)
            {
                Problems.Add(problem);
            }

            OnPropertyChanged(nameof(ProblemCount));
            var old = _all.Count(i => i.IsOld);
            SummaryText = $"{_all.Count} drivers instalados · {Problems.Count} aparelho(s) com problema · {old} para dar uma olhada";
            ApplyFilter();
            _loaded = true;
            _state.Say(report.Errors.Count == 0 ? "Drivers lidos. Nada foi alterado." : string.Join(" ", report.Errors));
        }
        catch (Exception ex)
        {
            // Limite da tela: qualquer falha inesperada vira uma mensagem, nunca derruba o programa.
            _log.Write($"ERRO inesperado ao ler drivers: {ex}");
            _prompt.ShowError("Drivers", $"Não foi possível ler os drivers. Detalhe: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = TweakItemViewModel.Normalize(SearchText.Trim());
        var visible = _all
            .Where(i => Filter switch
            {
                "old" => i.IsOld,
                "problems" => i.IsUnsigned,
                _ => true,
            })
            .Where(i => query.Length == 0 || i.SearchIndex.Contains(query, StringComparison.Ordinal));

        Items.Clear();
        foreach (var item in visible)
        {
            Items.Add(item);
        }
    }

    private async Task BackupAsync()
    {
        if (!_prompt.Confirm(
                "Backup de drivers",
                "O programa vai salvar uma cópia de todos os drivers instalados em uma pasta nova que você escolher " +
                "(pode ocupar alguns GB e levar alguns minutos). Nada no Windows é alterado.\n\nContinuar?"))
        {
            return;
        }

        var folder = _dialogs.PickFolder("Escolha onde salvar o backup de drivers");
        if (folder is null)
        {
            return;
        }

        _state.Say("Salvando os drivers… não feche o programa.");
        var result = await _backup.BackupAsync(folder);
        _state.Say(result.Message);
        Report("Backup de drivers", result);
    }

    private async Task RestoreAsync()
    {
        var folder = _dialogs.PickFolder("Escolha a pasta “Drivers-…” criada pelo backup");
        if (folder is null)
        {
            return;
        }

        if (!_prompt.Confirm(
                "Restaurar drivers",
                "Isto reinstala no Windows os drivers guardados nessa pasta. Use só com backups feitos por este programa, " +
                "no mesmo computador ou depois de formatar. Drivers sem assinatura válida são recusados.\n\nContinuar?"))
        {
            return;
        }

        _state.Say("Restaurando os drivers… não feche o programa.");
        var result = await _backup.RestoreAsync(folder);
        _state.Say(result.Message);
        Report("Restaurar drivers", result);
        _loaded = false;
    }

    private void Report(string title, PimpMyWindows.Core.OperationResult result)
    {
        if (result.Success)
        {
            _prompt.ShowInfo(title, result.Message);
        }
        else
        {
            _prompt.ShowError(title, result.Message);
        }
    }
}
