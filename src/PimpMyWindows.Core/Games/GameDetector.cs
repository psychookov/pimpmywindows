using System.Text.Json;
using System.Text.RegularExpressions;
using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Games;

/// <summary>
/// Encontra jogos instalados lendo apenas os arquivos e chaves que os próprios lançadores (Steam, Epic, GOG) mantêm.
/// Nada é instalado, alterado ou enviado para fora; se algo não puder ser lido, aquele jogo simplesmente não aparece.
/// </summary>
public sealed partial class GameDetector
{
    private const string GogKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    private static readonly string[] IgnoredSteamPrefixes =
    [
        "Steamworks Common", "Proton", "Steam Linux Runtime", "Steam Controller", "SteamVR",
    ];

    private static readonly string[] IgnoredExecutableWords =
    [
        "unins", "crash", "redist", "vc_redist", "vcredist", "dxsetup", "dotnet", "setup", "installer",
        "launcher", "eac", "easyanticheat", "battleye", "helper", "updater", "report",
    ];

    private readonly IRegistryAccess _registry;
    private readonly IGameFileSystem _files;
    private readonly string _programData;

    public GameDetector(IRegistryAccess registry, IGameFileSystem files, string programData)
    {
        _registry = registry;
        _files = files;
        _programData = programData;
    }

    public IReadOnlyList<GameInfo> DetectAll()
    {
        var all = new List<GameInfo>();
        foreach (var source in new Func<IEnumerable<GameInfo>>[] { DetectSteam, DetectEpic, DetectGog })
        {
            try
            {
                all.AddRange(source());
            }
            catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is JsonException or FormatException)
            {
                // Um lançador com arquivos estranhos não pode derrubar a lista dos outros.
            }
        }

        return all
            .GroupBy(g => g.Id)
            .Select(g => g.First())
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Cria um jogo a partir de um executável escolhido pelo usuário.</summary>
    public static GameInfo FromExecutable(string executablePath)
    {
        var name = Path.GetFileNameWithoutExtension(executablePath);
        return GameInfo.Create(name, "Manual", Path.GetDirectoryName(executablePath) ?? string.Empty, executablePath);
    }

    /// <summary>Escolhe o executável principal: o maior que não pareça instalador, atualizador ou antitrapaça.</summary>
    public static string? PickMainExecutable(IEnumerable<ExecutableFile> candidates) =>
        candidates
            .Where(c => !IgnoredExecutableWords.Any(
                w => Path.GetFileName(c.Path).Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(c => c.SizeBytes)
            .Select(c => c.Path)
            .FirstOrDefault();

    private IEnumerable<GameInfo> DetectSteam()
    {
        var steamPath = _registry.Read(new RegistryLocation(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath"));
        if (steamPath is null || steamPath.Kind != RegistryDataKind.Text)
        {
            yield break;
        }

        var root = NormalizePath(steamPath.AsText());
        var libraries = new List<string> { root };
        var vdf = _files.ReadAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
        if (vdf is not null)
        {
            libraries.AddRange(VdfPathRegex().Matches(vdf).Select(m => NormalizePath(m.Groups[1].Value.Replace(@"\\", @"\"))));
        }

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var steamApps = Path.Combine(library, "steamapps");
            foreach (var manifest in _files.ListFiles(steamApps, "appmanifest_*.acf"))
            {
                var text = _files.ReadAllText(manifest);
                var name = text is null ? null : FirstGroup(VdfNameRegex(), text);
                var installDir = text is null ? null : FirstGroup(VdfInstallDirRegex(), text);
                if (name is null || installDir is null
                    || IgnoredSteamPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var directory = Path.Combine(steamApps, "common", installDir);
                if (!_files.DirectoryExists(directory))
                {
                    continue;
                }

                var exe = PickMainExecutable(_files.ListExecutables(directory));
                if (exe is not null)
                {
                    yield return GameInfo.Create(name, "Steam", directory, exe);
                }
            }
        }
    }

    private IEnumerable<GameInfo> DetectEpic()
    {
        var manifests = Path.Combine(_programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        foreach (var file in _files.ListFiles(manifests, "*.item"))
        {
            var text = _files.ReadAllText(file);
            if (text is null)
            {
                continue;
            }

            using var document = JsonDocument.Parse(text);
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = StringOf(item, "DisplayName");
            var location = StringOf(item, "InstallLocation");
            var launch = StringOf(item, "LaunchExecutable");
            if (name is null || location is null || string.IsNullOrWhiteSpace(launch) || !IsGame(item))
            {
                continue;
            }

            var exe = Path.Combine(NormalizePath(location), launch);
            if (_files.FileExists(exe))
            {
                yield return GameInfo.Create(name, "Epic Games", NormalizePath(location), exe);
            }
        }
    }

    private IEnumerable<GameInfo> DetectGog()
    {
        foreach (var key in _registry.GetSubKeyNames(RegistryRoot.LocalMachine, GogKey))
        {
            string? Text(string value)
            {
                var data = _registry.Read(new RegistryLocation(RegistryRoot.LocalMachine, GogKey + "\\" + key, value));
                return data is { Kind: RegistryDataKind.Text } ? data.AsText() : null;
            }

            var name = Text("gameName");
            var path = Text("path");
            var exe = Text("exe");
            if (name is not null && path is not null && exe is not null && _files.FileExists(exe))
            {
                yield return GameInfo.Create(name, "GOG", NormalizePath(path), exe);
            }
        }
    }

    private static bool IsGame(JsonElement item)
    {
        if (!item.TryGetProperty("AppCategories", out var categories) || categories.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        return categories.EnumerateArray().Any(c => string.Equals(c.GetString(), "games", StringComparison.OrdinalIgnoreCase));
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? FirstGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string NormalizePath(string path) => path.Trim().Replace('/', '\\').TrimEnd('\\');

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();

    [GeneratedRegex("\"name\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfNameRegex();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfInstallDirRegex();
}
