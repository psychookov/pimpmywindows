namespace PimpMyWindows.Desktop.ViewModels;

/// <summary>Base das páginas do menu lateral.</summary>
public abstract class PageViewModel : ObservableObject
{
    public abstract string Key { get; }

    /// <summary>Chamado toda vez que o usuário abre a página, para ela se atualizar.</summary>
    public virtual void OnShown()
    {
    }
}
