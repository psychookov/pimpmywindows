namespace PimpMyWindows.Desktop.ViewModels;

/// <summary>Base das páginas do menu lateral.</summary>
public abstract class PageViewModel : ObservableObject
{
    public abstract string Key { get; }

    /// <summary>Chamado toda vez que o usuário abre a página, para ela se atualizar.</summary>
    public virtual void OnShown()
    {
    }

    /// <summary>Chamado ao sair da página, para ela parar o que estiver rodando em segundo plano.</summary>
    public virtual void OnHidden()
    {
    }
}
