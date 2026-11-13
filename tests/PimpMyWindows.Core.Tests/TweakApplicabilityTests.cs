using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

public class TweakApplicabilityTests
{
    private static readonly RegistryLocation Location = new(RegistryRoot.CurrentUser, @"Software\Teste", "X");

    private static TweakDefinition Tweak(int? min = null, int? max = null) => new(
        Id: "teste.build",
        Name: "Ajuste por build",
        Summary: "resumo",
        Details: "detalhes",
        Category: TweakCategory.Interface,
        Risk: RiskLevel.Safe,
        RequiresAdmin: false,
        Restart: RestartRequirement.None,
        Changes: [new RegistryChange(Location, RegistryData.DWord(0), RegistryData.DWord(1))],
        MinBuild: min,
        MaxBuild: max);

    [Theory]
    [InlineData(null, null, 19045, true)]
    [InlineData(null, null, 26100, true)]
    [InlineData(22000, null, 19045, false)]   // ajuste só do Windows 11 em um Windows 10
    [InlineData(22000, null, 22000, true)]    // limite inclusivo
    [InlineData(22000, null, 26100, true)]
    [InlineData(null, 19045, 19045, true)]    // ajuste só do Windows 10 (limite inclusivo)
    [InlineData(null, 19045, 22621, false)]   // ... em um Windows 11
    [InlineData(22621, 26100, 22631, true)]
    [InlineData(22621, 26100, 22000, false)]
    [InlineData(22621, 26100, 26200, false)]
    public void IsApplicable_RespectsBuildRange(int? min, int? max, int build, bool expected)
    {
        Assert.Equal(expected, TweakApplicability.IsApplicable(Tweak(min, max), build));
    }

    [Theory]
    [InlineData(null, null, "Windows 10 e Windows 11")]
    [InlineData(22000, null, "Windows 11 (build 22000 ou mais recente)")]
    [InlineData(null, 19045, "Windows 10")]
    [InlineData(22621, 26100, "builds 22621 a 26100")]
    public void DescribeSupportedVersions_IsReadable(int? min, int? max, string expected)
    {
        Assert.Equal(expected, TweakApplicability.DescribeSupportedVersions(Tweak(min, max)));
    }

    [Fact]
    public void Engine_RefusesToApply_OnUnsupportedBuild_AndWritesNothing()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryBackupStore(), new NullActionLog(), () => true, () => 19045);

        var result = engine.Apply(Tweak(min: WindowsBuilds.Windows11_21H2));

        Assert.False(result.Success);
        Assert.Contains("19045", result.Message);
        Assert.Equal(0, registry.WriteCount);
        Assert.Null(registry.Read(Location));
    }

    [Fact]
    public void Engine_Applies_OnSupportedBuild()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryBackupStore(), new NullActionLog(), () => true, () => 26100);

        var result = engine.Apply(Tweak(min: WindowsBuilds.Windows11_21H2));

        Assert.True(result.Success);
        Assert.Equal(RegistryData.DWord(0), registry.Read(Location));
    }

    [Fact]
    public void Engine_WithoutBuildProvider_DoesNotFilter()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryBackupStore(), new NullActionLog(), () => true);

        Assert.True(engine.Apply(Tweak(min: WindowsBuilds.Windows11_25H2)).Success);
    }

    [Fact]
    public void Engine_AllowsRevert_EvenOnUnsupportedBuild_SoUsersCanUndoAfterAWindowsUpdate()
    {
        var registry = new FakeRegistry();
        registry.Seed(Location, RegistryData.DWord(0)); // aplicado quando o Windows era mais novo
        var engine = new TweakEngine(registry, new InMemoryBackupStore(), new NullActionLog(), () => true, () => 19045);

        var result = engine.Revert(Tweak(min: WindowsBuilds.Windows11_21H2));

        Assert.True(result.Success);
        Assert.Equal(RegistryData.DWord(1), registry.Read(Location));
    }
}
