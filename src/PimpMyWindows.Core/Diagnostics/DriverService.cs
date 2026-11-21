namespace PimpMyWindows.Core.Diagnostics;

public sealed record DriverEntry(
    string DeviceName,
    string Manufacturer,
    string Version,
    DateTime? Date,
    string DeviceClass,
    bool? IsSigned,
    bool IsOldWatched);

public sealed record DeviceProblem(string DeviceName, int ErrorCode, string Description);

public sealed record DriverReport(
    IReadOnlyList<DriverEntry> Drivers,
    IReadOnlyList<DeviceProblem> Problems,
    IReadOnlyList<string> Errors);

/// <summary>Lista os drivers instalados e os aparelhos com problema. Somente leitura.</summary>
public sealed class DriverService
{
    private const string Cimv2 = @"root\cimv2";

    /// <summary>Classes em que um driver muito antigo costuma valer a pena atualizar.</summary>
    private static readonly HashSet<string> WatchedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "DISPLAY", "NET", "MEDIA", "BLUETOOTH",
    };

    private readonly ISystemQuery _query;
    private readonly Func<DateTime> _now;

    public DriverService(ISystemQuery query, Func<DateTime>? now = null)
    {
        _query = query;
        _now = now ?? (() => DateTime.Now);
    }

    public DriverReport Load()
    {
        var errors = new List<string>();
        var drivers = new List<DriverEntry>();
        var problems = new List<DeviceProblem>();

        try
        {
            foreach (var row in _query.Query(Cimv2,
                         "SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, DeviceClass, IsSigned FROM Win32_PnPSignedDriver"))
            {
                var name = row.Text("DeviceName");
                if (name.Length == 0)
                {
                    continue;
                }

                var entry = new DriverEntry(
                    name,
                    row.Text("Manufacturer"),
                    row.Text("DriverVersion"),
                    row.Date("DriverDate"),
                    row.Text("DeviceClass"),
                    row.Bool("IsSigned"),
                    false);
                drivers.Add(entry with { IsOldWatched = IsOldAndWorthWatching(entry, _now()) });
            }
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            errors.Add($"Não foi possível listar os drivers: {ex.Message}");
        }

        try
        {
            foreach (var row in _query.Query(Cimv2,
                         "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0"))
            {
                var code = (int)(row.Long("ConfigManagerErrorCode") ?? 0);
                var name = row.Text("Name");
                if (code != 0 && name.Length > 0)
                {
                    problems.Add(new DeviceProblem(name, code, DescribeError(code)));
                }
            }
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            errors.Add($"Não foi possível procurar aparelhos com problema: {ex.Message}");
        }

        return new DriverReport(
            drivers.OrderBy(d => d.DeviceClass, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.DeviceName).ToList(),
            problems.OrderBy(p => p.DeviceName).ToList(),
            errors);
    }

    /// <summary>
    /// "Antigo" só vale para vídeo, rede, áudio e Bluetooth, e nunca para drivers da própria Microsoft
    /// (que trazem datas antigas de propósito). Idade sozinha não é defeito: é só um motivo para dar uma olhada.
    /// </summary>
    internal static bool IsOldAndWorthWatching(DriverEntry driver, DateTime now) =>
        driver.Date is not null
        && WatchedClasses.Contains(driver.DeviceClass)
        && !driver.Manufacturer.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
        && driver.Date.Value < now.AddYears(-2);

    internal static string DescribeError(int code) => code switch
    {
        1 => "Mal configurado.",
        3 => "O driver pode estar corrompido ou falta memória.",
        10 => "O aparelho não consegue iniciar.",
        12 => "Não há recursos livres suficientes para ele.",
        14 => "Precisa reiniciar o computador para funcionar.",
        18 => "Os drivers precisam ser reinstalados.",
        19 => "Configuração danificada no registro.",
        21 => "O Windows está removendo este aparelho.",
        22 => "Está desativado.",
        24 => "Não está presente, não funciona direito ou falta driver.",
        28 => "Os drivers não estão instalados.",
        29 => "Desativado pela BIOS/firmware.",
        31 => "Não está funcionando direito (o Windows não carregou o driver).",
        32 => "O serviço do driver está desativado.",
        34 or 35 or 36 or 37 or 38 => "O Windows não conseguiu iniciar o driver.",
        39 => "O driver está corrompido ou ausente.",
        40 => "Falta informação do serviço do driver no registro.",
        41 => "O Windows carregou o driver, mas não achou o aparelho.",
        43 => "O Windows parou o aparelho porque ele relatou um problema.",
        45 => "O aparelho não está conectado.",
        48 => "O driver foi bloqueado por ser incompatível com esta versão do Windows.",
        52 => "O Windows não consegue verificar a assinatura digital do driver.",
        _ => $"Código de erro {code}.",
    };
}
