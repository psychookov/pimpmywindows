using System.Globalization;

namespace PimpMyWindows.Core.Platform;

public enum RegistryRoot
{
    CurrentUser,
    LocalMachine,
}

public enum RegistryDataKind
{
    DWord,
    Text,
    Binary,
}

/// <summary>Endereço de um valor no registro (chave + nome do valor).</summary>
public sealed record RegistryLocation(RegistryRoot Root, string SubKey, string ValueName)
{
    public override string ToString() =>
        $"{(Root == RegistryRoot.CurrentUser ? "HKCU" : "HKLM")}\\{SubKey} [{ValueName}]";
}

/// <summary>
/// Valor do registro. Só os tipos usados pelo projeto são suportados (DWORD, texto e binário).
/// </summary>
public sealed class RegistryData : IEquatable<RegistryData>
{
    private readonly object _value;

    private RegistryData(RegistryDataKind kind, object value)
    {
        Kind = kind;
        _value = value;
    }

    public RegistryDataKind Kind { get; }

    public static RegistryData DWord(int value) => new(RegistryDataKind.DWord, value);

    public static RegistryData Text(string value) => new(RegistryDataKind.Text, value);

    public static RegistryData Binary(byte[] value) => new(RegistryDataKind.Binary, (byte[])value.Clone());

    public int AsDWord() => Kind == RegistryDataKind.DWord
        ? (int)_value
        : throw new InvalidOperationException("O valor não é um DWORD.");

    public string AsText() => Kind == RegistryDataKind.Text
        ? (string)_value
        : throw new InvalidOperationException("O valor não é texto.");

    public byte[] AsBinary() => Kind == RegistryDataKind.Binary
        ? (byte[])((byte[])_value).Clone()
        : throw new InvalidOperationException("O valor não é binário.");

    /// <summary>Converte para texto, para guardar em arquivo de backup.</summary>
    public string ToStorageString() => Kind switch
    {
        RegistryDataKind.DWord => ((int)_value).ToString(CultureInfo.InvariantCulture),
        RegistryDataKind.Text => (string)_value,
        RegistryDataKind.Binary => Convert.ToBase64String((byte[])_value),
        _ => throw new InvalidOperationException("Tipo de valor desconhecido."),
    };

    public static RegistryData FromStorageString(RegistryDataKind kind, string stored) => kind switch
    {
        RegistryDataKind.DWord => DWord(int.Parse(stored, CultureInfo.InvariantCulture)),
        RegistryDataKind.Text => Text(stored),
        RegistryDataKind.Binary => Binary(Convert.FromBase64String(stored)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public bool Equals(RegistryData? other)
    {
        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return Kind == RegistryDataKind.Binary
            ? ((byte[])_value).AsSpan().SequenceEqual((byte[])other._value)
            : object.Equals(_value, other._value);
    }

    public override bool Equals(object? obj) => Equals(obj as RegistryData);

    public override int GetHashCode() => Kind == RegistryDataKind.Binary
        ? HashCode.Combine(Kind, Convert.ToHexString((byte[])_value))
        : HashCode.Combine(Kind, _value);

    public override string ToString() => Kind switch
    {
        RegistryDataKind.DWord => ((int)_value).ToString(CultureInfo.InvariantCulture),
        RegistryDataKind.Text => $"\"{(string)_value}\"",
        RegistryDataKind.Binary => Convert.ToHexString((byte[])_value),
        _ => string.Empty,
    };
}
