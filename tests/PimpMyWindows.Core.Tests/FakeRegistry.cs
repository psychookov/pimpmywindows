using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Tests;

/// <summary>Registro falso em memória, com opção de simular falha de gravação.</summary>
internal sealed class FakeRegistry : IRegistryAccess
{
    private readonly Dictionary<RegistryLocation, RegistryData> _values = new();

    /// <summary>Se definido, a gravação deste local lança <see cref="UnauthorizedAccessException"/>.</summary>
    public RegistryLocation? FailWritesAt { get; set; }

    public int WriteCount { get; private set; }

    public RegistryData? Read(RegistryLocation location) => _values.GetValueOrDefault(location);

    public void Write(RegistryLocation location, RegistryData data)
    {
        if (location == FailWritesAt)
        {
            throw new UnauthorizedAccessException("acesso negado (simulado)");
        }

        WriteCount++;
        _values[location] = data;
    }

    public void Delete(RegistryLocation location) => _values.Remove(location);

    public IReadOnlyList<string> GetValueNames(RegistryRoot root, string subKey) =>
        _values.Keys.Where(k => k.Root == root && k.SubKey == subKey).Select(k => k.ValueName).ToList();

    public void Seed(RegistryLocation location, RegistryData data) => _values[location] = data;
}

/// <summary>Backup que falha ao salvar, para provar que nada é alterado sem backup.</summary>
internal sealed class FailingBackupStore : PimpMyWindows.Core.Safety.IBackupStore
{
    public void Save(PimpMyWindows.Core.Safety.TweakBackup backup) => throw new IOException("disco cheio (simulado)");

    public PimpMyWindows.Core.Safety.TweakBackup? Load(string tweakId) => null;

    public void Delete(string tweakId)
    {
    }
}
