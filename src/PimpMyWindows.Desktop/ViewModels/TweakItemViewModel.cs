using System.Globalization;
using System.Text;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class TweakItemViewModel : ObservableObject
{
    private TweakState _state;
    private bool _desired;
    private bool _isExpanded;

    public TweakItemViewModel(TweakDefinition definition, TweakState state, string? unavailableReason = null)
    {
        Definition = definition;
        UnavailableReason = unavailableReason;
        _state = unavailableReason is null ? state : TweakState.NotApplied;
        _desired = state == TweakState.Applied;
        SearchIndex = Normalize($"{definition.Name} {definition.Summary} {definition.Details}");
    }

    /// <summary>Avisa a página quando o item entra ou sai da lista de pendentes.</summary>
    public event Action? PendingChanged;

    public TweakDefinition Definition { get; }

    public string SearchIndex { get; }

    /// <summary>Preenchido quando o ajuste não vale para este Windows; ele só aparece (em cinza) no Modo Técnico.</summary>
    public string? UnavailableReason { get; }

    public bool IsAvailable => UnavailableReason is null;

    public double RowOpacity => IsAvailable ? 1.0 : 0.55;

    public string Name => Definition.Name;

    public string Summary => Definition.Summary;

    public string Details => Definition.Details;

    public string? Warning => Definition.Warning;

    public bool HasWarning => !string.IsNullOrEmpty(Definition.Warning);

    public bool IsRecommended => Definition.Recommended;

    public bool IsTechnical => Definition.Mode == UserMode.Technical;

    public bool RequiresAdmin => Definition.RequiresAdmin;

    public string RiskKey => Definition.Risk.ToString();

    public string RiskText => Definition.Risk switch
    {
        RiskLevel.Safe => "Seguro",
        RiskLevel.Moderate => "Moderado",
        _ => "Avançado",
    };

    public TweakState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(HasStateTag));
                OnPropertyChanged(nameof(IsApplied));
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(IsPending));
                PendingChanged?.Invoke();
            }
        }
    }

    /// <summary>O que o usuário quer: ligado (aplicar) ou desligado (reverter). Só vale de fato ao clicar em Aplicar.</summary>
    public bool Desired
    {
        get => _desired;
        set
        {
            if (SetProperty(ref _desired, value))
            {
                OnPropertyChanged(nameof(IsPending));
                PendingChanged?.Invoke();
            }
        }
    }

    public bool IsApplied => _state == TweakState.Applied;

    public bool IsPending => _desired != IsApplied;

    /// <summary>Estado desconhecido não permite mexer: o programa não sabe como está agora.</summary>
    public bool CanToggle => IsAvailable && _state != TweakState.Unknown;

    public bool HasStateTag => !IsAvailable || _state is TweakState.Applied or TweakState.Partial or TweakState.Unknown;

    public string StateText => !IsAvailable ? "Indisponível neste Windows" : _state switch
    {
        TweakState.Applied => "Ativo",
        TweakState.Partial => "Parcial",
        TweakState.Unknown => "Estado desconhecido",
        _ => string.Empty,
    };

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

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

    /// <summary>Depois de aplicar/reverter: assume o estado real do sistema e zera o que estava pendente.</summary>
    public void Accept(TweakState realState)
    {
        State = realState;
        Desired = IsApplied;
    }

    public void ResetDesired() => Desired = IsApplied;

    /// <summary>Minúsculas e sem acentos, para a busca achar "extensao" ao procurar "extensões".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
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
