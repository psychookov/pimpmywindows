using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Tests;

public class TweakEngineTests
{
    private static readonly RegistryLocation A = new(RegistryRoot.CurrentUser, @"Software\Teste", "A");
    private static readonly RegistryLocation B = new(RegistryRoot.CurrentUser, @"Software\Teste", "B");
    private static readonly RegistryLocation Machine = new(RegistryRoot.LocalMachine, @"SOFTWARE\Teste", "M");

    private static TweakDefinition Tweak(bool admin = false, params RegistryChange[] changes) => new(
        Id: "teste.ajuste",
        Name: "Ajuste de teste",
        Summary: "resumo",
        Details: "detalhes",
        Category: TweakCategory.Performance,
        Risk: RiskLevel.Safe,
        RequiresAdmin: admin,
        Restart: RestartRequirement.None,
        Changes: changes.Length > 0
            ? changes
            : new[] { new RegistryChange(A, RegistryData.DWord(0), RegistryData.DWord(1)) });

    private static (TweakEngine Engine, FakeRegistry Registry, InMemoryBackupStore Backups) Create(bool admin = true)
    {
        var registry = new FakeRegistry();
        var backups = new InMemoryBackupStore();
        return (new TweakEngine(registry, backups, new NullActionLog(), () => admin), registry, backups);
    }

    [Fact]
    public void GetState_ReportsNotApplied_Applied_AndPartial()
    {
        var (engine, registry, _) = Create();
        var tweak = Tweak(false,
            new RegistryChange(A, RegistryData.DWord(0), null),
            new RegistryChange(B, RegistryData.DWord(0), null));

        Assert.Equal(TweakState.NotApplied, engine.GetState(tweak));

        registry.Seed(A, RegistryData.DWord(0));
        Assert.Equal(TweakState.Partial, engine.GetState(tweak));

        registry.Seed(B, RegistryData.DWord(0));
        Assert.Equal(TweakState.Applied, engine.GetState(tweak));
    }

    [Fact]
    public void Apply_WritesOptimizedValue_AndSavesBackupOfOriginal()
    {
        var (engine, registry, backups) = Create();
        registry.Seed(A, RegistryData.DWord(1));

        var result = engine.Apply(Tweak());

        Assert.True(result.Success);
        Assert.Equal(RegistryData.DWord(0), registry.Read(A));
        var backup = backups.Load("teste.ajuste");
        Assert.NotNull(backup);
        Assert.Equal(RegistryData.DWord(1), backup!.Values.Single().Previous);
    }

    [Fact]
    public void Apply_WhenAlreadyApplied_ChangesNothing()
    {
        var (engine, registry, backups) = Create();
        registry.Seed(A, RegistryData.DWord(0));

        var result = engine.Apply(Tweak());

        Assert.True(result.Success);
        Assert.Equal(0, registry.WriteCount);
        Assert.Null(backups.Load("teste.ajuste"));
    }

    [Fact]
    public void Revert_RestoresUserOriginalValue_NotJustWindowsDefault()
    {
        var (engine, registry, _) = Create();
        registry.Seed(A, RegistryData.DWord(7)); // valor personalizado do usuário
        var tweak = Tweak();

        engine.Apply(tweak);
        var result = engine.Revert(tweak);

        Assert.True(result.Success);
        Assert.Equal(RegistryData.DWord(7), registry.Read(A));
    }

    [Fact]
    public void Revert_DeletesValue_WhenItDidNotExistBefore()
    {
        var (engine, registry, _) = Create();
        var tweak = Tweak(false, new RegistryChange(A, RegistryData.DWord(1), null));

        engine.Apply(tweak);
        Assert.Equal(RegistryData.DWord(1), registry.Read(A));

        engine.Revert(tweak);
        Assert.Null(registry.Read(A));
    }

    [Fact]
    public void Revert_WithoutBackup_FallsBackToWindowsDefault()
    {
        var (engine, registry, _) = Create();
        registry.Seed(A, RegistryData.DWord(0)); // aplicado fora do programa, sem backup

        var result = engine.Revert(Tweak());

        Assert.True(result.Success);
        Assert.Equal(RegistryData.DWord(1), registry.Read(A));
    }

    [Fact]
    public void Revert_WhenAlreadyDefault_ChangesNothing()
    {
        var (engine, registry, _) = Create();
        var result = engine.Revert(Tweak());

        Assert.True(result.Success);
        Assert.Equal(0, registry.WriteCount);
    }

    [Fact]
    public void Apply_WhenWriteFailsMidway_RollsBackEverythingAlreadyWritten()
    {
        var (engine, registry, _) = Create();
        registry.Seed(A, RegistryData.DWord(1));
        registry.FailWritesAt = B;
        var tweak = Tweak(false,
            new RegistryChange(A, RegistryData.DWord(0), RegistryData.DWord(1)),
            new RegistryChange(B, RegistryData.DWord(0), null));

        var result = engine.Apply(tweak);

        Assert.False(result.Success);
        Assert.Equal(RegistryData.DWord(1), registry.Read(A)); // A foi desfeito
        Assert.Null(registry.Read(B));
    }

    [Fact]
    public void Apply_WhenBackupFails_DoesNotTouchTheRegistry()
    {
        var registry = new FakeRegistry();
        registry.Seed(A, RegistryData.DWord(1));
        var engine = new TweakEngine(registry, new FailingBackupStore(), new NullActionLog(), () => true);

        var result = engine.Apply(Tweak());

        Assert.False(result.Success);
        Assert.Equal(0, registry.WriteCount);
        Assert.Equal(RegistryData.DWord(1), registry.Read(A));
    }

    [Fact]
    public void Apply_RequiringAdmin_FailsWithoutAdmin_AndChangesNothing()
    {
        var (engine, registry, _) = Create(admin: false);
        var tweak = Tweak(true, new RegistryChange(Machine, RegistryData.DWord(0), null));

        var result = engine.Apply(tweak);

        Assert.False(result.Success);
        Assert.Contains("administrador", result.Message);
        Assert.Equal(0, registry.WriteCount);
    }

    [Fact]
    public void Revert_RequiringAdmin_FailsWithoutAdmin()
    {
        var (engine, registry, _) = Create(admin: false);
        registry.Seed(Machine, RegistryData.DWord(0));
        var tweak = Tweak(true, new RegistryChange(Machine, RegistryData.DWord(0), null));

        var result = engine.Revert(tweak);

        Assert.False(result.Success);
        Assert.Equal(RegistryData.DWord(0), registry.Read(Machine));
    }
}
