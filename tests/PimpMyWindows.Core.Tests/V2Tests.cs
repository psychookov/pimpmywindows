using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Settings;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

public class V2Tests
{
    private static readonly TweakDefinition Two = TweakCatalog.All.First(t => t.Changes.Count >= 2 && !t.RequiresAdmin);

    private static (TweakEngine Engine, FakeRegistry Reg, InMemoryBackupStore Backups) Setup(bool admin = true, int build = 26100)
    {
        var reg = new FakeRegistry();
        var backups = new InMemoryBackupStore();
        return (new TweakEngine(reg, backups, new NullActionLog(), () => admin, () => build), reg, backups);
    }

    [Fact]
    public void Apply_AfterPartialState_KeepsUsersOriginalValues()
    {
        var (engine, reg, backups) = Setup();
        var first = Two.Changes[0];
        var second = Two.Changes[1];
        reg.Seed(first.Location, RegistryData.DWord(7));
        reg.Seed(second.Location, RegistryData.DWord(9));
        Assert.True(engine.Apply(Two).Success);

        // O Windows desfaz só metade do ajuste numa atualização.
        reg.Seed(second.Location, RegistryData.DWord(9));
        Assert.True(engine.Apply(Two).Success);

        var saved = backups.Load(Two.Id)!;
        Assert.Equal(RegistryData.DWord(7), saved.Values.First(v => v.Location == first.Location).Previous);
        Assert.Equal(RegistryData.DWord(9), saved.Values.First(v => v.Location == second.Location).Previous);
    }

    [Fact]
    public void Planner_ListsOnlyRealChanges_AndDoesNotWrite()
    {
        var (_, reg, backups) = Setup();
        var planner = new TweakPlanner(reg, backups, () => true, () => 26100);
        reg.Seed(Two.Changes[0].Location, Two.Changes[0].OptimizedValue);

        var plan = planner.Plan(Two, TweakAction.Apply);

        Assert.Equal(Two.Changes.Count - 1, plan.Changes.Count);
        Assert.Equal(0, reg.WriteCount);
    }

    [Fact]
    public void Planner_BlocksWhenAdminIsMissing_AndWhenBuildIsTooOld()
    {
        var (_, reg, backups) = Setup();
        var admin = TweakCatalog.All.First(t => t.RequiresAdmin);
        Assert.True(new TweakPlanner(reg, backups, () => false).Plan(admin, TweakAction.Apply).IsBlocked);

        var win11 = TweakCatalog.All.First(t => t.MinBuild >= WindowsBuilds.Windows11_21H2 && !t.RequiresAdmin);
        Assert.True(new TweakPlanner(reg, backups, () => true, () => 19045).Plan(win11, TweakAction.Apply).IsBlocked);
    }

    [Fact]
    public void BatchRunner_ContinuesAfterAFailure()
    {
        var (engine, reg, backups) = Setup(admin: false);
        var planner = new TweakPlanner(reg, backups, () => true, () => 26100);
        var ok = TweakCatalog.All.First(t => !t.RequiresAdmin);
        var needsAdmin = TweakCatalog.All.First(t => t.RequiresAdmin && TweakApplicability.IsApplicable(t, 26100));
        var plans = new[] { planner.Plan(needsAdmin, TweakAction.Apply), planner.Plan(ok, TweakAction.Apply) };

        var result = new TweakBatchRunner(engine, new NullActionLog()).Run(plans);

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public void Profiles_OnlyReferenceExistingTweaks_ThatSuitBasicMode()
    {
        var ids = TweakCatalog.All.ToDictionary(t => t.Id);
        foreach (var profile in TweakProfiles.All)
        {
            Assert.NotEmpty(profile.TweakIds);
            Assert.All(profile.TweakIds, id =>
            {
                Assert.True(ids.ContainsKey(id), $"{profile.Id}: ajuste inexistente {id}");
                Assert.Equal(UserMode.Basic, ids[id].Mode);
            });
        }
    }

    [Fact]
    public void Selection_RoundTrips_AndRejectsGarbage()
    {
        var json = TweakSelection.Export(["b.two", "a.one", "a.one"]);
        var back = TweakSelection.Import(json, ["a.one", "b.two"], out var unknown);

        Assert.Equal(new[] { "a.one", "b.two" }, back);
        Assert.Empty(unknown);
        Assert.Throws<FormatException>(() => TweakSelection.Import("não é json", ["a.one"], out _));
        Assert.Throws<FormatException>(() => TweakSelection.Import("{\"version\":2,\"tweaks\":[]}", ["a.one"], out _));
    }

    [Fact]
    public void Selection_ReportsUnknownIds()
    {
        var json = TweakSelection.Export(["a.one", "x.evil"]);
        var valid = TweakSelection.Import(json, ["a.one"], out var unknown);

        Assert.Equal(new[] { "a.one" }, valid);
        Assert.Equal(new[] { "x.evil" }, unknown);
    }

    [Fact]
    public void UndoCenter_ListsAppliedTweaks_FlagsDrift_AndDiscards()
    {
        var (engine, reg, backups) = Setup();
        var tweak = TweakCatalog.All.First(t => !t.RequiresAdmin);
        Assert.True(engine.Apply(tweak).Success);
        var undo = new UndoCenterService(backups, engine, TweakCatalog.All, new NullActionLog());

        Assert.False(Assert.Single(undo.List()).WasUndoneOutside);

        foreach (var c in tweak.Changes)
        {
            reg.Delete(c.Location);
        }

        Assert.True(Assert.Single(undo.List()).WasUndoneOutside);
        Assert.True(undo.Discard(tweak).Success);
        Assert.Empty(undo.List());
    }

    [Fact]
    public void RoutingStore_SendsMachineTweaksToTheMachineStore()
    {
        var user = new InMemoryBackupStore();
        var machine = new InMemoryBackupStore();
        var routing = new RoutingBackupStore(user, machine, id => id.StartsWith("m."));
        routing.Save(new TweakBackup("m.one", DateTimeOffset.UtcNow, []));
        routing.Save(new TweakBackup("u.one", DateTimeOffset.UtcNow, []));

        Assert.NotNull(machine.Load("m.one"));
        Assert.Null(user.Load("m.one"));
        Assert.Equal(2, routing.ListAll().Count);
    }

    private sealed class DenyingGuard : IDirectoryGuard
    {
        public void BeforeRead(string directory)
        {
        }

        public void BeforeWrite(string directory) => throw new UnauthorizedAccessException("pasta não confiável");
    }

    [Fact]
    public void JsonStore_ListsBackups_AndHonorsTheGuard()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pmw-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonBackupStore(dir);
            store.Save(new TweakBackup("a.one", DateTimeOffset.UtcNow, []));
            File.WriteAllText(Path.Combine(dir, "b.two.json"), "lixo");

            Assert.Equal("a.one", Assert.Single(store.ListAll()).TweakId);
            Assert.Throws<UnauthorizedAccessException>(() =>
                new JsonBackupStore(dir, new DenyingGuard()).Save(new TweakBackup("c.three", DateTimeOffset.UtcNow, [])));
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
    public void Settings_FallBackToDefaults_AndRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmw-" + Guid.NewGuid().ToString("N"), "settings.json");
        var store = new JsonSettingsStore(path);
        Assert.Equal(ThemeChoice.System, store.Load().Theme);

        store.Save(new UserSettings { Theme = ThemeChoice.Dark });
        Assert.Equal(ThemeChoice.Dark, store.Load().Theme);

        File.WriteAllText(path, "{corrompido");
        Assert.Equal(ThemeChoice.System, store.Load().Theme);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void SystemTheme_ReadsRegistry_AndDefaultsToDark()
    {
        var reg = new FakeRegistry();
        Assert.False(SystemTheme.IsLight(reg));
        reg.Seed(new(RegistryRoot.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme"),
            RegistryData.DWord(1));
        Assert.True(SystemTheme.IsLight(reg));
    }

    [Theory]
    [InlineData("\"C:\\A B\\app.exe\" --x", "C:\\A B\\app.exe")]
    [InlineData("C:\\Prog\\app.exe /silent", "C:\\Prog\\app.exe")]
    [InlineData("app --flag", "app")]
    public void ExtractExecutable_HandlesCommonShapes(string command, string expected) =>
        Assert.Equal(expected, StartupCommand.ExtractExecutable(command));

    private sealed class FakeFiles : IStartupFileSystem
    {
        public List<StartupFile> UserFolder { get; } = [];

        public HashSet<string> Existing { get; } = [];

        public IReadOnlyList<StartupFile> ListFolder(bool allUsers) => allUsers ? [] : UserFolder;

        public bool FileExists(string path) => Existing.Contains(path);

        public string ExpandVariables(string text) => text;

        public string Describe(string path) => "Descrição";
    }

    [Fact]
    public void Startup_FlagsMissingPrograms_AndListsFolderEntries()
    {
        var reg = new FakeRegistry();
        reg.Seed(new(RegistryRoot.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Velho"),
            RegistryData.Text(@"C:\Sumiu\velho.exe"));
        var files = new FakeFiles();
        files.UserFolder.Add(new StartupFile("Atalho.lnk", @"C:\Ok\ok.exe", "x"));
        files.Existing.Add(@"C:\Ok\ok.exe");
        var service = new StartupService(reg, () => false, new NullActionLog(), files: files);

        var entries = service.List();

        Assert.True(entries.Single(e => e.Name == "Velho").FileMissing);
        var folder = entries.Single(e => e.Name == "Atalho.lnk");
        Assert.True(folder.IsFolderEntry);
        Assert.False(folder.FileMissing);

        Assert.True(service.SetEnabled(folder, enable: false).Success);
        Assert.False(service.List().Single(e => e.Name == "Atalho.lnk").IsEnabled);
    }

    [Fact]
    public void Catalog_RecommendedTweaks_AreSafeAndBasic()
    {
        Assert.NotEmpty(TweakCatalog.All.Where(t => t.Recommended));
        Assert.All(TweakCatalog.All.Where(t => t.Recommended), t =>
        {
            Assert.Equal(RiskLevel.Safe, t.Risk);
            Assert.Equal(UserMode.Basic, t.Mode);
        });
    }
}
