using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Startup;

public enum StartupSource
{
    /// <summary>Inicia só para o seu usuário (HKCU\...\Run).</summary>
    CurrentUser,

    /// <summary>Inicia para todos os usuários (HKLM\...\Run).</summary>
    AllUsers,

    /// <summary>Programas de 32 bits que iniciam para todos os usuários.</summary>
    AllUsers32Bit,

    /// <summary>Atalho na pasta Inicializar do seu usuário.</summary>
    UserStartupFolder,

    /// <summary>Atalho na pasta Inicializar de todos os usuários.</summary>
    AllUsersStartupFolder,
}

/// <param name="FileMissing">O programa que seria iniciado não existe mais (entrada órfã, sobra de desinstalação).</param>
public sealed record StartupEntry(
    string Name,
    string Command,
    StartupSource Source,
    bool IsEnabled,
    bool FileMissing = false,
    string Description = "")
{
    public bool RequiresAdministrator => Source is not (StartupSource.CurrentUser or StartupSource.UserStartupFolder);

    public bool IsFolderEntry => Source is StartupSource.UserStartupFolder or StartupSource.AllUsersStartupFolder;
}

/// <summary>
/// Lista os programas que iniciam com o Windows e permite ativá-los ou desativá-los usando o mesmo
/// mecanismo do Gerenciador de Tarefas (chave StartupApproved). Nenhum programa é desinstalado e
/// nenhuma entrada é apagada: desativar é totalmente reversível.
/// </summary>
public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolderKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private sealed record SourceInfo(StartupSource Source, RegistryRoot Root, string RunKey, string ApprovedKey);

    private static readonly SourceInfo[] Sources =
    [
        new(StartupSource.CurrentUser, RegistryRoot.CurrentUser, RunKey, ApprovedRunKey),
        new(StartupSource.AllUsers, RegistryRoot.LocalMachine, RunKey, ApprovedRunKey),
        new(StartupSource.AllUsers32Bit, RegistryRoot.LocalMachine, Run32Key, ApprovedRun32Key),
    ];

    private readonly IRegistryAccess _registry;
    private readonly Func<bool> _isAdministrator;
    private readonly IActionLog _log;
    private readonly Func<DateTime> _utcNow;
    private readonly IStartupFileSystem? _files;

    public StartupService(
        IRegistryAccess registry,
        Func<bool> isAdministrator,
        IActionLog log,
        Func<DateTime>? utcNow = null,
        IStartupFileSystem? files = null)
    {
        _files = files;
        _registry = registry;
        _isAdministrator = isAdministrator;
        _log = log;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public IReadOnlyList<StartupEntry> List()
    {
        var entries = new List<StartupEntry>();

        foreach (var source in Sources)
        {
            IReadOnlyList<string> names;
            try
            {
                names = _registry.GetValueNames(source.Root, source.RunKey);
            }
            catch (Exception ex) when (IsExpectedFailure(ex))
            {
                _log.Write($"ERRO ao listar inicialização ({source.Source}): {ex.Message}");
                continue;
            }

            foreach (var name in names.Where(n => !string.IsNullOrEmpty(n)))
            {
                try
                {
                    var command = _registry.Read(new RegistryLocation(source.Root, source.RunKey, name));
                    var approved = _registry.Read(new RegistryLocation(source.Root, source.ApprovedKey, name));

                    var commandText = command?.Kind == RegistryDataKind.Text ? command.AsText() : string.Empty;
                    var missing = false;
                    var description = string.Empty;
                    if (_files is not null && commandText.Length > 0)
                    {
                        var exe = _files.ExpandVariables(StartupCommand.ExtractExecutable(commandText));
                        if (Path.IsPathRooted(exe))
                        {
                            missing = !_files.FileExists(exe);
                            description = missing ? string.Empty : _files.Describe(exe);
                        }
                    }

                    entries.Add(new StartupEntry(
                        name, commandText, source.Source, IsEnabled(approved), missing, description));
                }
                catch (Exception ex) when (IsExpectedFailure(ex))
                {
                    _log.Write($"ERRO ao ler a entrada de inicialização “{name}”: {ex.Message}");
                }
            }
        }

        AddFolderEntries(entries, allUsers: false);
        AddFolderEntries(entries, allUsers: true);

        return entries
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Source)
            .ToList();
    }

    private void AddFolderEntries(List<StartupEntry> entries, bool allUsers)
    {
        if (_files is null)
        {
            return;
        }

        var source = allUsers ? StartupSource.AllUsersStartupFolder : StartupSource.UserStartupFolder;
        var root = allUsers ? RegistryRoot.LocalMachine : RegistryRoot.CurrentUser;
        try
        {
            foreach (var file in _files.ListFolder(allUsers))
            {
                var approved = _registry.Read(new RegistryLocation(root, ApprovedFolderKey, file.FileName));
                var target = _files.ExpandVariables(file.Target);
                var missing = Path.IsPathRooted(target) && !_files.FileExists(target);
                entries.Add(new StartupEntry(file.FileName, target, source, IsEnabled(approved), missing, file.Description));
            }
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao listar a pasta Inicializar ({source}): {ex.Message}");
        }
    }

    public OperationResult SetEnabled(StartupEntry entry, bool enable)
    {
        if (entry.RequiresAdministrator && !_isAdministrator())
        {
            return OperationResult.Fail(
                "Este programa inicia para todos os usuários. Para alterá-lo, execute o Pimp my Windows como administrador.");
        }

        var location = LocationFor(entry);

        // Formato usado pelo próprio Windows: 12 bytes. Primeiro byte 02 = ativo, 03 = desativado;
        // quando desativado, os 8 últimos bytes guardam a data/hora (FILETIME) em que foi desativado.
        var data = new byte[12];
        if (enable)
        {
            data[0] = 0x02;
        }
        else
        {
            data[0] = 0x03;
            BitConverter.TryWriteBytes(data.AsSpan(4), _utcNow().ToFileTimeUtc());
        }

        try
        {
            _registry.Write(location, RegistryData.Binary(data));
            _log.Write($"INICIALIZAÇÃO {(enable ? "ATIVADO" : "DESATIVADO")}: {entry.Name} ({entry.Source})");
            return OperationResult.Ok(enable
                ? $"“{entry.Name}” voltou a iniciar com o Windows."
                : $"“{entry.Name}” não vai mais iniciar com o Windows. O programa continua instalado e você pode reativá-lo quando quiser.");
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _log.Write($"ERRO ao alterar inicialização de {entry.Name}: {ex.Message}");
            return OperationResult.Fail($"Não foi possível alterar “{entry.Name}”. Detalhe: {ex.Message}");
        }
    }

    private static RegistryLocation LocationFor(StartupEntry entry) => entry.Source switch
    {
        StartupSource.UserStartupFolder => new RegistryLocation(RegistryRoot.CurrentUser, ApprovedFolderKey, entry.Name),
        StartupSource.AllUsersStartupFolder => new RegistryLocation(RegistryRoot.LocalMachine, ApprovedFolderKey, entry.Name),
        _ => FromRunSource(entry),
    };

    private static RegistryLocation FromRunSource(StartupEntry entry)
    {
        var source = Sources.First(s => s.Source == entry.Source);
        return new RegistryLocation(source.Root, source.ApprovedKey, entry.Name);
    }

    /// <summary>Sem valor em StartupApproved, ou com primeiro byte par, o programa está ativo.</summary>
    internal static bool IsEnabled(RegistryData? approved)
    {
        if (approved is null || approved.Kind != RegistryDataKind.Binary)
        {
            return true;
        }

        var bytes = approved.AsBinary();
        return bytes.Length == 0 || (bytes[0] & 1) == 0;
    }

    private static bool IsExpectedFailure(Exception ex) => ExpectedFailures.Is(ex);
}
