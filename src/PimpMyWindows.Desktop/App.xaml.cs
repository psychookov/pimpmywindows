using System.IO;
using System.Windows;
using PimpMyWindows.Core.Benchmarks;
using PimpMyWindows.Core.Controllers;
using PimpMyWindows.Core.Diagnostics;
using PimpMyWindows.Core.Games;
using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Settings;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;
using PimpMyWindows.Desktop.Theme;
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
        var windowsBuild = environment.Build;

        var settingsStore = new JsonSettingsStore(Path.Combine(dataDirectory, "settings.json"));
        var settings = settingsStore.Load();
        var themeController = new ThemeController(settings, settingsStore, registry);
        themeController.ApplySaved();

        // Ajustes que exigem administrador guardam as cópias de segurança em uma pasta protegida da máquina,
        // para que um programa comum não consiga adulterá-las. Os demais usam a pasta do usuário.
        var machineBackupsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PimpMyWindows-Backups");
        var machineTweakIds = TweakCatalog.All.Where(t => t.RequiresAdmin).Select(t => t.Id).ToHashSet();
        var backups = new RoutingBackupStore(
            new JsonBackupStore(Path.Combine(dataDirectory, "backups")),
            new JsonBackupStore(machineBackupsDirectory, new WindowsDirectoryGuard(IsAdministrator)),
            machineTweakIds.Contains);

        var engine = new TweakEngine(registry, backups, _log, IsAdministrator, () => windowsBuild);
        var planner = new TweakPlanner(registry, backups, IsAdministrator, () => windowsBuild);
        var runner = new TweakBatchRunner(engine, _log);
        var startup = new StartupService(registry, IsAdministrator, _log, files: new WindowsStartupFileSystem());
        var gameStore = new ManagedGameStore(Path.Combine(dataDirectory, "games.json"));
        var undo = new UndoCenterService(
            backups, engine, () => TweakCatalog.All.Concat(gameStore.AllTweaks()).ToList(), _log);
        var restorePoints = new RestorePointService(IsAdministrator, _log);

        var appState = new AppState();
        var prompt = new MessageBoxPrompt();
        var restore = new RestorePointCoordinator(restorePoints, settings, settingsStore, appState);

        _log.Write($"Pimp my Windows iniciado em {environment.Description}. Administrador: {isAdministrator}.");

        var fileDialogs = new WindowsFileDialogs();
        var tweaksPage = new TweaksPageViewModel(
            TweakCatalog.All, engine, planner, runner, restore, prompt, fileDialogs, appState, _log, windowsBuild);
        var startupPage = new StartupPageViewModel(startup, prompt, appState);

        var wmi = new WmiSystemQuery();
        var hardwareService = new HardwareService(wmi);
        var driverService = new DriverService(wmi);
        var diskService = new DiskHealthService(wmi);
        var driverBackup = new DriverBackupService(new WindowsProcessRunner(), IsAdministrator, _log);
        var pcPage = new PcPageViewModel(hardwareService, driverService, diskService, appState, prompt, _log);
        var driversPage = new DriversPageViewModel(driverService, driverBackup, fileDialogs, prompt, appState, _log);
        var disksPage = new DisksPageViewModel(diskService, prompt, appState, _log);
        var testsPage = new TestsPageViewModel(
            new BenchmarkHistoryStore(Path.Combine(dataDirectory, "benchmarks.json")),
            new NetworkTester(new WindowsNetworkProbe()),
            new BatteryService(wmi),
            prompt,
            appState,
            _log);
        var gamesPage = new GamesPageViewModel(
            new GameDetector(
                registry,
                new WindowsGameFileSystem(),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            gameStore,
            engine,
            planner,
            runner,
            fileDialogs,
            prompt,
            appState,
            _log,
            windowsBuild);
        var controllerPage = new ControllerPageViewModel(new XInputProbe(), appState);
        var undoPage = new UndoPageViewModel(undo, prompt, appState);
        var aboutPage = new AboutPageViewModel(dataDirectory, machineBackupsDirectory, prompt);

        ShellViewModel? shell = null;
        var overview = new OverviewPageViewModel(
            TweakCatalog.All,
            engine,
            startup,
            undo,
            restore,
            appState,
            windowsBuild,
            environment.Description,
            new OverviewActions(
                key => shell!.Navigate(key),
                () =>
                {
                    shell!.Navigate("tweaks");
                    tweaksPage.MarkRecommended();
                },
                async () =>
                {
                    var result = await restore.CreateAsync();
                    if (result.Success)
                    {
                        prompt.ShowInfo("Ponto de restauração", result.Message);
                    }
                    else
                    {
                        prompt.ShowError("Ponto de restauração", result.Message);
                    }
                }));

        shell = new ShellViewModel(
            overview, tweaksPage, startupPage, pcPage, driversPage, disksPage, testsPage, gamesPage, controllerPage, undoPage, aboutPage, undo,
            appState, prompt, _log, themeController, restore, isAdministrator, environment.Description);
        var viewModel = shell;

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
