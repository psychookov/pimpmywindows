# Pimp my Windows

Aplicativo **de código aberto**, feito no Brasil, para otimizar o Windows 10 e o Windows 11 com **segurança** e **clareza**, usando apenas recursos nativos do sistema e uma interface em português pensada para quem **não é técnico**. Quem é, tem um Modo Técnico.

> **Status: v0.1, em desenvolvimento.** O código compila e os testes automáticos passam no GitHub Actions (Windows). A interface já foi aberta e usada em uma máquina virtual pelo autor, sem problemas, mas há uma **versão de testes (v0.1.0)** nas Releases e **falta a validação ajuste a ajuste** em um Windows real. Veja [o que ainda falta](#o-que-ainda-não-existe).

## A ideia

Existem ótimos scripts de otimização para quem já entende de Windows, como o [Sophia Script](https://github.com/farag2/Sophia-Script-for-Windows). O Pimp my Windows leva essa ideia para um aplicativo com interface gráfica, em português, com foco total em **não quebrar nada**:

- **Você vê o que está ativo** no seu Windows e decide o que ligar ou desligar.
- **Cada ajuste é explicado** em linguagem simples: o que faz, o que muda, o que se perde, se precisa reiniciar e qual o nível de risco.
- **Tudo é reversível.** Antes de qualquer mudança, o programa guarda o valor que você tinha e devolve exatamente esse valor ao reverter.
- **Só recursos nativos**: registro do Windows e comandos oficiais. Nada é baixado e nenhum dado seu sai do computador (a única exceção é o teste de rede, que só roda quando você clica e avisa o que faz).

## O que o programa já faz

### Ajustes

O catálogo tem hoje **24 ajustes**: 20 no Modo Básico e 4 no Modo Técnico. Todos estão em [`TweakCatalog.cs`](src/PimpMyWindows.Core/Tweaks/TweakCatalog.cs), com texto em português e o valor padrão original do Windows.

| Ajuste | Categoria | Modo | Observações |
| --- | --- | --- | --- |
| Desativar o ID de publicidade | Privacidade | Básico | |
| Desativar sugestões e dicas do Windows | Privacidade | Básico | |
| Desativar a pesquisa na web (Bing) no Menu Iniciar | Privacidade | Básico | pede sair e entrar |
| Reduzir a coleta de dados de diagnóstico (telemetria) | Privacidade | Básico | risco moderado · administrador · reiniciar |
| Não registrar arquivos e pastas recentes | Privacidade | Básico | pede sair e entrar |
| Parar de pedir sua opinião ao Windows | Privacidade | Básico | |
| Impedir o Recall (análise de tela por IA) | Privacidade | Básico | risco moderado · administrador · **Windows 11 24H2+** |
| Desativar o envio de relatórios de erro à Microsoft | Privacidade | **Técnico** | risco moderado · administrador |
| Desativar o histórico de atividades | Privacidade | **Técnico** | risco moderado · administrador · pede sair e entrar |
| Desativar os efeitos de transparência | Desempenho | Básico | |
| Abrir os programas de inicialização sem atraso | Desempenho | Básico | pede sair e entrar |
| Menus mais rápidos | Desempenho | Básico | pede sair e entrar |
| Desativar a inicialização rápida | Desempenho | **Técnico** | risco moderado · administrador · reiniciar |
| Mostrar as extensões dos arquivos | Interface | Básico | |
| Abrir o Explorador em "Este Computador" | Interface | Básico | |
| Desativar atalhos de acessibilidade acidentais | Interface | Básico | pede sair e entrar |
| Alinhar a barra de tarefas à esquerda | Interface | Básico | **só Windows 11** |
| Mostrar arquivos e pastas ocultos | Interface | **Técnico** | |
| Esconder o botão de Visão de Tarefas | Interface | Básico | |
| Esconder a caixa de pesquisa da barra de tarefas | Interface | Básico | |
| Mostrar os segundos no relógio | Interface | Básico | pede sair e entrar |
| Desativar o painel de Widgets e Notícias | Interface | Básico | risco moderado · administrador · **só Windows 11** |
| Desativar a captura de jogos em segundo plano (Game DVR) | Jogos | Básico | pede sair e entrar |
| Desativar a aceleração do mouse | Jogos | Básico | pede sair e entrar |

> Os valores do registro de cada ajuste vêm da documentação e de fontes públicas, mas **ainda precisam ser conferidos em um Windows real** (veja o roadmap). Por isso a prévia mostra exatamente o que será gravado.

### Recursos

- **Interface nova**: menu lateral, tema **escuro e claro** (segue o Windows ou você escolhe), interruptores, selos de risco e busca por ajuste.
- **Painel**: resumo do seu Windows, o percentual do essencial já aplicado (a regra do cálculo é mostrada na tela) e uma lista do que vale a pena olhar.
- **Prévia antes de aplicar**: você marca vários ajustes, clica em *Revisar e aplicar* e vê, linha por linha, `valor atual → novo valor`. Só então confirma. Há opção de criar um ponto de restauração junto.
- **Central Desfazer**: tudo o que o programa já alterou, com botão para desfazer um, vários ou todos. Se o Windows desfez algo por conta própria (atualização), o programa avisa.
- **Perfis prontos** (Privacidade, Jogos, Notebook, Conforto) e **exportar/importar** a sua seleção em um arquivo, para levar a mesma configuração a outro computador.
- **Programas na inicialização**: registro **e** pasta Inicializar, com o mesmo mecanismo do Gerenciador de Tarefas. Mostra a descrição do programa e destaca entradas que apontam para programas que não existem mais. Nada é desinstalado nem apagado.
- **Meu PC**: Windows, placa-mãe, processador, memória e vídeo, com um botão que copia um resumo para pedir ajuda, sem nome, nome do PC nem números de série.
- **Drivers**: lista os drivers instalados, destaca aparelhos com problema e drivers antigos de vídeo, rede, áudio e Bluetooth. Faz **backup e restauração** de todos os drivers pelo `pnputil` (a restauração só aceita pastas criadas pelo programa) e abre o Windows Update. Não baixa drivers de site nenhum.
- **Saúde dos discos**: estado, desgaste, temperatura e erros de cada HD/SSD pelas APIs de armazenamento do Windows. Só leitura.
- **Testes**: benchmarks de processador, memória e disco, **sempre comparados com o teste anterior** do mesmo computador (para ver se um ajuste ajudou de verdade), teste de rede (latência, variação, perda de pacotes e DNS) e estado e desgaste da bateria. O teste de disco usa um arquivo temporário próprio e o apaga no fim.
- **Verificar atualizações** (na página Sobre): só quando você clica, faz uma consulta de leitura ao GitHub e, se houver versão nova, mostra o link da página. Não baixa nem instala nada.
- **Serviços e tarefas** (novo): desativa, um a um e com “Restaurar”, serviços e tarefas agendadas de telemetria e de recursos que quase ninguém usa (DiagTrack, mapas offline, avaliador de compatibilidade, entre outros). A lista é fechada e avaliada; nada de segurança ou Windows Update. Guarda como cada item estava de verdade para voltar exatamente a isso.
- **Energia**: mostra os planos de energia do Windows, troca de plano com **volta garantida** (o anterior fica guardado, até depois de fechar o programa) e cria o plano **Desempenho máximo** do próprio Windows. Em notebook, avisa o que vale na bateria e na tomada. Usa só o `powercfg`.
- **Limpeza**: mostra quanto espaço dá para liberar em temporários do usuário e do Windows, cache de miniaturas e relatórios de erro, **antes** de apagar. Só mexe em pastas de lixo conhecidas, só em arquivos parados há mais de 1 dia, pula arquivos em uso e nunca segue atalhos de pasta para fora. Apagar arquivo não tem como desfazer, por isso pede confirmação com os tamanhos.
- **Teste de estresse**: coloca todos os núcleos e um bloco de memória para trabalhar por 1, 5 ou 10 minutos e diz se o computador aguentou: queda de desempenho (sinal de aquecimento) e erros de memória. Pede confirmação, tem botão de cancelar, para sozinho se você sair da página e não grava nada no disco.
- **Jogos**: acha os jogos da Steam, Epic Games e GOG lendo só os arquivos dos próprios lançadores (você também pode adicionar um .exe à mão) e oferece, **para cada jogo**, usar a placa de vídeo dedicada e, no Modo Técnico, desativar as otimizações de tela inteira. Passa pela mesma prévia, backup e Central Desfazer dos outros ajustes.
- **Teste de controle**: mostra em tempo real botões, gatilhos e direcionais de controles compatíveis com Xbox (XInput), testa a **vibração** e mede o **desvio (drift)** dos direcionais. Só lê o controle. Controles de PlayStation e Switch só aparecem se algum programa, como o Steam Input, os apresentar ao Windows como Xbox.
- **Modo Básico e Modo Técnico**: o programa abre sempre no Básico. O Técnico mostra ajustes avançados e pede confirmação para ser ativado; nele, os ajustes que só existem em outras versões do Windows aparecem **em cinza**, com o motivo.
- **Ajustes sob medida para o seu Windows**: cada ajuste declara em quais builds existe; o programa esconde o que não vale e o motor se recusa a aplicar fora da faixa.
- **Níveis de risco** (Seguro, Moderado, Avançado) e avisos sobre o que se perde.
- **Administrador só quando precisa**: o programa abre sem privilégios e oferece reiniciar como administrador quando um ajuste exige.
- **Log** de tudo o que foi alterado, em `%LOCALAPPDATA%\PimpMyWindows\logs`.

## Segurança em primeiro lugar

Leia [docs/SEGURANCA.md](docs/SEGURANCA.md). Em resumo:

1. Ao abrir, o programa **só lê**. Nada muda sem você clicar em Aplicar.
2. **Backup antes de alterar.** Se o backup falhar, nada é mexido. Ajustes que exigem administrador guardam o backup em uma **pasta protegida da máquina**, que um programa comum não consegue adulterar.
3. **Desfaz sozinho** se uma alteração falhar no meio.
4. Reverter devolve o **seu** valor anterior (ou o padrão do Windows, se não houver backup).
5. **Não apaga arquivos, não desinstala programas** e não mexe em antivírus, firewall nem no Windows Update.
6. Funciona **sem administrador** para a maioria dos ajustes.

## Baixar e testar

A versão de testes **v0.1.0** está na página de [**Releases**](https://github.com/psychookov/pimpmywindows/releases):

1. Instale o [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download), se ainda não tiver.
2. Baixe o `.zip` da release, extraia e execute `PimpMyWindows.exe`. O arquivo `.sha256` serve para conferir o download.
3. O `.exe` **não é assinado digitalmente**, então o SmartScreen do Windows pode avisar sobre "aplicativo não reconhecido".

**Teste de preferência em uma máquina virtual**, e crie um ponto de restauração antes. Se um ajuste não fizer o que promete, abra uma *issue* dizendo qual ajuste e o que você viu.

## Como compilar

Requisitos: Windows 10/11 e o [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/psychookov/pimpmywindows.git
cd pimpmywindows
dotnet test
dotnet run --project src/PimpMyWindows.Desktop
```

## Estrutura

```
src/PimpMyWindows.Core      Motor e serviços, sem interface: ajustes, prévia (planner), lote, backup, Central Desfazer,
                            inicialização, diagnósticos (PC, drivers, discos), benchmarks e jogos
src/PimpMyWindows.Desktop   Interface WPF (MVVM)
tests/                      Testes do Core (rodam com um registro falso, sem tocar no seu Windows)
docs/                       Segurança, arquitetura e roadmap
```

Além dos 24 ajustes do catálogo, a página Jogos cria ajustes por jogo na hora, com o mesmo motor.

Os testes também checam regras do catálogo: IDs únicos, valor otimizado diferente do padrão, administrador exigido só quando o ajuste mexe em `HKLM`, nenhuma chave de segurança (Defender, firewall, Windows Update) e ajuste "Avançado" só no Modo Técnico.

## O que ainda não existe

- **Assinatura digital** do `.exe` (por isso o SmartScreen pode avisar) e uma versão estável (1.0).
- **Validação completa em Windows real**: cada ajuste, e as leituras de discos e drivers, ainda precisam ser conferidos um a um em máquina virtual e em hardware de verdade.
- **Lembrar o Modo Técnico** entre execuções: de propósito, o programa sempre abre no Básico. (O tema escolhido é lembrado.)
- DNS sobre HTTPS.
- Remoção de aplicativos pré-instalados (decisão de projeto em aberto, por não ser 100% reversível).

O plano completo está no [roadmap](docs/ROADMAP.md).

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Adicionar um novo ajuste é basicamente escrever uma entrada declarativa no catálogo e passar pelo checklist de segurança.

## Aviso

O Pimp my Windows altera configurações do sistema. Apesar de todas as proteções, **use por sua conta e risco** e crie um ponto de restauração antes (o programa tem um botão para isso). O software é fornecido "como está", sem garantias, conforme a licença MIT.

## Licença

[MIT](LICENSE)

## Quem faz

Projeto de código aberto da **4Cloudapps**, a divisão de aplicativos da **it4cloud solutions**, aberto a contribuições de qualquer pessoa.
