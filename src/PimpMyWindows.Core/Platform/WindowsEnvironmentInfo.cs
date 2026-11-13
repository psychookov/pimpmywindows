using System.Runtime.Versioning;
using System.Security.Principal;

namespace PimpMyWindows.Core.Platform;

/// <summary>Informações sobre o Windows em que o programa está rodando.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsEnvironmentInfo
{
    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public Version OsVersion => Environment.OSVersion.Version;

    /// <summary>Número de build do Windows (por exemplo 19045 ou 26100).</summary>
    public int Build => OsVersion.Build;

    /// <summary>O projeto suporta Windows 10 e Windows 11.</summary>
    public bool IsSupportedWindows => OperatingSystem.IsWindows() && OsVersion.Major >= 10;

    public string Description
    {
        get
        {
            var build = OsVersion.Build;
            var name = build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{name} (build {build})";
        }
    }
}
