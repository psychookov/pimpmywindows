using System.Windows.Input;
using PimpMyWindows.Core.Benchmarks;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

/// <summary>Teste de estresse de CPU e memória, com limite de tempo e botão de cancelar.</summary>
public sealed class StressPageViewModel : PageViewModel
{
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private readonly IActionLog _log;
    private CancellationTokenSource? _cancel;
    private bool _isRunning;
    private int _progress;
    private string _liveText = string.Empty;
    private string _resultText = string.Empty;
    private string _resultKey = "Info";
    private double _totalSeconds = 1;

    public StressPageViewModel(IUserPrompt prompt, AppState state, IActionLog log)
    {
        _prompt = prompt;
        _state = state;
        _log = log;
        StartCommand = new AsyncRelayCommand(p => RunAsync(int.Parse((string)p!)), _ => !IsRunning);
        CancelCommand = new RelayCommand(_ => _cancel?.Cancel(), _ => IsRunning);
    }

    public override string Key => "stress";

    public ICommand StartCommand { get; }

    public ICommand CancelCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public int Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string LiveText
    {
        get => _liveText;
        private set => SetProperty(ref _liveText, value);
    }

    public string ResultText
    {
        get => _resultText;
        private set
        {
            if (SetProperty(ref _resultText, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    public bool HasResult => _resultText.Length > 0;

    /// <summary>Ok | Warn | Bad | Info (define a cor).</summary>
    public string ResultKey
    {
        get => _resultKey;
        private set => SetProperty(ref _resultKey, value);
    }

    public override void OnHidden()
    {
        // Um teste pesado não deve continuar escondido quando o usuário sai da página.
        _cancel?.Cancel();
    }

    private async Task RunAsync(int minutes)
    {
        if (!_prompt.Confirm(
                "Teste de estresse",
                $"O teste vai usar 100% do processador e uma parte da memória por {minutes} minuto(s). " +
                "O computador pode esquentar, os ventiladores vão acelerar e ele fica lento durante o teste.\n\n" +
                "Salve o que estiver aberto e feche outros programas. Você pode cancelar a qualquer momento. " +
                "O teste não grava nada no disco nem altera o Windows.\n\nComeçar agora?"))
        {
            return;
        }

        var duration = TimeSpan.FromMinutes(minutes);
        _totalSeconds = duration.TotalSeconds;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        IsRunning = true;
        Progress = 0;
        ResultText = string.Empty;
        LiveText = "Aquecendo…";
        _state.Say("Teste de estresse em andamento…");

        var context = SynchronizationContext.Current;
        void OnProgress(int second, double ops) => context?.Post(
            _ =>
            {
                Progress = (int)(second / _totalSeconds * 100);
                LiveText = $"{second}s de {(int)_totalSeconds}s · {ops / 1e6:N0} milhões de operações/s";
            },
            null);

        try
        {
            var memory = ChooseMemoryBytes();
            var result = await Task.Run(() => StressTest.Run(duration, memory, OnProgress, token));
            ResultText = result.Text;
            ResultKey = result.Verdict switch
            {
                StressVerdict.Stable => "Ok",
                StressVerdict.Throttled => "Warn",
                StressVerdict.MemoryErrors => "Bad",
                _ => "Info",
            };
            _state.Say("Teste de estresse terminado.");
            _log.Write($"Teste de estresse: {result.Verdict}, queda {result.DropPercent:0}%, erros de memória {result.MemoryErrors}");
        }
        finally
        {
            IsRunning = false;
            Progress = 0;
            LiveText = string.Empty;
            _cancel?.Dispose();
            _cancel = null;
        }
    }

    /// <summary>Usa um oitavo da memória livre, entre 64 MB e 512 MB, para não sufocar o Windows.</summary>
    private static int ChooseMemoryBytes()
    {
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var target = Math.Clamp(available / 8, 64L * 1024 * 1024, 512L * 1024 * 1024);
        return (int)target;
    }
}
