using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PimpMyWindows.Core.Diagnostics;

/// <summary>Consulta o próprio Windows (WMI). Somente leitura: nada é alterado.</summary>
[SupportedOSPlatform("windows")]
public sealed class WmiSystemQuery : ISystemQuery
{
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string scope, string wql)
    {
        try
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();
            using var searcher = new ManagementObjectSearcher(scope, wql);
            using var results = searcher.Get();

            foreach (var item in results)
            {
                using (item)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in item.Properties)
                    {
                        row[property.Name] = Convert(property.Value);
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"O Windows não respondeu à consulta ({ex.Message})", ex);
        }
    }

    private static object? Convert(object? value)
    {
        if (value is string text && LooksLikeDmtfDate(text))
        {
            try
            {
                return ManagementDateTimeConverter.ToDateTime(text);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return value;
    }

    // Formato de data do WMI: 20230115000000.000000-180
    private static bool LooksLikeDmtfDate(string text) =>
        text.Length == 25 && text[14] == '.' && (text[21] is '+' or '-') && text.Take(14).All(char.IsDigit);
}
