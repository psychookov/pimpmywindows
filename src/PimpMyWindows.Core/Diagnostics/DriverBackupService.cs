using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Diagnostics;

/// <summary>
/// Backup e restauração de drivers pelo <c>pnputil</c>, o programa oficial do Windows para isso.
/// Nada é baixado. A restauração só aceita pastas criadas por este programa (têm um arquivo marcador).
/// </summary>
public sealed class DriverBackupService
{
    public const string MarkerFileName = "pimpmywindows-drivers.txt";
    private const int RebootRequired = 3010;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    private readonly IProcessRunner _runner;
    private readonly Func<bool> _isAdministrator;
    private readonly IActionLog _log;
    private readonly Func<DateTime> _now;

    public DriverBackupService(
        IProcessRunner runner,
        Func<bool> isAdministrator,
        IActionLog log,
        Func<DateTime>? now = null)
    {
        _runner = runner;
        _isAdministrator = isAdministrator;
        _log = log;
        _now = now ?? (() => DateTime.Now);
    }

    /// <summary>Cria uma subpasta nova dentro de <paramref name="parentFolder"/> e exporta todos os drivers para ela.</summary>
    public async Task<OperationResult> BackupAsync(string parentFolder, CancellationToken cancellationToken = default)
    {
        if (!_isAdministrator())
        {
            return OperationResult.Fail("Fazer o backup de drivers exige o programa executado como administrador.");
        }

        if (string.IsNullOrWhiteSpace(parentFolder) || !Directory.Exists(parentFolder))
        {
            return OperationResult.Fail("A pasta escolhida não existe.");
        }

        var target = Path.Combine(parentFolder, $"Drivers-{_now():yyyy-MM-dd-HHmm}");
        try
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(
                Path.Combine(target, MarkerFileName),
                "Backup de drivers criado pelo Pimp my Windows. Para restaurar, use a tela Drivers do programa.");

            _log.Write($"BACKUP DE DRIVERS para {target}");
            var result = await _runner.RunAsync("pnputil.exe", ["/export-driver", "*", target], Timeout, cancellationToken);

            if (result.ExitCode is not (0 or RebootRequired))
            {
                _log.Write($"ERRO no backup de drivers (código {result.ExitCode})");
                return OperationResult.Fail(
                    $"O Windows não conseguiu exportar os drivers (código {result.ExitCode}). Nada no sistema foi alterado.");
            }

            var count = Directory.GetDirectories(target).Length;
            _log.Write($"BACKUP DE DRIVERS concluído: {count} pacotes");
            return OperationResult.Ok($"Backup concluído: {count} pacotes de drivers salvos em {target}.");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is TimeoutException)
        {
            _log.Write($"ERRO no backup de drivers: {ex.Message}");
            return OperationResult.Fail($"Não foi possível fazer o backup de drivers. Detalhe: {ex.Message}");
        }
    }

    /// <summary>Reinstala os drivers de uma pasta de backup feita por este programa.</summary>
    public async Task<OperationResult> RestoreAsync(string backupFolder, CancellationToken cancellationToken = default)
    {
        if (!_isAdministrator())
        {
            return OperationResult.Fail("Restaurar drivers exige o programa executado como administrador.");
        }

        if (string.IsNullOrWhiteSpace(backupFolder)
            || !File.Exists(Path.Combine(backupFolder, MarkerFileName)))
        {
            return OperationResult.Fail(
                "Esta pasta não parece ser um backup de drivers feito pelo Pimp my Windows. " +
                "Escolha a pasta “Drivers-…” criada pelo backup.");
        }

        try
        {
            _log.Write($"RESTAURAR DRIVERS de {backupFolder}");
            var result = await _runner.RunAsync(
                "pnputil.exe",
                ["/add-driver", Path.Combine(backupFolder, "*.inf"), "/subdirs", "/install"],
                Timeout,
                cancellationToken);

            if (result.ExitCode == RebootRequired)
            {
                return OperationResult.Ok("Drivers restaurados. Reinicie o computador para concluir.");
            }

            if (result.ExitCode != 0)
            {
                _log.Write($"ERRO ao restaurar drivers (código {result.ExitCode})");
                return OperationResult.Fail(
                    $"O Windows terminou a restauração com o código {result.ExitCode}. " +
                    "Alguns drivers podem não ter sido instalados (drivers sem assinatura válida são recusados).");
            }

            return OperationResult.Ok("Drivers restaurados.");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is TimeoutException)
        {
            _log.Write($"ERRO ao restaurar drivers: {ex.Message}");
            return OperationResult.Fail($"Não foi possível restaurar os drivers. Detalhe: {ex.Message}");
        }
    }
}
