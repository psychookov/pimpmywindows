using PimpMyWindows.Desktop.ViewModels;

namespace PimpMyWindows.Desktop.Services;

/// <summary>
/// Estado compartilhado entre as páginas: mensagem da barra de status, Modo Técnico e um aviso de
/// "algo mudou" para que o Painel e a Central Desfazer se atualizem.
/// </summary>
public sealed class AppState : ObservableObject
{
    private string _statusText = "Pronto. Nada é alterado até você confirmar.";
    private bool _isTechnicalMode;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>O programa sempre abre no Modo Básico; o Técnico nunca é lembrado de propósito.</summary>
    public bool IsTechnicalMode
    {
        get => _isTechnicalMode;
        set => SetProperty(ref _isTechnicalMode, value);
    }

    public event Action? StateChanged;

    public void Say(string message) => StatusText = message;

    public void NotifyStateChanged() => StateChanged?.Invoke();
}
