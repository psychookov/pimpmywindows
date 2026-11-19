using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PimpMyWindows.Desktop.Theme;

/// <summary>Faz a barra de título do Windows acompanhar o tema (escura ou clara).</summary>
public static class WindowTheming
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var value = dark ? 1 : 0;

        if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
        }
    }
}
