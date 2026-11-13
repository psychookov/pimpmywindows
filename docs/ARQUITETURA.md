# Arquitetura

```
┌────────────────────────┐        ┌───────────────────────────────────────┐
│ PimpMyWindows.Desktop  │───────▶│ PimpMyWindows.Core                    │
│ WPF + MVVM             │        │                                       │
│ MainWindow, ViewModels │        │ Tweaks/    TweakEngine, TweakCatalog  │
└────────────────────────┘        │ Startup/   StartupService             │
                                  │ Safety/    backup, log, restauração   │
                                  │ Platform/  IRegistryAccess (+ Windows)│
                                  └───────────────────────────────────────┘
```

## Decisões

- **Ajustes são dados, não código.** Cada ajuste é um `TweakDefinition` declarativo (textos, risco, valores). O `TweakEngine` é o único que grava, e portanto o único lugar onde backup, rollback e log precisam estar corretos.
- **`IRegistryAccess` isola o registro.** Em produção é `WindowsRegistryAccess` (visão de 64 bits); nos testes é um `FakeRegistry`, então a suíte nunca toca o Windows de quem desenvolve.
- **O Core não depende da interface**, o que permite no futuro uma CLI ou perfis por linha de comando usando o mesmo motor.
- **Estado é derivado do registro**, não guardado em arquivo. O programa mostra sempre a realidade do sistema, mesmo que algo tenha sido mudado fora dele.
- **Programas de inicialização** usam a chave `StartupApproved`, o mesmo mecanismo do Gerenciador de Tarefas: desativar não apaga a entrada original.

## Fluxo de `Apply`

1. Checa administrador, se necessário.
2. Se já está aplicado, não faz nada.
3. Lê os valores atuais e **salva o backup** (falhou? aborta sem alterar).
4. Grava cada valor, registrando no log.
5. Se algo falhar, restaura os valores do backup.

## Fluxo de `Revert`

1. Usa o backup se ele cobre exatamente os valores do ajuste; senão usa o padrão do Windows.
2. Restaura (valor inexistente = remove a chave de valor).
3. Apaga o backup usado.

## Onde ficam os dados do usuário

`%LOCALAPPDATA%\PimpMyWindows\` com `backups\` (JSON legível) e `logs\`.
