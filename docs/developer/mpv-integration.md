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
- `src/MpvNet/Integration/Mpv/MpvClient.cs` - wrapper de cliente, comandos, propriedades e eventos;
- `src/MpvNet/Integration/Mpv/Player.cs` - estado principal do player;
- `src/MpvNet/Integration/Mpv/Player.Initialization.cs` - inicialização e configuração inicial do mpv/libmpv;
- `src/MpvNet/Integration/Mpv/Player.ObservedProperties.cs` - propriedades observadas;
- `src/MpvNet/Integration/Mpv/Player.Events.cs` - eventos vindos do mpv;
- `src/MpvNet/Integration/Mpv/Player.Lifecycle.cs` - loop principal, shutdown e destruição de handles;
- `src/MpvNet/Integration/Mpv/Player.MediaLoading.cs` - carregamento de mídia, playlists, URLs, ISO/DVD/BD e pasta automática;
- `src/MpvNet/Integration/Mpv/Player.Capabilities.cs` - perfis, decoders, protocolos, demuxers e criação de clientes adicionais.

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
`Destroyed`). Os loops nativos recebem o cancelamento da instância e usam uma
espera limitada, permitindo que `Destroy` cancele e aguarde as tarefas antes de
encerrar os handles. O handle principal inicializado é encerrado com
`mpv_terminate_destroy`; handles de clientes são destruídos antes dele.

As tarefas de playlist e metadata são serializadas por instância do player e
canceladas durante o fechamento. Isso impede que uma leitura atrasada de
MediaInfo ou uma normalização antiga continue consultando libmpv depois da
destruição do player, mantendo essas operações serializadas durante a troca de
mídia.

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


