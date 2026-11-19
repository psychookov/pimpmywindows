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

    /// <summary>Descrição do programa, ou o comando quando não há descrição.</summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Entry.Description) ? Entry.Command : Entry.Description;

    public string Command => Entry.Command;

    public bool IsEnabled => Entry.IsEnabled;

    public bool FileMissing => Entry.FileMissing;

    public bool NeedsAdmin => Entry.RequiresAdministrator;

    public string ScopeText => Entry.Source switch
    {
        StartupSource.CurrentUser => "Só você",
        StartupSource.AllUsers => "Todos os usuários",
        StartupSource.AllUsers32Bit => "Todos (32 bits)",
        StartupSource.UserStartupFolder => "Pasta Inicializar",
        _ => "Pasta Inicializar (todos)",
    };
}
