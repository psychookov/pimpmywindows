using System.Collections.ObjectModel;
using System.Windows.Input;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class DiskItemViewModel
{
    public DiskItemViewModel(DiskHealth disk)
    {
        Disk = disk;
    }

    public DiskHealth Disk { get; }

    public string Name => Disk.Name;

    public string Subtitle => string.Join(
        " · ",
        new[] { Disk.MediaType, Disk.Bus, HardwareService.FormatBytes(Disk.SizeBytes) }.Where(s => s.Length > 0));

    public string VerdictText => Disk.VerdictText;

    /// <summary>Ok | Warn | Bad | Info (define a cor).</summary>
    public string SeverityKey => Disk.Verdict switch
    {
        DiskVerdict.Healthy => "Ok",
        DiskVerdict.Attention => "Warn",
        DiskVerdict.Problem => "Bad",
        _ => "Info",
    };

    public string Glyph => Disk.Verdict switch
    {
        DiskVerdict.Healthy => "",
        DiskVerdict.Unknown => "",
        _ => "",
    };

    public string TemperatureText => Disk.TemperatureC is null ? "indisponível" : $"{Disk.TemperatureC} °C";

    public string WearText => Disk.WearPercent is null ? "indisponível" : $"{Disk.WearPercent}% usado";

    public string HoursText => Disk.PowerOnHours is null ? "indisponível" : $"{Disk.PowerOnHours} h";

    public string ErrorsText => Disk.ReadErrors is null && Disk.WriteErrors is null
        ? "indisponível"
        : $"{Disk.ReadErrors ?? 0} leitura · {Disk.WriteErrors ?? 0} escrita";

    public string NotesText => string.Join("\n", Disk.Notes);

    public bool HasNotes => Disk.Notes.Count > 0;
}

public sealed class DisksPageViewModel : PageViewModel
{
    private readonly DiskHealthService _service;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private bool _loaded;
    private bool _isLoading;
    private string _infoText = string.Empty;

    public DisksPageViewModel(DiskHealthService service, IUserPrompt prompt, AppState state, IActionLog log)
    {
        _service = service;
        _prompt = prompt;
        _state = state;
        _log = log;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsLoading);
    }

    public override string Key => "disks";

    public ObservableCollection<DiskItemViewModel> Disks { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Avisos de leitura (ex.: desgaste indisponível neste computador).</summary>
    public string InfoText
    {
        get => _infoText;
        private set => SetProperty(ref _infoText, value);
    }

    public ICommand RefreshCommand { get; }

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
        _state.Say("Lendo a saúde dos discos…");
        try
        {
            var report = await Task.Run(_service.Load);
            Disks.Clear();
            foreach (var disk in report.Disks)
            {
                Disks.Add(new DiskItemViewModel(disk));
            }

            InfoText = string.Join(" ", report.Errors);
            _loaded = true;
            _state.Say("Discos lidos. Nada foi alterado: esta tela só lê.");
        }
        catch (Exception ex)
        {
            // Limite da tela: qualquer falha inesperada vira uma mensagem, nunca derruba o programa.
            _log.Write($"ERRO inesperado ao ler discos: {ex}");
            _prompt.ShowError("Discos", $"Não foi possível ler os discos. Detalhe: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
