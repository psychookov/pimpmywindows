using System.Diagnostics;

namespace PimpMyWindows.Core.Startup;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsStartupFileSystem : IStartupFileSystem
{
    public IReadOnlyList<StartupFile> ListFolder(bool allUsers)
    {
        var folder = Environment.GetFolderPath(allUsers
            ? Environment.SpecialFolder.CommonStartup
            : Environment.SpecialFolder.Startup);

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var result = new List<StartupFile>();
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(path);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ResolveShortcut(path) : path;
            result.Add(new StartupFile(name, target, Describe(target)));
        }

        return result;
    }

    public bool FileExists(string path) => File.Exists(path);

    public string ExpandVariables(string text) => Environment.ExpandEnvironmentVariables(text);

    public string Describe(string path)
    {
        try
        {
            return File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileDescription ?? string.Empty : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string ResolveShortcut(string path)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return path;
            }

            dynamic? shell = Activator.CreateInstance(type);
            dynamic? link = shell?.CreateShortcut(path);
            string target = link?.TargetPath ?? string.Empty;
            return string.IsNullOrWhiteSpace(target) ? path : target;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                       or InvalidCastException
                                       or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
                                       or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            return path;
        }
    }
}
