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

## Extensões, playlists e capacidade de decodificação

`src/MpvNet/Media/FileTypes.cs` mantém as extensões usadas pelo filtro de
abertura, classificação de entradas, descoberta de mídias na pasta e registro
de associações do instalador. O manifesto MSIX declara separadamente os tipos
de arquivo que o Windows pode encaminhar ao pacote; os testes comparam esse
manifesto com as listas padrão do player para evitar divergências. O instalador
registra áudio, vídeo e playlists; imagens permanecem opt-in no desktop. O ZIP
portátil não registra associações no Windows.

O frontend também expande playlists locais `.m3u`, `.m3u8`, `.pls`, `.xspf`,
`.asx`, `.wpl`, `.cue` e `.jspf`, aceitando itens reproduzíveis de áudio/vídeo
ou URLs. Imagens são entradas diretas do player, não itens expandidos por esses
formatos de playlist.

### Matriz de filtros, descoberta e associações

| Tipo | Diálogo e linha de comando | Descoberta em pastas | Instalador desktop | Microsoft Store |
| --- | --- | --- | --- | --- |
| Vídeo | `GetSupportedVideoExts()` combina `DefaultVideoExts` com `video-exts` do libmpv. Extensões reconhecidas passam pelo filtro e pela linha de comando; caminhos existentes também são encaminhados ao mpv. | Ao abrir áudio ou vídeo, `GetMediaFiles` inclui vídeos, áudios e playlists reconhecidos. | O Inno Setup registra vídeo automaticamente; o registro consulta a lista de vídeo exposta pelo libmpv, com defaults como fallback. | O manifesto declara a lista estática `DefaultVideoExts` e as oito extensões de playlist dentro da associação de vídeo. Não altera o aplicativo padrão do usuário. |
| Áudio | `GetAudioExts()` usa `audio-exts` do libmpv ou `DefaultAudioExts` quando a propriedade está vazia. O mesmo conjunto alimenta o filtro e a classificação por extensão. | Ao abrir áudio ou vídeo, inclui áudios, vídeos e playlists reconhecidos. | O Inno Setup registra áudio automaticamente a partir da lista de áudio do libmpv, com defaults como fallback. | O manifesto declara `DefaultAudioExts`. |
| Imagem | `GetImgExts()` usa `image-exts` do libmpv ou `DefaultImageExts` como fallback. A lista está no diálogo; a linha de comando e a entrada de mídia também aceitam imagens. | Ao abrir uma imagem, a descoberta automática fica restrita a outras imagens da pasta. Imagens não entram na expansão das playlists locais. | Não é registrada automaticamente. O usuário pode habilitar a associação em `Config > Setup > Register image file associations`. | O manifesto declara `DefaultImageExts` em associação própria para `Abrir com` e `Aplicativos padrão`, sem definir um padrão automaticamente. |
| Playlist | O filtro, a linha de comando e `PlaylistFile.Read` cobrem `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue` e `.jspf`. Entradas locais relativas são resolvidas em relação à playlist; URLs são preservadas. | É incluída na descoberta de pasta quando a abertura parte de áudio/vídeo; não é misturada à descoberta de imagens. | O Inno Setup registra as oito extensões automaticamente. | As oito extensões aparecem na associação de vídeo do manifesto. |

O ZIP portátil não registra associações no Windows. O manifesto MSIX usa as
listas estáticas versionadas no projeto; o filtro e o instalador desktop também
podem receber extensões específicas da build pelas propriedades `video-exts`,
`audio-exts` e `image-exts` do libmpv. No diálogo do libmpv distribuído nesta
auditoria, as propriedades expuseram `.ivf`, `.dff`, `.dsf`, `.mp1`, `.heic`,
`.heif` e `.qoi` além das listas estáticas. Esses nomes não foram promovidos aos
defaults nem ao manifesto: a presença no filtro, sem reprodução de amostra
confirmada, não basta para declarar suporte.

Uma extensão reconhecida informa como o frontend trata ou associa o caminho;
ela não comprova que o arquivo contém um codec ou container reproduzível. A
capacidade depende dos demuxers e decoders presentes na revisão empacotada de
libmpv/FFmpeg e deve ser verificada com arquivos de amostra. As DLLs normal e
x86-64-v3 são variantes de CPU da mesma revisão upstream, e a seleção de uma
delas não é uma declaração de suporte a formatos. `mpvnet.exe
--diagnose-libmpv` confirma a seleção/carregamento do binário; a reprodução
real de amostras é uma verificação separada.

### Binários identificados na auditoria de 28/10/2026

Os dois arquivos publicados em `win-x64` foram identificados como PE AMD64 e
reportam a mesma versão de arquivo/produto e o mesmo build upstream:
`v0.41.0-1227-ge471496c5` (`20261021-git-e471496c56`). São variantes de CPU,
não builds com listas de codecs diferentes.

| Arquivo | Variante | SHA-256 |
| --- | --- | --- |
| `libmpv-2.dll` | x86-64 normal | `04E4D27DC385DC18DDD93FBE69C75E635B864F3C6EC53A9122DF54C2F7842F4A` |
| `libmpv-2-v3.dll` | x86-64-v3 | `9197A10DD712A15D652CCB2E915F47E377605599BC9FAA9A137BB8A1EF2BB6C4` |

Na verificação manual, ambas reproduziram amostras locais PNG, WAV e MP4. Uma
playlist M3U8 com entrada local WAV também abriu e carregou a mídia. Isso
confirma os caminhos básicos de imagem, áudio, vídeo e playlist nesta build; não
é uma prova exaustiva de todos os codecs e containers anunciados pelo mpv.

---

# Wrapper e comunicação

Arquivos principais:

- `src/MpvNet/Native/LibMpv.cs` - P/Invoke para `libmpv-2.dll`;
- `src/MpvNet/Native/LibMpvRuntime.cs` - seleção da variante e diagnóstico de carregamento;
- `src/MpvNet/Integration/Mpv/MpvClient.cs` - wrapper de cliente, comandos, propriedades e eventos;
- `src/MpvNet/Integration/Mpv/MpvClient.Artwork.cs` - captura limitada de pixels de capa e inclusão assíncrona de `albumart` com seleção local ao arquivo;
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

Para áudio local, a camada Windows resolve a arte em segundo plano e pode
acrescentar uma imagem validada ao arquivo corrente com \`video-add\` assíncrono.
O frontend SMTC usa a mesma imagem quando está ativo. As regras de prioridade,
cancelamento e cache estão em [Integração Windows](windows-ui.md).

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

### Cache automático por item

A política usa somente `App.AutomaticNetworkCache`, ativado por padrão.
`NetworkCacheProfile`/`--network-cache-profile` são aliases de compatibilidade
que convertem os perfis antigos em ativado/desativado, sem selecionar políticas.
A migração e o backup do editor estão em [Configuração](configuration.md#mpvnetconf).
O editor apresenta **Cache automático de streaming**, traduzido nos 19 catálogos;
as opções nativas avançadas do mpv permanecem disponíveis.

`BuildLoadfileArgs` não injeta mais cache. `on_load` de prioridade 50 aplica a
política depois do `ytdl_hook` e das opções locais anteriores. Isso evita que
valores do frontend pareçam escolhas explícitas e unifica `loadfile`, `loadlist`,
playlists expandidas e navegação. `on_preloaded` refina a classificação antes de
iniciar decoders; `file-loaded` reavalia propriedades que só se tornam disponíveis
mais tarde. Nenhuma mídia é reiniciada. Escritas idênticas da política são evitadas.
Se uma build externa não oferecer hooks, a reprodução nativa continua, sem
injetar automaticamente cache no comando como fallback.

`NetworkCacheMedia` combina `path`, `stream-open-filename`, `file-format`,
duração/seekability e os campos `is_live`, `duration`, `protocol` e
`requested_formats` do resultado já disponível do yt-dlp. A leitura do resultado
é limitada a 4 MiB e uma vez por item, inclusive quando resolvido em `on_load_fail`.
URLs assinadas e o JSON completo não são retidos nem registrados. Protocolos de
fragmentos permitem identificar HLS/DASH mesmo dentro de EDL. O resolvedor e o
mpv continuam responsáveis por abrir o conteúdo; não há requisição de sondagem.

Uma página, extensão de manifesto ou duração de DVR não prova VOD.
`is_live=true` confirma live; `false` confirma VOD. Sem esse campo, duração
positiva e seekability confirmam fontes não manifestas; HLS/DASH também exigem
EOF do demuxer observado numa fase de classificação. Um manifesto ainda incerto
usa os tetos do grupo HTTP/HTTPS sem forçar buffer inicial. Fontes ainda sem
classificação de protocolo/formato mantêm o grupo menor até serem identificadas. RTSP/RTMP/datagramas são
tratados como contínuos. Arquivos locais nunca são promovidos a rede.

| Grupo de mídia | À frente | Para trás | Prefetch em segundos | Espera inicial/retomada |
| --- | ---: | ---: | ---: | --- |
| HTTP/HTTPS progressivo, HLS/DASH VOD ou live, yt-dlp identificado e FTP/FTPS/SFTP | 512 MiB | 64 MiB | 240 | VOD: inicial sim, 3 s; live/incerto: inicial não, 1 s |
| RTSP / RTMP / RTMPS / UDP / TCP / SRT / SRTP | 128 MiB | 16 MiB | 90 | inicial não; 1 s |
| Fontes ainda não identificadas | 64 MiB | 8 MiB | 70 | inicial não; 1 s |
| Arquivo local | nenhuma opção automática | — | — | — |

HTTP e HTTPS compartilham o classificador e a política: TLS não reduz os tetos.
Uma página de serviço ainda sem resultado do resolvedor, ou um protocolo genérico
sem identificação, usa o grupo de 64 MiB/70 s; depois da identificação, yt-dlp
entra no grupo maior. A classificação VOD/live continua determinando a espera,
separadamente dos tetos de bytes/tempo. Uma janela de manifesto curta ou a
produção em tempo real pode impedir preenchimento maior. Os 240 s não são
espera obrigatória, reserva de memória nem atraso imposto à live.

São tetos do demuxer, não reservas ou duração garantida. `cache=yes` e
`cache-pause=yes` habilitam o mecanismo nativo; `demuxer-max-bytes` e
`demuxer-max-back-bytes` limitam dados à frente/para trás; `cache-secs` e
`demuxer-readahead-secs` limitam prefetch. Como o mpv usa o maior dos dois limites
temporais, uma escolha explícita em qualquer deles impede injeção no outro.
A política não altera `network-timeout`, transporte, resolução ou codec.
`cache-on-disk` nunca é habilitado automaticamente, inclusive para FTP/SFTP.
Quando já está explicitamente ligado, toda a política automática recua: no
mpv, os limites em bytes passam a medir metadados e não limitam o arquivo em disco.

A precedência combina nomes de CLI/`mpv.conf`, `option-info/*/set-locally`,
`set-from-commandline` e comparação com `default-value`. `include`, `profile`
selecionado externamente e `use-filedir-conf` fazem a política recuar por
inteiro quando a origem indireta não é inspecionada. Opções em perfis declarados
no arquivo também são tratadas conservadoramente como explícitas. Depois de uma
escrita automática, o player guarda o valor efetivo e deixa de controlar aquela
opção se observar uma alteração externa. Uma escrita de script idêntica ao valor
automático não pode ser atribuída pela API; para fixar esse valor, declare-o em
CLI/`mpv.conf`, antes do hook como opção local, ou desligue a política.

`NetworkCacheSession` reúne contagem/duração de buffering numa política pura,
sem timers/tarefas/handles. O observador de `demuxer-cache-duration` aciona no
máximo uma amostra a cada 5 s; lê taxa de entrada, bitrates das faixas presentes
e durações individuais de áudio/vídeo em `ts-per-stream`. A menor duração das
faixas é usada; valores ausentes/inválidos impedem adaptação. São estimativas do
demuxer principal e podem não cobrir demuxers externos. Não se inventa bitrate.

Após pelo menos três amostras consecutivas válidas e 30 s desde a última
mudança, duas novas interrupções podem aumentar `cache-pause-wait` em 1 s,
até 8 s em VOD ou 2 s em live/incerto. A taxa estimada deve exceder 115% do
bitrate. Margem insuficiente/incerta gera somente diagnóstico. Com 120 s de
estabilidade observada e buffer suficiente, a espera diminui 1 s até a base
3/1 s. Intervalos de amostra superiores a 15 s reiniciam a evidência de
estabilidade. Os tetos em bytes não crescem em runtime. Pausa, startup e seek
não contam como underrun; fim/troca de item e dispose limpam o estado. Não há
watchdog de vídeo nem seek/reload causado pela adaptação.

O mecanismo existente `LiveStreamRecoveryPolicy` mantém três tentativas e
backoff 1/2/4 s para falhas transitórias comprovadas de streams contínuos ou
manifests live carregados. Evidência de live do resolvedor também atende fontes
com URL opaca e janela DVR finita, sempre recarregando a entrada original.
Sem essa evidência, permanece o critério conservador anterior para HLS/DASH
diretos sem duração finita. Autenticação/403, antibot, URL explicitamente
expirada (`ExpiredUrl`), manifesto inválido (`Manifest`) e decoder não geram
retry. Não há reutilização automática de URL assinada identificada como expirada.

`MpvEventSnapshot` e o dispatcher mantêm a continuação dos hooks em `finally`
e o gate de lifetime. O estado novo fica na thread de eventos e é descartado
no dispose após aguardar os loops. Nenhum observador extra sobrevive ao player.

Os contratos foram conferidos na libmpv normal efetivamente preparada pelo
build de 08/11/2026, `mpv v0.42.0-5-g55d3feb45`, e nas fontes dessa revisão:
[opções](https://github.com/mpv-player/mpv/blob/55d3feb45/DOCS/man/options.rst),
[hooks e propriedades](https://github.com/mpv-player/mpv/blob/55d3feb45/DOCS/man/input.rst),
[atualização das opções do demuxer](https://github.com/mpv-player/mpv/blob/55d3feb45/demux/demux.c).
As opções usadas são atualizáveis nessa revisão; isso também foi exercitado com
`file-local-options` no libmpv real. Nenhuma opção de abertura imutável foi
adicionada. Builds externas continuam sujeitas à disponibilidade de propriedades;
ausência de evidência faz o frontend recuar.

### Validação do cache unificado — 08/11/2026

A branch inicial era `master`, HEAD `02f4f656a9e5ea5a0d004a293fce1e61fdee1067`,
sem alterações locais. A suíte original aprovou 1.012 testes. Causas confirmadas:
os perfis ainda controlavam limites, `loadfile` e `on_load` aplicavam cache em
fases distintas, a identificação dependia da entrada original e faltavam
métricas com histerese. O editor não oferecia o toggle automático.

A suíte da primeira implementação aprovou **1.074 testes**, sem falhas/erros/skip. Os dois avisos
`xUnit1031` em `CoreCoverageTests` já existiam no baseline. Casos novos cobrem
migração, limites, scripts/CLI/perfis/configuração, limites temporais pareados,
cache em disco explícito, metadados ausentes, EOF, dados sanitizados, adaptação,
URL expirada e manifesto inválido. Os testes nativos usam HTTP loopback real
para VOD/restauração e redirecionamento para mídia local para simular resultados
do resolvedor HLS/DASH; estes últimos não comprovam os transportes externos.
Um teste passou a aguardar a restauração da opção após Stop, pois `idle-active`
pode ser publicado antes dessa restauração nativa.

Build Windows Debug com preparação normal: sem avisos/erros, 19 catálogos
compilados, 807 entradas por catálogo. Os seis testes Python de localização
passaram, assim como os contratos de pacote de localização (pasta, ZIP, Store
aninhado e rejeição de catálogos ausentes/vazios). Em configuração isolada sob `artifacts/cache-qa`, a interface foi
verificada nos temas escuro/claro: toggle em português, migração `off` → Não,
alteração para Sim, persistência sem perfil antigo, backup e encerramento normal.
Nenhum arquivo da instalação do usuário foi alterado.

Medições exploratórias e pendências de desempenho são registradas abaixo.
Não se infere disponibilidade de YouTube, transporte HLS/DASH/RTSP/RTMP real,
latência de lives, funcionamento em outros DPIs ou estabilidade prolongada a
partir desses testes. Permanecem externos o checklist completo de arquivo/URL,
playlist/pasta, drag/drop, menu, fullscreen, faixas/legendas, cursor/OSC e stress
da aplicação completa. Essas verificações não são substituídas pelo build.

Revisão final da primeira implementação: 173 testes direcionados de cache/recovery/diagnóstico passaram
após o último ajuste, além da suíte completa acima. Foram conferidos 78 links
locais/âncoras nos documentos alterados e `git diff --check` sem problemas.

### Arquivos da implementação unificada

| Área | Arquivos alterados |
| --- | --- |
| Migração e configuração | `App.cs`, `Configuration/NetworkCacheConfiguration.cs`, `MpvNet.Windows/Configuration/Conf.cs`, `WPF/ConfWindow.xaml.cs`, `Resources/editor_conf.txt` |
| Política e identificação | `Media/MediaInput.cs`, `Media/NetworkCacheSession.cs` |
| Integração | `Integration/Mpv/Player.NetworkCache.cs`, `Player.Initialization.cs`, `Player.Events.cs`, `Player.ObservedProperties.cs`, `Player.MediaLoading.cs`, `Player.Lifecycle.cs` |
| Diagnóstico | `Media/StreamingFailureDiagnostics.cs` |
| Testes | `NetworkCachePolicyTests.cs`, `NetworkCacheSessionTests.cs`, `NetworkCacheNativeTests.cs`, `NativePlaylistTitleTests.cs`, `MediaInputTests.cs`, `StreamingFailureDiagnosticsTests.cs`, `Program.cs` |
| Traduções | `lang/source.pot` e os 19 arquivos em `lang/po/` |
| Documentação e orientações | README, manual, configuração, logs, guia operacional, integração mpv, interface Windows, localização, `AGENTS.md`, `.ai/README.md`, skill de manutenção e agentes de libmpv/configuração/UI/componentes |

Os nomes curtos de código são relativos a `src/MpvNet/`, salvo a área Windows
indicada; os testes estão em `src/MpvNet.Tests/`. Não foram alterados runtime
components, formatos de configuração nativos, assets visuais, codecs ou o motor
de reprodução. Não houve commit, push, publicação ou deploy desta implementação.

### Ampliação e agrupamento dos tetos — 08/11/2026

Após solicitação para ampliar MiB e tempo de leitura antecipada, os limites foram
agrupados nas três faixas acima. A baseline desta revisão era `master`, HEAD
`232e84a62a918b19a523f94d6e7935004bca1a9c`, sem alterações locais. Os arquivos
alterados são `MediaInput.cs`, `NetworkCachePolicyTests.cs`,
`NetworkCacheNativeTests.cs`, manual, esta documentação e orientações locais da
skill/agente de libmpv. Não se alteraram toggle, migração, recuperação,
classificação, espera inicial/adaptativa ou precedência de opções explícitas.

Os testes da política verificam os tetos à frente/para trás e os dois limites
em segundos, a equivalência HTTP/HTTPS e o fallback de páginas ainda não
resolvidas. Os testes com libmpv real exercitam os valores efetivos dos grupos,
sem nova espera inicial nos transportes contínuos. As fixtures redirecionam os
protocolos para WAV local; comprovam aplicação/restauração de opções, sem
comprovar conectividade FTP/FTPS/SFTP/RTSP/RTMP/SRT ou TLS real.

Validação local da revisão: **1.094 testes aprovados**, sem falhas, erros ou
skip, usando libmpv `v0.42.0-5-g55d3feb45`. A rodada direcionada anterior aprovou
197 casos de cache/classificação/playlist/recovery; a suíte final inclui os oito
casos adicionais de equivalência HTTP/HTTPS. Permanecem apenas os dois avisos
`xUnit1031` já existentes em `CoreCoverageTests`. Foram conferidos 35 links
locais/âncoras e `git diff --check` sem problemas.

Um grupo maior pode consumir mais RAM se houver dados disponíveis. Os tetos são
do demuxer, não um limite global do processo: faixas/demuxers separados,
decodificadores e GPU podem consumir memória adicional. A janela do
servidor, o bitrate e o menor dos limites efetivamente alcançados continuam
restringindo o preenchimento. Os tetos novos ainda precisam de medições
prolongadas de memória, buffering e atraso de live; a medição anterior abaixo
não foi reaproveitada como prova desses valores. O checklist manual completo
da aplicação continua independente dos testes nativos sem interface.

### Medição exploratória anterior ao agrupamento — 08/11/2026

Comparação local em processos separados, libmpv normal `v0.42.0-5-g55d3feb45`,
sem UI, `vo=null`, `ao=null`, `hwdec=no`. São amostras sintéticas H.264/AAC,
8 s, 24 fps, 1920×1080 a 6 Mbit/s e 3840×2160 a 12 Mbit/s de alvo, geradas com
FFmpeg/testsrc2, libx264 ultrafast, dois threads e `faststart`. O servidor HTTP
loopback aceita ranges e entrega a 1,3 vez o tamanho médio por segundo da mídia;
no cenário oscilante, interrompe a entrega por 3 s após 3 s de dados enviados.
Cada célula abaixo é **anterior → automático**, uma única execução por caso.

A referência usa os valores antigos de `resilient` (256 MiB, espera de 5 s).
O caso automático reproduziu as opções anteriores ao agrupamento: 512 MiB à
frente, 32 MiB para trás, 120 s de prefetch e espera de 3 s após identificar VOD.
Esses números não medem o grupo atual de 512/64 MiB e 180 s. Essa sonda compara
as opções nativas; não executa a política C# de histerese nem mede o startup da
UI/.NET. O script e JSON brutos ficaram localmente em
`artifacts/cache-qa/cache-benchmark.py` e `cache-benchmark-results.json`,
artefatos ignorados pelo Git desta execução.

| Amostra | Início (s) | CPU do processo (s) | Pico do processo (MiB) | Rebuffer após início |
| --- | ---: | ---: | ---: | ---: |
| 1080p, estável | 4,269 → 2,771 | 1,078 → 0,859 | 116,3 → 116,0 | 0 → 0 |
| 1080p, pausa de rede | 7,326 → 2,731 | 1,016 → 0,859 | 116,3 → 116,3 | 0 → 0 |
| 4K, estável | 4,267 → 2,759 | 2,766 → 2,812 | 279,8 → 279,4 | 0 → 0 |
| 4K, pausa de rede | 7,330 → 2,760 | 2,719 → 2,984 | 279,6 → 279,7 | 0 → 0 |

O tempo total de rebuffer após o início foi 0 s em todos os casos. O pico inclui
Python/servidor/decoders/libmpv no processo; não representa RAM do frontend ou
VRAM. A redução de início é compatível com a menor margem inicial (5 → 3 s).
Amostras tão curtas não preenchem os tetos e não comprovam benefício de 512 MiB,
melhoria estatística de CPU/memória, interrupções por hora ou resistência a falhas
longas. Não houve reconexão a medir. A latência de live, serviços externos,
transporte HLS/DASH/RTSP/RTMP real e execução prolongada continuam sem medição.

### Registros históricos de cache e diagnóstico

Os resultados datados a seguir descrevem versões anteriores e não medem a
política automática atual.

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

