using PimpMyWindows.Core.Startup;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class StartupItemViewModel
{
    public StartupItemViewModel(StartupEntry entry)
    {
        Entry = entry;
    }

    public StartupEntry Entry { get; }

    public string Name => Entry.Name;

    public string Command => Entry.Command;

    public bool IsEnabled => Entry.IsEnabled;

    public string StatusText => Entry.IsEnabled ? "Inicia com o Windows" : "Desativado";

    public string ActionLabel => Entry.IsEnabled ? "Desativar" : "Ativar";

    public string ScopeText => Entry.Source switch
    {
        StartupSource.CurrentUser => "Só para o seu usuário",
        StartupSource.AllUsers => "Todos os usuários (exige administrador)",
        _ => "Todos os usuários, 32 bits (exige administrador)",
    };
}
