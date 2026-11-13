# Pimp my Windows

Aplicativo **de código aberto**, feito no Brasil, para otimizar o Windows 10 e o Windows 11 com **segurança** e **clareza**, usando apenas recursos nativos do sistema e uma interface em português pensada para quem **não é técnico**. Quem é, tem um Modo Técnico.

> **Status: v0.1, em desenvolvimento.** O código compila e os testes automáticos passam no GitHub Actions (Windows), mas **ainda não há versão publicada** e os ajustes **ainda não foram validados em um Windows real**. Veja [o que ainda falta](#o-que-ainda-não-existe).

## A ideia

Existem ótimos scripts de otimização para quem já entende de Windows, como o [Sophia Script](https://github.com/farag2/Sophia-Script-for-Windows). O Pimp my Windows leva essa ideia para um aplicativo com interface gráfica, em português, com foco total em **não quebrar nada**:

- **Você vê o que está ativo** no seu Windows e decide o que ligar ou desligar.
- **Cada ajuste é explicado** em linguagem simples: o que faz, o que muda, o que se perde, se precisa reiniciar e qual o nível de risco.
- **Tudo é reversível.** Antes de qualquer mudança, o programa guarda o valor que você tinha e devolve exatamente esse valor ao reverter.
- **Só recursos nativos**: registro do Windows e comandos oficiais. Nada é baixado e nenhum dado sai do seu computador.

## O que o programa já faz

### Ajustes

O catálogo tem hoje **13 ajustes**: 12 no Modo Básico e 1 no Modo Técnico. Todos estão em [`TweakCatalog.cs`](src/PimpMyWindows.Core/Tweaks/TweakCatalog.cs), com texto em português e o valor padrão original do Windows.

| Ajuste | Categoria | Modo | Observações |
| --- | --- | --- | --- |
| Desativar o ID de publicidade | Privacidade | Básico | |
| Desativar sugestões e dicas do Windows | Privacidade | Básico | |
| Desativar a pesquisa na web (Bing) no Menu Iniciar | Privacidade | Básico | pede sair e entrar |
| Reduzir a coleta de dados de diagnóstico (telemetria) | Privacidade | Básico | risco moderado · administrador · reiniciar |
| Desativar o histórico de atividades | Privacidade | **Técnico** | risco moderado · administrador · pede sair e entrar |
| Desativar os efeitos de transparência | Desempenho | Básico | |
| Abrir os programas de inicialização sem atraso | Desempenho | Básico | pede sair e entrar |
| Menus mais rápidos | Desempenho | Básico | pede sair e entrar |
| Mostrar as extensões dos arquivos | Interface | Básico | |
| Abrir o Explorador em "Este Computador" | Interface | Básico | |
| Desativar atalhos de acessibilidade acidentais | Interface | Básico | pede sair e entrar |
| Alinhar a barra de tarefas à esquerda | Interface | Básico | **só Windows 11** |
| Desativar a captura de jogos em segundo plano (Game DVR) | Jogos | Básico | pede sair e entrar |

### Outros recursos

- **Programas na inicialização**: lista o que abre junto com o Windows e permite ativar ou desativar, pelo mesmo mecanismo do Gerenciador de Tarefas. Nada é desinstalado nem apagado.
- **Ponto de restauração**: botão que cria um ponto de restauração do Windows antes de você mexer em qualquer coisa.
- **Modo Básico e Modo Técnico**: o programa abre sempre no Básico, com só o que é simples e seguro. O Técnico mostra ajustes avançados e pede confirmação para ser ativado.
- **Ajustes sob medida para o seu Windows**: cada ajuste declara em quais builds existe. O programa esconde o que não vale para a sua versão e avisa quantos ajustes foram escondidos. O motor também se recusa a aplicar um ajuste fora da faixa de builds dele.
- **Níveis de risco** (Seguro, Moderado, Avançado) e **confirmação** antes de aplicar o que muda o comportamento do sistema ou tira alguma função.
- **Administrador só quando precisa**: o programa abre sem privilégios; quando um ajuste exige, avisa e oferece reiniciar como administrador.
- **Log** de tudo o que foi alterado, em `%LOCALAPPDATA%\PimpMyWindows\logs`.

## Segurança em primeiro lugar

Leia [docs/SEGURANCA.md](docs/SEGURANCA.md). Em resumo:

1. Ao abrir, o programa **só lê**. Nada muda sem você clicar em Aplicar.
2. **Backup antes de alterar.** Se o backup falhar, nada é mexido.
3. **Desfaz sozinho** se uma alteração falhar no meio.
4. Reverter devolve o **seu** valor anterior (ou o padrão do Windows, se não houver backup).
5. **Não apaga arquivos, não desinstala programas** e não mexe em antivírus, firewall nem no Windows Update.
6. Funciona **sem administrador** para a maioria dos ajustes.

## Testar agora

Ainda não há release. Para experimentar:

1. Abra a aba [**Actions**](https://github.com/psychookov/pimpmywindows/actions) do repositório e entre na execução mais recente do workflow `build` com sucesso.
2. Baixe o artefato **PimpMyWindows-win-x64** e extraia.
3. Execute `PimpMyWindows.exe`. Precisa do [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download) instalado.

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
src/PimpMyWindows.Core      Motor: ajustes, backup, reversão, inicialização (sem interface)
src/PimpMyWindows.Desktop   Interface WPF (MVVM)
tests/                      Testes do Core (rodam com um registro falso, sem tocar no seu Windows)
docs/                       Segurança, arquitetura e roadmap
```

Os testes também checam regras do catálogo: IDs únicos, valor otimizado diferente do padrão, administrador exigido só quando o ajuste mexe em `HKLM`, nenhuma chave de segurança (Defender, firewall, Windows Update) e ajuste "Avançado" só no Modo Técnico.

## O que ainda não existe

- **Release publicada** (`.exe` pronto para baixar).
- **Validação em Windows real**: cada ajuste ainda precisa ser aplicado, conferido e revertido em máquina virtual.
- **Lembrar o modo escolhido** entre execuções (hoje abre sempre no Básico).
- Serviços do Windows, tarefas agendadas, plano de energia, DNS sobre HTTPS, limpeza de temporários e perfis prontos (Jogos, Notebook, Privacidade).
- Remoção de aplicativos pré-instalados (decisão de projeto em aberto, por não ser 100% reversível).

O plano completo está no [roadmap](docs/ROADMAP.md).

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Adicionar um novo ajuste é basicamente escrever uma entrada declarativa no catálogo e passar pelo checklist de segurança.

## Aviso

O Pimp my Windows altera configurações do sistema. Apesar de todas as proteções, **use por sua conta e risco** e crie um ponto de restauração antes (o programa tem um botão para isso). O software é fornecido "como está", sem garantias, conforme a licença MIT.

## Licença

[MIT](LICENSE)
