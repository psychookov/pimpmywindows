using PimpMyWindows.Core.Diagnostics;

namespace PimpMyWindows.Core.Benchmarks;

public sealed record BatteryInfo(
    bool HasBattery,
    int? ChargePercent,
    string Status,
    int? DesignedMilliwattHours,
    int? FullChargeMilliwattHours,
    double? WearPercent,
    int? Cycles,
    IReadOnlyList<string> Notes);

/// <summary>Estado e desgaste da bateria do notebook, pelo próprio Windows. Somente leitura.</summary>
public sealed class BatteryService
{
    private readonly ISystemQuery _query;

    public BatteryService(ISystemQuery query)
    {
        _query = query;
    }

    public BatteryInfo Read()
    {
        var notes = new List<string>();
        IReadOnlyList<IReadOnlyDictionary<string, object?>> batteries;
        try
        {
            batteries = _query.Query(@"root\cimv2", "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return new BatteryInfo(false, null, string.Empty, null, null, null, null,
                [$"Não foi possível consultar a bateria: {ex.Message}"]);
        }

        if (batteries.Count == 0)
        {
            return new BatteryInfo(false, null, string.Empty, null, null, null, null, []);
        }

        var designed = FirstValue(notes, "SELECT DesignedCapacity FROM BatteryStaticData", "DesignedCapacity");
        var full = FirstValue(notes, "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", "FullChargedCapacity");
        var cycles = FirstValue(notes, "SELECT CycleCount FROM BatteryCycleCount", "CycleCount");

        double? wear = designed is > 0 && full is > 0
            ? Math.Clamp((1 - ((double)full.Value / designed.Value)) * 100, 0, 100)
            : null;

        var row = batteries[0];
        return new BatteryInfo(
            true,
            (int?)row.Long("EstimatedChargeRemaining"),
            DescribeStatus(row.Long("BatteryStatus")),
            (int?)designed,
            (int?)full,
            wear,
            // Muitos notebooks informam 0 ciclos por não suportarem a contagem.
            (int?)(cycles is > 0 ? cycles : null),
            notes);
    }

    private long? FirstValue(List<string> notes, string wql, string property)
    {
        try
        {
            return _query.Query(@"root\wmi", wql).FirstOrDefault()?.Long(property);
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            notes.Add($"{property} não está disponível neste computador.");
            return null;
        }
    }

    internal static string DescribeStatus(long? code) => code switch
    {
        1 => "Descarregando",
        2 => "Na tomada",
        3 => "Carga completa",
        4 => "Baixa",
        5 => "Crítica",
        6 => "Carregando",
        7 => "Carregando (alta)",
        8 => "Carregando (baixa)",
        9 => "Carregando (crítica)",
        _ => "Estado não informado",
    };
}
