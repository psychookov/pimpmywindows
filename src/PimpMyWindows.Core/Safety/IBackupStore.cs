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

    /// <summary>Todos os backups legíveis (backups corrompidos são ignorados).</summary>
    IReadOnlyList<TweakBackup> ListAll();
}

public sealed class InMemoryBackupStore : IBackupStore
{
    private readonly Dictionary<string, TweakBackup> _items = new();

    public void Save(TweakBackup backup) => _items[backup.TweakId] = backup;

    public TweakBackup? Load(string tweakId) => _items.GetValueOrDefault(tweakId);

    public void Delete(string tweakId) => _items.Remove(tweakId);

    public IReadOnlyList<TweakBackup> ListAll() => _items.Values.ToList();
}

/// <summary>
/// Escolhe onde cada backup fica: ajustes que exigem administrador usam a pasta protegida da máquina
/// (que um programa comum não consegue adulterar); os demais usam a pasta do usuário.
/// Na leitura, procura nos dois lugares para não perder backups feitos de outra forma.
/// </summary>
public sealed class RoutingBackupStore : IBackupStore
{
    private readonly IBackupStore _user;
    private readonly IBackupStore _machine;
    private readonly Func<string, bool> _usesMachineStore;

    public RoutingBackupStore(IBackupStore user, IBackupStore machine, Func<string, bool> usesMachineStore)
    {
        _user = user;
        _machine = machine;
        _usesMachineStore = usesMachineStore;
    }

    public void Save(TweakBackup backup) => StoreFor(backup.TweakId).Save(backup);

    public TweakBackup? Load(string tweakId) => StoreFor(tweakId).Load(tweakId);

    public void Delete(string tweakId) => StoreFor(tweakId).Delete(tweakId);

    public IReadOnlyList<TweakBackup> ListAll()
    {
        var result = new List<TweakBackup>();
        result.AddRange(_user.ListAll().Where(b => !_usesMachineStore(b.TweakId)));
        try
        {
            result.AddRange(_machine.ListAll().Where(b => _usesMachineStore(b.TweakId)));
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            // Pasta da máquina inacessível ou não confiável: os backups dela simplesmente não aparecem.
        }

        return result;
    }

    private IBackupStore StoreFor(string tweakId) => _usesMachineStore(tweakId) ? _machine : _user;
}
