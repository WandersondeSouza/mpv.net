# Integração com mpv/libmpv

## Objetivo

Documentar conceitos relacionados à integração do MPV.NET Media Player com mpv/libmpv.

---

# Visão geral

O mpv.net utiliza o mpv/libmpv como motor principal de reprodução multimídia.

A integração com libmpv é considerada uma das áreas mais críticas do projeto.

---

# Responsabilidades da integração

- reprodução de mídia;
- propriedades;
- comandos;
- sincronização;
- eventos;
- estado do player;
- integração com scripts.

---

# Wrapper e comunicação

Arquivos principais:

- `src/MpvNet/Native/LibMpv.cs` - P/Invoke para `libmpv-2.dll`;
- `src/MpvNet/Native/LibMpvRuntime.cs` - seleção da variante e diagnóstico de carregamento;
- `src/MpvNet/Integration/Mpv/MpvClient.cs` - wrapper de cliente, comandos, propriedades e eventos;
- `src/MpvNet/Integration/Mpv/Player.cs` - estado principal do player;
- `src/MpvNet/Integration/Mpv/Player.Initialization.cs` - inicialização e configuração inicial do mpv/libmpv;
- `src/MpvNet/Integration/Mpv/Player.ObservedProperties.cs` - propriedades observadas;
- `src/MpvNet/Integration/Mpv/Player.Events.cs` - eventos vindos do mpv;
- `src/MpvNet/Integration/Mpv/Player.Lifecycle.cs` - loop principal, shutdown e destruição de handles;
- `src/MpvNet/Integration/Mpv/Player.MediaLoading.cs` - carregamento de mídia, playlists, URLs, ISO/DVD/BD e pasta automática;
- `src/MpvNet/Integration/Mpv/Player.Capabilities.cs` - perfis, decoders, protocolos, demuxers e criação de clientes adicionais.

## Carregamento dual de libmpv

As distribuições x64 contêm `libmpv-2.dll` (normal) e
`libmpv-2-v3.dll` (x86-64-v3), com a mesma API. O P/Invoke continua usando o
nome lógico único `libmpv-2`. Antes de `mpv_create`, um resolvedor registrado
uma única vez no assembly dos imports decide o arquivo por caminho absoluto a
partir de `AppContext.BaseDirectory`; ele não usa o diretório de trabalho nem
altera o `PATH` global.

A v3 só é tentada quando a CPU oferece SSE3, SSSE3, SSE4.1, SSE4.2, POPCNT,
AVX, AVX2, F16C, FMA, BMI1, BMI2, LZCNT e MOVBE. Uma falha de carregamento da
v3 é registrada e tenta-se `libmpv-2.dll` no mesmo processo. A normal é o
fallback obrigatório e é usada diretamente em CPU incompatível. O log de
depuração inclui `libmpv selection completed` e informa CPU, DLL preferida,
DLL carregada, caminho e motivo do fallback.

Para um smoke test técnico, execute `mpvnet.exe --diagnose-libmpv`. Ele carrega
a variante selecionada, chama `mpv_client_api_version`, chama `mpv_create` e
destrói o contexto sem abrir a interface. Em desenvolvimento, a variável de
ambiente não persistida `MPVNET_FORCE_LIBMPV_VARIANT` aceita `auto`, `normal`
e `x86_64-v3`; a última exige CPU compatível e existe somente para diagnóstico.

## Ciclo de vida

Fluxo principal em `Player.Initialization.cs`:

1. cria contexto com `mpv_create`;
2. registra eventos com `mpv_request_event`;
3. prepara propriedades iniciais;
4. define `config-dir` como `Player.ConfigFolder`;
5. direciona o diretório de cache temporário do mpv para `%LOCALAPPDATA%\mpv.net\Cache`, sem forçar `cache` global;
6. carrega `input.conf` em memória quando existe conteúdo;
7. processa argumentos de linha de comando antes de `mpv_initialize`;
8. chama `mpv_initialize`;
9. cria cliente secundário `mpvnet`;
10. inicia o loop de eventos;
11. registra observadores de propriedades;
12. dispara o estado pronto para a UI.

O estado do player é explícito (`Created`, `Initializing`, `Running`, `Shutdown` e
`Destroyed`). Ao iniciar `Destroy`, cada cliente fecha seu gate de lifetime antes
do cancelamento: novas chamadas de propriedade, comando, opção ou observação são
rejeitadas, enquanto uma chamada nativa já iniciada termina sob o gate. Os
callbacks de eventos ficam fora do lock para poderem consultar o wrapper sem
deadlock; tarefas e loops são aguardados antes de destruir qualquer handle. O
handle principal inicializado é encerrado com `mpv_terminate_destroy`; handles de
clientes são destruídos antes dele. Depois que os loops e chamadas existentes
terminam, cada cliente descarta seu `ReaderWriterLockSlim`; o player descarta o
`CancellationTokenSource`, `SemaphoreSlim` e sinal de shutdown. `Destroy()` e
`Dispose()` são idempotentes e removem callbacks gerenciados para não reter a UI.

As tarefas de playlist e metadata são serializadas por instância do player e
canceladas durante o fechamento. Uma tarefa atrasada pode terminar sem executar
`GetProperty*`, `SetProperty*` ou `Command*` depois do início de `Destroy`,
mantendo essas operações serializadas durante a troca de mídia e o encerramento.

O módulo AviSynth carregado para arquivos `.avs` possui lifetime de processo.
O handle retornado por `LoadLibrary` é mantido explicitamente e não recebe
`FreeLibrary` no `Destroy`, pois o mpv/AviSynth ainda pode executar código do
módulo enquanto encerra filtros e mídia.

## Eventos, propriedades e comandos

`MpvClient` traduz eventos nativos para eventos .NET e expõe callbacks para mudanças de propriedades, incluindo valores booleanos, inteiros, `double` e `string`.

Eventos importantes:

- `MPV_EVENT_SHUTDOWN`;
- `MPV_EVENT_LOG_MESSAGE`;
- `MPV_EVENT_CLIENT_MESSAGE`;
- `MPV_EVENT_END_FILE`;
- `MPV_EVENT_FILE_LOADED`;
- `MPV_EVENT_PROPERTY_CHANGE`;
- `MPV_EVENT_START_FILE`;
- `MPV_EVENT_PLAYBACK_RESTART`.

Comandos:

- `Command(string command)` para comandos textuais;
- `CommandV(params string[] args)` para comandos com argumentos separados.

Regra prática: quando o trabalho tocar eventos, propriedades, comandos ou `input.conf`, a investigação deve apontar a camada certa antes de alterar a implementação.

## Controles de mídia do Windows

Os controles de mídia não alteram o wrapper libmpv. A camada Windows observa os
eventos `start-file`, `file-loaded`, `end-file`, `pause`, `seek`,
`playback-restart` e `playlist-pos`, consulta propriedades somente para
metadata/timeline e converte os comandos do SMTC em `CommandV` (`set pause`,
`stop`, `playlist-next`, `playlist-prev` e `seek absolute`). O player continua
responsável pela reprodução e a integração é desligada se o SMTC não estiver
disponível.

## Metadados auxiliares

A reprodução tem prioridade sobre metadados auxiliares. `MediaInfo.dll`,
listas de faixas, capítulos, duração, codec, idioma, título e propriedades
consultadas apenas para montar a interface não devem bloquear a tentativa de
reprodução quando o arquivo local existe ou a URL de streaming foi encaminhada
ao mpv. Falhas nessa coleta devem ser registradas como diagnóstico técnico e
tratadas com listas vazias ou valores padrão.

O mpv/libmpv é a autoridade final para decidir se a mídia abre. Validações do
frontend devem rejeitar apenas entradas claramente inválidas antes do `loadfile`;
ausência de legenda, áudio, duração, título ou dados de MediaInfo é falha não
bloqueante.

URLs HTTP/HTTPS são encaminhadas diretamente por `loadfile`. O frontend não faz
uma requisição de sondagem antes da reprodução; a identificação de mídia ou
playlist remota fica a cargo do mpv/libmpv.

O mesmo vale para playlists criadas ou expandidas pelo frontend. A linha de
comando pode receber somente uma URL, um arquivo local, ou um titulo visual
seguido de uma URL. Quando a midia principal e resolvida, o titulo deve ser
aplicado como metadado e a URL ou caminho bruto deve ser enviado ao mpv/libmpv
sem depender de playlist. Expansoes auxiliares de playlist devem continuar
opcionais: se falharem, o frontend deve preservar a tentativa de reproducao da
midia principal antes de desistir. Playlists locais expandidas com sucesso
enviam seus itens preparados individualmente por `loadfile`, permitindo opções
de rede por item sem transformar um arquivo local em stream de rede.

Toda coleção controlada pelo frontend converge em
`PlaylistFile.PrepareForPlayback`: os títulos são normalizados antes da remoção
de duplicatas pelo caminho ou URL. Isso vale para arquivos de playlist, pasta
automática e para a fotografia estável de uma playlist nativa.

## Mídia online e yt-dlp multissserviço

O frontend trata o yt-dlp como resolvedor genérico, não como cliente interno do
YouTube. Toda URL HTTP/HTTPS válida continua elegível ao `loadfile`; páginas sem
extensão de mídia direta recebem `NetworkMediaKind.OnlineResolver`, enquanto
arquivos HTTP progressivos, HLS e DASH mantêm classificações específicas. Essa
decisão não usa whitelist de host e permite que um extractor novo do yt-dlp
funcione sem um novo `if` no MPV.NET.

O MPV.NET não executa o yt-dlp antecipadamente em C#, não repete requisições de
metadata e não implementa HTML, API privada ou autenticação de Bilibili,
Niconico, Naver, Dailymotion ou outros provedores. O `ytdl_hook` do mpv conserva
headers, cookies, proxy, fragmentos, legendas e metadados fornecidos pelo
resolvedor. Quando o resultado possui múltiplas entradas, a coleção converge
para a playlist nativa do mpv; próximo/anterior, avanço automático, títulos e
SMTC permanecem independentes do provedor.

### Política específica de playlist do YouTube

O `ytdl_hook` do mpv atual chama o extrator com `--no-playlist` por padrão. O
frontend só acrescenta a opção local
`ytdl-raw-options-append=yes-playlist=` quando `YouTubeMediaPolicy` encontra
intenção explícita de coleção por meio de um parâmetro `list=` em host legítimo
do YouTube. A URL original, inclusive `v=`, `index=`, parâmetros desconhecidos
e fragmento, permanece como um único argumento de `loadfile`.

Não existe playlist paralela em C#. Com `yes-playlist`, o hook nativo recebe o
JSON plano do yt-dlp, cria a playlist do mpv e sua implementação atual compara
`index=` e `v=` com as entradas retornadas para escolher o item inicial. Assim,
`playlist-next`, `playlist-prev`, avanço automático e SMTC continuam operando
sobre a mesma fila nativa. Vídeos sem `list=` não recebem a opção de expansão.
Após a expansão, o frontend normaliza os títulos dos itens nativos e remove
duplicatas pelo caminho ou URL. Essa correção é feita na própria fila do mpv:
o item atual é preservado, recebe `force-media-title`, e os itens anteriores e
posteriores voltam por M3U temporárias com `#EXTINF`. O observador usa debounce
e registra a sequência de endereços preparada antes de aceitar outra execução;
assim, os eventos produzidos por `playlist-clear` e `loadlist` não realimentam
a normalização.

Essa decisão foi confrontada em 2026-09-21 com o manual e o `ytdl_hook.lua`
atuais do mpv. A validação local usou yt-dlp `2026.08.19` e uma playlist pública
de 31 itens: o hook recebeu `--yes-playlist`, registrou a correspondência do
vídeo solicitado, definiu `playlist-start=2` e o mpv expôs `playlist-count=31`,
`playlist-pos=2` e o vídeo da URL como item atual. Como a resposta vem de um
serviço externo, essa prova deve ser repetida antes de uma release.

### Validação multissserviço de 2026-09-21

Com yt-dlp `2026.08.19`, o comando `--list-extractors` confirmou extractors para
YouTube, Bilibili (incluindo Bangumi, coleção, favoritos, playlist e live),
Niconico (vídeo, playlist, série e live), Naver (vídeo e live), Dailymotion
(vídeo, playlist e usuário), Douyin, Douyu, Twitch, Vimeo, SoundCloud, iQIYI e
Youku. A presença do nome não é tratada como prova de funcionamento.

Testes online sem download resolveram uma URL pública individual de Bilibili,
Niconico, Naver e Dailymotion. A URL obrigatória de YouTube foi reconhecida pelo
extractor, mas o serviço exigiu autenticação para confirmar que a origem não era
um robô; nenhum cookie foi fornecido. O modo de playlist plano retornou três
entradas de uma anthology do Bilibili e três entradas de uma coleção de usuário
do Niconico. Uma live do Naver e um canal Twitch chegaram aos extractors corretos,
mas estavam offline. SoundCloud resolveu uma entrada pública; Vimeo exigiu login,
e Douyu reconheceu o extractor mas falhou ao obter o identificador da sala.
Douyin não foi validado online. Esses resultados comprovam somente a execução do
yt-dlp nessa máquina: próximo/anterior, SMTC, lives ativas e playback final no mpv
permanecem itens manuais antes da release. A suíte offline cobre preservação de
query, fragmento, Unicode chinês/japonês/coreano, clipboard, IPC, `loadfile`,
domínio desconhecido e ausência de opções específicas de provedor.

## Diagnóstico da cadeia online

`mpvnet.exe --diagnose-components` mantém a resolução central de
`RuntimeComponents` e, sem abrir a UI ou baixar componentes, executa sondagens
curtas para informar a versão efetiva de `yt-dlp` e FFmpeg, runtimes JavaScript
compatíveis e a capability opcional de browser impersonation. O diagnóstico
não recebe URL, cookie, header de autorização, query assinada nem PO Token.

O executável oficial do yt-dlp continua sendo atualizado pelo fluxo existente
de componentes, fora do caminho de reprodução. O mesmo bootstrap agora baixa o
`deno.exe` x64 (Deno 2.3 ou posterior), runtime recomendado e habilitado por
padrão pelo yt-dlp. Os scripts EJS já vêm dentro do executável oficial do
yt-dlp; Node.js 22 ou posterior e QuickJS 2023-12-9 ou posterior continuam
sendo alternativas que exigem configuração explícita. O MPV.NET não baixa
esses runtimes a cada reprodução e não substitui um `ytdl-path` configurado
pelo usuário.

Remote components do EJS não são habilitados silenciosamente. PO Token continua
sendo responsabilidade de provider/plugin ou configuração externa; o player não
gera, captura ou registra tokens. Browser impersonation via `curl_cffi` é uma
capability opcional detectada no yt-dlp, não uma dependência obrigatória.

Falhas de streaming são classificadas somente quando mensagens do mpv/yt-dlp
fornecem evidência. O log final separa categoria provável, mensagem original
sanitizada, componente e ação sugerida. A classificação distingue rede/DNS,
timeout, HTTP, autenticação, conteúdo indisponível, região, extractor,
JavaScript/EJS, PO Token, impersonation, TLS, protocolo, demuxer e playlist;
sem evidência, permanece `Unknown`. Queries de URL e valores de cookies,
Authorization, sessão, assinatura e token são removidos antes desse resumo.
Quando não existe reconexão pendente, a categoria também seleciona uma mensagem
OSD curta por chave gettext. Todos os catálogos distribuídos possuem as mesmas
chaves; detalhes e ações sugeridas continuam apenas no log técnico sanitizado.

A recuperação automática é deliberadamente limitada. Somente uma falha
classificada como transitória agenda reload do mesmo item: no máximo três
tentativas com backoff de 1, 2 e 4 segundos para RTSP/RTMP/SRT/UDP/TCP, ou para
HLS/DASH que já carregou sem duração finita. A fila deve ter no máximo um item;
se houver próximo item, permanece o avanço já existente. Uma geração de mídia e
a fila serial de tarefas impedem retry da mídia anterior, duas reproduções
concorrentes e trabalho posterior ao fechamento. Stop e nova carga invalidam a
geração pendente.

No caminho de `loadfile`, as opções explícitas da linha de comando e do
`mpv.conf` são materializadas uma vez por item. A mesma resolução é reutilizada
para log e construção do comando, evitando a leitura anterior do arquivo para
cada opção de cache e a dupla classificação da mesma URL. URLs legítimas do
páginas candidatas a resolução online recebem o tipo `OnlineResolver`, com
cache inicial conservador, em vez de serem confundidas com HTTP progressivo;
arquivos diretos e manifestos preservam suas classificações, e o protocolo
final continua sob controle do ytdl hook, yt-dlp e mpv.

---

# Compatibilidade

O objetivo arquitetural é manter compatibilidade máxima com mpv.

Mudanças nessa camada devem considerar:

- scripts existentes;
- propriedades;
- linha de comando;
- comportamento esperado do mpv.

---

# Áreas críticas

## Eventos

Mudanças em eventos podem impactar:

- UI;
- scripts;
- sincronização;
- estado do player.

## Fullscreen

Pode depender da integração entre janela e libmpv.

## Input

Atalhos e comandos podem depender da integração com mpv.

---

# Recomendações para manutenção

1. Fazer mudanças pequenas.
2. Testar reprodução.
3. Validar scripts.
4. Validar fullscreen.
5. Validar comandos.
6. Validar propriedades.
7. Preservar a ordem de inicialização de `Player.Initialization.cs`.
8. Rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore` quando tocar paths, playlist, parser de comandos, títulos ou MediaInfo.

---

# Melhorias futuras sugeridas

- documentação dos wrappers;
- detalhamento do fluxo de eventos;
- fluxo de propriedades;
- logs estruturados;
- diagnóstico avançado;
- ferramentas de debug.


