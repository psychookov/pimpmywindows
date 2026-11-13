# Segurança

O objetivo do Pimp my Windows é deixar o Windows mais enxuto **sem criar risco**. Estas são as regras do projeto.

## Garantias do programa

| Garantia | Como é cumprida |
| --- | --- |
| Nada muda sem ação do usuário | Ao abrir, só há leitura (`TweakEngine.GetState`). Alterar exige clicar em Aplicar/Reverter. |
| Backup antes de alterar | `TweakEngine.Apply` grava os valores atuais em `%LOCALAPPDATA%\PimpMyWindows\backups\<ajuste>.json` **antes** da primeira gravação. Se o backup falhar, nada é alterado. |
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

- Alguns ajustes de `HKCU` só têm efeito depois de sair e entrar no Windows; a interface avisa.
- Atualizações grandes do Windows podem redefinir configurações. Use o botão **Atualizar** para ver o estado real.
- Em computadores gerenciados por uma empresa, políticas do administrador podem sobrepor os ajustes.
