using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed record InfoRow(string Label, string Value);

public sealed class InfoSection
{
    public InfoSection(string title, IReadOnlyList<InfoRow> rows)
    {
        Title = title;
        Rows = rows;
    }

    public string Title { get; }

    public IReadOnlyList<InfoRow> Rows { get; }
}

/// <summary>Meu PC: o que há dentro do computador. Somente leitura.</summary>
public sealed class PcPageViewModel : PageViewModel
{
    private readonly HardwareService _hardware;
    private readonly DriverService _drivers;
    private readonly DiskHealthService _disks;
    private readonly AppState _state;
    private readonly IUserPrompt _prompt;
    private readonly IActionLog _log;
    private bool _loaded;
    private bool _isLoading;
    private HardwareSnapshot? _snapshot;

    public PcPageViewModel(
        HardwareService hardware,
        DriverService drivers,
        DiskHealthService disks,
        AppState state,
        IUserPrompt prompt,
        IActionLog log)
    {
        _hardware = hardware;
        _drivers = drivers;
        _disks = disks;
        _state = state;
        _prompt = prompt;
        _log = log;

        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsLoading);
        CopyReportCommand = new AsyncRelayCommand(_ => CopyReportAsync(), _ => !IsLoading && _snapshot is not null);
    }

    public override string Key => "pc";

    public ObservableCollection<InfoSection> Sections { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }

    public ICommand CopyReportCommand { get; }

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
        _state.Say("Lendo as informações do seu computador…");
        try
        {
            var snapshot = await Task.Run(_hardware.Collect);
            _snapshot = snapshot;
            Build(snapshot);
            _loaded = true;
            _state.Say("Informações atualizadas. Nada foi alterado: esta tela só lê.");
        }
        catch (Exception ex)
        {
            // Limite da tela: qualquer falha inesperada vira uma mensagem, nunca derruba o programa.
            _log.Write($"ERRO inesperado ao ler o hardware: {ex}");
            _state.Say("Não foi possível ler as informações do computador.");
            _prompt.ShowError("Meu PC", $"Não foi possível ler as informações. Detalhe: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Build(HardwareSnapshot hw)
    {
        Sections.Clear();

        Sections.Add(new InfoSection("Windows",
        [
            new("Sistema", hw.OsName),
            new("Versão", hw.OsVersion),
            new("Arquitetura", hw.Architecture),
        ]));

        if (hw.Board.Length > 0)
        {
            Sections.Add(new InfoSection("Placa-mãe", [new("Modelo", hw.Board)]));
        }

        Sections.Add(new InfoSection("Processador",
        [
            new("Modelo", hw.CpuName),
            new("Núcleos / threads", $"{hw.CpuCores} / {hw.CpuThreads}"),
            new("Velocidade máxima", hw.CpuMaxMhz is null ? "Não informada" : $"{hw.CpuMaxMhz / 1000.0:0.0} GHz"),
        ]));

        var memoryRows = new List<InfoRow> { new("Total", HardwareService.FormatBytes(hw.RamTotalBytes)) };
        for (var i = 0; i < hw.RamModules.Count; i++)
        {
            var module = hw.RamModules[i];
            var speed = module.SpeedMhz is > 0 ? $" · {module.SpeedMhz} MHz" : string.Empty;
            memoryRows.Add(new InfoRow($"Pente {i + 1}", $"{HardwareService.FormatBytes(module.CapacityBytes)}{speed} · {module.Manufacturer}".TrimEnd(' ', '·')));
        }

        Sections.Add(new InfoSection("Memória", memoryRows));

        if (hw.Gpus.Count > 0)
        {
            Sections.Add(new InfoSection(
                "Placa de vídeo",
                hw.Gpus.Select(g => new InfoRow(
                    g.Name,
                    $"driver {g.DriverVersion}" + (g.DriverDate is null ? string.Empty : $" de {g.DriverDate:dd/MM/yyyy}"))).ToList()));
        }

        if (hw.Errors.Count > 0)
        {
            Sections.Add(new InfoSection("Não foi possível ler", hw.Errors.Select(e => new InfoRow("Aviso", e)).ToList()));
        }
    }

    private async Task CopyReportAsync()
    {
        if (_snapshot is null)
        {
            return;
        }

        _state.Say("Montando o relatório…");
        var report = await Task.Run(() => SupportReport.Build(_snapshot, _drivers.Load(), _disks.Load()));

        try
        {
            Clipboard.SetText(report);
            _state.Say("Relatório copiado. Cole onde precisar. Ele não leva nome, nome do PC nem números de série.");
        }
        catch (ExternalException)
        {
            _prompt.ShowError("Copiar relatório", "O Windows não deixou usar a área de transferência agora. Tente de novo.");
        }
    }
}
