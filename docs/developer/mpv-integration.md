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
clientes são destruídos antes dele.

As tarefas de playlist e metadata são serializadas por instância do player e
canceladas durante o fechamento. Uma tarefa atrasada pode terminar sem executar
`GetProperty*`, `SetProperty*` ou `Command*` depois do início de `Destroy`,
mantendo essas operações serializadas durante a troca de mídia e o encerramento.

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
enviam seus itens individualmente por `loadfile`, permitindo opções por item
sem transformar um arquivo local em stream de rede.

## YouTube e playlists remotas

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

Essa decisão foi confrontada em 2026-09-21 com o manual e o `ytdl_hook.lua`
atuais do mpv. A validação local usou yt-dlp `2026.08.19`; expansão e posição
reais continuam dependentes da resposta externa do YouTube e devem ser
revalidadas manualmente antes de uma release.

## Diagnóstico da cadeia online

`mpvnet.exe --diagnose-components` mantém a resolução central de
`RuntimeComponents` e, sem abrir a UI ou baixar componentes, executa sondagens
curtas para informar a versão efetiva de `yt-dlp` e FFmpeg, runtimes JavaScript
compatíveis e a capability opcional de browser impersonation. O diagnóstico
não recebe URL, cookie, header de autorização, query assinada nem PO Token.

O executável oficial do yt-dlp continua sendo atualizado pelo fluxo existente
de componentes, fora do caminho de reprodução. Para os desafios atuais do
YouTube, Deno 2.3 ou posterior é a opção recomendada e habilitada por padrão
pelo yt-dlp; Node.js 22 ou posterior e QuickJS 2023-12-9 ou posterior exigem
configuração explícita do yt-dlp. O MPV.NET apenas detecta esses runtimes: não
adiciona um segundo gerenciador, não os baixa a cada reprodução e não substitui
um `ytdl-path` configurado pelo usuário.

Remote components do EJS não são habilitados silenciosamente. PO Token continua
sendo responsabilidade de provider/plugin ou configuração externa; o player não
gera, captura ou registra tokens. Browser impersonation via `curl_cffi` é uma
capability opcional detectada no yt-dlp, não uma dependência obrigatória.

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


