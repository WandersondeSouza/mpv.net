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

## Abertura explícita de unidade/pasta

`open-optical-media` mantém o binding e o diálogo existentes.
`LoadDiskFolderAsync` usa `Media/FolderMediaDiscovery.cs` para distinguir
estruturas DVD/Blu-ray de coleções comuns antes de parar ou substituir a mídia.
O identificador histórico também atende unidades de dados e pastas recursivas.
A descoberta é uma tarefa cancelável do player, fora da UI; erros de sistema
de arquivos têm contagem e amostra limitada no log, sem uma linha por arquivo.

A classificação reconhece `VIDEO_TS/VIDEO_TS.IFO` e `BDMV/index.bdmv`, incluindo
a seleção da pasta interna, normalizada para o dispositivo na raiz pai. O
fluxo usa `dvd-device`/`dvd://` ou `bluray-device`/`bd://`, conforme o
[contrato de disco do mpv](https://mpv.io/manual/master/#disc-devices).
ISO mantém a detecção por MediaInfo existente. Discos aninhados em coleções e
diretórios reservados `VIDEO_TS`/`BDMV` são ignorados, sem tentar montar uma
fila de vários discos. Reparse points também são excluídos.

Os arquivos usam `FileTypes`, `PlaylistFile.Read` e `PrepareForPlayback`.
`StringLogicalComparer` ordena caminhos relativos, com desempate ordinal;
as URLs de playlists conservam sua ordem na posição da playlist fonte.
Títulos vindos de playlists têm preferência sobre a descoberta direta do mesmo
caminho, mas a identidade e normalização continuam centrais em `PlaylistFile`.

Uma coleção válida é enviada de uma vez ao mesmo núcleo
`SendPreparedPlaylistItems` usado pelas playlists locais. A operação usa
substituição explícita e desarma o autoload (`loadFolder=false`), sem passar
pela janela temporal de append de `LoadFiles`. Resultado vazio ou raiz ilegível
não muda o estado de carregamento nem a fila corrente. O autoload de abertura
individual continua restrito à mesma pasta e condicionado a `AutoLoadFolder`.

`SendPreparedPlaylistItems` grava M3U temporária UTF-8 com `#EXTINF` e usa `loadlist`
em substituição ou append: todos os títulos são publicados na playlist nativa
antes da reprodução de seus itens. `loadfile` com `force-media-title` não
preenche antecipadamente o campo `playlist/N/title` de entradas pendentes.
Arquivos locais comuns abertos por `LoadFiles` também usam esse transporte;
ISO, legendas e entradas especiais preservam seus caminhos próprios.
O arquivo usa `PlaylistFile.WriteTempM3u` e entra na limpeza de temporários
do aplicativo no encerramento. A gravação termina antes de `loadlist`; uma
falha nessa etapa não substitui a fila nativa. Não usar `memory://` para esse transporte:
o mpv publica a origem em `playlist/N/playlist-path` de cada entrada. Uma origem
com o texto completo da M3U faz a consulta nativa da fila pelo seletor F8 copiar
um volume de texto que cresce aproximadamente com o quadrado do número de itens.

O hook `on_load` de prioridade 5 aplica o título da entrada como opção local,
quando não há título forçado explícito, antes de ler tags da mídia. Ele também
preserva a expansão de URLs YouTube com `list=` antes do `ytdl_hook` de prioridade
10. O hook de cache continua na prioridade 50 e respeita opções explícitas por
item. Templates, URLs e caminhos originais não são reescritos.

O estado de fila preparada considera caminhos **e títulos**. Em eventos da
playlist, a fotografia atual é comparada com esse estado antes de executar
`PrepareForPlayback`; uma fila inalterada não é renormalizada. Para coleções
criadas pelo mpv/scripts, a normalização posterior mantém a entrada em reprodução
e reinsere as demais com títulos preenchidos. Como os títulos da playlist são
somente leitura, o título original da entrada já em reprodução de uma coleção
externa pode permanecer no seletor até sua próxima carga; o texto do tocador
recebe `force-media-title`. A fotografia nativa resultante é registrada para
evitar reconstruções repetidas. Coleções preparadas pelo frontend já possuem
todos os títulos normalizados desde a inserção, incluindo o item inicial.

Validação local em 2026-10-17: 680 testes aprovados; build Windows Debug x64
sem avisos/erros; 13 catálogos gettext validados e compilados. Uma sonda sem
interface, com libmpv normal real e WAVs locais, verificou substituição da fila
dentro da janela de append, ordem natural, título de playlist, deduplicação,
preservação da fila para pasta vazia/inválida, próximo/anterior, avanço no EOF
e encerramento, com `AutoLoadFolder` ligado e desligado. Erros parciais e
reparse points foram cobertos por testes com sistema de arquivos controlado.
Isso não comprova reprodução de DVD/Blu-ray físico ou ISO real, remoção de uma
unidade durante a busca nem o checklist visual da interface Windows
(menu/tradução, temas, DPI, fullscreen, atalhos, drag/drop, faixas e cursor/OSC).

Correção posterior dos títulos pendentes em 2026-10-17: 696 testes aprovados,
incluindo 13 testes com libmpv normal real. A regressão verifica o campo `title`
dos 200 itens antes de reproduzir o próximo, com autoload ligado/desligado;
entradas por diálogo, clipboard, drag/drop, CLI e IPC; append sem alterar o ID,
caminho ou posição da mídia ativa; título de playlist sobre tag incorporada;
mudança de títulos em uma fila com os mesmos caminhos, sem loop de reconstrução;
e opções de cache/YouTube por item, inclusive overrides explícitos. Os testes
de URL usam redirecionamento nativo para WAV local, sem acessar serviços externos.
O build Windows Debug x64 terminou sem avisos/erros. A verificação visual do
seletor F8 na aplicação completa permanece manual; os testes consultam a mesma
playlist nativa usada por esse seletor.

Correção de desempenho do seletor em 2026-10-17: 698 testes aprovados e build
Windows Release x64 sem avisos/erros. Dois casos novos com 200 e 2.000 entradas
reproduziram a origem excessiva antes da correção e verificaram origem curta,
títulos antecipados e registro do temporário para limpeza após a mudança.
Uma sonda isolada com o frontend corrigido, libmpv normal e uma M3U de 14.892
itens, precedidos de um WAV local pausado para não acessar os streams, mediu
aproximadamente 66 ms na primeira abertura do F8 e 50–51 ms nas quatro seguintes;
com `memory://`, a abertura não havia concluído em 15 segundos. Não houve eventos
de alteração da playlist nessas aberturas. Esses tempos são evidência local;
não substituem o checklist visual da aplicação completa ou reprodução de streams.

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
`stop`, `playlist-next`, `playlist-prev` e `seek absolute+exact`). A capacidade de
busca combina `seekable` com duração finita e positiva. Fast Forward/Rewind
calculam saltos de 5 segundos a partir da posição real, limitados à duração,
e reutilizam seek sem alterar a velocidade. A sessão permanece ativa em
fullscreen; o timer de timeline usa 5 segundos durante reprodução, com
atualizações imediatas por eventos. O player continua
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
enviam seus itens preparados em M3U com títulos preenchidos por `loadlist`.
O transporte usa um arquivo temporário com origem curta; os caminhos locais
são absolutos. As opções de rede são aplicadas por item no hook nativo, sem
transformar um arquivo local em stream de rede.

Toda coleção controlada pelo frontend converge em
`PlaylistFile.PrepareForPlayback`: os títulos são normalizados antes da remoção
de duplicatas pelo caminho ou URL. Isso vale para arquivos de playlist, pasta
automática e para a fotografia estável de uma playlist nativa.

## Mídia online e serviços do yt-dlp

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
e fragmento, permanece intacta no `loadfile` direto ou na entrada da M3U preparada.

Não existe playlist paralela em C#. Com `yes-playlist`, o hook nativo recebe o
JSON plano do yt-dlp, cria a playlist do mpv e sua implementação atual compara
`index=` e `v=` com as entradas retornadas para escolher o item inicial. Assim,
`playlist-next`, `playlist-prev`, avanço automático e SMTC continuam operando
sobre a mesma fila nativa. Vídeos sem `list=` não recebem a opção de expansão.
Após a expansão, o frontend normaliza os títulos dos itens nativos e remove
duplicatas pelo caminho ou URL. Essa correção é feita na própria fila do mpv:
o item atual é preservado, recebe `force-media-title`, e os itens anteriores e
posteriores voltam por M3U temporárias com `#EXTINF`. O observador usa debounce
e registra a fotografia de endereços e títulos antes de aceitar outra execução;
assim, os eventos produzidos por `playlist-clear` e `loadlist` não realimentam
a normalização.

Essa regra foi conferida no manual e no `ytdl_hook.lua` do mpv. Em teste local
com yt-dlp e uma playlist pública de 31 itens, o hook recebeu
`--yes-playlist`, registrou a correspondência do vídeo solicitado e definiu
`playlist-start=2`. O mpv expôs `playlist-count=31`, `playlist-pos=2` e o vídeo
solicitado como item atual. Como o resultado depende de um serviço externo,
repita a validação antes de uma release.

### Validação de serviços externos

Na validação local, `--list-extractors` confirmou a presença dos extractors,
mas essa listagem não comprova acesso ao conteúdo. Testes online sem download
apresentaram estes resultados:

- **URLs públicas resolvidas:** Bilibili, Niconico, Naver, Dailymotion e
  SoundCloud.
- **Playlists:** três entradas retornadas para uma coletânea do Bilibili e três
  para uma coleção de usuário do Niconico.
- **Sem confirmação:** YouTube solicitou autenticação, sem cookies; Vimeo exigiu
  login. A live do Naver e o canal Twitch estavam offline. O extractor do Douyu
  falhou ao obter o identificador da sala.
- **Não validado online:** Douyin.

As consultas confirmaram somente os casos de resolução listados, nessa máquina
e versão do yt-dlp. Não comprovaram reprodução no mpv, navegação da playlist,
integração com SMTC ou reprodução de lives ativas; valide esses casos antes de
uma release. A suíte offline cobre parâmetros e fragmentos de URL, Unicode em
chinês, japonês e coreano, entrada pela área de transferência, IPC, `loadfile`,
domínios não reconhecidos e ausência de opções específicas por provedor.

## Diagnóstico da cadeia online

`mpvnet.exe --diagnose-components` mantém a resolução central de
`RuntimeComponents` e, sem abrir a UI ou baixar componentes, executa sondagens
curtas para informar a versão efetiva de `yt-dlp` e FFmpeg, runtimes JavaScript
compatíveis e a capability opcional de browser impersonation. O diagnóstico
não recebe URL, cookie, header de autorização, query assinada nem PO Token.

O executável Windows x64 oficial do yt-dlp é obtido do canal nightly, recomendado
pelo projeto para uso regular, e continua sendo atualizado pelo fluxo existente
de componentes, fora do caminho de reprodução. O bootstrap começa em segundo
plano depois que a janela é exibida e, ao terminar, atualiza a chave
`ytdl_hook-ytdl_path` de `script-opts` para
as próximas reproduções; uma configuração explícita do usuário continua tendo
precedência. O mesmo bootstrap baixa o
`deno.exe` x64 (Deno 2.3 ou posterior), runtime recomendado e habilitado por
padrão pelo yt-dlp. O executável oficial inclui os componentes de script EJS;
Node.js 22 ou posterior e QuickJS 2023-12-9 ou posterior continuam
sendo alternativas que exigem configuração explícita. O MPV.NET não baixa
esses runtimes a cada reprodução e não substitui um caminho do hook configurado
pelo usuário.

A configuração do caminho ocorre depois de `mpv_initialize`, quando o mapa
efetivo de `script-opts` já inclui CLI, `mpv.conf` e perfis iniciais. O frontend
preserva as outras chaves, o arquivo `script-opts/ytdl_hook.conf` e mudanças em
runtime. A atualização após bootstrap acompanha somente o caminho automático;
não recarrega mídia nem reconstrói a playlist. A opção nativa `ytdl-path` não
existe nesse contrato; o nome correto do script é `ytdl_path`.

Remote components do EJS não são habilitados silenciosamente. PO Token continua
sendo responsabilidade de provider/plugin ou configuração externa; o player não
gera, captura ou registra tokens. Browser impersonation via `curl_cffi` é uma
capability opcional detectada no yt-dlp, não uma dependência obrigatória.

Falhas de streaming são classificadas somente quando mensagens do mpv/yt-dlp
fornecem evidência. O log final separa categoria provável, mensagem original
sanitizada, componente e ação sugerida. A classificação distingue rede/DNS,
timeout, HTTP, autenticação, verificação antibot, conteúdo indisponível, região, extractor,
JavaScript/EJS, PO Token, impersonation, TLS, protocolo, demuxer e playlist;
sem evidência, permanece `Unknown`. Queries de URL e valores de cookies,
Authorization, sessão, assinatura e token são removidos antes desse resumo.
Quando não existe reconexão pendente, a categoria também seleciona uma mensagem
OSD curta por chave gettext. Todos os catálogos distribuídos possuem as mesmas
chaves; detalhes e ações sugeridas continuam apenas no log técnico sanitizado.
`Sign in to confirm you're not a bot` (inclusive com apóstrofo tipográfico ou
substituído durante a conversão de stderr CP1252/UTF-8 no Windows) tem
prioridade sobre erros genéricos de HTTP/cookies e não agenda reconexão. Conteúdo
privado e confirmação de idade continuam classificados como autenticação.
Esse desafio depende do serviço, da rede/IP e da sessão; dependências válidas
ou enumeração plana de playlist não comprovam acesso aos formatos do vídeo.

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
cache conforme o perfil escolhido, em vez de serem confundidas com HTTP progressivo;
arquivos diretos e manifestos preservam suas classificações, e o protocolo
final continua sob controle do ytdl hook, yt-dlp e mpv.

### Cache por item de playlist

O padrão `resilient` fornece 256 MiB à frente e buffer inicial/retomada de
5 segundos de mídia para HTTP progressivo, DASH e `OnlineResolver`, incluindo
URLs `ytdl://`. `balanced` fornece 128 MiB/3 segundos; `low-latency` mantém
64 MiB sem buffer inicial. Não há limitação automática de resolução/codec.

A política de `OnlineResolver` independe do domínio: páginas de YouTube,
Bilibili, Niconico, Naver, Dailymotion, Twitch, Vimeo, SoundCloud e demais
serviços seguem o mesmo perfil. A resolução permanece com os extractors
disponíveis no yt-dlp instalado; os testes de classificação não comprovam
disponibilidade ou reprodução em cada serviço.

Após criar o cliente `mpvnet`, o player registra o hook nativo `on_load` com
prioridade 50, antes de iniciar o loop de eventos. Assim, cada item criado
pelo mpv ou reinserido por `loadlist` recebe a política mesmo quando não passou
por `SendLoadfile`. O hook classifica `path`, preservando a URL original mesmo
quando o yt-dlp já transformou `stream-open-filename` em EDL ou URL assinada.
Somente opções ausentes são escritas em `file-local-options`: nomes explícitos
do `mpv.conf` ativo/linha de comando, `option-info/*/set-locally` e
`set-from-commandline` têm precedência. A política existente de `loadfile`
continua funcionando e seus valores locais não são sobrescritos.

`MpvEventSnapshot` copia nome, ID e userdata do hook antes da próxima chamada a
`mpv_wait_event`. O dispatcher chama `mpv_hook_continue` em `finally`, inclusive
quando a aplicação da política falha. Registro e continuação usam o mesmo gate
de lifetime das demais chamadas nativas; após shutdown, o cliente é destruído
antes do contexto principal, liberando hooks pendentes sem acessar handles
inválidos. Se o libmpv fornecido pelo usuário não suportar o registro, o player
registra a falha e preserva o caminho de `loadfile`.

Com `debug-mode=yes` em uma build com logging de diagnóstico, `Network cache
state` informa opções efetivas por item; `buffering-start`/`buffering-end`
informam transições, posição e duração da espera, buffer e taxa de entrada estimados.
`Network cache stream durations` informa durações numéricas de áudio/vídeo
quando disponíveis em `demuxer-cache-state/ts-per-stream`.
Esses registros não incluem URLs resolvidas, cookies ou metadados do extractor.
Na build com logging em arquivo, o cliente usa `terminal-default` para receber
os níveis por módulo: `curl=trace`, `demux=debug`, `lavf=debug` e
`ffmpeg/demuxer=debug`, salvo `msg-level` explícito. A seleção evita trace global
por pacote/quadro. Mensagens passam por sanitização antes da divisão em blocos
de até 500 caracteres; cabeçalhos curl preservam somente campos de transporte.
As propriedades de stderr/path do yt-dlp são decodificadas quando contêm uma
string JSON, antes da separação em linhas. Dumps de configuração/argumentos
são ocultados; as demais linhas preservam os detalhes após o limite de 500
caracteres que antes cortava toda a string escapada.
Propriedades de buffer são estimativas do demuxer principal, não a soma
garantida dos buffers separados de áudio e vídeo.

Validação local em 2026-10-12: 551 testes aprovados e build Windows Release
sem avisos/erros. Com libmpv normal, sondas sem interface verificaram opções
por item, próximo/anterior/seek HTTP, precedência de script e fechamento com
hook pendente. No cenário HTTP com entrega oscilante, a pausa de 1,49 s da
referência não ocorreu com `resilient`. Na amostragem dos itens 6–8 de uma
playlist YouTube, AV1/2160p foi preservado e não houve pausa durante reprodução
observada após a mudança; houve buffer inicial. A verificação online adicional
foi bloqueada por confirmação de login do YouTube. Reprodução completa e
checklist manual da interface Windows permanecem pendentes.

Validação do diagnóstico em 2026-10-15: 640 testes aprovados, inclusive com
logging em arquivo habilitado. Uma sonda nativa confirmou os níveis por módulo
e a serialização JSON de strings em `user-data`. O executável de diagnóstico
reproduziu áudio HTTP local até EOF, registrou status HTTP/trace de transporte
e ocultou cookies/cabeçalhos de teste. Uma fixture Lua verificou a saída do
yt-dlp com múltiplas linhas e mais de 1.200 caracteres; `msg-level` explícito
no `mpv.conf` de `--config-dir` foi preservado. Isso valida a captura. A análise
da falha VP9/transferência com esse diagnóstico está descrita a seguir.

A análise do log de 2026-10-15 identificou uma transferência HTTP/3 encerrada
com `ERR_DRAINING`/`Failure when receiving data from the peer` às 10:39:57 e
erros de bitstream VP9 a partir de 10:48:05. O encerramento às 10:52:09 foi
`QUIT`, sem erro final. A distância entre esses eventos não comprova que a
falha de rede causou os erros de vídeo. A sonda sem interface com o mesmo
libmpv v3 e a mesma página reproduziu o intervalo 515–545 s sem erro de
decodificação, mas a fonte entregou H.264, inclusive com `ytdl-format=616+251`;
portanto, não reproduziu o caso VP9 do log.

O diagnóstico agora distingue `NetworkTransport` de `Decoder` e persiste um
resumo por categoria/item nas builds com `EnableFileLogging=true`, mesmo sem `debug-mode`, quando recebe `ERROR` ou
`FATAL`. O resumo não envia comandos de reprodução; a recuperação continua
restrita ao fluxo de erro final e às regras de live já existentes. Status HTTP
exige evidência textual de HTTP, evitando interpretar tamanho de pacote ou
valores do codec como status. Caminhos assinados absolutos/relativos são
ocultados antes da divisão das mensagens em blocos.

Para comparar uma falha HTTP/3, o mpv oferece `--curl-http-version=2`, que
limita a negociação e permite HTTP/1.1 conforme o servidor. É uma opção de
diagnóstico, sem mudança automática do padrão de transporte ou do formato
selecionado pelo yt-dlp. Consulte o [manual de rede do mpv](https://mpv.io/manual/master/#network-backend-libcurl).
Se a falha se repetir na instalação, a mesma mitigação pode ser mantida no
`mpv.conf` ativo com `curl-http-version=2`, após fazer backup do arquivo. Isso
limita o transporte dessa configuração a HTTP/2 ou inferior; remova a linha ou
use `curl-http-version=auto` para restaurar a negociação automática. O padrão
global do frontend permanece inalterado. Essa opção não garante correção de
bitstream VP9, nem impede respostas HTTP 429 ou exigências de autenticação.
O caso VP9 original e o checklist manual completo da interface Windows
continuam sem comprovação de correção.

Validação das correções do log em 2026-10-15: 656 testes aprovados com logging
padrão e 656 com logging em arquivo habilitado. Build Windows Release com
dependências nativas e catálogos preparados: zero avisos/erros. O executável
Release de diagnóstico reproduziu áudio HTTP local até EOF e ocultou caminhos
assinados absolutos e relativos, cookies e cabeçalhos da fixture Lua/HTTP.

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

