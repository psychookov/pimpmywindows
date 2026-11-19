using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class AboutPageViewModel : PageViewModel
{
    private const string RepositoryUrl = "https://github.com/psychookov/pimpmywindows";

    private readonly string _dataDirectory;
    private readonly string _machineBackupsDirectory;
    private readonly IUserPrompt _prompt;

    public AboutPageViewModel(string dataDirectory, string machineBackupsDirectory, IUserPrompt prompt)
    {
        _dataDirectory = dataDirectory;
        _machineBackupsDirectory = machineBackupsDirectory;
        _prompt = prompt;
        OpenLogsCommand = new RelayCommand(_ => Open(Path.Combine(_dataDirectory, "logs")));
        OpenBackupsCommand = new RelayCommand(_ => Open(Path.Combine(_dataDirectory, "backups")));
        OpenMachineBackupsCommand = new RelayCommand(_ => Open(_machineBackupsDirectory));
        OpenRepositoryCommand = new RelayCommand(_ => Open(RepositoryUrl));
    }

    public override string Key => "about";

    public string VersionText { get; } =
        "Versão " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");

    public ICommand OpenLogsCommand { get; }

    public ICommand OpenBackupsCommand { get; }

    public ICommand OpenMachineBackupsCommand { get; }

    public ICommand OpenRepositoryCommand { get; }

    private void Open(string target)
    {
        try
        {
            if (!target.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(target))
            {
                _prompt.ShowInfo("Pasta ainda não existe", "Ela é criada quando o programa guarda a primeira informação.");
                return;
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _prompt.ShowError("Não foi possível abrir", ex.Message);
        }
    }
}
