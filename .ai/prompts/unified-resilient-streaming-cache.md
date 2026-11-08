# Codex — Cache de streaming resiliente unificado (MPV.NET Media Player)

**Repositório:** `WandersondeSouza/mpv.net`  
**Stack:** C# / .NET 10 / libmpv / yt-dlp  
**Escopo:** somente cache de streaming, configuração, diagnóstico, testes e documentação relacionados.

## Objetivo

Substituir os perfis manuais `low-latency`, `balanced` e `resilient` por **uma única política automática resiliente**, ajustada internamente ao tipo de transmissão e às condições da rede. Para o usuário, apresentar somente **Cache automático de streaming: ativado/desativado** (`AutomaticNetworkCache`, ativado por padrão em novas instalações).

Não construir downloader, cache paralelo, novo player nem serviço externo; o mpv/libmpv continua responsável pela reprodução e cache nativo. Melhorar estabilidade sem acrescentar atraso desnecessário às lives ou consumo excessivo de memória.

## 1. Auditoria antes de editar

1. Ler `AGENTS.md`, `.ai/skills/mpvnet-maintainer.md`, agentes relevantes em `.ai/agents/`, documentação de integração mpv e configuração.
2. Registrar branch, HEAD e git status; preservar alterações preexistentes.
3. Confirmar a versão efetiva do mpv/libmpv e a semântica/documentação das opções/propriedades. Não presumir que todas as opções são mutáveis em runtime.
4. Inspecionar:
   - `src/MpvNet/Media/MediaInput.cs` (`MediaInputClassifier`, `NetworkCachePolicy`, `MpvOptionConfiguration`).
   - `src/MpvNet/Integration/Mpv/Player.NetworkCache.cs`.
   - `src/MpvNet/Integration/Mpv/Player.MediaLoading.cs`.
   - `src/MpvNet/Integration/Mpv/Player.Initialization.cs`.
   - `src/MpvNet/Integration/Mpv/Player.Events.cs`.
   - `src/MpvNet/Integration/Mpv/Player.ObservedProperties.cs`.
   - `src/MpvNet/Media/LiveStreamRecoveryPolicy.cs`, `src/MpvNet/App.cs`.
   - `src/MpvNet.Tests/NetworkCachePolicyTests.cs`, testes de reprodução e diagnóstico relacionados.
5. Consultar os prompts existentes, sem sobrepor seus escopos: `.ai/prompts/online-media-youtube-streaming-modernization.md`, `.ai/prompts/youtube-video-freeze-audio-continues.md` e `.ai/prompts/playback-reliability-deep-audit.md`.
6. Relatar evidências e hipóteses; em seguida implementar de modo incremental sem pedir autorização intermediária.

## 2. Unificação e migração

- Eliminar da interface seleção dos três perfis; manter somente `AutomaticNetworkCache`.
- `true`: política automática resiliente; `false`: nenhuma opção automática, preservando comportamentos/configurações explícitas do mpv.
- Migrar `low-latency`, `balanced` e `resilient` para ativado; migrar `off` para desativado. Preservar configurações antigas durante a leitura e persistir formato novo quando seguro.
- Remover dependência operacional de `NetworkCacheProfile` após migração, evitando quebra de arquivos existentes, CLI e consumidores internos.
- Respeitar `mpv.conf`, perfis do mpv, CLI, configurações de script e opções locais por mídia. Auditar precedência de verdade, inclusive `option-info/*` e hooks; jamais sobrescrever intenção explícita.
- Evitar opções conflitantes, aplicação duplicada e divergências entre `loadfile` e `on_load`/playlists.

## 3. Limites ampliados para testes

**Valores iniciais de referência, não reservas nem garantias de preenchimento:**

| Mídia | Teto inicial |
| --- | ---: |
| YouTube / yt-dlp VOD | 512 MiB |
| HTTP progressivo VOD | 512 MiB |
| DASH VOD | 512 MiB |
| HLS VOD | 256 MiB |
| HLS/DASH live | 64–128 MiB conforme latência e janela |
| RTSP/RTMP | 32–64 MiB conforme protocolo |
| Outros streams | limite conservador comprovado |
| Arquivos locais | sem política de cache de rede |

- Revisar `demuxer-max-bytes`, `demuxer-max-back-bytes`, `demuxer-readahead-secs`, `cache-secs`, `cache-pause`, `cache-pause-initial`, `cache-pause-wait`, `cache-on-disk` e opções de rede suportadas.
- Os limites em MiB são tetos de dados do demuxer; não significam cache efetivamente ocupado ou minutos assegurados. Considerar cache para trás, consumo global de memória, decodificador e VRAM.
- **Não** habilitar indiscriminadamente `cache-on-disk` para HTTP/YouTube. Se proposto, tornar opcional, temporário, limitado, limpável e respaldado por testes.
- Validar os números com medições. Se uma opção/valor não for adequada à versão mpv usada, corrigir e documentar o motivo; não forçar o valor do prompt em detrimento de estabilidade.
- Em lives não sacrificar latência indiscriminadamente para obter mais buffer; não fazer seeks automáticos até o live edge sem política explícita.

## 4. Classificação real de mídia

- Distinguir HTTP progressivo, HLS VOD/live, DASH VOD/live, yt-dlp/EDL, RTSP/RTMP e arquivos locais.
- O URL original pode ser uma página de serviço, não o formato final. Investigar dados do demuxer/stream após resolução pelo yt-dlp, sem expor URLs assinadas nem criar requisições redundantes.
- Aplicar opções na fase suportada pelo mpv. Não reiniciar mídia para alterar cache; opções imutáveis devem valer no próximo carregamento.
- Quando não for possível distinguir VOD/live com segurança, usar comportamento conservador sem inferir somente pelo sufixo da URL.

## 5. Monitoramento e adaptação segura

- Reaproveitar eventos e observadores mpv: `paused-for-cache`, `demuxer-cache-duration`, `demuxer-cache-state`, `cache-buffering-state`, `cache-speed`, pausas, seeks e eventos de carregamento/fim.
- Implementar métricas de sessão (quantidade/duração de buffering, duração efetiva audio/vídeo, velocidade de entrada e, quando confiável, bitrate da mídia) e política pura/testável de decisão. Preferir poucas classes e reutilizar responsabilidades existentes.
- Evitar polling intenso, acesso bloqueante na UI, logs por frame/pacote, alocações repetidas e alterações frequentes.
- Fazer adaptação somente com base em métricas confiáveis, histerese, limites e períodos mínimos; respeitar configurações explícitas.
- Rede estável: não aumentar cache sem ganho. Rede oscilante: margem de retomada apropriada. Rede insuficiente: diagnosticar sem loop; não prometer reproduzir bitrate acima da capacidade.
- Não mudar automaticamente resolução, codec ou representação do YouTube neste escopo.
- Cancelar e limpar estado/observadores a cada troca de mídia, encerramento e dispose; prevenir callbacks tardios.

## 6. Recuperação e congelamento

- Reutilizar `LiveStreamRecoveryPolicy` e o mecanismo existente.
- Distinguir buffering, timeout, rede, HTTP, manifesto, URL assinada expirada, yt-dlp, autenticação, restrição regional, transporte e decoder.
- Retentar somente falhas transitórias comprovadas com limite/backoff, nunca repetidamente autenticação/403 de verificação de bot.
- Não reutilizar URL assinada expirada como se fosse permanente.
- Alinhar com `.ai/prompts/youtube-video-freeze-audio-continues.md`: áudio avançando com vídeo congelado pode ser erro VP9/AV1 ou transporte; cache maior não é solução garantida. Não duplicar watchdog ou criar loops/reinícios de playlist; evitar falso positivo em pausa, seek, áudio-only e buffering.

## 7. Interface, tradução e logs

- Configuração simplificada: único toggle **Cache automático de streaming**. Atualizar traduções dos idiomas existentes e documentação.
- Reutilizar indicador de cache/OSC; não redesenhar telas fora do escopo.
- Em logs controlados, registrar formato identificado, política, opções efetivas, métricas de buffering e resultados de recuperação.
- Sanitizar URLs, cookies, tokens, headers e parâmetros assinados. Não habilitar log detalhado por padrão em produção.

## 8. Testes e validações

Cobrir testes unitários e de integração apropriados para:
1. Migração de cada valor antigo (inclusive `off`) e instalação nova.
2. Cache ativado/desativado, preservação de CLI/`mpv.conf`/perfis/scripts/file-local-options.
3. Seleção de limites 512/256/128/64 MiB por mídia e fallback seguro, inclusive yt-dlp resolvido como HLS/DASH.
4. HTTP, YouTube, HLS/DASH VOD/live, RTSP/RTMP, playlists e arquivos locais.
5. Buffer esgotado, rede oscilante, timeout, reconexão, erros definitivos, URL expirada, seek, pausa, troca de item e encerramento.
6. Ausência de corrida, loop, vazamento de recurso, regressão no player e vazamento de segredo em logs.

Executar build e testes conforme padrões do repositório e registrar resultados. Sempre que houver ambiente válido, comparar antes/depois em condições equivalentes: tempo para iniciar, interrupções/hora, duração total de buffering, RAM/CPU, tempo de recuperação, 1080p/4K, atraso nas lives. Diferenciar medições reais de hipóteses e marcar testes externos indisponíveis como pendentes.

## 9. Sequência recomendada

**Sprint 1:** unificar perfil, migração e interface.  
**Sprint 2:** novos limites e identificação correta da mídia.  
**Sprint 3:** métricas, política adaptativa segura e integração com recovery existente.  
**Sprint 4:** traduções, documentação, testes de regressão e relatório de desempenho.

Entregar relatório com arquivos alterados, causas confirmadas, comportamento final, limites efetivos, migração, testes realizados/pendentes, métricas reais, riscos e limitações.

**Critérios de aceite:** um único toggle; política resiliente por tipo; limites maiores onde apropriado; usuário avançado respeitado; nenhuma regressão em arquivos locais, YouTube ou playlists; nenhuma recuperação infinita; nenhuma exposição de segredos; testes passando ou pendências justificadas.

**Não realizar commit adicional, push de implementação, publicação ou deploy sem pedido explícito.** A gravação deste próprio arquivo de prompt no repositório é a única mudança autorizada nesta etapa.
