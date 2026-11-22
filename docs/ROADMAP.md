# Roadmap

Ideias organizadas por etapa. Nada aqui é promessa: entra o que for seguro e verificável.

## v0.1 — MVP (em andamento)

- [x] Motor de ajustes com backup, rollback e log
- [x] Catálogo inicial (privacidade, desempenho, interface, jogos)
- [x] Programas na inicialização
- [x] Ponto de restauração
- [x] Interface WPF em português
- [ ] **Validar no Windows real** (compilar, rodar testes pelo CI e testar cada ajuste em VM)
- [ ] Primeira release (.exe) pelo GitHub Releases

## v0.2 — Ver o que está ativo

- [x] Ajustes filtrados pela build do Windows (`MinBuild`/`MaxBuild`)
- [x] Modo Básico e Modo Técnico
- [x] Preferências em arquivo (tema e ponto de restauração); o Modo Técnico nunca é lembrado, de propósito
- [ ] No Modo Técnico, mostrar os ajustes de outras builds como indisponíveis, com o motivo
- [ ] Mais ajustes no Modo Técnico (serviços, tarefas agendadas, DNS sobre HTTPS)

- [x] Painel de visão geral: o que já está otimizado, o que não está
- [x] Perfis prontos (Privacidade, Jogos, Conforto) com pré-seleção segura
- [x] Exportar e importar a seleção (JSON)
- [x] Busca por ajuste, tema claro/escuro, prévia antes de aplicar e Central Desfazer
- [ ] Perfil Notebook (depende de plano de energia)
- [ ] Filtro por categoria e risco

## v0.3 — Mais recursos nativos

- [x] Meu PC, Drivers (inventário, backup e restauração) e saúde dos discos
- [x] Benchmarks (CPU, memória, disco) com comparação com o teste anterior, teste de rede e bateria
- [x] Jogos: biblioteca (Steam, Epic, GOG e manual) e ajustes por jogo (placa de vídeo dedicada, tela inteira)
- [ ] Teste de controle de videogame (XInput)
- [ ] Teste de estresse (CPU e memória, com limite de tempo)

- Serviços do Windows (lista, explicação e desativar só os seguros, sempre reversível)
- Tarefas agendadas de telemetria
- Plano de energia (alto desempenho / equilibrado) via `powercfg`
- Recursos opcionais do Windows (lista com explicação, via DISM nativo)
- Limpeza segura de temporários (somente pastas temporárias conhecidas, com prévia do que será removido)

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
