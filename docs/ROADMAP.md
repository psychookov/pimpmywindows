# Roadmap

Ideias organizadas por etapa. Nada aqui é promessa: entra o que for seguro e verificável.

## v0.1 — MVP (em andamento)

- [x] Motor de ajustes com backup, rollback e log
- [x] Catálogo inicial (privacidade, desempenho, interface, jogos)
- [x] Programas na inicialização
- [x] Ponto de restauração
- [x] Interface WPF em português
- [ ] **Validar no Windows real** (compilar, rodar testes pelo CI e testar cada ajuste em VM)
- [x] Primeira release (.exe) pelo GitHub Releases (v0.1.0, pré-lançamento). Para publicar outra: mude `RELEASE_VERSION` no main

## v0.2 — Ver o que está ativo

- [x] Ajustes filtrados pela build do Windows (`MinBuild`/`MaxBuild`)
- [x] Modo Básico e Modo Técnico
- [x] Preferências em arquivo (tema e ponto de restauração); o Modo Técnico nunca é lembrado, de propósito
- [x] No Modo Técnico, mostrar os ajustes de outras builds em cinza, com o motivo
- [x] Serviços e tarefas agendadas (lista fechada, um a um, reversível)
- [ ] Mais ajustes no Modo Técnico (DNS sobre HTTPS)

- [x] Painel de visão geral: o que já está otimizado, o que não está
- [x] Perfis prontos (Privacidade, Jogos, Notebook, Conforto) com pré-seleção segura
- [x] Exportar e importar a seleção (JSON)
- [x] Busca por ajuste, tema claro/escuro, prévia antes de aplicar e Central Desfazer
- [x] Página Energia (planos, troca reversível e Desempenho máximo)
- [x] Perfil Notebook (menos segundo plano e efeitos pesados para a bateria durar mais)
- [ ] Filtro por categoria e risco

## v0.3 — Mais recursos nativos

- [x] Meu PC, Drivers (inventário, backup e restauração) e saúde dos discos
- [x] Benchmarks (CPU, memória, disco) com comparação com o teste anterior, teste de rede e bateria
- [x] Jogos: biblioteca (Steam, Epic, GOG e manual) e ajustes por jogo (placa de vídeo dedicada, tela inteira)
- [x] Teste de controle de videogame (XInput): botões, direcionais, gatilhos, vibração e drift
- [x] Teste de estresse (CPU e memória, com limite de tempo e cancelamento)
- [x] Verificação de versão nova (só ao clicar, sem baixar nada)
- [x] Limpeza segura de temporários com prévia (só pastas conhecidas, arquivos parados há mais de 1 dia)

- Recursos opcionais do Windows (lista com explicação, via DISM nativo)

## v0.4 — Comunidade BR

- Internacionalização preparada (pt-BR primeiro, outros idiomas depois)
- Documentação de cada ajuste com fonte
- Guia de contribuição com vídeo
- Winget e Scoop

## v1.0

- Instalador assinado
- Atualizador que apenas avisa de nova versão (nunca instala sozinho)
- Modo avançado, escondido e claramente identificado

## Fora de escopo (por princípio)

- Desativar Defender, firewall, UAC ou Windows Update
- Executáveis ou scripts baixados da internet
- "Debloat" agressivo que remove componentes do sistema sem volta
- Qualquer coleta de dados
