using System.ComponentModel;
using System.Diagnostics;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

/// <summary>Abre algo do próprio Windows (Windows Update, Gerenciador de Dispositivos, pasta).</summary>
internal static class ShellLauncher
{
    public static void Open(string target, IUserPrompt prompt)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            prompt.ShowError("Não foi possível abrir", ex.Message);
        }
    }
}
