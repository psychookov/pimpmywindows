namespace PimpMyWindows.Core.Tweaks;

/// <summary>
/// Quem vê o ajuste. Básico: simples e seguro, para qualquer pessoa.
/// Técnico: para quem sabe o que está fazendo; só aparece depois que o usuário ativa o Modo Técnico.
/// </summary>
public enum UserMode
{
    Basic,
    Technical,
}

/// <summary>Números de build das versões do Windows, para usar em MinBuild/MaxBuild.</summary>
public static class WindowsBuilds
{
    public const int Windows10_2004 = 19041;
    public const int Windows10_22H2 = 19045;
    public const int Windows11_21H2 = 22000;
    public const int Windows11_22H2 = 22621;
    public const int Windows11_23H2 = 22631;
    public const int Windows11_24H2 = 26100;
    public const int Windows11_25H2 = 26200;
}

/// <summary>
/// Decide se um ajuste vale para o Windows em que o programa está rodando.
/// Ajustes fora da faixa de builds não aparecem para o usuário e não são aplicados.
/// </summary>
public static class TweakApplicability
{
    public static bool IsApplicable(TweakDefinition tweak, int windowsBuild) =>
        (tweak.MinBuild is null || windowsBuild >= tweak.MinBuild)
        && (tweak.MaxBuild is null || windowsBuild <= tweak.MaxBuild);

    /// <summary>Explica, em português, em quais versões o ajuste existe.</summary>
    public static string DescribeSupportedVersions(TweakDefinition tweak)
    {
        var min = tweak.MinBuild;
        var max = tweak.MaxBuild;

        if (min is null && max is null)
        {
            return "Windows 10 e Windows 11";
        }

        if (min is >= WindowsBuilds.Windows11_21H2 && max is null)
        {
            return $"Windows 11 (build {min} ou mais recente)";
        }

        if (max is < WindowsBuilds.Windows11_21H2 && min is null or < WindowsBuilds.Windows11_21H2)
        {
            return "Windows 10";
        }

        return (min, max) switch
        {
            (not null, not null) => $"builds {min} a {max}",
            (not null, null) => $"build {min} ou mais recente",
            _ => $"build {max} ou mais antiga",
        };
    }
}
