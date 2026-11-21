using System.Text.Json;

namespace PimpMyWindows.Core.Benchmarks;

/// <summary>Um resultado de teste. <see cref="HigherIsBetter"/> diz se número maior é melhor (ex.: MB/s) ou pior (ex.: ms).</summary>
public sealed record BenchmarkResult(string Id, string Name, double Value, string Unit, bool HigherIsBetter);

/// <summary>Um resultado guardado no histórico, para comparar antes e depois de um ajuste.</summary>
public sealed record BenchmarkRecord(DateTimeOffset At, string Id, double Value);

public static class BenchmarkComparison
{
    /// <summary>Variação em % em relação ao teste anterior (positivo = número subiu). Nulo se não há teste anterior.</summary>
    public static double? PercentChange(double? previous, double current) =>
        previous is > 0 ? (current - previous.Value) / previous.Value * 100 : null;

    /// <summary>Texto curto: "melhorou 4%", "piorou 12%" ou "igual ao anterior" (variação até 3% conta como igual).</summary>
    public static string Describe(double? percentChange, bool higherIsBetter)
    {
        if (percentChange is null)
        {
            return "primeiro teste";
        }

        var change = percentChange.Value;
        if (Math.Abs(change) < 3)
        {
            return "igual ao anterior";
        }

        var better = higherIsBetter ? change > 0 : change < 0;
        return $"{(better ? "melhorou" : "piorou")} {Math.Abs(change):0}%";
    }
}

/// <summary>Guarda o histórico de testes em JSON. Nunca lança erro: arquivo ausente ou corrompido = histórico vazio.</summary>
public sealed class BenchmarkHistoryStore
{
    private const int MaxRecords = 500;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public BenchmarkHistoryStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<BenchmarkRecord> Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<List<BenchmarkRecord>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Último valor guardado de cada teste.</summary>
    public IReadOnlyDictionary<string, double> Latest() =>
        Load().GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.OrderBy(r => r.At).Last().Value);

    public void Add(IEnumerable<BenchmarkResult> results, DateTimeOffset at)
    {
        try
        {
            var all = Load().ToList();
            all.AddRange(results.Select(r => new BenchmarkRecord(at, r.Id, r.Value)));
            var kept = all.OrderBy(r => r.At).TakeLast(MaxRecords).ToList();

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(kept, Options));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Histórico não salvo não pode derrubar o programa.
        }
    }
}
