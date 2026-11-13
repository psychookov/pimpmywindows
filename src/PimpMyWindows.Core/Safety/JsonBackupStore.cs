using System.Text.Json;
using System.Text.RegularExpressions;
using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Safety;

/// <summary>
/// Guarda um arquivo JSON legível por ajuste, em <c>%LOCALAPPDATA%\PimpMyWindows\backups</c>.
/// O usuário pode abrir os arquivos para ver exatamente o que foi guardado.
/// </summary>
public sealed partial class JsonBackupStore : IBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonBackupStore(string directory)
    {
        _directory = directory;
    }

    public void Save(TweakBackup backup)
    {
        var path = PathFor(backup.TweakId);
        Directory.CreateDirectory(_directory);

        // Escreve em um arquivo temporário e troca no final, para nunca deixar um backup pela metade.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(BackupDto.From(backup), JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public TweakBackup? Load(string tweakId)
    {
        var path = PathFor(tweakId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BackupDto>(File.ReadAllText(path))?.ToModel();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or IOException)
        {
            // Backup ilegível: o motor volta para os valores padrão documentados.
            return null;
        }
    }

    public void Delete(string tweakId)
    {
        var path = PathFor(tweakId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathFor(string tweakId)
    {
        // Impede que um identificador malicioso escape da pasta de backups.
        if (!ValidId().IsMatch(tweakId))
        {
            throw new ArgumentException($"Identificador de ajuste inválido: {tweakId}", nameof(tweakId));
        }

        return Path.Combine(_directory, tweakId + ".json");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$")]
    private static partial Regex ValidId();
}

internal sealed class BackupDto
{
    public string TweakId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<BackupValueDto> Values { get; set; } = [];

    public static BackupDto From(TweakBackup backup) => new()
    {
        TweakId = backup.TweakId,
        CreatedAt = backup.CreatedAt,
        Values = backup.Values.Select(BackupValueDto.From).ToList(),
    };

    public TweakBackup ToModel() => new(TweakId, CreatedAt, Values.Select(v => v.ToModel()).ToList());
}

internal sealed class BackupValueDto
{
    public string Root { get; set; } = string.Empty;

    public string SubKey { get; set; } = string.Empty;

    public string ValueName { get; set; } = string.Empty;

    /// <summary>Nulo quando o valor não existia antes do ajuste.</summary>
    public string? Kind { get; set; }

    public string? Data { get; set; }

    public static BackupValueDto From(BackedUpValue value) => new()
    {
        Root = value.Location.Root.ToString(),
        SubKey = value.Location.SubKey,
        ValueName = value.Location.ValueName,
        Kind = value.Previous?.Kind.ToString(),
        Data = value.Previous?.ToStorageString(),
    };

    public BackedUpValue ToModel()
    {
        var location = new RegistryLocation(Enum.Parse<RegistryRoot>(Root), SubKey, ValueName);
        RegistryData? previous = Kind is null || Data is null
            ? null
            : RegistryData.FromStorageString(Enum.Parse<RegistryDataKind>(Kind), Data);
        return new BackedUpValue(location, previous);
    }
}
