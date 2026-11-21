using System.Globalization;

namespace PimpMyWindows.Core.Diagnostics;

/// <summary>Uma linha de resultado de uma consulta ao Windows (nome da propriedade → valor).</summary>
public interface ISystemQuery
{
    /// <summary>
    /// Consulta o Windows (WMI). Datas já chegam como <see cref="DateTime"/>.
    /// Lança <see cref="InvalidOperationException"/> com mensagem em português se a consulta falhar.
    /// </summary>
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string scope, string wql);
}

public static class QueryRow
{
    public static string Text(this IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is not Array ? value?.ToString()?.Trim() ?? string.Empty : string.Empty;

    public static long? Long(this IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null || value is string or Array or DateTime)
        {
            return null;
        }

        try
        {
            return value is IConvertible ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : null;
        }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        {
            return null;
        }
    }

    public static DateTime? Date(this IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is DateTime date ? date : null;

    public static bool? Bool(this IReadOnlyDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value) && value is bool b ? b : null;

    /// <summary>Valores de uma propriedade em formato de lista (ex.: OperationalStatus).</summary>
    public static IReadOnlyList<long> Longs(this IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return [];
        }

        if (value is Array array)
        {
            var list = new List<long>();
            foreach (var item in array)
            {
                try
                {
                    if (item is IConvertible)
                    {
                        list.Add(Convert.ToInt64(item, CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
                {
                    // Item ilegível: ignorado.
                }
            }

            return list;
        }

        var single = row.Long(key);
        return single is null ? [] : [single.Value];
    }
}
