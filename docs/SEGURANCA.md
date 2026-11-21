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

- Apagar arquivos ou desinstalar programas.
- Desativar ou alterar Windows Defender, firewall, UAC, SmartScreen ou o Windows Update.
- Desativar serviços do Windows (por enquanto).
- Baixar ou executar código de fora, ou enviar qualquer dado.
- Usar ferramentas de terceiros: só registro e comandos nativos.

Os testes do catálogo bloqueiam automaticamente ajustes em chaves sensíveis (`TweakCatalogTests`).

## Teste de rede (a única exceção)

O programa não envia dados seus. O **teste de rede** é a única função que fala com a internet, e só roda quando você clica, depois de uma confirmação que diz o que será feito: alguns pings ao seu roteador e aos servidores públicos 1.1.1.1 (Cloudflare) e 8.8.8.8 (Google), e a consulta do nome de `microsoft.com`, `google.com` e `wikipedia.org`. Esses servidores veem o endereço IP da sua conexão, como em qualquer acesso à internet. Nada é baixado.

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
