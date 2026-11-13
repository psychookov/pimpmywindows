using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Tweaks;

/// <summary>
/// Catálogo de ajustes incluídos. Para adicionar um ajuste, veja docs/ARQUITETURA.md
/// e o checklist de segurança em docs/SEGURANCA.md.
/// A ordem da lista define a ordem dos grupos na tela.
/// </summary>
public static class TweakCatalog
{
    public static IReadOnlyList<TweakDefinition> All { get; } =
    [
        // ---------- Privacidade ----------
        new TweakDefinition(
            Id: "privacy.advertising-id",
            Name: "Desativar o ID de publicidade",
            Summary: "Impede que aplicativos usem um identificador único seu para mostrar anúncios personalizados.",
            Details: "O Windows cria um ID de publicidade para cada usuário, que os aplicativos podem ler para montar um perfil de interesses. " +
                     "Desativar não remove os anúncios, apenas deixa de personalizá-los e reduz o rastreamento entre aplicativos.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled"), optimized: 0, windowsDefault: 1),
            ]),

        new TweakDefinition(
            Id: "privacy.suggestions",
            Name: "Desativar sugestões e dicas do Windows",
            Summary: "Remove sugestões de aplicativos no Menu Iniciar, dicas nas Configurações e conteúdo promocional.",
            Details: "Desliga o “conteúdo sugerido” do Windows (Content Delivery Manager): aplicativos sugeridos no Menu Iniciar, " +
                     "dicas e truques nas Configurações e ofertas na tela. Não afeta atualizações nem a segurança do sistema.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(ContentDelivery("SubscribedContent-338388Enabled"), 0, 1),
                DWord(ContentDelivery("SubscribedContent-338389Enabled"), 0, 1),
                DWord(ContentDelivery("SubscribedContent-338393Enabled"), 0, 1),
                DWord(ContentDelivery("SubscribedContent-353694Enabled"), 0, 1),
                DWord(ContentDelivery("SubscribedContent-353696Enabled"), 0, 1),
                DWord(ContentDelivery("SystemPaneSuggestionsEnabled"), 0, 1),
                DWord(ContentDelivery("SoftLandingEnabled"), 0, 1),
            ]),

        new TweakDefinition(
            Id: "privacy.bing-search",
            Name: "Desativar a pesquisa na web (Bing) no Menu Iniciar",
            Summary: "A busca do Menu Iniciar passa a procurar apenas no seu computador.",
            Details: "Por padrão, o que você digita no Menu Iniciar também é enviado ao Bing para exibir sugestões da web. " +
                     "Com este ajuste a busca fica só local: mais rápida e mais privada.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hkcu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"), 1, null),
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled"), 0, null),
            ]),

        new TweakDefinition(
            Id: "privacy.telemetry-minimal",
            Name: "Reduzir a coleta de dados de diagnóstico (telemetria)",
            Summary: "Define o envio de dados de diagnóstico para o menor nível que a sua edição do Windows permite.",
            Details: "Usa a política oficial AllowTelemetry. Nas edições Home e Pro, o menor nível aceito é o “Obrigatório” " +
                     "(antes chamado de Básico); apenas Enterprise e Education chegam ao nível “Segurança”. " +
                     "O Windows Update e a segurança do sistema continuam funcionando normalmente.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.Restart,
            Changes:
            [
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"), 0, null),
            ],
            Warning: "Como o ajuste usa uma política do sistema, o Windows pode mostrar o aviso “algumas configurações são " +
                     "gerenciadas pela sua organização” na página de privacidade. Isso é esperado e some ao reverter."),

        // ---------- Desempenho ----------
        new TweakDefinition(
            Id: "performance.transparency",
            Name: "Desativar os efeitos de transparência",
            Summary: "Deixa o Menu Iniciar, a barra de tarefas e as janelas opacos, aliviando placas de vídeo mais fracas.",
            Details: "Os efeitos de transparência (acrílico/mica) exigem processamento gráfico extra. Em computadores antigos " +
                     "ou com vídeo integrado, desativá-los pode deixar a interface mais fluida. Só muda a aparência.",
            Category: TweakCategory.Performance,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"), 0, 1),
            ]),

        new TweakDefinition(
            Id: "performance.startup-delay",
            Name: "Abrir os programas de inicialização sem atraso",
            Summary: "Remove a espera que o Windows aplica antes de abrir os programas que iniciam com ele.",
            Details: "Depois do login, o Windows espera alguns segundos antes de iniciar os programas de inicialização, para " +
                     "priorizar a área de trabalho. Com este ajuste eles abrem imediatamente. Se você tem muitos programas " +
                     "na inicialização, a área de trabalho pode demorar um pouco mais para ficar utilizável.",
            Category: TweakCategory.Performance,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec"), 0, null),
            ]),

        new TweakDefinition(
            Id: "performance.menu-delay",
            Name: "Menus mais rápidos",
            Summary: "Reduz o atraso para abrir submenus de 400 ms para 100 ms.",
            Details: "O Windows espera um instante antes de abrir um submenu quando você passa o mouse. " +
                     "Reduzir essa espera faz a navegação parecer mais ágil. Não consome recurso nenhum do computador.",
            Category: TweakCategory.Performance,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                Text(Hkcu(@"Control Panel\Desktop", "MenuShowDelay"), "100", "400"),
            ]),

        // ---------- Interface e conforto ----------
        new TweakDefinition(
            Id: "interface.file-extensions",
            Name: "Mostrar as extensões dos arquivos",
            Summary: "Exibe o final do nome dos arquivos (.pdf, .docx, .exe), o que ajuda a reconhecer arquivos suspeitos.",
            Details: "Golpes comuns usam nomes como “fatura.pdf.exe”: com as extensões escondidas, o arquivo parece um PDF. " +
                     "Mostrar as extensões é uma proteção simples e não muda nada no funcionamento do sistema.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt"), 0, 1),
            ]),

        new TweakDefinition(
            Id: "interface.open-this-pc",
            Name: "Abrir o Explorador de Arquivos em “Este Computador”",
            Summary: "Em vez de “Acesso rápido” ou “Início”, o Explorador abre direto nos seus discos e pastas.",
            Details: "Muda apenas a página inicial do Explorador de Arquivos. Nenhum arquivo é movido ou apagado.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo"), 1, null),
            ]),

        new TweakDefinition(
            Id: "interface.accessibility-shortcuts",
            Name: "Desativar atalhos de acessibilidade que disparam sem querer",
            Summary: "Evita a janela de “Teclas de Aderência” (apertar Shift 5 vezes) e os avisos de Teclas de Alternância e Filtro.",
            Details: "Esses avisos costumam aparecer durante jogos e digitação rápida. Os recursos continuam disponíveis em " +
                     "Configurações > Acessibilidade; só o atalho de teclado que os liga é desativado.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                Text(Hkcu(@"Control Panel\Accessibility\StickyKeys", "Flags"), "506", "510"),
                Text(Hkcu(@"Control Panel\Accessibility\ToggleKeys", "Flags"), "58", "63"),
                Text(Hkcu(@"Control Panel\Accessibility\Keyboard Response", "Flags"), "122", "126"),
            ],
            Warning: "Se você ou alguém que usa este computador depende desses atalhos de acessibilidade, não aplique este ajuste."),

        // ---------- Jogos ----------
        new TweakDefinition(
            Id: "gaming.game-dvr",
            Name: "Desativar a captura de jogos em segundo plano (Game DVR)",
            Summary: "Impede que o Windows grave seus jogos em segundo plano, liberando processador e placa de vídeo.",
            Details: "A captura em segundo plano do Xbox Game Bar mantém um buffer de vídeo consumindo recursos enquanto você joga, " +
                     "o que pode causar queda de FPS em computadores mais modestos.",
            Category: TweakCategory.Gaming,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hkcu(@"System\GameConfigStore", "GameDVR_Enabled"), 0, 1),
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled"), 0, 1),
            ],
            Warning: "Capturas e gravações feitas pelo Xbox Game Bar deixam de funcionar. Se você grava seus jogos com ele, não aplique."),
    ];

    private static RegistryLocation Hkcu(string subKey, string valueName) =>
        new(RegistryRoot.CurrentUser, subKey, valueName);

    private static RegistryLocation Hklm(string subKey, string valueName) =>
        new(RegistryRoot.LocalMachine, subKey, valueName);

    private static RegistryLocation ContentDelivery(string valueName) =>
        Hkcu(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", valueName);

    private static RegistryChange DWord(RegistryLocation location, int optimized, int? windowsDefault) =>
        new(location, RegistryData.DWord(optimized), windowsDefault is null ? null : RegistryData.DWord(windowsDefault.Value));

    private static RegistryChange Text(RegistryLocation location, string optimized, string? windowsDefault) =>
        new(location, RegistryData.Text(optimized), windowsDefault is null ? null : RegistryData.Text(windowsDefault));
}
