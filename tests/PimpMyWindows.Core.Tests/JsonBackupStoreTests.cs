using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tests;

public sealed class JsonBackupStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pmw-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTrips_AllValueKinds_AndMissingValues()
    {
        var store = new JsonBackupStore(_directory);
        var created = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        var backup = new TweakBackup("teste.ajuste", created,
        [
            new(new(RegistryRoot.CurrentUser, @"Software\A", "Dword"), RegistryData.DWord(-5)),
            new(new(RegistryRoot.CurrentUser, @"Software\A", "Texto"), RegistryData.Text("olá \"mundo\"")),
            new(new(RegistryRoot.LocalMachine, @"SOFTWARE\B", "Binario"), RegistryData.Binary([1, 2, 255])),
            new(new(RegistryRoot.CurrentUser, @"Software\A", "NaoExistia"), null),
        ]);

        store.Save(backup);
        var loaded = store.Load("teste.ajuste");

        Assert.NotNull(loaded);
        Assert.Equal(created, loaded!.CreatedAt);
        Assert.Equal(backup.Values.Count, loaded.Values.Count);
        for (var i = 0; i < backup.Values.Count; i++)
        {
            Assert.Equal(backup.Values[i].Location, loaded.Values[i].Location);
            Assert.Equal(backup.Values[i].Previous, loaded.Values[i].Previous);
        }
    }

    [Fact]
    public void Load_ReturnsNull_WhenThereIsNoBackup()
    {
        Assert.Null(new JsonBackupStore(_directory).Load("nao.existe"));
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileIsCorrupted()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "quebrado.json"), "{ isto não é json");

        Assert.Null(new JsonBackupStore(_directory).Load("quebrado"));
    }

    [Fact]
    public void Delete_RemovesTheFile()
    {
        var store = new JsonBackupStore(_directory);
        store.Save(new TweakBackup("x.y", DateTimeOffset.UtcNow, []));

        store.Delete("x.y");

        Assert.Null(store.Load("x.y"));
    }

    [Theory]
    [InlineData(@"..\fora")]
    [InlineData("../fora")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("MAIUSCULO")]
    public void InvalidIds_AreRejected_SoNothingEscapesTheBackupFolder(string id)
    {
        var store = new JsonBackupStore(_directory);

        Assert.Throws<ArgumentException>(() => store.Load(id));
        Assert.Throws<ArgumentException>(() => store.Save(new TweakBackup(id, DateTimeOffset.UtcNow, [])));
    }
}
