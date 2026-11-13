# Como contribuir

Obrigado por querer ajudar! O projeto é brasileiro, em português, e feito para quem **não é técnico**. Isso guia todas as decisões.

## Princípios

1. **Segurança antes de desempenho.** Um ajuste que ganha 1% de velocidade mas pode quebrar algo não entra.
2. **Reversível sempre.** Todo ajuste precisa ter volta clara.
3. **Explicado em português simples.** Sem jargão. Se precisa de jargão, explique.
4. **Só recursos nativos do Windows.** Sem baixar nada, sem binários de terceiros, sem enviar dados.
5. **Sem mexer em segurança.** Defender, firewall, Windows Update, UAC e serviços ficam fora do escopo.

## Adicionando um ajuste

1. Abra uma *issue* descrevendo o ajuste e **a fonte** (documentação da Microsoft, ou um comportamento verificável).
2. Adicione uma entrada em `src/PimpMyWindows.Core/Tweaks/TweakCatalog.cs` com:
   - `Id` único, em minúsculas (`categoria.nome-do-ajuste`);
   - `Name`, `Summary` e `Details` em português claro;
   - `Risk` honesto (`Safe` só para o que é cosmético/conforto e trivial de reverter);
   - `RequiresAdmin` verdadeiro **se e somente se** mexer em `HKLM`;
   - `Restart` correto;
   - `Warning` quando houver perda de funcionalidade;
   - para cada valor: o valor otimizado e o **padrão real do Windows** (`null` quando o valor normalmente não existe).
3. Rode `dotnet test`. Os testes do catálogo checam várias regras automaticamente.
4. Teste manualmente em uma máquina virtual: aplicar, conferir, reverter, conferir de novo.
5. Marque o checklist de [docs/SEGURANCA.md](docs/SEGURANCA.md) na descrição do PR.

## Código

- C# com `Nullable` habilitado. Siga o `.editorconfig`.
- Mensagens para o usuário em português, e ditas em tom calmo e direto.
- O `Core` não conhece a interface. Lógica nova vai no `Core` e ganha testes com o `FakeRegistry`.

## Commits e PRs

Mensagens curtas, no imperativo, em português ou inglês. Um PR por assunto.
