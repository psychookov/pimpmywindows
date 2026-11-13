using System.ComponentModel;
using System.Diagnostics;

namespace PimpMyWindows.Core.Safety;

/// <summary>
/// Cria um ponto de restauração do Windows usando o comando nativo <c>Checkpoint-Computer</c>.
/// </summary>
public sealed class RestorePointService
{
    private readonly Func<bool> _isAdministrator;
    private readonly IActionLog _log;

    public RestorePointService(Func<bool> isAdministrator, IActionLog log)
    {
        _isAdministrator = isAdministrator;
        _log = log;
    }

    public async Task<OperationResult> CreateAsync(string description, CancellationToken cancellationToken = default)
    {
        if (!_isAdministrator())
        {
            return OperationResult.Fail(
                "Criar um ponto de restauração exige executar o Pimp my Windows como administrador.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            $"Checkpoint-Computer -Description '{Sanitize(description)}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Não foi possível iniciar o PowerShell.");

            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var errorText = (await stderr).Trim();
            _ = await stdout;

            if (process.ExitCode != 0)
            {
                _log.Write($"PONTO DE RESTAURAÇÃO falhou: {errorText}");
                return OperationResult.Fail(
                    "O Windows não conseguiu criar o ponto de restauração. Verifique se a Proteção do Sistema " +
                    "está ligada para o disco C: (Painel de Controle > Sistema > Proteção do Sistema). " +
                    $"Detalhe: {FirstLine(errorText)}");
            }

            _log.Write("PONTO DE RESTAURAÇÃO solicitado ao Windows.");
            return OperationResult.Ok(
                "Ponto de restauração solicitado. O Windows normalmente só cria um a cada 24 horas; " +
                "se já existir um recente, ele continua valendo.");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("A criação do ponto de restauração demorou demais e foi cancelada.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            _log.Write($"PONTO DE RESTAURAÇÃO falhou: {ex.Message}");
            return OperationResult.Fail($"Não foi possível executar o PowerShell: {ex.Message}");
        }
    }

    /// <summary>Mantém só caracteres seguros para colocar dentro de aspas simples do PowerShell.</summary>
    internal static string Sanitize(string description)
    {
        var cleaned = new string(description
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.')
            .ToArray()).Trim();

        if (cleaned.Length > 100)
        {
            cleaned = cleaned[..100];
        }

        return cleaned.Length == 0 ? "Pimp my Windows" : cleaned;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrEmpty(line) ? "sem detalhes" : line;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // O processo já terminou.
        }
    }
}
