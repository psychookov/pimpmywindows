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
- [ ] Lembrar o último modo escolhido (arquivo de configurações)
- [ ] No Modo Técnico, mostrar os ajustes de outras builds como indisponíveis, com o motivo
- [ ] Mais ajustes no Modo Técnico (serviços, tarefas agendadas, DNS sobre HTTPS)

- Painel de visão geral: o que já está otimizado, o que não está
- Recomendações por perfil (Básico, Jogos, Notebook, Privacidade) com pré-seleção segura
- Exportar e importar perfis (JSON)
- Busca e filtro por categoria e risco

## v0.3 — Mais recursos nativos

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
