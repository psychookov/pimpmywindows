using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PimpMyWindows.Core.Platform;

/// <summary>
/// Implementação real, baseada no registro do Windows. Usa sempre a visão de 64 bits.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryAccess : IRegistryAccess
{
    public RegistryData? Read(RegistryLocation location)
    {
        using var baseKey = OpenBase(location.Root);
        using var key = baseKey.OpenSubKey(location.SubKey, writable: false);
        if (key is null)
        {
            return null;
        }

        var value = key.GetValue(location.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            return null;
        }

        return key.GetValueKind(location.ValueName) switch
        {
            RegistryValueKind.DWord => RegistryData.DWord((int)value),
            RegistryValueKind.String or RegistryValueKind.ExpandString => RegistryData.Text((string)value),
            RegistryValueKind.Binary => RegistryData.Binary((byte[])value),
            var other => throw new NotSupportedException(
                $"Tipo de valor do registro não suportado ({other}) em {location}."),
        };
    }

    public void Write(RegistryLocation location, RegistryData data)
    {
        using var baseKey = OpenBase(location.Root);
        using var key = baseKey.CreateSubKey(location.SubKey, writable: true)
            ?? throw new InvalidOperationException($"Não foi possível abrir ou criar a chave {location.SubKey}.");

        switch (data.Kind)
        {
            case RegistryDataKind.DWord:
                key.SetValue(location.ValueName, data.AsDWord(), RegistryValueKind.DWord);
                break;
            case RegistryDataKind.Text:
                key.SetValue(location.ValueName, data.AsText(), RegistryValueKind.String);
                break;
            case RegistryDataKind.Binary:
                key.SetValue(location.ValueName, data.AsBinary(), RegistryValueKind.Binary);
                break;
            default:
                throw new NotSupportedException($"Tipo de valor não suportado: {data.Kind}.");
        }
    }

    public void Delete(RegistryLocation location)
    {
        using var baseKey = OpenBase(location.Root);
        using var key = baseKey.OpenSubKey(location.SubKey, writable: true);
        key?.DeleteValue(location.ValueName, throwOnMissingValue: false);
    }

    public IReadOnlyList<string> GetValueNames(RegistryRoot root, string subKey)
    {
        using var baseKey = OpenBase(root);
        using var key = baseKey.OpenSubKey(subKey, writable: false);
        return key?.GetValueNames() ?? [];
    }

    private static RegistryKey OpenBase(RegistryRoot root) => RegistryKey.OpenBaseKey(
        root == RegistryRoot.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
        RegistryView.Registry64);
}
