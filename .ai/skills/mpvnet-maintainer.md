# mpv.net Maintainer

Guia curto para manter o fork sem quebrar compatibilidade com mpv/libmpv.

## Leitura base

1. `AGENTS.md`
2. `README.md`
3. `docs/manual.md`
4. `docs/guia-operacional.md`
5. a documentação específica da área alterada em `docs/developer/`
6. pendências registradas em `docs/guia-operacional.md` ou na documentação técnica da área quando houver priorização

Para componentes auxiliares, leia também `docs/developer/configuration.md` e
`docs/developer/build-release.md`. Para cache de rede, leia
`docs/developer/mpv-integration.md`. Confirme o estado no código antes de usar
qualquer descrição de prompt como baseline.

## Regras

- preserve compatibilidade com arquivos e comandos existentes;
- faça mudanças pequenas e verificáveis;
- atualize documentação técnica apenas quando houver mudança consolidada;
- prefira documentos existentes e evite criar arquivos redundantes;
- trate integração com mpv/libmpv como área de alto risco;
- valide UI, configuração, build e release conforme a área tocada.
- diferencie dependências preparadas no build (`libmpv`, `MediaInfo.dll` e o
  helper empacotado `mpvnet.com`) dos componentes auxiliares baixados em runtime;
- considere a resolução de `AppPaths`: `MPVNET_LOCAL_APPDATA` tem precedência,
  e o MSIX usa a área `LocalCache\Local` privada do pacote;
- trate o fluxo de release GitHub e a validação/submissão WAP/Store como
  evidências separadas.
- preserve o carregamento dual de libmpv: normal obrigatória e v3 selecionada
  somente em CPU compatível, com fallback. Confira `LibMpvRuntime.cs` e a
  documentação técnica antes de alterar seleção ou empacotamento;
- para texto de interface, leia `docs/developer/localization.md`: gettext usa
  `lang/source.pot` e os `.po` distribuídos. Preserve placeholders, aliases,
  fallback, RTL/LTR e a formatação regional do usuário.

## Topologia consolidada

- `src/MpvNet.Windows/WinForms/MainForm.cs` guarda o estado principal da janela; responsabilidades de UI ficam em partials `MainForm.*.cs` por tema: comandos, menu, cursor/OSC, drag/drop, fullscreen e eventos do player.
- `src/MpvNet/Integration/Mpv/Player.cs` guarda o estado principal do player; responsabilidades críticas ficam em partials `Integration/Mpv/Player.*.cs`: inicialização, propriedades observadas, eventos, ciclo de vida, carregamento de mídia e capacidades.
- Para mudanças em parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo, verifique se os testes da área em `src/MpvNet.Tests/` (incluindo `Program.cs` quando aplicável) cobrem o caso, amplie a cobertura quando houver comportamento novo e rode `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.
- Para mudanças em UI/libmpv, rode build/testes e faça checklist manual: arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento.

## Cache resiliente unificado

- A política operacional consulta somente `AutomaticNetworkCache`; o perfil legado é alias de migração. Conferir `NetworkCacheConfiguration.cs` antes de mudar leitura/persistência; o editor faz backup e a inicialização não regrava o arquivo.
- `on_load`/`on_preloaded` e `file-loaded` aplicam/refinam cache por item. Não voltar a injetar cache em `BuildLoadfileArgs`: isso conflita com precedência e classificação posterior.
- `Media/NetworkCacheSession.cs` contém identificação e decisão pura com histerese. Metadados ausentes, janela DVR finita e URLs opacas exigem fallback conservador.
- Respeitar opções explícitas, inclusive o par `cache-secs`/`demuxer-readahead-secs`, scripts/perfis e cache em disco. Não aumentar limites sem prova nem habilitar disco automaticamente.
- Manter poucos grupos de tetos de memória/prefetch, separados da espera inicial/de retomada. HTTP e HTTPS seguem o mesmo grupo. Alterar tetos exige validar valores nativos e documentar consumo potencial; testes curtos anteriores não comprovam desempenho dos novos limites.
- Executar os testes `NetworkCachePolicyTests`, `NetworkCacheSessionTests`, `NetworkCacheNativeTests` e regressões de playlist/recovery. Confirmar a revisão do libmpv efetivamente usada.
- Consultar [integração mpv](../../docs/developer/mpv-integration.md#cache-automático-por-item) e [configuração](../../docs/developer/configuration.md#mpvnetconf) para limites, precedência, migração e evidências. Registros de desempenho anteriores são históricos.

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


