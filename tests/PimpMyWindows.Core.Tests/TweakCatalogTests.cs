using System.Text.RegularExpressions;
using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

/// <summary>
/// Regras de qualidade e segurança que todo ajuste do catálogo precisa cumprir.
/// Se você adicionar um ajuste novo e algum destes testes falhar, o motivo está na mensagem.
/// </summary>
public class TweakCatalogTests
{
    public static IEnumerable<object[]> AllTweaks => TweakCatalog.All.Select(t => new object[] { t });

    [Fact]
    public void Ids_AreUnique_AndFileSafe()
    {
        var ids = TweakCatalog.All.Select(t => t.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Matches("^[a-z0-9][a-z0-9.-]*$", id));
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void Tweak_HasUserFacingTexts(TweakDefinition tweak)
    {
        Assert.False(string.IsNullOrWhiteSpace(tweak.Name), $"{tweak.Id}: falta o nome");
        Assert.False(string.IsNullOrWhiteSpace(tweak.Summary), $"{tweak.Id}: falta o resumo");
        Assert.False(string.IsNullOrWhiteSpace(tweak.Details), $"{tweak.Id}: faltam os detalhes");
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void Tweak_HasAtLeastOneChange_AndNoDuplicateLocations(TweakDefinition tweak)
    {
        Assert.NotEmpty(tweak.Changes);
        var locations = tweak.Changes.Select(c => c.Location).ToList();
        Assert.Equal(locations.Count, locations.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void Tweak_OptimizedValueDiffersFromDefault(TweakDefinition tweak)
    {
        Assert.All(tweak.Changes, c =>
            Assert.NotEqual(c.OptimizedValue, c.DefaultValue));
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void Tweak_RequiresAdmin_IffItTouchesLocalMachine(TweakDefinition tweak)
    {
        var touchesMachine = tweak.Changes.Any(c => c.Location.Root == RegistryRoot.LocalMachine);
        Assert.Equal(touchesMachine, tweak.RequiresAdmin);
    }

    [Theory]
    [MemberData(nameof(AllTweaks))]
    public void Tweak_NeverTouchesSecurityCriticalKeys(TweakDefinition tweak)
    {
        var forbidden = new[]
        {
            @"Windows Defender", @"Windows Update", @"WindowsUpdate", @"SecurityHealth",
            @"Control\Lsa", @"Services\", @"Firewall", @"Policies\Microsoft\Windows Defender",
        };

        Assert.All(tweak.Changes, c =>
            Assert.DoesNotContain(forbidden, f => c.Location.SubKey.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void NoTweak_IsAdvancedRisk_InTheDefaultCatalog()
    {
        Assert.DoesNotContain(TweakCatalog.All, t => t.Risk == RiskLevel.Advanced);
    }
}
