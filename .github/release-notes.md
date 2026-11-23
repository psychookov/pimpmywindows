Primeira versão de testes do **Pimp my Windows**: otimize o Windows 10/11 com segurança, em português, só com recursos nativos e com tudo reversível.

## Como usar

1. Instale o [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download) (se ainda não tiver).
2. Baixe o `.zip` abaixo, extraia e abra o `PimpMyWindows.exe`.
3. **Teste de preferência em uma máquina virtual** e crie um ponto de restauração antes de aplicar ajustes (o programa tem um botão para isso).

## O que tem

- 24 ajustes explicados, com prévia, backup antes de alterar e Central Desfazer
- Modo Básico e Modo Técnico, filtrados pela versão do seu Windows
- Programas na inicialização, Meu PC, Drivers (com backup e restauração), saúde dos discos
- Benchmarks com comparação, teste de rede, bateria e teste de estresse
- Jogos (Steam, Epic, GOG) com ajustes por jogo e teste de controle

## Avisos importantes

- É uma **versão de testes (pré-lançamento)**: os ajustes ainda não foram validados um a um em Windows real. Use por sua conta e risco.
- O `.exe` **não é assinado digitalmente**, então o Windows SmartScreen pode avisar sobre "aplicativo não reconhecido". Confira o hash SHA-256 do arquivo `.sha256` anexo antes de abrir.
- Nenhum dado seu sai do computador. A única função que acessa a internet é o teste de rede, e só quando você clica.

Achou um problema? Abra uma *issue* dizendo qual ajuste e o que você viu.
