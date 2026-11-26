using System.Text.RegularExpressions;
using PimpMyWindows.Core.Tweaks;

namespace PimpMyWindows.Core.Components;

/// <summary>
/// Lista fechada de serviços e tarefas que o programa mexe. Não há como o usuário (nem um arquivo) acrescentar itens:
/// só entra aqui o que foi avaliado e tem texto explicando o efeito.
/// </summary>
public static partial class ComponentCatalog
{
    public static IReadOnlyList<ManagedComponent> All { get; } =
    [
        // ---------- Serviços ----------
        new("svc.diagtrack", ComponentKind.Service, "DiagTrack",
            "Serviço de telemetria (Experiências do Usuário Conectado)",
            "Para de rodar o serviço que coleta e envia dados de diagnóstico à Microsoft.",
            "O DiagTrack é o serviço que faz o envio da telemetria. Desativá-lo complementa o ajuste de telemetria da página Ajustes. " +
            "O Windows Update e a segurança do sistema não dependem dele.",
            RiskLevel.Moderate, UserMode.Basic, ServiceStartType.Auto,
            Restart: RestartRequirement.Restart),

        new("svc.dmwappushservice", ComponentKind.Service, "dmwappushservice",
            "Serviço de mensagens WAP Push",
            "Desativa um serviço de mensagens de operadora que quase ninguém usa e que também serve à telemetria.",
            "Recebe mensagens WAP Push (usadas em alguns planos de dados móveis) e participa do envio de diagnóstico. " +
            "Em computadores comuns não tem utilidade no dia a dia.",
            RiskLevel.Safe, UserMode.Basic, ServiceStartType.Manual,
            Restart: RestartRequirement.Restart),

        new("svc.mapsbroker", ComponentKind.Service, "MapsBroker",
            "Gerenciador de mapas baixados",
            "Para de baixar e atualizar mapas offline em segundo plano.",
            "Serve ao aplicativo Mapas do Windows. Se você não usa mapas offline, ele só ocupa memória e rede.",
            RiskLevel.Safe, UserMode.Basic, ServiceStartType.Auto,
            Restart: RestartRequirement.Restart),

        new("svc.retaildemo", ComponentKind.Service, "RetailDemo",
            "Serviço de demonstração para lojas",
            "Desativa o modo de demonstração usado em computadores de vitrine.",
            "Só existe para o computador funcionar como demonstração em loja. Em uso doméstico nunca é necessário.",
            RiskLevel.Safe, UserMode.Basic, ServiceStartType.Manual,
            Restart: RestartRequirement.Restart),

        new("svc.geolocation", ComponentKind.Service, "lfsvc",
            "Serviço de localização",
            "Impede o Windows e os aplicativos de descobrirem onde o computador está.",
            "Desativar o serviço de geolocalização faz o clima, mapas e outros aplicativos perderem a localização automática. " +
            "Bom para privacidade; você pode informar a cidade à mão.",
            RiskLevel.Moderate, UserMode.Basic, ServiceStartType.Manual,
            Warning: "Aplicativos que dependem de localização (clima, mapas, “encontrar meu dispositivo”) deixam de saber onde você está.",
            Restart: RestartRequirement.Restart),

        new("svc.sysmain", ComponentKind.Service, "SysMain",
            "SysMain (Superfetch)",
            "Para de pré-carregar programas na memória.",
            "O SysMain aprende quais programas você usa e os carrega antes. Em computador com SSD o ganho é pequeno e ele pode " +
            "gerar uso de disco e memória; em HDD costuma ajudar, então deixe ligado nesse caso.",
            RiskLevel.Moderate, UserMode.Technical, ServiceStartType.Auto,
            Warning: "Em computador com HDD (disco mecânico) pode deixar a abertura de programas mais lenta.",
            Restart: RestartRequirement.Restart),

        new("svc.spooler", ComponentKind.Service, "Spooler",
            "Fila de impressão",
            "Desativa o serviço de impressão. Só para quem nunca imprime.",
            "O Spooler gerencia impressoras, inclusive “Imprimir em PDF”. Se você não usa impressora nem PDF virtual, " +
            "desativar reduz a superfície de ataque e a memória usada.",
            RiskLevel.Moderate, UserMode.Technical, ServiceStartType.Auto,
            Warning: "Sem ele, nenhuma impressão funciona, incluindo “Microsoft Print to PDF”. Reverta quando precisar imprimir.",
            Restart: RestartRequirement.Restart),

        // ---------- Tarefas agendadas ----------
        new("task.compat-appraiser", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            "Tarefa: Avaliador de compatibilidade",
            "Impede a tarefa que escaneia seu computador periodicamente para enviar dados de compatibilidade.",
            "Roda de tempos em tempos e pode usar bastante disco e processador por alguns minutos, coletando dados de " +
            "compatibilidade de programas. Atualizações do Windows continuam funcionando.",
            RiskLevel.Safe, UserMode.Basic),

        new("task.programdata-updater", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            "Tarefa: Atualizador de dados de programas",
            "Para a coleta periódica de dados sobre os programas instalados.",
            "Parte da telemetria de compatibilidade. Sem ela, o Windows não envia a lista de programas que você usa.",
            RiskLevel.Safe, UserMode.Basic),

        new("task.ceip-consolidator", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            "Tarefa: Programa de Melhoria da Experiência (consolidador)",
            "Para a tarefa que reúne dados de uso do Windows para envio.",
            "Faz parte do Programa de Melhoria da Experiência do Usuário, que coleta como você usa o Windows.",
            RiskLevel.Safe, UserMode.Basic),

        new("task.ceip-usb", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            "Tarefa: Programa de Melhoria (dispositivos USB)",
            "Para a coleta de dados sobre os dispositivos USB que você conecta.",
            "Coleta estatísticas dos dispositivos USB usados, para o Programa de Melhoria da Experiência.",
            RiskLevel.Safe, UserMode.Basic),

        new("task.siuf-dmclient", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Feedback\Siuf\DmClient",
            "Tarefa: Coleta de comentários do Windows",
            "Para a tarefa que pergunta e coleta sua opinião sobre o Windows.",
            "Ligada às perguntas de “Como está o Windows para você?”. Sem ela, essas perguntas deixam de aparecer.",
            RiskLevel.Safe, UserMode.Basic),

        new("task.maps-update", ComponentKind.ScheduledTask,
            @"\Microsoft\Windows\Maps\MapsUpdateTask",
            "Tarefa: Atualização de mapas",
            "Para a tarefa que atualiza mapas offline em segundo plano.",
            "Combina com o serviço de mapas baixados: sem mapas offline, não há o que atualizar.",
            RiskLevel.Safe, UserMode.Basic),
    ];

    /// <summary>Nomes de serviço só com letras, números e <c>_ . -</c>, para nunca virar outro comando.</summary>
    public static bool IsSafeServiceName(string name) => ServiceNameRegex().IsMatch(name);

    public static bool IsSafeTaskPath(string path) =>
        TaskPathRegex().IsMatch(path) && !path.Contains("..", StringComparison.Ordinal);

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,64}$")]
    private static partial Regex ServiceNameRegex();

    [GeneratedRegex(@"^\\[A-Za-z0-9 _.\-]+(\\[A-Za-z0-9 _.\-]+)+$")]
    private static partial Regex TaskPathRegex();
}
