# Segurança

O objetivo do Pimp my Windows é deixar o Windows mais enxuto **sem criar risco**. Estas são as regras do projeto.

## Garantias do programa

| Garantia | Como é cumprida |
| --- | --- |
| Nada muda sem ação do usuário | Ao abrir, só há leitura (`TweakEngine.GetState`). Alterar exige clicar em Aplicar/Reverter. |
| Backup antes de alterar | `TweakEngine.Apply` grava os valores atuais **antes** da primeira gravação. Se o backup falhar, nada é alterado. Ajustes de usuário: `%LOCALAPPDATA%\PimpMyWindows\backups`. Ajustes que exigem administrador: `%ProgramData%\PimpMyWindows-Backups`. |
| Backup do administrador protegido | A pasta da máquina só aceita escrita de Administradores/SYSTEM (usuários só leem). Antes de ler ou escrever, o programa confere o **dono** da pasta; se não for o administrador, a pasta é ignorada. Assim um programa comum não consegue plantar backups falsos para fazer um processo elevado gravar onde não deveria. |
| Original preservado | Se o Windows desfizer parte de um ajuste e você aplicar de novo, o backup mantém o valor **original seu**, não o valor já otimizado. |
| Você vê antes de confirmar | Aplicar passa por uma prévia com `valor atual → novo valor` de cada chave. Nada é gravado antes da confirmação. |
| Central Desfazer | Lista tudo o que o programa alterou, detecta o que o Windows já desfez sozinho e permite reverter um, vários ou todos. |
| Sem alterações pela metade | Se uma gravação falhar, o que já foi escrito é desfeito na hora. |
| Reverter devolve o seu valor | Reverter restaura o que **você** tinha. Sem backup, usa o padrão documentado do Windows. |
| Transparência | Cada alteração (chave, valor novo, valor antigo) vai para `%LOCALAPPDATA%\PimpMyWindows\logs`. |
| Privilégio mínimo | O programa abre sem administrador (`asInvoker`). Só ajustes em `HKLM` exigem; o programa avisa e oferece reiniciar com permissão. |
| Confirmação para o que importa | Ajustes de risco moderado, ou que tiram alguma funcionalidade, pedem confirmação mostrando o que muda. |
| Ponto de restauração | Botão para criar um ponto de restauração do Windows antes de começar. |

## O que o programa nunca faz

- Desinstalar programas ou apagar arquivos fora da página Limpeza (e lá só pastas de lixo conhecidas, com confirmação).
- Desativar ou alterar Windows Defender, firewall, UAC, SmartScreen ou o Windows Update.
- Mexer em serviços ou tarefas fora de uma lista fechada e avaliada: nunca segurança, Defender, firewall nem Windows Update.
- Baixar ou executar código de fora, ou enviar qualquer dado.
- Usar ferramentas de terceiros: só registro e comandos nativos.

Os testes do catálogo bloqueiam automaticamente ajustes em chaves sensíveis (`TweakCatalogTests`).

## Serviços e tarefas agendadas

A lista é **fechada e escrita no código**: ninguém (nem um arquivo de configuração) consegue acrescentar itens. Cada item tem texto explicando o efeito e um nível de risco, e os testes recusam qualquer item de segurança ou de atualização. O programa usa só o `sc.exe` e o `schtasks.exe` do Windows, com argumentos separados (nunca uma linha de comando montada) e nomes validados por regra estrita. Antes de desativar, guarda **como o item estava de verdade** (por exemplo, início automático atrasado) em `%LOCALAPPDATA%\PimpMyWindows\components.json`, e “Restaurar” volta a isso, mesmo depois de reiniciar o programa. Depois de cada alteração o estado é lido de novo para conferir que ela realmente valeu. Serviços não são parados na hora: a mudança vale por completo após reiniciar.

## Teste de rede (a única exceção)

O programa não envia dados seus. O **teste de rede** é a única função que fala com a internet, e só roda quando você clica, depois de uma confirmação que diz o que será feito: alguns pings ao seu roteador e aos servidores públicos 1.1.1.1 (Cloudflare) e 8.8.8.8 (Google), e a consulta do nome de `microsoft.com`, `google.com` e `wikipedia.org`. Esses servidores veem o endereço IP da sua conexão, como em qualquer acesso à internet. Nada é baixado.

## Jogos

A página **Jogos** só lê os arquivos que Steam, Epic e GOG já mantêm (listas de bibliotecas, manifestos e chaves do registro) e não instala, baixa nem envia nada. Os ajustes por jogo gravam apenas valores do usuário atual (`HKCU`) cujo nome é o caminho do executável daquele jogo, passam pela prévia e têm backup. A lista de jogos que você adicionou ou otimizou fica em `%LOCALAPPDATA%\PimpMyWindows\games.json`, para a Central Desfazer continuar reconhecendo os ajustes mesmo se o jogo for desinstalado; um arquivo adulterado é ignorado se o id não bater com o executável.

## Energia

A página Energia só executa o `powercfg.exe` do Windows, com argumentos fixos e identificadores de plano (GUID) conferidos antes de chegar ao comando: listar, ativar um plano que existe e duplicar o modelo oculto “Desempenho máximo” para um plano novo. Nenhum plano existente é alterado ou apagado. Antes de trocar, o plano ativo é guardado em `%LOCALAPPDATA%\PimpMyWindows\power.json`, e “Voltar ao plano anterior” funciona mesmo depois de reiniciar o programa.

## Limpeza

É a única parte do programa que apaga arquivos, e **apagar não tem como desfazer**, então as regras são mais rígidas: só pastas de lixo conhecidas, com caminhos vindos do próprio Windows (nunca digitados); só arquivos parados há pelo menos 1 dia (temporários); nunca entra em junções ou links simbólicos; cada arquivo é conferido para estar dentro da pasta escolhida antes de apagar; arquivos em uso são pulados; e nada é apagado sem a pessoa marcar e confirmar vendo a quantidade e o tamanho. Navegadores, Downloads, Documentos e a Lixeira não são tocados.

## Níveis de risco

- **Seguro**: muda aparência ou conforto, é trivial de reverter e não tira funcionalidade importante.
- **Moderado**: muda comportamento do sistema. Sempre pede confirmação e explica o impacto.
- **Avançado**: só pode existir no Modo Técnico (há teste garantindo isso).

## Modo Básico e Modo Técnico

- **Modo Básico** (padrão): só ajustes simples, que qualquer pessoa pode aplicar entendendo o que mudam. Nunca inclui risco "Avançado".
- **Modo Técnico**: mostra também ajustes para quem sabe o que está fazendo. Para ativá-lo, o programa pede confirmação e lembra do ponto de restauração. Todas as proteções (backup, reversão, log) continuam valendo.
- O programa sempre abre no Modo Básico.

## Ajustes por versão do Windows

Cada ajuste pode declarar `MinBuild` e `MaxBuild`. O programa:

- **esconde** da tela o que não existe na sua versão do Windows e mostra quantos foram escondidos;
- **recusa aplicar** (no motor, não só na interface) um ajuste fora da faixa de builds dele;
- **permite reverter** sempre, para desfazer algo mesmo depois de uma atualização do Windows.

## Checklist para novos ajustes

- [ ] Existe fonte confiável (documentação oficial ou comportamento verificável).
- [ ] O valor padrão do Windows está correto em `DefaultValue` (`null` se não existe).
- [ ] Reverter foi testado em uma máquina virtual.
- [ ] Funciona em Windows 10 e 11, ou o texto deixa claro onde não funciona.
- [ ] Não toca em segurança, atualizações, serviços nem arquivos do usuário.
- [ ] O texto explica o que muda **e o que se perde**, em português simples.
- [ ] `Risk`, `RequiresAdmin` e `Restart` estão corretos.
- [ ] `dotnet test` passa.

## Limitações conhecidas

- Os valores de registro dos ajustes ainda **não foram validados em um Windows real**; use primeiro em uma máquina virtual.
- Backups corrompidos ou que não batem com o ajuste são ignorados: o motor volta ao padrão documentado do Windows.
- Alguns ajustes de `HKCU` só têm efeito depois de sair e entrar no Windows; a interface avisa.
- Atualizações grandes do Windows podem redefinir configurações. Use o botão **Atualizar** para ver o estado real.
- Em computadores gerenciados por uma empresa, políticas do administrador podem sobrepor os ajustes.
