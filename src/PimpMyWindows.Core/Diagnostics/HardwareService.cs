namespace PimpMyWindows.Core.Diagnostics;

public sealed record MemoryModule(long CapacityBytes, int? SpeedMhz, string Manufacturer);

public sealed record GpuInfo(string Name, string DriverVersion, DateTime? DriverDate);

/// <summary>Resumo do computador. Não coleta nome do PC, do usuário nem números de série.</summary>
public sealed record HardwareSnapshot(
    string OsName,
    string OsVersion,
    string Architecture,
    string Board,
    string CpuName,
    int CpuCores,
    int CpuThreads,
    int? CpuMaxMhz,
    long RamTotalBytes,
    IReadOnlyList<MemoryModule> RamModules,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<string> Errors);

public sealed class HardwareService
{
    private const string Cimv2 = @"root\cimv2";
    private readonly ISystemQuery _query;

    public HardwareService(ISystemQuery query)
    {
        _query = query;
    }

    public HardwareSnapshot Collect()
    {
        var errors = new List<string>();

        var os = Try(errors, "o Windows", () => _query.Query(Cimv2,
            "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem")).FirstOrDefault();
        var board = Try(errors, "a placa-mãe", () => _query.Query(Cimv2,
            "SELECT Manufacturer, Product FROM Win32_BaseBoard")).FirstOrDefault();
        var cpus = Try(errors, "o processador", () => _query.Query(Cimv2,
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"));
        var memory = Try(errors, "a memória", () => _query.Query(Cimv2,
            "SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer FROM Win32_PhysicalMemory"));
        var gpus = Try(errors, "a placa de vídeo", () => _query.Query(Cimv2,
            "SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController"));

        var modules = memory
            .Select(m => new MemoryModule(
                m.Long("Capacity") ?? 0,
                (int?)(m.Long("ConfiguredClockSpeed") ?? m.Long("Speed")),
                m.Text("Manufacturer")))
            .Where(m => m.CapacityBytes > 0)
            .ToList();

        var firstCpu = cpus.FirstOrDefault();
        var boardText = board is null ? string.Empty : $"{board.Text("Manufacturer")} {board.Text("Product")}".Trim();

        return new HardwareSnapshot(
            OsName: os?.Text("Caption") ?? string.Empty,
            OsVersion: os is null ? string.Empty : $"{os.Text("Version")} (build {os.Text("BuildNumber")})",
            Architecture: os?.Text("OSArchitecture") ?? string.Empty,
            Board: boardText,
            CpuName: CleanCpuName(firstCpu?.Text("Name") ?? string.Empty),
            CpuCores: cpus.Sum(c => (int)(c.Long("NumberOfCores") ?? 0)),
            CpuThreads: cpus.Sum(c => (int)(c.Long("NumberOfLogicalProcessors") ?? 0)),
            CpuMaxMhz: (int?)firstCpu?.Long("MaxClockSpeed"),
            RamTotalBytes: modules.Sum(m => m.CapacityBytes),
            RamModules: modules,
            Gpus: gpus
                .Select(g => new GpuInfo(g.Text("Name"), g.Text("DriverVersion"), g.Date("DriverDate")))
                .Where(g => g.Name.Length > 0)
                .ToList(),
            Errors: errors);
    }

    /// <summary>Tira os espaços repetidos que o fabricante coloca no nome do processador.</summary>
    internal static string CleanCpuName(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Try(
        List<string> errors,
        string what,
        Func<IReadOnlyList<IReadOnlyDictionary<string, object?>>> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            errors.Add($"Não foi possível ler {what}: {ex.Message}");
            return [];
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit >= 3 ? $"{value:0.#} {units[unit]}" : $"{value:0} {units[unit]}";
    }
}
