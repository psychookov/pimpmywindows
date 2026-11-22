using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Games;

/// <summary>
/// Cria os ajustes de um jogo. Eles usam o mesmo motor dos ajustes comuns (prévia, backup, desfazer),
/// só que o valor do registro é o caminho do executável do jogo.
/// </summary>
public static class GameTweaks
{
    public const string GpuSuffix = ".gpu";
    public const string FullscreenSuffix = ".fso";

    public static string GpuId(GameInfo game) => $"game.{game.Id}{GpuSuffix}";

    public static string FullscreenId(GameInfo game) => $"game.{game.Id}{FullscreenSuffix}";

    public static TweakDefinition HighPerformanceGpu(GameInfo game) => new(
        Id: GpuId(game),
        Name: $"{game.Name}: usar a placa de vídeo dedicada",
        Summary: "Pede ao Windows para sempre rodar este jogo na placa de vídeo mais potente.",
        Details: "Em notebooks e PCs com vídeo integrado e dedicado, o Windows às vezes escolhe a placa fraca para um jogo. " +
                 "Este ajuste grava a preferência “Alto desempenho” só para este executável, o mesmo que fazer em " +
                 "Configurações > Sistema > Tela > Elementos gráficos. Não mexe em nenhum outro programa.",
        Category: TweakCategory.Gaming,
        Risk: RiskLevel.Safe,
        RequiresAdmin: false,
        Restart: RestartRequirement.None,
        Changes:
        [
            new RegistryChange(
                new RegistryLocation(
                    RegistryRoot.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", game.ExecutablePath),
                RegistryData.Text("GpuPreference=2;"),
                null),
        ],
        MinBuild: 17134);

    public static TweakDefinition DisableFullscreenOptimizations(GameInfo game) => new(
        Id: FullscreenId(game),
        Name: $"{game.Name}: desativar otimizações de tela inteira",
        Summary: "Marca a opção “Desativar otimizações de tela inteira” nas propriedades do jogo.",
        Details: "Alguns jogos antigos ou mal adaptados ficam com atraso nos controles ou travamentos em tela cheia por causa " +
                 "das otimizações de tela inteira do Windows. Este ajuste liga a caixa que existe em " +
                 "Propriedades do executável > Compatibilidade. Só vale para este jogo.",
        Category: TweakCategory.Gaming,
        Risk: RiskLevel.Moderate,
        RequiresAdmin: false,
        Restart: RestartRequirement.None,
        Changes:
        [
            new RegistryChange(
                new RegistryLocation(
                    RegistryRoot.CurrentUser,
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
                    game.ExecutablePath),
                RegistryData.Text("~ DISABLEDXMAXIMIZEDWINDOWEDMODE"),
                null),
        ],
        Warning: "Se você já tinha outras opções de compatibilidade neste jogo (como “executar como administrador”), " +
                 "elas serão substituídas enquanto este ajuste estiver ativo e voltam quando você desfizer.",
        Mode: UserMode.Technical);

    public static IReadOnlyList<TweakDefinition> For(GameInfo game) =>
        [HighPerformanceGpu(game), DisableFullscreenOptimizations(game)];
}
