# mpv.net Maintainer

Guia curto para manter o fork sem quebrar compatibilidade com mpv/libmpv.

## Leitura base

1. `AGENTS.md`
2. `README.md`
3. `docs/manual.md`
4. `docs/guia-operacional.md`
5. a documentação específica da área alterada em `docs/developer/`
6. pendências registradas em `docs/guia-operacional.md` ou na documentação técnica da área quando houver priorização

## Regras

- preserve compatibilidade com arquivos e comandos existentes;
- faça mudanças pequenas e verificáveis;
- atualize documentação técnica apenas quando houver mudança consolidada;
- prefira documentos existentes e evite criar arquivos redundantes;
- trate integração com mpv/libmpv como área de alto risco;
- valide UI, configuração, build e release conforme a área tocada.

## Topologia consolidada

- `src/MpvNet.Windows/WinForms/MainForm.cs` guarda o estado principal da janela; responsabilidades de UI ficam em partials `MainForm.*.cs` por tema: comandos, menu, cursor/OSC, drag/drop, fullscreen e eventos do player.
- `src/MpvNet/Player.cs` guarda o estado principal do player; responsabilidades críticas ficam em partials `Player.*.cs`: inicialização, propriedades observadas, eventos, ciclo de vida, carregamento de mídia e capacidades.
- Para mudanças em parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo, verifique se `src/MpvNet.Tests/Program.cs` cobre o caso, amplie o harness quando houver comportamento novo e rode `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.
- Para mudanças em UI/libmpv, rode build/testes e faça checklist manual: arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento.

## Resumo para a mudança

Antes de editar, registre:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```
