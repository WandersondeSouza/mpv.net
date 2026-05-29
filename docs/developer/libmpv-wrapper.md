# Wrapper e Integração com libmpv

## Objetivo

Documentar como o mpv.net se comunica com `libmpv-2.dll`.

---

# Arquivos principais

| Arquivo | Papel |
| --- | --- |
| `src/MpvNet/Native/LibMpv.cs` | Declarações P/Invoke para `libmpv-2.dll`, enums e structs nativas. |
| `src/MpvNet/MpvClient.cs` | Wrapper de cliente mpv: comandos, propriedades, eventos e marshaling. |
| `src/MpvNet/Player.cs` | Cliente principal: inicialização do mpv, estado de reprodução e integração com a aplicação. |

---

# Ciclo de vida

Fluxo principal em `Player.Init`:

1. chama `App.ApplyShowMenuFix()`;
2. cria contexto com `mpv_create`;
3. registra eventos via `mpv_request_event`;
4. solicita mensagens de log;
5. quando há janela, inicia `MainEventLoop`;
6. configura propriedades iniciais;
7. define `config-dir` como `Player.ConfigFolder`;
8. carrega `input.conf` como `memory://` quando há conteúdo;
9. processa argumentos de linha de comando pré-inicialização;
10. chama `mpv_initialize`;
11. cria cliente secundário com `mpv_create_client(MainHandle, "mpvnet")`;
12. solicita logs no cliente secundário;
13. quando há janela, inicia `EventLoop`;
14. define dados de frontend em `user-data/frontend/*`;
15. registra observadores de propriedades;
16. dispara `Initialized`.

Encerramento:

- `Player.Destroy()` chama `mpv_destroy` para `MainHandle`, `Handle` e clientes adicionais.

---

# Handles e clientes

`MainPlayer` herda de `MpvClient`.

Handles observados:

- `MainHandle`: contexto principal retornado por `mpv_create`;
- `Handle`: inicialmente recebe `MainHandle`, depois passa a ser o cliente `mpvnet`;
- `Clients`: lista de clientes adicionais criados por `mpv_create_client`.

Essa distinção é importante porque comandos, eventos e propriedades podem passar pelo cliente ativo.

---

# Configuração inicial enviada ao mpv

Durante `Player.Init`, o projeto define várias propriedades antes de inicializar o mpv, incluindo:

- `terminal`;
- `input-terminal`;
- `force-window`;
- `wid`;
- `osd-duration`;
- `input-default-bindings`;
- `input-builtin-bindings`;
- `input-media-keys`;
- `autocreate-playlist`;
- `media-controls`;
- `idle`;
- `screenshot-directory`;
- `osd-playing-msg`;
- `osc`;
- `config-dir`;
- `config`;
- `input-conf`.

Alterações nessa lista podem quebrar comportamento esperado, scripts, atalhos ou integração visual.

---

# Eventos

`MpvClient.EventLoop` usa `mpv_wait_event` e traduz eventos nativos para eventos .NET.

Eventos tratados:

- `MPV_EVENT_SHUTDOWN`;
- `MPV_EVENT_LOG_MESSAGE`;
- `MPV_EVENT_CLIENT_MESSAGE`;
- `MPV_EVENT_VIDEO_RECONFIG`;
- `MPV_EVENT_END_FILE`;
- `MPV_EVENT_FILE_LOADED`;
- `MPV_EVENT_PROPERTY_CHANGE`;
- `MPV_EVENT_GET_PROPERTY_REPLY`;
- `MPV_EVENT_SET_PROPERTY_REPLY`;
- `MPV_EVENT_COMMAND_REPLY`;
- `MPV_EVENT_START_FILE`;
- `MPV_EVENT_AUDIO_RECONFIG`;
- `MPV_EVENT_SEEK`;
- `MPV_EVENT_PLAYBACK_RESTART`.

Eventos expostos por `MpvClient` incluem `ClientMessage`, `LogMessage`, `EndFile`, `Shutdown`, `StartFile`, `FileLoaded`, `VideoReconfig`, `AudioReconfig`, `Seek` e `PlaybackRestart`.

---

# Propriedades observadas

`MpvClient` mantém dicionários de callbacks por tipo:

- `PropChangeActions`;
- `IntPropChangeActions`;
- `BoolPropChangeActions`;
- `DoublePropChangeActions`;
- `StringPropChangeActions`.

`Player.Init` observa pelo menos:

- `pause`;
- `video-rotate`;
- `playlist-pos`.

Essas propriedades atualizam estado interno e notificam UI/outros componentes.

---

# Comandos

`MpvClient` oferece duas formas principais:

| Método | Uso |
| --- | --- |
| `Command(string command)` | Envia comando textual para `mpv_command_string`. |
| `CommandV(params string[] args)` | Envia comando vetorial para `mpv_command`. |

`CommandV` deve ser preferido quando há argumentos separados, porque reduz risco de parsing incorreto por espaços, aspas e caminhos.

---

# Propriedades

O wrapper expõe getters/setters tipados:

- bool;
- int;
- long;
- double;
- string.

Internamente usa formatos mpv como:

- `MPV_FORMAT_FLAG`;
- `MPV_FORMAT_INT64`;
- `MPV_FORMAT_DOUBLE`;
- `MPV_FORMAT_STRING`.

---

# Áreas críticas

## Threading

Loops de eventos rodam em tarefas separadas. Mudanças podem causar travamento, corrida de estado ou eventos perdidos.

## Marshaling

`MpvClient` converte structs, ponteiros e strings UTF-8. Erros de marshaling podem causar crash nativo.

## Inicialização

Algumas propriedades precisam ser definidas antes de `mpv_initialize`. Mover esse processamento pode alterar o comportamento.

## Compatibilidade

Scripts, configurações e atalhos esperam comportamento compatível com mpv. Mudanças em comandos/propriedades devem ser tratadas como alto risco.

---

# Checklist para alterações nessa área

- Validar abertura de arquivo local.
- Validar URL.
- Validar playlist.
- Validar pause/play.
- Validar seek.
- Validar fullscreen.
- Validar scripts.
- Validar `input.conf`.
- Validar encerramento sem processo preso.
- Comparar comportamento com mpv quando a alteração envolver opção/comando nativo.
