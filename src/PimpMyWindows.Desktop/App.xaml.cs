using System.IO;
using System.Windows;
using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;
using PimpMyWindows.Desktop.ViewModels;

namespace PimpMyWindows.Desktop;

public partial class App : Application
{
    private IActionLog _log = new NullActionLog();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var environment = new WindowsEnvironmentInfo();
        if (!environment.IsSupportedWindows)
        {
            MessageBox.Show(
                "O Pimp my Windows funciona apenas no Windows 10 e no Windows 11.",
                "Sistema não suportado",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PimpMyWindows");

        _log = new FileActionLog(Path.Combine(dataDirectory, "logs", "pimpmywindows.log"));
        DispatcherUnhandledException += OnUnhandledException;

        var isAdministrator = environment.IsAdministrator();
        bool IsAdministrator() => isAdministrator;

        var registry = new WindowsRegistryAccess();
        var backups = new JsonBackupStore(Path.Combine(dataDirectory, "backups"));
        var windowsBuild = environment.Build;
        var engine = new TweakEngine(registry, backups, _log, IsAdministrator, () => windowsBuild);
        var startup = new StartupService(registry, IsAdministrator, _log);
        var restorePoints = new RestorePointService(IsAdministrator, _log);

        _log.Write($"Pimp my Windows iniciado em {environment.Description}. Administrador: {isAdministrator}.");

        var viewModel = new MainViewModel(
            TweakCatalog.All,
            engine,
            startup,
            restorePoints,
            new MessageBoxPrompt(),
            _log,
            isAdministrator,
            environment.Description,
            windowsBuild);

        new MainWindow { DataContext = viewModel }.Show();
    }

    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Write($"ERRO INESPERADO: {e.Exception}");
        MessageBox.Show(
            "Ocorreu um erro inesperado, mas nada foi alterado de forma pela metade: cada ajuste guarda uma cópia " +
            "de segurança antes de mudar qualquer coisa.\n\n" +
            $"Detalhe: {e.Exception.Message}\n\nO log fica em %LOCALAPPDATA%\\PimpMyWindows\\logs.",
            "Algo deu errado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
