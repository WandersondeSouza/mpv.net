# AUDITORIA E MELHORIA INCREMENTAL DO NÚCLEO DE REPRODUÇÃO — MPV.NET MEDIA PLAYER

Trabalhe no repositório atual **MPV.NET Media Player**.

O objetivo principal desta tarefa é elevar ao máximo possível a **estabilidade, robustez e qualidade da reprodução de vídeo e áudio**, especialmente na integração com **mpv/libmpv**, sem realizar uma grande refatoração indiscriminada e sem alterar funcionalidades que não tenham relação direta com reprodução.

## REGRA PRINCIPAL

Este trabalho DEVE ser realizado **por etapas pequenas e independentes**.

NÃO implemente tudo de uma vez.

Para CADA etapa:

1. analisar o comportamento atual;
2. identificar um problema concreto, risco concreto ou melhoria claramente justificável;
3. implementar apenas a alteração necessária;
4. criar ou ampliar testes;
5. compilar a solução;
6. executar os testes relevantes;
7. corrigir qualquer regressão;
8. revisar o diff;
9. fazer um commit pequeno e descritivo;
10. fazer push;
11. somente então iniciar a próxima etapa.

Se uma etapa revelar um problema maior, subdivida-a antes de continuar.

Nunca faça commits gigantes contendo diversas alterações independentes.

---

# PRINCÍPIO DE SEGURANÇA DA REPRODUÇÃO

O núcleo `mpv/libmpv` é uma área crítica e de alto risco.

Antes de alterar essa região, leia obrigatoriamente:

- `AGENTS.md`
- `.ai/agents/mpvnet-libmpv-agent.md`
- `docs/manual.md`
- `docs/guia-operacional.md`
- `docs/developer/mpv-integration.md`
- `docs/developer/architecture.md`

Analise principalmente:

- `src/MpvNet/Integration/Mpv/Player.cs`
- `src/MpvNet/Integration/Mpv/Player.*.cs`
- `src/MpvNet/Integration/Mpv/MpvClient.cs`
- `src/MpvNet/Native/LibMpv.cs`
- `src/MpvNet/Native/LibMpvRuntime.cs`
- `src/MpvNet/Command.cs`
- `src/MpvNet/Media/MediaInput.cs`
- `src/MpvNet/Media/PlaylistFile.cs`
- `src/MpvNet/Configuration/CommandLine.cs`
- `src/MpvNet/Configuration/InputConf.cs`
- testes existentes em `src/MpvNet.Tests`

Preserve a ordem necessária de inicialização de `Player.Initialization.cs`.

Não misture carregamento de mídia, eventos, propriedades observadas, cache e shutdown na mesma alteração quando puderem ser tratados separadamente.

---

# ETAPA 0 — BASELINE

Antes de modificar código:

- confirmar branch atual;
- confirmar que o working tree está limpo;
- registrar commit inicial;
- restaurar dependências;
- compilar a solução;
- executar toda a suíte de testes existente;
- registrar warnings e erros já existentes;
- não atribuir à nova implementação problemas que já existiam.

Executar pelo menos:

```text
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln
dotnet test src\MpvNet.sln
```

E o fluxo específico recomendado pelo projeto quando aplicável:

```text
dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
```

Se houver testes ou comandos adicionais definidos no repositório, executá-los também.

Não iniciar alterações enquanto não houver um baseline conhecido.

---

# ETAPA 1 — INTEROP E MEMÓRIA NATIVA

Fazer uma auditoria detalhada de todas as chamadas P/Invoke para libmpv.

Verificar:

- assinaturas das funções;
- tipos utilizados;
- tamanho das estruturas;
- alinhamento;
- marshaling;
- UTF-8;
- ownership de ponteiros;
- quem aloca;
- quem libera;
- momento correto de liberação;
- retorno de erros;
- comportamento durante shutdown.

Revisar especialmente:

- `mpv_create`
- `mpv_create_client`
- `mpv_initialize`
- `mpv_destroy`
- `mpv_terminate_destroy`
- `mpv_command`
- `mpv_command_string`
- `mpv_command_ret`
- `mpv_wait_event`
- `mpv_get_property`
- `mpv_set_property`
- `mpv_set_option_string`
- `mpv_observe_property`
- `mpv_unobserve_property`
- `mpv_free`
- `mpv_free_node_contents`

Comparar as assinaturas com a API oficial da versão de libmpv utilizada pelo projeto.

Não alterar uma assinatura somente por preferência estética.

Alterar apenas quando houver justificativa técnica.

---

# ETAPA 2 — PROTEGER ALOCAÇÕES UNMANAGED

Auditar principalmente `MpvClient.CommandV` e `MpvClient.Expand`.

Hoje existem alocações através de `Marshal.AllocHGlobal`.

Garantir que TODA memória unmanaged seja liberada mesmo quando ocorrer:

- exceção;
- erro durante marshaling;
- erro ao montar argumentos;
- erro durante comando;
- shutdown concorrente.

Preferir uma solução clara com `try/finally` ou abstração segura equivalente.

Evitar adicionar complexidade desnecessária.

Criar testes para as funções auxiliares que puderem ser isoladas.

Depois: build, testes, revisão de diff, commit e push.

Commit sugerido:

```text
fix(player): harden unmanaged command allocations
```

---

# ETAPA 3 — CICLO DE VIDA E CONCORRÊNCIA DO HANDLE LIBMPV

Auditar profundamente a interação entre:

- `EventLoop`
- `MainEventLoop`
- `BeginShutdown`
- `TryEnterNativeOperation`
- `DestroyHandle`
- `Destroy`
- `_nativeLifetimeGate`
- `_nativeLifetimeLock`
- `_playerCancellation`
- tasks do player
- tasks de eventos.

Objetivos:

- impedir chamada nativa depois da destruição do handle;
- impedir destruição do handle enquanto uma operação nativa ainda o utiliza;
- impedir race conditions durante fechamento;
- impedir deadlocks;
- impedir eventos acessando memória inválida;
- garantir encerramento determinístico.

ATENÇÃO ESPECIAL:

Investigar a vida útil dos dados retornados por `mpv_wait_event`.

O código copia `mpv_event`, libera o lease de operação nativa e depois pode acessar `evt.data`.

Confirmar, usando a documentação oficial do libmpv, até quando `evt.data` permanece válido.

Verificar principalmente:

- log messages;
- client messages;
- property changes;
- end-file events.

Se necessário, copie para estruturas/valores gerenciados todos os dados que dependem da memória do evento enquanto a proteção do handle ainda estiver válida e somente depois despache callbacks.

NÃO mantenha locks durante execução arbitrária de handlers da aplicação se isso puder provocar deadlocks.

A solução ideal deve:

1. proteger a leitura da memória nativa;
2. copiar os dados necessários;
3. liberar a proteção nativa;
4. processar callbacks com dados gerenciados.

Criar testes de concorrência quando possível.

Commit separado.

---

# ETAPA 4 — EVENT LOOP E CALLBACKS

Revisar o processamento de:

- `SHUTDOWN`
- `LOG_MESSAGE`
- `CLIENT_MESSAGE`
- `VIDEO_RECONFIG`
- `AUDIO_RECONFIG`
- `END_FILE`
- `FILE_LOADED`
- `PROPERTY_CHANGE`
- `START_FILE`
- `SEEK`
- `PLAYBACK_RESTART`

Verificar:

- exceções em callbacks;
- eventos lentos bloqueando consumo de eventos do mpv;
- possibilidade de `MPV_EVENT_QUEUE_OVERFLOW`;
- concorrência nas coleções de observers;
- duplicação de eventos;
- callbacks durante shutdown;
- callbacks executados depois da destruição;
- handlers que fazem operações pesadas diretamente na thread de eventos.

O objetivo é manter a thread responsável pelos eventos do mpv extremamente previsível.

Não transformar indiscriminadamente todos os eventos em `Task.Run`.

Utilizar processamento assíncrono apenas onde houver necessidade demonstrável.

---

# ETAPA 5 — CARREGAMENTO DE MÍDIA

Auditar profundamente `Player.MediaLoading.cs`.

Testar separadamente arquivos locais: MP4, MKV, AVI, MOV, TS, M2TS, WEBM, FLV quando suportado, MP3, AAC, FLAC, WAV, OGG e M4A.

Testar caminhos absolutos e relativos, nomes com espaço, Unicode, caracteres especiais, compartilhamentos de rede, arquivos inexistentes e extensões em maiúsculas/minúsculas.

Testar origens: linha de comando, drag and drop, clipboard, file dialog, playlist, recent files, IPC e comandos internos.

Confirmar que todos terminam em um fluxo coerente de `MediaLoadRequest`/`LoadFiles`/`loadfile`.

Evitar caminhos paralelos que configurem reprodução de forma diferente sem necessidade.

---

# ETAPA 6 — STREAMING E CACHE

O projeto já possui `NetworkMediaKind`, `NetworkCachePolicy` e `NetworkCacheProfile` e classificação para HTTP/HTTPS progressivo, HLS, DASH, FTP/FTPS, SFTP, RTSP, RTMP, UDP/TCP/SRT/SRTP e rede genérica.

PRESERVAR essa arquitetura.

Não substituir tudo por um único cache global.

Revisar os perfis atuais: `off`, `low-latency`, `balanced` e `resilient`.

Para cada protocolo verificar:

- tempo para iniciar;
- estabilidade;
- rebuffering;
- consumo de RAM;
- seek;
- live edge;
- reconexão;
- perda de conexão;
- timeout;
- servidor lento;
- conteúdo de bitrate alto.

Verificar se os valores de `cache`, `cache-on-disk`, `cache-pause`, `cache-pause-initial`, `cache-pause-wait`, `cache-secs`, `demuxer-max-bytes`, `demuxer-max-back-bytes`, `demuxer-readahead-secs` e `network-timeout` são coerentes.

IMPORTANTE: a configuração automática NÃO deve sobrescrever configuração explicitamente definida pelo usuário através de linha de comando ou `mpv.conf`.

Preservar a precedência existente.

Para streams live, priorizar baixa latência.

Para VOD remoto, priorizar estabilidade.

Não aumentar cache indiscriminadamente porque isso pode aumentar RAM, startup e latência.

Qualquer alteração dos valores deve possuir justificativa e testes.

---

# ETAPA 7 — REDE INSTÁVEL E RECUPERAÇÃO

Investigar comportamento quando conexão cai, Wi-Fi oscila, servidor demora, servidor fecha conexão, HTTP retorna erro, stream expira, HLS perde segmentos, DNS falha, FTP/SFTP deixa de responder ou stream live apresenta interrupção.

Não criar retry infinito.

Se implementar recuperação automática, utilizar número máximo de tentativas, backoff apropriado, identificação clara de erros recuperáveis e não recuperáveis, cancelamento imediato quando usuário troca mídia e cancelamento durante shutdown.

Nunca reabrir automaticamente uma mídia que o usuário já abandonou.

---

# ETAPA 8 — PLAYLISTS

Preservar suporte existente para M3U, M3U8, PLS, XSPF, ASX, WPL, CUE e JSPF.

Validar playlists contendo simultaneamente arquivos locais, HTTP, HTTPS, FTP, HLS, DASH, áudio e vídeo.

Verificar caminhos relativos, URLs codificadas, títulos, duplicatas, item inexistente, item indisponível, primeiro item com erro, item intermediário com erro e último item com erro.

REGRESSÃO CRÍTICA: existe proteção para impedir que a normalização da playlist automática recarregue uma mídia que já está tocando.

Preservar obrigatoriamente esse comportamento.

Não alterar essa regra sem teste específico demonstrando o novo comportamento.

---

# ETAPA 9 — TRATAMENTO DE ERRO DE REPRODUÇÃO

Revisar `OnEndFile` e preservar a filosofia atual de recuperação conservadora.

Verificar erros de formato não reconhecido, codec, áudio output, vídeo output, rede, timeout, URL inválida, arquivo inexistente, acesso negado, demuxer e decoder.

Quando houver playlist:

- não travar no item defeituoso;
- não pular dois itens;
- não entrar em loop;
- não avançar caso o usuário já tenha mudado manualmente de posição.

Criar testes para todas essas decisões antes de modificar o algoritmo.

---

# ETAPA 10 — ÁUDIO

Auditar seleção de dispositivo de áudio, dispositivo salvo anteriormente, dispositivo removido, troca de dispositivo, HDMI, Bluetooth, WASAPI, passthrough quando configurado, mudança de faixa, áudio sem vídeo, pause/resume, seek, mute e volume.

Se o dispositivo salvo não existir mais, o player deve poder recuperar-se de maneira segura usando a seleção padrão do mpv.

Não introduzir preferência fixa de dispositivo.

---

# ETAPA 11 — VÍDEO E ACELERAÇÃO POR HARDWARE

Auditar como o player atualmente delega ao mpv video output, GPU API, hardware decoding, rendering, HDR, resize e fullscreen.

NÃO force `hwdec` específico globalmente sem evidência.

Comparar opções atuais com recomendações da versão de mpv distribuída pelo projeto.

Testar pelo menos H.264, H.265/HEVC, VP9, AV1 quando hardware/software disponível, SDR e HDR quando ambiente permitir.

Verificar comportamento de fallback quando hardware decoding não funciona.

O player deve preferir continuar reproduzindo por software quando possível em vez de simplesmente falhar.

---

# ETAPA 12 — LIBMPV NORMAL E X86-64-V3

Preservar o mecanismo atual de detecção automática.

Revalidar:

- CPU compatível → variante v3;
- CPU incompatível → normal;
- DLL v3 ausente → fallback normal;
- DLL v3 inválida → fallback normal;
- DLL normal ausente → erro claro;
- override por variável de ambiente;
- mensagens de diagnóstico.

Não reescrever essa funcionalidade sem necessidade.

Criar/atualizar testes apenas se encontrar lacuna concreta.

---

# ETAPA 13 — YT-DLP

Auditar integração com `yt-dlp`.

Verificar detecção, caminho configurado, componente ausente, componente inválido, atualização, refresh após bootstrap, execução com URL suportada e falha do resolver.

Não confundir falha do yt-dlp com falha do decoder do libmpv.

Logs devem indicar claramente a camada que falhou.

URLs normais HTTP/HLS/DASH que não necessitam de yt-dlp não devem depender dele.

---

# ETAPA 14 — OBSERVABILIDADE DA REPRODUÇÃO

Melhorar logs somente quando trouxerem valor de diagnóstico.

Para falha de reprodução registrar, quando disponível, tipo de mídia, origem, tipo de rede, perfil de cache, posição da playlist, código do mpv, descrição do erro e etapa em que ocorreu.

NÃO registrar cookies, authorization headers, credenciais, tokens ou query strings sensíveis completas.

Utilizar os helpers de segurança de log já existentes.

Evitar log excessivo durante reprodução normal.

---

# ETAPA 15 — TESTES DE REGRESSÃO

Ampliar a suíte `MpvNet.Tests`.

Priorizar testes determinísticos para classificação das URLs, políticas de cache, precedência de configuração, argumentos `loadfile`, escaping, playlist, caminhos relativos, títulos, recuperação de erro, lifecycle helpers, seleção libmpv, parsers e regras de retry se adicionadas.

Não criar testes que dependam permanentemente de um servidor público instável.

Para rede, preferir servidor HTTP local de teste quando necessário.

---

# ETAPA 16 — TESTE MANUAL OBRIGATÓRIO

Antes de considerar o trabalho concluído executar uma matriz manual mínima.

Local: vídeo, áudio, pause/play, seek, mudança de faixa, playlist, fullscreen, fechar durante reprodução e abrir outro arquivo rapidamente.

HTTP/HTTPS: MP4 remoto, áudio remoto e seek.

HLS: VOD e live quando disponível.

Playlist mista: arquivo local, HTTP, item inválido e outro item válido.

Shutdown: fechar parado, reproduzindo, durante buffering e durante troca rápida de arquivos.

Não considerar um crash durante shutdown aceitável.

---

# ETAPA 17 — STRESS

Criar ou executar cenário de stress:

```text
abrir mídia A
abrir mídia B
abrir mídia C
seek
pause
play
trocar faixa
abrir mídia D
fechar aplicação
```

Repetir diversas vezes.

Observar crash, deadlock, acesso inválido, crescimento contínuo de memória, threads sobrevivendo após shutdown, handles não liberados e arquivos temporários presos.

Utilizar ferramentas de diagnóstico .NET/Windows quando necessário.

---

# POLÍTICA DE COMMITS

Cada assunto independente deve possuir commit independente.

Exemplos:

```text
fix(player): harden unmanaged command allocations
fix(player): protect libmpv event payload lifetime
fix(player): harden shutdown synchronization
test(player): expand playback lifecycle coverage
fix(streaming): improve HLS cache behavior
fix(playlist): preserve playback after invalid item
```

Não utilizar mensagens vagas como `ajustes`, `correções`, `melhorias`, `update` ou `fix things`.

Sempre dizer claramente o que foi alterado.

Após cada commit:

```text
git status
git log -1
git push
```

Confirmar que o push foi concluído.

---

# REGRA DE NÃO REGRESSÃO

Antes de qualquer alteração, perguntar internamente:

```text
Que comportamento atual posso quebrar com esta mudança?
```

Depois criar ou localizar o teste que protege esse comportamento.

Somente então implementar.

O objetivo NÃO é maximizar quantidade de código alterado.

O objetivo é maximizar estabilidade, previsibilidade, reprodução contínua, segurança do ciclo de vida, compatibilidade e facilidade de diagnóstico.

---

# NÃO FAZER

Não:

- reescrever todo o player;
- trocar libmpv;
- trocar arquitetura principal;
- alterar interface gráfica sem necessidade;
- mudar atalhos;
- alterar idiomas;
- alterar instalação;
- mudar comportamento de recursos não relacionados;
- modificar dezenas de arquivos por estética;
- introduzir abstrações sem benefício concreto;
- criar cache gigantesco indiscriminadamente;
- criar retry infinito;
- ignorar configuração explícita do usuário;
- fazer um único commit com todo o trabalho;
- prosseguir quando testes estiverem quebrados.

---

# DEFINIÇÃO DE PRONTO DE CADA ETAPA

Uma etapa somente termina quando:

- problema identificado;
- causa entendida;
- alteração mínima implementada;
- teste adicionado/atualizado;
- build aprovado;
- testes aprovados;
- regressões verificadas;
- diff revisado;
- commit criado;
- push realizado.

Depois iniciar a próxima.

---

# RELATÓRIO FINAL

Ao finalizar todas as etapas possíveis, produzir relatório contendo problemas encontrados classificados como CRÍTICO, ALTO, MÉDIO e BAIXO.

Para cada problema informar arquivo, método, causa, efeito sobre reprodução, solução aplicada, teste criado e commit correspondente.

Também informar melhorias de streaming, áudio, vídeo, lifecycle, memória, playlist, tratamento de erro, itens investigados e considerados corretos, riscos ainda existentes e recomendações futuras.

Por fim mostrar:

```text
git status
git log --oneline
```

e confirmar que todos os commits realizados nesta tarefa foram enviados ao repositório remoto.

---

# PRIORIDADE DE EXECUÇÃO

Execute nesta ordem:

1. baseline;
2. interop/memória unmanaged;
3. lifecycle e concorrência;
4. event loop;
5. carregamento de mídia;
6. streaming/cache;
7. recuperação de rede;
8. playlists;
9. erros de reprodução;
10. áudio;
11. vídeo/hardware decoding;
12. libmpv normal/v3;
13. yt-dlp;
14. logs/diagnóstico;
15. testes de regressão;
16. testes manuais;
17. stress.

Mais importante que terminar todas as etapas rapidamente é **não introduzir regressões no player**.

Trabalhe com cautela, uma melhoria por vez.
