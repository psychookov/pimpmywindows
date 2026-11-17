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
            Recommended: true,
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
            Recommended: true,
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
            Recommended: true,
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

        new TweakDefinition(
            Id: "privacy.activity-history",
            Name: "Desativar o histórico de atividades",
            Summary: "Impede o Windows de registrar e enviar à Microsoft o que você abre e faz no computador.",
            Details: "O histórico de atividades guarda os aplicativos, arquivos e sites que você usou para montar uma “linha do tempo” " +
                     "e sincronizá-la com a conta Microsoft. Estas políticas desligam o registro local e o envio. " +
                     "Não apaga o que já foi gravado antes.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed"), 0, null),
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities"), 0, null),
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities"), 0, null),
            ],
            Warning: "Por usar políticas do sistema, o Windows pode mostrar “algumas configurações são gerenciadas pela sua organização”. " +
                     "Isso some ao reverter.",
            Mode: UserMode.Technical),

        new TweakDefinition(
            Id: "privacy.recent-docs",
            Name: "Não registrar arquivos e pastas recentes",
            Summary: "O Windows deixa de lembrar os últimos documentos que você abriu (Menu Iniciar, Acesso rápido, listas de salto).",
            Details: "Útil em computadores compartilhados ou para quem não quer deixar rastros locais do que abriu. Não apaga o histórico que já existe; ele só deixa de crescer.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs"), 0, 1),
            ]),

        new TweakDefinition(
            Id: "privacy.feedback",
            Name: "Parar de pedir sua opinião ao Windows",
            Summary: "Desativa os pedidos de feedback (“Como foi sua experiência?”) que o Windows mostra de tempos em tempos.",
            Details: "Define que o Windows nunca deve solicitar avaliação. Não afeta atualizações nem recursos do sistema.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"), 0, null),
            ]),

        new TweakDefinition(
            Id: "privacy.recall",
            Name: "Impedir o Recall (análise de tela por IA)",
            Summary: "Bloqueia o Windows de tirar e analisar capturas periódicas da sua tela para o recurso Recall.",
            Details: "Usa a política oficial DisableAIDataAnalysis. Vale só para versões do Windows 11 que têm o recurso (24H2 em diante, em computadores Copilot+).",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.Restart,
            Changes:
            [
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis"), 1, null),
            ],
            Warning: "Por usar uma política do sistema, o Windows pode mostrar “algumas configurações são gerenciadas pela sua organização”. Isso some ao reverter.",
            MinBuild: WindowsBuilds.Windows11_24H2),

        new TweakDefinition(
            Id: "privacy.error-reporting",
            Name: "Desativar o envio de relatórios de erro à Microsoft",
            Summary: "Quando um programa trava, o Windows deixa de enviar o relatório do problema.",
            Details: "Desliga o Relatório de Erros do Windows (WER) para o computador inteiro. Relatórios podem conter trechos de memória do programa que travou.",
            Category: TweakCategory.Privacy,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hklm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled"), 1, null),
            ],
            Mode: UserMode.Technical),

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

        new TweakDefinition(
            Id: "performance.fast-startup",
            Name: "Desativar a inicialização rápida",
            Summary: "Faz “Desligar” realmente desligar tudo, em vez de hibernar parte do sistema.",
            Details: "A inicialização rápida salva o núcleo do Windows no disco ao desligar. Isso acelera ligar o computador, mas pode causar problemas com atualizações, drivers e dual boot. Desativar deixa o desligamento completo.",
            Category: TweakCategory.Performance,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.Restart,
            Changes:
            [
                DWord(Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled"), 0, 1),
            ],
            Warning: "O computador pode demorar alguns segundos a mais para ligar, principalmente em discos HDD.",
            Mode: UserMode.Technical),

        // ---------- Interface e conforto ----------
        new TweakDefinition(
            Id: "interface.file-extensions",
            Recommended: true,
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

        new TweakDefinition(
            Id: "interface.taskbar-left",
            Name: "Alinhar a barra de tarefas à esquerda",
            Summary: "Deixa o botão Iniciar e os ícones no canto esquerdo, como no Windows 10.",
            Details: "No Windows 11 os ícones da barra de tarefas ficam centralizados e mudam de lugar conforme você abre ou fecha programas. " +
                     "Alinhados à esquerda, o botão Iniciar fica sempre no mesmo canto. Só muda a posição dos ícones.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl"), 0, 1),
            ],
            MinBuild: WindowsBuilds.Windows11_21H2),

        new TweakDefinition(
            Id: "interface.show-hidden-files",
            Name: "Mostrar arquivos e pastas ocultos",
            Summary: "Exibe no Explorador os itens marcados como ocultos.",
            Details: "Útil para quem precisa chegar a pastas como AppData. Os arquivos continuam exatamente como estão; só passam a ser visíveis. Cuidado ao apagar o que você não conhece.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden"), 1, 2),
            ],
            Mode: UserMode.Technical),

        new TweakDefinition(
            Id: "interface.taskview-button",
            Name: "Esconder o botão de Visão de Tarefas",
            Summary: "Remove da barra de tarefas o botão que mostra todas as janelas abertas.",
            Details: "O atalho Win+Tab continua funcionando. Só o botão deixa de ocupar espaço na barra.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton"), 0, 1),
            ]),

        new TweakDefinition(
            Id: "interface.taskbar-search",
            Name: "Esconder a caixa de pesquisa da barra de tarefas",
            Summary: "Libera espaço na barra de tarefas; a pesquisa continua disponível ao apertar a tecla Windows.",
            Details: "Só esconde o campo de busca da barra. Digitar com o Menu Iniciar aberto continua pesquisando normalmente.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.None,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode"), 0, null),
            ]),

        new TweakDefinition(
            Id: "interface.clock-seconds",
            Name: "Mostrar os segundos no relógio",
            Summary: "O relógio da barra de tarefas passa a exibir os segundos.",
            Details: "Só muda a aparência do relógio. Em computadores muito antigos o relógio é atualizado com mais frequência, o que é imperceptível.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSecondsInSystemClock"), 1, null),
            ]),

        new TweakDefinition(
            Id: "interface.widgets",
            Name: "Desativar o painel de Widgets e Notícias",
            Summary: "Remove o painel de notícias e clima do Windows 11 e o botão da barra de tarefas.",
            Details: "Usa a política oficial AllowNewsAndInterests. O painel deixa de rodar em segundo plano, o que poupa um pouco de memória.",
            Category: TweakCategory.Interface,
            Risk: RiskLevel.Moderate,
            RequiresAdmin: true,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                DWord(Hklm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"), 0, null),
            ],
            Warning: "Por usar uma política do sistema, o Windows pode mostrar “algumas configurações são gerenciadas pela sua organização”. Isso some ao reverter.",
            MinBuild: WindowsBuilds.Windows11_21H2),

        // ---------- Jogos ----------
        new TweakDefinition(
            Id: "gaming.game-dvr",
            Recommended: true,
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

        new TweakDefinition(
            Id: "gaming.mouse-accel",
            Name: "Desativar a aceleração do mouse",
            Summary: "O ponteiro passa a se mover proporcionalmente ao movimento da mão, o que dá mais precisão em jogos.",
            Details: "A “precisão do ponteiro” do Windows altera a velocidade conforme você move o mouse rápido ou devagar. Desativá-la deixa o movimento previsível, como em jogos de tiro.",
            Category: TweakCategory.Gaming,
            Risk: RiskLevel.Safe,
            RequiresAdmin: false,
            Restart: RestartRequirement.SignOut,
            Changes:
            [
                Text(Hkcu(@"Control Panel\Mouse", "MouseSpeed"), "0", "1"),
                Text(Hkcu(@"Control Panel\Mouse", "MouseThreshold1"), "0", "6"),
                Text(Hkcu(@"Control Panel\Mouse", "MouseThreshold2"), "0", "10"),
            ],
            Warning: "Pode parecer estranho nos primeiros dias, até acostumar. Reverter devolve o comportamento padrão."),
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
