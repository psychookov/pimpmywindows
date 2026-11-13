using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Safety;

/// <summary>Valor que existia no registro antes do ajuste (<c>null</c> = o valor não existia).</summary>
public sealed record BackedUpValue(RegistryLocation Location, RegistryData? Previous);

/// <summary>Cópia dos valores originais de um ajuste, guardada antes de alterá-lo.</summary>
public sealed record TweakBackup(string TweakId, DateTimeOffset CreatedAt, IReadOnlyList<BackedUpValue> Values);

public interface IBackupStore
{
    void Save(TweakBackup backup);

    TweakBackup? Load(string tweakId);

    void Delete(string tweakId);
}

public sealed class InMemoryBackupStore : IBackupStore
{
    private readonly Dictionary<string, TweakBackup> _items = new();

    public void Save(TweakBackup backup) => _items[backup.TweakId] = backup;

    public TweakBackup? Load(string tweakId) => _items.GetValueOrDefault(tweakId);

    public void Delete(string tweakId) => _items.Remove(tweakId);
}
