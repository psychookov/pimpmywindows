using System.ComponentModel;
using System.Diagnostics;

namespace PimpMyWindows.Desktop.Services;

internal static class ElevationHelper
{
    /// <summary>
    /// Abre uma nova instância pedindo permissão de administrador (janela do UAC).
    /// Retorna false se o usuário cancelar.
    /// </summary>
    public static bool TryStartElevatedInstance()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception)
        {
            // O usuário recusou a janela de permissão (UAC).
            return false;
        }
    }
}
