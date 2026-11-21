using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PimpMyWindows.Core.Benchmarks;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class TestResultRowViewModel
{
    public TestResultRowViewModel(BenchmarkResult result, double? previous)
    {
        Name = result.Name;
        ValueText = Format(result.Value);
        Unit = result.Unit;

        var change = BenchmarkComparison.PercentChange(previous, result.Value);
        ChangeText = BenchmarkComparison.Describe(change, result.HigherIsBetter);
        ChangeKey = change is null
            ? "First"
            : Math.Abs(change.Value) < 3
                ? "Same"
                : (result.HigherIsBetter ? change.Value > 0 : change.Value < 0) ? "Better" : "Worse";
    }

    public string Name { get; }

    public string ValueText { get; }

    public string Unit { get; }

    public string ChangeText { get; }

    /// <summary>First | Same | Better | Worse (define a cor).</summary>
    public string ChangeKey { get; }

    private static string Format(double value) =>
        value >= 100 ? value.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")) : value.ToString("0.0#", CultureInfo.GetCultureInfo("pt-BR"));
}

/// <summary>Testes: desempenho (CPU, memória, disco), rede e bateria, sempre comparando com o teste anterior.</summary>
public sealed class TestsPageViewModel : PageViewModel
{
    private const long DiskTestBytes = 256L * 1024 * 1024;
    private static readonly TimeSpan CpuDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MemoryDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RandomReadDuration = TimeSpan.FromSeconds(4);

    private readonly BenchmarkHistoryStore _history;
    private readonly NetworkTester _network;
    private readonly BatteryService _battery;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly Dictionary<string, TestResultRowViewModel> _rows = [];
    private CancellationTokenSource? _cancel;
    private bool _isBusy;
    private string _busyText = string.Empty;
    private string _networkAdvice = string.Empty;
    private bool _batteryLoaded;

    public TestsPageViewModel(
        BenchmarkHistoryStore history,
        NetworkTester network,
        BatteryService battery,
        IUserPrompt prompt,
        AppState state,
        IActionLog log,
        Func<DateTimeOffset>? now = null)
    {
        _history = history;
        _network = network;
        _battery = battery;
        _prompt = prompt;
        _state = state;
        _log = log;
        _now = now ?? (() => DateTimeOffset.Now);

        RunCpuMemoryCommand = new AsyncRelayCommand(_ => RunCpuMemoryAsync(), _ => !IsBusy);
        RunDiskCommand = new AsyncRelayCommand(_ => RunDiskAsync(), _ => !IsBusy);
        RunAllCommand = new AsyncRelayCommand(_ => RunAllAsync(), _ => !IsBusy);
        RunNetworkCommand = new AsyncRelayCommand(_ => RunNetworkAsync(), _ => !IsBusy);
        RefreshBatteryCommand = new RelayCommand(_ => LoadBattery());
        CancelCommand = new RelayCommand(_ => _cancel?.Cancel(), _ => IsBusy);
    }

    public override string Key => "tests";

    public ObservableCollection<TestResultRowViewModel> Results { get; } = [];

    public ObservableCollection<InfoRow> NetworkLines { get; } = [];

    public ObservableCollection<InfoRow> BatteryLines { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public string NetworkAdvice
    {
        get => _networkAdvice;
        private set => SetProperty(ref _networkAdvice, value);
    }

    public ICommand RunCpuMemoryCommand { get; }

    public ICommand RunDiskCommand { get; }

    public ICommand RunAllCommand { get; }

    public ICommand RunNetworkCommand { get; }

    public ICommand RefreshBatteryCommand { get; }

    public ICommand CancelCommand { get; }

    public override void OnShown()
    {
        if (!_batteryLoaded)
        {
            LoadBattery();
        }
    }

    private const string QuietAdvice = "Feche os outros programas antes de testar: eles tiram desempenho do teste.";

    private async Task RunCpuMemoryAsync()
    {
        await RunBenchmarksAsync(token =>
        {
            var results = new List<BenchmarkResult>();
            Report("Testando o processador…");
            results.AddRange(CpuMemoryBenchmark.RunCpu(CpuDuration, token));
            Report("Testando a memória…");
            results.AddRange(CpuMemoryBenchmark.RunMemory(MemoryDuration, token));
            return results;
        });
    }

    private async Task RunDiskAsync()
    {
        if (!_prompt.Confirm(
                "Teste de disco",
                "O teste cria um arquivo temporário de 256 MB na pasta temporária do Windows, mede a velocidade e apaga o arquivo no fim. " +
                "Nenhum arquivo seu é tocado.\n\nContinuar?"))
        {
            return;
        }

        await RunBenchmarksAsync(token =>
        {
            Report("Testando o disco…");
            var outcome = DiskBenchmark.Run(DiskTestBytes, RandomReadDuration, token);
            if (!outcome.Success)
            {
                throw new InvalidOperationException(outcome.Message);
            }

            return outcome.Value!;
        });
    }

    private async Task RunAllAsync()
    {
        if (!_prompt.Confirm(
                "Testar tudo",
                "Vai testar processador, memória e disco (cerca de 30 segundos). O teste de disco cria um arquivo temporário de 256 MB e apaga no fim. " +
                "Feche os outros programas antes.\n\nContinuar?"))
        {
            return;
        }

        await RunBenchmarksAsync(token =>
        {
            var results = new List<BenchmarkResult>();
            Report("Testando o processador…");
            results.AddRange(CpuMemoryBenchmark.RunCpu(CpuDuration, token));
            Report("Testando a memória…");
            results.AddRange(CpuMemoryBenchmark.RunMemory(MemoryDuration, token));
            Report("Testando o disco…");
            var disk = DiskBenchmark.Run(DiskTestBytes, RandomReadDuration, token);
            if (disk.Success)
            {
                results.AddRange(disk.Value!);
            }
            else
            {
                _state.Say(disk.Message);
            }

            return results;
        });
    }

    private void Report(string text) =>
        System.Windows.Application.Current.Dispatcher.Invoke(() => BusyText = text);

    private async Task RunBenchmarksAsync(Func<CancellationToken, IReadOnlyList<BenchmarkResult>> work)
    {
        using var cancel = new CancellationTokenSource();
        _cancel = cancel;
        IsBusy = true;
        BusyText = "Preparando…";
        _state.Say(QuietAdvice);

        try
        {
            var results = await Task.Run(() => work(cancel.Token));
            var previous = _history.Latest();

            foreach (var result in results)
            {
                previous.TryGetValue(result.Id, out var before);
                var row = new TestResultRowViewModel(result, previous.ContainsKey(result.Id) ? before : null);
                _rows[result.Id] = row;
            }

            Results.Clear();
            foreach (var row in _rows.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Value))
            {
                Results.Add(row);
            }

            _history.Add(results, _now());
            _log.Write($"TESTES concluídos: {results.Count} medições");
            _state.Say("Teste concluído. Cada resultado mostra a comparação com o teste anterior neste computador.");
        }
        catch (OperationCanceledException)
        {
            _state.Say("Teste cancelado.");
        }
        catch (InvalidOperationException ex)
        {
            _state.Say(ex.Message);
            _prompt.ShowError("Teste", ex.Message);
        }
        catch (Exception ex)
        {
            // Limite da tela: qualquer falha inesperada vira uma mensagem, nunca derruba o programa.
            _log.Write($"ERRO inesperado nos testes: {ex}");
            _prompt.ShowError("Teste", $"O teste não pôde ser concluído. Detalhe: {ex.Message}");
        }
        finally
        {
            _cancel = null;
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private async Task RunNetworkAsync()
    {
        if (!_prompt.Confirm(
                "Teste de rede",
                "O teste envia alguns pings ao seu roteador e aos servidores públicos 1.1.1.1 (Cloudflare) e 8.8.8.8 (Google) e consulta o nome de alguns sites " +
                "(microsoft.com, google.com e wikipedia.org). Nenhum dado seu é enviado.\n\nContinuar?"))
        {
            return;
        }

        using var cancel = new CancellationTokenSource();
        _cancel = cancel;
        IsBusy = true;
        BusyText = "Preparando…";

        try
        {
            var progress = new Progress<string>(text => BusyText = text);
            var report = await _network.RunAsync(progress, cancel.Token);
            ShowNetwork(report);
            _state.Say("Teste de rede concluído.");
        }
        catch (OperationCanceledException)
        {
            _state.Say("Teste cancelado.");
        }
        finally
        {
            _cancel = null;
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private void ShowNetwork(NetworkReport report)
    {
        NetworkLines.Clear();
        var advice = new List<string>();

        if (report.Gateway is not null)
        {
            NetworkLines.Add(new InfoRow("Seu roteador", Describe(report.Gateway)));
            if (report.Gateway.AverageMs > 20)
            {
                advice.Add("O roteador demora para responder. Se você usa Wi-Fi, teste por cabo ou chegue mais perto do roteador.");
            }
        }
        else
        {
            NetworkLines.Add(new InfoRow("Seu roteador", "Não encontrado (sem conexão de rede ativa?)"));
        }

        foreach (var target in report.Targets)
        {
            NetworkLines.Add(new InfoRow(target.Name, Describe(target.Stats)));
            if (target.Stats.LossPercent > 2)
            {
                advice.Add($"Houve perda de pacotes até {target.Name}. Isso causa lag em jogos e travamentos em chamadas.");
            }
        }

        foreach (var dns in report.Dns)
        {
            NetworkLines.Add(new InfoRow($"DNS: {dns.Host}", dns.Milliseconds is null ? "falhou" : $"{dns.Milliseconds} ms"));
        }

        if (report.Dns.Any(d => d.Milliseconds > 150))
        {
            advice.Add("A resolução de nomes está lenta. Trocar o DNS do roteador ou do Windows pode ajudar.");
        }

        NetworkAdvice = string.Join("\n", advice.Distinct());
    }

    private static string Describe(NetworkStats stats) =>
        stats.AverageMs is null
            ? $"sem resposta ({stats.LossPercent:0}% de perda)"
            : $"{stats.AverageMs:0} ms em média · variação {stats.JitterMs ?? 0:0} ms · perda {stats.LossPercent:0}%";

    private void LoadBattery()
    {
        var info = _battery.Read();
        BatteryLines.Clear();
        _batteryLoaded = true;

        if (!info.HasBattery)
        {
            BatteryLines.Add(new InfoRow("Bateria", info.Notes.Count > 0 ? info.Notes[0] : "Este computador não tem bateria."));
            return;
        }

        BatteryLines.Add(new InfoRow("Carga", info.ChargePercent is null ? "—" : $"{info.ChargePercent}%"));
        BatteryLines.Add(new InfoRow("Estado", info.Status));
        if (info.DesignedMilliwattHours is not null)
        {
            BatteryLines.Add(new InfoRow("Capacidade de fábrica", $"{info.DesignedMilliwattHours / 1000.0:0.0} Wh"));
        }

        if (info.FullChargeMilliwattHours is not null)
        {
            BatteryLines.Add(new InfoRow("Capacidade atual", $"{info.FullChargeMilliwattHours / 1000.0:0.0} Wh"));
        }

        BatteryLines.Add(new InfoRow("Desgaste", info.WearPercent is null ? "indisponível" : $"{info.WearPercent:0}%"));
        BatteryLines.Add(new InfoRow("Ciclos de carga", info.Cycles is null ? "indisponível" : info.Cycles.ToString()!));

        foreach (var note in info.Notes)
        {
            BatteryLines.Add(new InfoRow("Aviso", note));
        }
    }
}
