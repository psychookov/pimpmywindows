using System.Windows.Media;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class TweakItemViewModel : ObservableObject
{
    private TweakState _state;

    public TweakItemViewModel(TweakDefinition definition, TweakState state)
    {
        Definition = definition;
        _state = state;
    }

    public TweakDefinition Definition { get; }

    public string Name => Definition.Name;

    public string Summary => Definition.Summary;

    public string Details => Definition.Details;

    public string? Warning => Definition.Warning;

    public bool HasWarning => !string.IsNullOrEmpty(Definition.Warning);

    public TweakState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(ActionLabel));
                OnPropertyChanged(nameof(IsApplied));
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public bool IsApplied => _state is TweakState.Applied;

    /// <summary>Desconhecido não permite aplicar: o programa não sabe o estado atual.</summary>
    public bool CanApply => _state != TweakState.Unknown;

    public string StateText => _state switch
    {
        TweakState.Applied => "Ativo",
        TweakState.NotApplied => "Não aplicado",
        TweakState.Partial => "Parcialmente aplicado",
        _ => "Estado desconhecido",
    };

    public string ActionLabel => _state is TweakState.Applied or TweakState.Partial ? "Reverter" : "Aplicar";

    public string RiskText => Definition.Risk switch
    {
        RiskLevel.Safe => "Seguro",
        RiskLevel.Moderate => "Moderado",
        _ => "Avançado",
    };

    public Brush RiskBackground => Definition.Risk switch
    {
        RiskLevel.Safe => new SolidColorBrush(Color.FromRgb(0xDD, 0xF3, 0xE4)),
        RiskLevel.Moderate => new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xBD)),
        _ => new SolidColorBrush(Color.FromRgb(0xF9, 0xD6, 0xD3)),
    };

    public string RequirementsText
    {
        get
        {
            var parts = new List<string>();
            if (Definition.MinBuild is not null || Definition.MaxBuild is not null)
            {
                parts.Add($"existe em: {TweakApplicability.DescribeSupportedVersions(Definition)}");
            }

            if (Definition.RequiresAdmin)
            {
                parts.Add("exige administrador");
            }

            parts.Add(Definition.Restart switch
            {
                RestartRequirement.SignOut => "pede sair e entrar no Windows",
                RestartRequirement.Restart => "pede reiniciar o computador",
                _ => "vale na hora",
            });

            return string.Join(" · ", parts);
        }
    }
}

public sealed class TweakGroupViewModel
{
    public TweakGroupViewModel(string title, IReadOnlyList<TweakItemViewModel> items)
    {
        Title = title;
        Items = items;
    }

    public string Title { get; }

    public IReadOnlyList<TweakItemViewModel> Items { get; }
}
