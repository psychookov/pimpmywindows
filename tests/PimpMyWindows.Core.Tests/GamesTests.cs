using PimpMyWindows.Core.Games;
using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

public class GamesTests
{
    private sealed class FakeFiles : IGameFileSystem
    {
        public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<ExecutableFile>> Executables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => Files.Contains(path) || Texts.ContainsKey(path);

        public bool DirectoryExists(string path) => Executables.ContainsKey(path);

        public string? ReadAllText(string path) => Texts.GetValueOrDefault(path);

        public IReadOnlyList<string> ListFiles(string directory, string pattern)
        {
            var regex = new System.Text.RegularExpressions.Regex(
                "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return Texts.Keys
                .Where(k => string.Equals(Path.GetDirectoryName(k), directory, StringComparison.OrdinalIgnoreCase)
                    && regex.IsMatch(Path.GetFileName(k)))
                .ToList();
        }

        public IReadOnlyList<ExecutableFile> ListExecutables(string directory) =>
            Executables.GetValueOrDefault(directory) ?? [];
    }

    private static readonly string ProgramData = @"C:\ProgramData";

    private static GameInfo Sample(string exe = @"D:\Jogos\Foo\foo.exe") =>
        GameInfo.Create("Foo", "Manual", @"D:\Jogos\Foo", exe);

    [Fact]
    public void Steam_FindsGamesInEveryLibrary_AndSkipsRuntimes()
    {
        var reg = new FakeRegistry();
        reg.Seed(new RegistryLocation(RegistryRoot.CurrentUser, @"Software\Valve\Steam", "SteamPath"), RegistryData.Text("c:/steam"));
        var files = new FakeFiles();
        files.Texts[@"c:\steam\steamapps\libraryfolders.vdf"] =
            "\"libraryfolders\"\n{\n\"0\"\n{\n\"path\"\t\t\"c:\\\\steam\"\n}\n\"1\"\n{\n\"path\"\t\t\"D:\\\\Biblioteca\"\n}\n}";
        files.Texts[@"c:\steam\steamapps\appmanifest_1.acf"] = "\"AppState\"\n{\n\"name\"\t\"Jogo A\"\n\"installdir\"\t\"JogoA\"\n}";
        files.Texts[@"D:\Biblioteca\steamapps\appmanifest_2.acf"] = "\"AppState\"\n{\n\"name\"\t\"Jogo B\"\n\"installdir\"\t\"JogoB\"\n}";
        files.Texts[@"D:\Biblioteca\steamapps\appmanifest_3.acf"] = "\"AppState\"\n{\n\"name\"\t\"Steamworks Common Redistributables\"\n\"installdir\"\t\"Redist\"\n}";
        files.Executables[@"c:\steam\steamapps\common\JogoA"] = [new(@"c:\steam\steamapps\common\JogoA\a.exe", 5000)];
        files.Executables[@"D:\Biblioteca\steamapps\common\JogoB"] = [new(@"D:\Biblioteca\steamapps\common\JogoB\b.exe", 9000)];
        files.Executables[@"D:\Biblioteca\steamapps\common\Redist"] = [new(@"D:\Biblioteca\steamapps\common\Redist\r.exe", 1)];

        var games = new GameDetector(reg, files, ProgramData).DetectAll();

        Assert.Equal(new[] { "Jogo A", "Jogo B" }, games.Select(g => g.Name).ToArray());
        Assert.All(games, g => Assert.Equal("Steam", g.Launcher));
    }

    [Fact]
    public void Epic_ReadsManifests_AndIgnoresNonGames()
    {
        var files = new FakeFiles();
        var manifests = Path.Combine(ProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
        files.Texts[Path.Combine(manifests, "a.item")] =
            "{\"DisplayName\":\"Epic Jogo\",\"InstallLocation\":\"E:/Epic/Jogo\",\"LaunchExecutable\":\"Bin/jogo.exe\",\"AppCategories\":[\"public\",\"games\"]}";
        files.Texts[Path.Combine(manifests, "b.item")] =
            "{\"DisplayName\":\"Editor\",\"InstallLocation\":\"E:/Epic/Editor\",\"LaunchExecutable\":\"ed.exe\",\"AppCategories\":[\"applications\"]}";
        files.Files.Add(Path.Combine(@"E:\Epic\Jogo", "Bin/jogo.exe"));

        var games = new GameDetector(new FakeRegistry(), files, ProgramData).DetectAll();

        var game = Assert.Single(games);
        Assert.Equal("Epic Jogo", game.Name);
        Assert.Equal("Epic Games", game.Launcher);
    }

    [Fact]
    public void Gog_ReadsRegistrySubKeys()
    {
        var reg = new FakeRegistry();
        const string key = @"SOFTWARE\WOW6432Node\GOG.com\Games\123";
        reg.Seed(new RegistryLocation(RegistryRoot.LocalMachine, key, "gameName"), RegistryData.Text("Jogo GOG"));
        reg.Seed(new RegistryLocation(RegistryRoot.LocalMachine, key, "path"), RegistryData.Text(@"F:\GOG\Jogo"));
        reg.Seed(new RegistryLocation(RegistryRoot.LocalMachine, key, "exe"), RegistryData.Text(@"F:\GOG\Jogo\g.exe"));
        var files = new FakeFiles();
        files.Files.Add(@"F:\GOG\Jogo\g.exe");

        var games = new GameDetector(reg, files, ProgramData).DetectAll();

        Assert.Equal("GOG", Assert.Single(games).Launcher);
    }

    [Fact]
    public void Detection_WithNothingInstalled_ReturnsEmpty() =>
        Assert.Empty(new GameDetector(new FakeRegistry(), new FakeFiles(), ProgramData).DetectAll());

    [Fact]
    public void PickMainExecutable_SkipsHelpersAndTakesTheBiggest()
    {
        var pick = GameDetector.PickMainExecutable(
        [
            new(@"X\unins000.exe", 99_000_000),
            new(@"X\UnityCrashHandler64.exe", 80_000_000),
            new(@"X\game.exe", 50_000_000),
            new(@"X\small.exe", 1_000),
        ]);
        Assert.Equal(@"X\game.exe", pick);
        Assert.Null(GameDetector.PickMainExecutable([new(@"X\setup.exe", 5)]));
    }

    [Fact]
    public void GameId_IsStableAndSafeForBackupFiles()
    {
        var a = GameInfo.IdFor(@"D:\Jogos\Foo\foo.exe");
        Assert.Equal(a, GameInfo.IdFor(@"d:\jogos\foo\FOO.EXE"));
        Assert.Matches("^[a-z0-9]{12}$", a);
        Assert.Matches("^[a-z0-9][a-z0-9.-]*$", GameTweaks.GpuId(Sample()));
    }

    [Fact]
    public void GameTweaks_ApplyAndRevert_RestoreTheOriginalState()
    {
        var game = Sample();
        var tweak = GameTweaks.HighPerformanceGpu(game);
        var reg = new FakeRegistry();
        var engine = new TweakEngine(reg, new InMemoryBackupStore(), new NullActionLog(), () => false, () => 26100);

        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));
        Assert.True(engine.Apply(tweak).Success);
        Assert.Equal(RegistryData.Text("GpuPreference=2;"), reg.Read(tweak.Changes[0].Location));
        Assert.Equal(TweakState.Applied, engine.GetState(tweak));

        Assert.True(engine.Revert(tweak).Success);
        Assert.Null(reg.Read(tweak.Changes[0].Location));
    }

    [Fact]
    public void FullscreenTweak_KeepsExistingCompatFlagsOnRevert()
    {
        var game = Sample();
        var tweak = GameTweaks.DisableFullscreenOptimizations(game);
        var reg = new FakeRegistry();
        reg.Seed(tweak.Changes[0].Location, RegistryData.Text("~ RUNASADMIN"));
        var engine = new TweakEngine(reg, new InMemoryBackupStore(), new NullActionLog(), () => false, () => 26100);

        Assert.True(engine.Apply(tweak).Success);
        Assert.True(engine.Revert(tweak).Success);
        Assert.Equal(RegistryData.Text("~ RUNASADMIN"), reg.Read(tweak.Changes[0].Location));
        Assert.Equal(UserMode.Technical, tweak.Mode);
    }

    [Fact]
    public void GpuTweak_IsBlockedOnWindowsOlderThan1803()
    {
        var tweak = GameTweaks.HighPerformanceGpu(Sample());
        var planner = new TweakPlanner(new FakeRegistry(), new InMemoryBackupStore(), () => false, () => 17000);
        Assert.True(planner.Plan(tweak, TweakAction.Apply).IsBlocked);
    }

    [Fact]
    public void ManagedStore_RoundTrips_AndRejectsTamperedIds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pmw-games-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "games.json");
            var store = new ManagedGameStore(path);
            Assert.Empty(store.Load());

            var game = Sample();
            store.Remember(game);
            store.Remember(game);
            Assert.Equal(game, Assert.Single(store.Load()));
            Assert.Equal(2, store.AllTweaks().Count);

            File.WriteAllText(path, "[{\"Id\":\"../x\",\"Name\":\"x\",\"Launcher\":\"Manual\",\"InstallDirectory\":\"\",\"ExecutablePath\":\"C:\\\\x.exe\"}]");
            Assert.Empty(store.Load());

            File.WriteAllText(path, "isto não é json");
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void UndoCenter_ListsGameTweaks_FromTheDynamicCatalog()
    {
        var game = Sample();
        var tweak = GameTweaks.HighPerformanceGpu(game);
        var reg = new FakeRegistry();
        var backups = new InMemoryBackupStore();
        var engine = new TweakEngine(reg, backups, new NullActionLog(), () => false, () => 26100);
        Assert.True(engine.Apply(tweak).Success);

        var withoutGame = new UndoCenterService(backups, engine, TweakCatalog.All, new NullActionLog());
        Assert.Empty(withoutGame.List());

        var withGame = new UndoCenterService(backups, engine, () => GameTweaks.For(game), new NullActionLog());
        var entry = Assert.Single(withGame.List());
        Assert.Equal(tweak.Id, entry.Tweak.Id);
    }

    [Fact]
    public void FakeRegistry_SubKeyNames_AreImmediateChildrenOnly()
    {
        var reg = new FakeRegistry();
        reg.Seed(new RegistryLocation(RegistryRoot.LocalMachine, @"A\B\C", "v"), RegistryData.DWord(1));
        reg.Seed(new RegistryLocation(RegistryRoot.LocalMachine, @"A\D", "v"), RegistryData.DWord(1));
        Assert.Equal(new[] { "B", "D" }, reg.GetSubKeyNames(RegistryRoot.LocalMachine, "A").OrderBy(s => s).ToArray());
    }
}
