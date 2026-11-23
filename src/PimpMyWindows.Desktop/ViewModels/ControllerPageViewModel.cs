using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using PimpMyWindows.Core.Controllers;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class ButtonIndicatorViewModel : ObservableObject
{
    private bool _isDown;

    public ButtonIndicatorViewModel(ControllerButtons button, string name)
    {
        Button = button;
        Name = name;
    }

    public ControllerButtons Button { get; }

    public string Name { get; }

    public bool IsDown
    {
        get => _isDown;
        set => SetProperty(ref _isDown, value);
    }
}

/// <summary>Teste de controle: mostra em tempo real botões, gatilhos e direcionais, testa vibração e desvio (drift).</summary>
public sealed class ControllerPageViewModel : PageViewModel
{
    private const double StickRadius = 48;
    private const int Slots = 4;

    private readonly IControllerProbe _probe;
    private readonly AppState _state;
    private readonly DispatcherTimer _timer;
    private int _slot = -1;
    private string _statusText = "Procurando controle…";
    private bool _isConnected;
    private int _leftTrigger;
    private int _rightTrigger;
    private double _leftDotX;
    private double _leftDotY;
    private double _rightDotX;
    private double _rightDotY;
    private string _leftValues = string.Empty;
    private string _rightValues = string.Empty;
    private string _pressedText = "Nenhum botão";
    private string _driftText = string.Empty;
    private string _driftKey = "Info";
    private bool _isBusy;
    private ControllerState? _last;

    public ControllerPageViewModel(IControllerProbe probe, AppState state)
    {
        _probe = probe;
        _state = state;
        foreach (var (button, name) in ControllerAnalysis.Names)
        {
            Buttons.Add(new ButtonIndicatorViewModel(button, name));
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Poll();

        VibrateCommand = new AsyncRelayCommand(_ => VibrateAsync(), _ => IsConnected && !_isBusy);
        DriftCommand = new AsyncRelayCommand(_ => DriftAsync(), _ => IsConnected && !_isBusy);
    }

    public override string Key => "controller";

    public ObservableCollection<ButtonIndicatorViewModel> Buttons { get; } = [];

    public ICommand VibrateCommand { get; }

    public ICommand DriftCommand { get; }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int LeftTrigger
    {
        get => _leftTrigger;
        private set => SetProperty(ref _leftTrigger, value);
    }

    public int RightTrigger
    {
        get => _rightTrigger;
        private set => SetProperty(ref _rightTrigger, value);
    }

    public double LeftDotX
    {
        get => _leftDotX;
        private set => SetProperty(ref _leftDotX, value);
    }

    public double LeftDotY
    {
        get => _leftDotY;
        private set => SetProperty(ref _leftDotY, value);
    }

    public double RightDotX
    {
        get => _rightDotX;
        private set => SetProperty(ref _rightDotX, value);
    }

    public double RightDotY
    {
        get => _rightDotY;
        private set => SetProperty(ref _rightDotY, value);
    }

    public string LeftValues
    {
        get => _leftValues;
        private set => SetProperty(ref _leftValues, value);
    }

    public string RightValues
    {
        get => _rightValues;
        private set => SetProperty(ref _rightValues, value);
    }

    public string PressedText
    {
        get => _pressedText;
        private set => SetProperty(ref _pressedText, value);
    }

    public string DriftText
    {
        get => _driftText;
        private set
        {
            if (SetProperty(ref _driftText, value))
            {
                OnPropertyChanged(nameof(HasDriftText));
            }
        }
    }

    public bool HasDriftText => _driftText.Length > 0;

    /// <summary>Ok | Warn | Bad | Info (define a cor).</summary>
    public string DriftKey
    {
        get => _driftKey;
        private set => SetProperty(ref _driftKey, value);
    }

    public override void OnShown()
    {
        DriftText = string.Empty;
        Poll();
        _timer.Start();
    }

    public override void OnHidden()
    {
        _timer.Stop();
        if (_slot >= 0)
        {
            _probe.SetVibration(_slot, 0, 0);
        }
    }

    private void Poll()
    {
        // Mantém o controle já escolhido; só procura outro se ele sumiu.
        var state = _slot >= 0 ? _probe.GetState(_slot) : null;
        if (state is null)
        {
            _slot = -1;
            for (var i = 0; i < Slots; i++)
            {
                state = _probe.GetState(i);
                if (state is not null)
                {
                    _slot = i;
                    break;
                }
            }
        }

        _last = state;
        IsConnected = state is not null;
        if (state is null)
        {
            StatusText = "Nenhum controle encontrado. Conecte um controle de Xbox (ou compatível) e ele aparece aqui. " +
                         "Controles de PlayStation e Switch só aparecem se algum programa, como o Steam Input, os apresentar ao Windows como controle Xbox.";
            Reset();
            return;
        }

        StatusText = $"Controle {_slot + 1} conectado.";
        LeftTrigger = (int)Math.Round(state.LeftTrigger / 2.55);
        RightTrigger = (int)Math.Round(state.RightTrigger / 2.55);
        LeftDotX = ControllerAnalysis.Normalize(state.LeftX) * StickRadius;
        LeftDotY = -ControllerAnalysis.Normalize(state.LeftY) * StickRadius;
        RightDotX = ControllerAnalysis.Normalize(state.RightX) * StickRadius;
        RightDotY = -ControllerAnalysis.Normalize(state.RightY) * StickRadius;
        LeftValues = $"X {ControllerAnalysis.Normalize(state.LeftX):+0.00;-0.00;0.00}   Y {ControllerAnalysis.Normalize(state.LeftY):+0.00;-0.00;0.00}";
        RightValues = $"X {ControllerAnalysis.Normalize(state.RightX):+0.00;-0.00;0.00}   Y {ControllerAnalysis.Normalize(state.RightY):+0.00;-0.00;0.00}";

        foreach (var button in Buttons)
        {
            button.IsDown = state.Buttons.HasFlag(button.Button);
        }

        var pressed = ControllerAnalysis.PressedNames(state.Buttons);
        PressedText = pressed.Count == 0 ? "Nenhum botão" : string.Join(" + ", pressed);
    }

    private void Reset()
    {
        LeftTrigger = RightTrigger = 0;
        LeftDotX = LeftDotY = RightDotX = RightDotY = 0;
        LeftValues = RightValues = string.Empty;
        PressedText = "Nenhum botão";
        foreach (var button in Buttons)
        {
            button.IsDown = false;
        }
    }

    private async Task VibrateAsync()
    {
        if (_slot < 0)
        {
            return;
        }

        _isBusy = true;
        var slot = _slot;
        try
        {
            if (!_probe.SetVibration(slot, 40000, 40000))
            {
                _state.Say("O controle não respondeu ao pedido de vibração.");
                return;
            }

            _state.Say("Vibrando por um instante…");
            await Task.Delay(700);
        }
        finally
        {
            _probe.SetVibration(slot, 0, 0);
            _isBusy = false;
        }

        _state.Say("Teste de vibração concluído.");
    }

    private async Task DriftAsync()
    {
        _isBusy = true;
        try
        {
            DriftKey = "Info";
            DriftText = "Solte os dois direcionais e não encoste neles por 3 segundos…";
            var left = new List<(short, short)>();
            var right = new List<(short, short)>();
            for (var i = 0; i < 60; i++)
            {
                await Task.Delay(50);
                if (_last is null)
                {
                    DriftText = "O controle foi desconectado durante o teste.";
                    DriftKey = "Warn";
                    return;
                }

                left.Add((_last.LeftX, _last.LeftY));
                right.Add((_last.RightX, _last.RightY));
            }

            var l = ControllerAnalysis.EvaluateDrift(left);
            var r = ControllerAnalysis.EvaluateDrift(right);
            DriftText = $"Esquerdo: {l.Text}\nDireito: {r.Text}";
            var worst = (DriftVerdict)Math.Max((int)l.Verdict, (int)r.Verdict);
            DriftKey = worst switch
            {
                DriftVerdict.Good => "Ok",
                DriftVerdict.Slight => "Warn",
                _ => "Bad",
            };
            _state.Say("Teste de desvio concluído.");
        }
        finally
        {
            _isBusy = false;
        }
    }
}
