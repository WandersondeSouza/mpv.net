# Agente: Integração mpv/libmpv

## Missão

Analisar mudanças na integração entre mpv.net e mpv/libmpv com foco em compatibilidade, eventos, propriedades, comandos e ciclo de vida do player.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/mpv-integration.md`;
6. `docs/developer/architecture.md` quando a alteração envolver múltiplos módulos.

## Arquivos críticos

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/Integration/Mpv/Player.*.cs`;
- `src/MpvNet/Integration/Mpv/Player.NetworkCache.cs` quando a alteração envolver mídia de rede;
- `src/MpvNet/Media/NetworkCacheSession.cs` e `MediaInput.cs` para classificação e decisão de cache;
- `src/MpvNet.Tests/NetworkCacheNativeTests.cs`, `NetworkCacheSessionTests.cs` e `NetworkCachePolicyTests.cs`;
- `src/MpvNet/Integration/Mpv/MpvClient.cs`;
- `src/MpvNet/Native/LibMpv.cs`;
- `src/MpvNet/Native/LibMpvRuntime.cs`;
- `src/MpvNet.Tests/LibMpvRuntimeTests.cs` e `LibMpvInteropTests.cs`;
- `src/MpvNet/Command.cs`;
- `src/MpvNet/Configuration/CommandLine.cs`;
- `src/MpvNet/Configuration/InputConf.cs`.

## Regras

- Tratar toda alteração nesta área como alto risco.
- Não renomear comando, propriedade ou opção sem migração.
- Comparar comportamento com mpv sempre que possível.
- Validar scripts Lua/JavaScript quando tocar input, comandos ou eventos.
- Evitar bloquear thread de evento do mpv.
- Se o efeito observado vier de uma camada acima do wrapper, apontar a camada correta antes de mexer no nativo.
- Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes.
- Preservar a ordem de inicialização em `Player.Initialization.cs`.
- Preservar a seleção dual por caminho absoluto: normal obrigatória, v3 apenas
  em CPU compatível e fallback para normal se a v3 não carregar. O parâmetro
  legado de build não seleciona mais um pacote de variante única.
- Para cache de rede, consultar `docs/developer/mpv-integration.md`, manter a política por item e preservar as opções explícitas em `mpv.conf` e na linha de comando.
- Manter um único toggle automático, sem dependência operacional do perfil legado. Aplicar cache nos hooks por item, preservando perfis/scripts/opções locais e o par de limites temporais; não reintroduzir injeção em `loadfile`.
- Classificar usando evidências já obtidas pelo mpv/yt-dlp; extensão e duração de DVR não provam VOD. Sem métricas completas, não adaptar. Preservar histerese, limites, limpeza por item e o recovery existente, sem novo watchdog.
- Ao agrupar/ampliar tetos, verificar bytes à frente/para trás e `cache-secs`/`demuxer-readahead-secs` no libmpv real, incluindo equivalência HTTP/HTTPS. Prefetch maior não deve virar espera inicial maior; manter o fallback de fontes ainda não identificadas. Registrar medições novas separadas das anteriores.
- Conferir opções suportadas/mutáveis na revisão nativa efetivamente testada. Distinguir teste HTTP loopback, simulação de resolvedor e reprodução de transportes/serviços reais no relatório.
- Não misturar carregamento de mídia, eventos, propriedades observadas e shutdown em uma mesma alteração quando uma mudança puder ser menor.
- Verificar e ampliar os testes da área em `src/MpvNet.Tests/` (incluindo `Program.cs` quando aplicável) quando tocar paths, playlist, parser de comandos, títulos, seleção de idioma ou MediaInfo; depois rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.

## Testes manuais esperados

- Abrir mídia local.
- Abrir mídia via linha de comando.
- Testar pause/play, seek, playlist e encerramento.
- Testar fullscreen.
- Testar script simples e comando `script-message-to mpvnet`.


