using System.Text.Json;

namespace PimpMyWindows.Core.Tweaks;

/// <summary>
/// Exporta/importa uma seleção de ajustes como um arquivo JSON simples, para levar a mesma configuração
/// a outro computador. O arquivo só guarda IDs: o que cada ID faz vem sempre do catálogo do programa.
/// </summary>
public static class TweakSelection
{
    private const int MaxItems = 500;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Export(IEnumerable<string> tweakIds) =>
        JsonSerializer.Serialize(
            new SelectionDto { Version = 1, Tweaks = tweakIds.Distinct().OrderBy(i => i, StringComparer.Ordinal).ToList() },
            Options);

    /// <summary>Lê o arquivo e devolve só os IDs conhecidos; IDs desconhecidos vão em <paramref name="unknown"/>.</summary>
    public static IReadOnlyList<string> Import(string json, IEnumerable<string> knownIds, out IReadOnlyList<string> unknown)
    {
        SelectionDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SelectionDto>(json);
        }
        catch (JsonException)
        {
            throw new FormatException("O arquivo não é uma seleção válida do Pimp my Windows.");
        }

        if (dto is null || dto.Version != 1 || dto.Tweaks is null)
        {
            throw new FormatException("O arquivo não é uma seleção válida do Pimp my Windows.");
        }

        if (dto.Tweaks.Count > MaxItems)
        {
            throw new FormatException("O arquivo tem itens demais para ser uma seleção válida.");
        }

        var known = new HashSet<string>(knownIds, StringComparer.Ordinal);
        var valid = dto.Tweaks.Where(known.Contains).Distinct().ToList();
        unknown = dto.Tweaks.Where(t => !known.Contains(t)).Distinct().ToList();
        return valid;
    }

    private sealed class SelectionDto
    {
        public int Version { get; set; }

        public List<string>? Tweaks { get; set; }
    }
}
