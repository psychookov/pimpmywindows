using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Startup;

namespace PimpMyWindows.Core.Tests;

public class StartupServiceTests
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private static StartupService Create(FakeRegistry registry, bool admin = false) =>
        new(registry, () => admin, new NullActionLog(), () => FixedNow);

    [Fact]
    public void List_ShowsEntries_AsEnabledByDefault()
    {
        var registry = new FakeRegistry();
        registry.Seed(new(RegistryRoot.CurrentUser, Run, "Discord"), RegistryData.Text(@"C:\Discord\Update.exe"));

        var entries = Create(registry).List();

        var entry = Assert.Single(entries);
        Assert.Equal("Discord", entry.Name);
        Assert.True(entry.IsEnabled);
        Assert.Equal(StartupSource.CurrentUser, entry.Source);
    }

    [Fact]
    public void List_DetectsDisabledEntries_FromStartupApproved()
    {
        var registry = new FakeRegistry();
        registry.Seed(new(RegistryRoot.CurrentUser, Run, "Teams"), RegistryData.Text("teams.exe"));
        registry.Seed(new(RegistryRoot.CurrentUser, Approved, "Teams"),
            RegistryData.Binary([0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));

        Assert.False(Create(registry).List().Single().IsEnabled);
    }

    [Fact]
    public void SetEnabled_False_WritesDisabledMarker_WithoutDeletingTheEntry()
    {
        var registry = new FakeRegistry();
        var runLocation = new RegistryLocation(RegistryRoot.CurrentUser, Run, "Spotify");
        registry.Seed(runLocation, RegistryData.Text("spotify.exe"));
        var service = Create(registry);

        var result = service.SetEnabled(service.List().Single(), enable: false);

        Assert.True(result.Success);
        Assert.NotNull(registry.Read(runLocation)); // a entrada original continua lá
        var marker = registry.Read(new(RegistryRoot.CurrentUser, Approved, "Spotify"))!.AsBinary();
        Assert.Equal(12, marker.Length);
        Assert.Equal(0x03, marker[0]);
        Assert.Equal(FixedNow.ToFileTimeUtc(), BitConverter.ToInt64(marker, 4));
        Assert.False(service.List().Single().IsEnabled);
    }

    [Fact]
    public void SetEnabled_True_ReEnablesEntry()
    {
        var registry = new FakeRegistry();
        registry.Seed(new(RegistryRoot.CurrentUser, Run, "Spotify"), RegistryData.Text("spotify.exe"));
        var service = Create(registry);

        service.SetEnabled(service.List().Single(), enable: false);
        service.SetEnabled(service.List().Single(), enable: true);

        Assert.True(service.List().Single().IsEnabled);
    }

    [Fact]
    public void SetEnabled_ForAllUsersEntry_RequiresAdministrator()
    {
        var registry = new FakeRegistry();
        registry.Seed(new(RegistryRoot.LocalMachine, Run, "Driver"), RegistryData.Text("driver.exe"));
        var entry = Create(registry).List().Single();

        var denied = Create(registry, admin: false).SetEnabled(entry, enable: false);
        Assert.False(denied.Success);
        Assert.True(Create(registry).List().Single().IsEnabled);

        var allowed = Create(registry, admin: true).SetEnabled(entry, enable: false);
        Assert.True(allowed.Success);
        Assert.False(Create(registry).List().Single().IsEnabled);
    }

    [Theory]
    [InlineData(0x02, true)]
    [InlineData(0x06, true)]
    [InlineData(0x03, false)]
    [InlineData(0x07, false)]
    public void IsEnabled_FollowsLowBitOfFirstByte(byte first, bool expected)
    {
        var data = RegistryData.Binary([first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        Assert.Equal(expected, StartupService.IsEnabled(data));
    }
}
