namespace PimpMyWindows.Core.Tweaks;

/// <summary>Conjunto pronto de ajustes para um objetivo (ex.: privacidade).</summary>
public sealed record TweakProfile(string Id, string Name, string Description, IReadOnlyList<string> TweakIds);

public static class TweakProfiles
{
    public static IReadOnlyList<TweakProfile> All { get; } =
    [
        new("privacidade", "Privacidade",
            "Reduz anúncios, sugestões e rastros de uso, sem tirar nenhum recurso do Windows.",
            [
                "privacy.advertising-id",
                "privacy.suggestions",
                "privacy.bing-search",
                "privacy.recent-docs",
            ]),
        new("jogos", "Jogos",
            "Tira o que atrapalha o desempenho em jogos e deixa o mouse mais previsível.",
            [
                "gaming.game-dvr",
                "gaming.mouse-accel",
                "performance.transparency",
            ]),
        new("notebook", "Notebook",
            "Menos coisas em segundo plano e efeitos que gastam GPU, para a bateria durar mais e o aparelho esquentar menos. " +
            "Combine com a página Energia (plano Equilibrado ou Economia na bateria) e com Serviços e tarefas.",
            [
                "performance.transparency",
                "interface.widgets",
                "privacy.suggestions",
            ]),
        new("conforto", "Conforto no dia a dia",
            "Deixa o Windows mais direto: extensões visíveis, abertura no Este Computador e menos esperas.",
            [
                "interface.file-extensions",
                "interface.open-this-pc",
                "interface.accessibility-shortcuts",
                "performance.startup-delay",
                "performance.menu-delay",
            ]),
    ];
}
