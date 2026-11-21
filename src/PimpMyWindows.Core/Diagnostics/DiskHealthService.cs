namespace PimpMyWindows.Core.Diagnostics;

public enum DiskVerdict
{
    Unknown,
    Healthy,
    Attention,
    Problem,
}

public sealed record DiskHealth(
    string Name,
    string MediaType,
    string Bus,
    long SizeBytes,
    DiskVerdict Verdict,
    string VerdictText,
    int? TemperatureC,
    int? WearPercent,
    long? PowerOnHours,
    long? ReadErrors,
    long? WriteErrors,
    IReadOnlyList<string> Notes);

public sealed record DiskReport(IReadOnlyList<DiskHealth> Disks, IReadOnlyList<string> Errors);

/// <summary>
/// Saúde dos discos pelas APIs de armazenamento do próprio Windows. Somente leitura.
/// Nem todo disco informa desgaste e temperatura; o que não vier é mostrado como indisponível.
/// </summary>
public sealed class DiskHealthService
{
    private const string Storage = @"root\Microsoft\Windows\Storage";
    private readonly ISystemQuery _query;

    public DiskHealthService(ISystemQuery query)
    {
        _query = query;
    }

    public DiskReport Load()
    {
        var errors = new List<string>();
        IReadOnlyList<IReadOnlyDictionary<string, object?>> disks;
        try
        {
            disks = _query.Query(Storage,
                "SELECT DeviceId, FriendlyName, MediaType, BusType, HealthStatus, Size FROM MSFT_PhysicalDisk");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return new DiskReport([], [$"Não foi possível ler os discos: {ex.Message}"]);
        }

        IReadOnlyList<IReadOnlyDictionary<string, object?>> counters = [];
        try
        {
            counters = _query.Query(Storage,
                "SELECT DeviceId, Temperature, Wear, PowerOnHours, ReadErrorsTotal, WriteErrorsTotal FROM MSFT_StorageReliabilityCounter");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            errors.Add($"Desgaste e temperatura não estão disponíveis neste computador: {ex.Message}");
        }

        var result = disks
            .Select(d => Build(d, counters.FirstOrDefault(c => c.Text("DeviceId") == d.Text("DeviceId"))))
            .OrderBy(d => d.Name)
            .ToList();
        return new DiskReport(result, errors);
    }

    internal static DiskHealth Build(
        IReadOnlyDictionary<string, object?> disk,
        IReadOnlyDictionary<string, object?>? counter)
    {
        var temperature = Positive(counter?.Long("Temperature"));
        var wear = counter?.Long("Wear");
        var wearPercent = wear is >= 0 and <= 100 ? (int?)wear : null;
        var hours = counter?.Long("PowerOnHours");
        var readErrors = counter?.Long("ReadErrorsTotal");
        var writeErrors = counter?.Long("WriteErrorsTotal");

        var notes = new List<string>();
        var verdict = disk.Long("HealthStatus") switch
        {
            0 => DiskVerdict.Healthy,
            1 => DiskVerdict.Attention,
            2 => DiskVerdict.Problem,
            _ => DiskVerdict.Unknown,
        };

        if (verdict == DiskVerdict.Problem)
        {
            notes.Add("O Windows marcou este disco como com defeito. Faça backup dos seus arquivos agora.");
        }
        else if (verdict == DiskVerdict.Attention)
        {
            notes.Add("O Windows indica um aviso neste disco. Faça backup e acompanhe.");
        }

        if (wearPercent >= 90)
        {
            notes.Add($"Desgaste alto ({wearPercent}% da vida útil usada).");
            verdict = Worse(verdict, DiskVerdict.Attention);
        }

        if (temperature >= 70)
        {
            notes.Add($"Temperatura alta ({temperature} °C).");
            verdict = Worse(verdict, DiskVerdict.Attention);
        }

        if (readErrors > 0 || writeErrors > 0)
        {
            notes.Add("O disco já registrou erros de leitura ou escrita.");
            verdict = Worse(verdict, DiskVerdict.Attention);
        }

        return new DiskHealth(
            Name: disk.Text("FriendlyName").Length > 0 ? disk.Text("FriendlyName") : "Disco sem nome",
            MediaType: disk.Long("MediaType") switch
            {
                3 => "HD",
                4 => "SSD",
                5 => "SCM",
                _ => "Tipo não informado",
            },
            Bus: disk.Long("BusType") switch
            {
                17 => "NVMe",
                11 => "SATA",
                7 => "USB",
                8 => "RAID",
                3 => "ATA",
                _ => string.Empty,
            },
            SizeBytes: disk.Long("Size") ?? 0,
            Verdict: verdict,
            VerdictText: verdict switch
            {
                DiskVerdict.Healthy => "Saudável",
                DiskVerdict.Attention => "Atenção",
                DiskVerdict.Problem => "Com defeito",
                _ => "Sem informação",
            },
            TemperatureC: (int?)temperature,
            WearPercent: wearPercent,
            PowerOnHours: hours,
            ReadErrors: readErrors,
            WriteErrors: writeErrors,
            Notes: notes);
    }

    private static long? Positive(long? value) => value is > 0 and < 150 ? value : null;

    private static DiskVerdict Worse(DiskVerdict current, DiskVerdict other) =>
        (DiskVerdict)Math.Max((int)current, (int)other);
}
