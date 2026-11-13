# Pimp my Windows

Aplicativo **de código aberto**, feito no Brasil, para otimizar o Windows 10 e o Windows 11 com **segurança** e **clareza**, usando apenas recursos nativos do sistema.

> **Status: v0.1 (MVP em desenvolvimento).** Ainda não há versão publicada. O projeto está começando e toda ajuda é bem-vinda.

## A ideia

Existem ótimos scripts de otimização para quem já entende de Windows (como o [Sophia Script](https://github.com/farag2/Sophia-Script-for-Windows)). O Pimp my Windows quer levar essa mesma ideia para **quem não tem experiência técnica**, com interface em português e foco total em não quebrar nada:

- **Você vê o que está ativo** no seu Windows e decide o que ligar ou desligar.
- **Cada ajuste é explicado** em linguagem simples: o que faz, o que muda, se precisa reiniciar e qual o nível de risco.
- **Tudo é reversível.** Os valores originais são guardados antes de qualquer mudança.
- **Só recursos nativos**: registro do Windows e comandos oficiais. Nada de baixar executáveis de terceiros nem de enviar dados para fora.

## O que o MVP já faz

| Recurso | Descrição |
| --- | --- |
| Ajustes de privacidade | ID de publicidade, sugestões do Windows, busca na web no Menu Iniciar, telemetria mínima |
| Ajustes de desempenho | Transparência, atraso de inicialização, velocidade dos menus |
| Interface e conforto | Extensões de arquivo, página inicial do Explorador, atalhos de acessibilidade acidentais |
| Jogos | Captura em segundo plano (Game DVR) |
| Programas na inicialização | Lista e ativa/desativa, igual ao Gerenciador de Tarefas, sem desinstalar nada |
| Ponto de restauração | Cria um ponto de restauração do Windows antes de você mexer em qualquer coisa |

Todos os ajustes estão em [`TweakCatalog.cs`](src/PimpMyWindows.Core/Tweaks/TweakCatalog.cs), com texto em português e valores originais do Windows.

## Segurança em primeiro lugar

Leia [docs/SEGURANCA.md](docs/SEGURANCA.md). Em resumo:

1. Ao abrir, o programa **só lê**. Nada muda sem você clicar em Aplicar.
2. **Backup antes de alterar.** Se o backup falhar, nada é mexido.
3. **Desfaz sozinho** se uma alteração falhar no meio.
4. **Não apaga arquivos, não desinstala programas** e não mexe em antivírus, firewall nem no Windows Update.
5. Funciona **sem administrador** para a maioria dos ajustes; quando precisa, o programa avisa e pede.
6. Tudo fica registrado em `%LOCALAPPDATA%\PimpMyWindows\logs`.

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
src/PimpMyWindows.Desktop   Interface WPF
tests/                      Testes do Core (rodam com um registro falso, sem tocar no seu Windows)
docs/                       Segurança, arquitetura e roadmap
```

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Adicionar um novo ajuste é basicamente escrever uma entrada declarativa no catálogo e passar pelo checklist de segurança.

## Aviso

O Pimp my Windows altera configurações do sistema. Apesar de todas as proteções, **use por sua conta e risco** e crie um ponto de restauração antes (o programa tem um botão para isso). O software é fornecido "como está", sem garantias, conforme a licença MIT.

## Licença

[MIT](LICENSE)
