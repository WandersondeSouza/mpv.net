# Sistema de Comandos do mpv.net

## Objetivo

Documentar o funcionamento do sistema de comandos do mpv.net e os comandos específicos observados no código atual.

---

# Visão geral

O mpv.net aceita comandos nativos do mpv e adiciona comandos próprios do frontend.

Comandos específicos do mpv.net normalmente são acionados por:

```text
script-message-to mpvnet <comando> [argumentos]
```

Arquivos principais:

- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/GuiCommand.cs`;
- `src/MpvNet/InputHelp.cs`;
- `src/MpvNet/InputConf.cs`;
- `src/MpvNet/CommandLine.cs`.

---

# Tipos de comandos

## Comandos nativos do mpv

São enviados diretamente ao libmpv, por exemplo:

- `pause`;
- `seek`;
- `cycle`;
- `set`;
- `loadfile`;
- `show-text`;
- `script-binding`.

## Comandos específicos do mpv.net

São comandos registrados em `Command.cs` e `GuiCommand.cs`.

Esses comandos cobrem:

- UI;
- arquivos e playlist;
- janela;
- configuração;
- integração com Windows;
- informações do mpv;
- comandos de compatibilidade/deprecated.

---

# Comandos do núcleo

Fonte: `src/MpvNet/Command.cs`.

| Comando | Função |
| --- | --- |
| `open-conf-folder` | Abre a pasta de configuração atual. |
| `play-pause` | Alterna play/pause ou carrega arquivo recente quando não há playlist. |
| `shell-execute` | Executa/abre arquivo ou URL via shell do Windows. |
| `show-text` | Mostra texto OSD com duração e tamanho de fonte opcionais. |
| `cycle-audio` | Alterna faixa de áudio conhecida pelo player. |
| `cycle-subtitles` | Alterna legenda conhecida pelo player. |
| `playlist-first` | Vai para o primeiro item da playlist. |
| `playlist-last` | Vai para o último item da playlist. |

Comandos deprecated ainda registrados:

| Comando | Observação |
| --- | --- |
| `playlist-add` | Mantido para compatibilidade. |
| `show-progress` | Delegado ao comando mpv `show-progress`. |
| `playlist-random` | Mantido para compatibilidade. |

---

# Comandos de UI/Windows

Fonte: `src/MpvNet.Windows/GuiCommand.cs`.

| Comando | Função |
| --- | --- |
| `add-to-path` | Adiciona mpv.net ao `PATH`. |
| `remove-from-path` | Remove mpv.net do `PATH`. |
| `edit-conf-file` | Abre/cria arquivo de configuração, como `mpv.conf` ou `input.conf`. |
| `load-audio` | Abre diálogo para adicionar arquivos externos de áudio. |
| `load-sub` | Abre diálogo para adicionar legendas externas. |
| `move-window` | Move a janela para `left`, `top`, `right`, `bottom` ou `center`. |
| `open-clipboard` | Abre arquivo/URL vindo da área de transferência. |
| `open-files` | Abre arquivos por diálogo; aceita modo `append`. |
| `open-optical-media` | Abre DVD/Blu-ray por pasta/dispositivo. |
| `reg-file-assoc` | Registra associações de arquivo para áudio, vídeo, imagem ou remove registro. |
| `scale-window` | Ajusta escala da janela por fator. |
| `window-scale` | Ajusta escala da janela pelo mecanismo net/window. |
| `show-about` | Abre janela Sobre. |
| `show-bindings` | Mostra bindings ativos em editor de texto. |
| `show-commands` | Mostra lista de comandos. |
| `show-conf-editor` | Abre editor de configuração. |
| `show-input-editor` | Abre editor de atalhos/input. |
| `show-decoders` | Mostra decoders disponíveis. |
| `show-demuxers` | Mostra demuxers disponíveis. |
| `show-info` | Mostra informações de mídia no OSD. |
| `show-keys` | Mostra teclas conhecidas pelo mpv. |
| `show-media-info` | Mostra informações de mídia por OSD ou janela. |
| `show-menu` | Abre menu de contexto. |
| `show-profiles` | Mostra perfis mpv. |
| `show-properties` | Chama script-binding de propriedades. |
| `show-protocols` | Mostra protocolos disponíveis. |

Comandos deprecated ainda registrados:

| Comando | Observação |
| --- | --- |
| `show-recent` | Recurso removido; mantido para compatibilidade/mensagem. |
| `quick-bookmark` | Recurso removido; mantido para compatibilidade/mensagem. |
| `show-history` | Recurso removido; mantido para compatibilidade/mensagem. |
| `show-playlist` | Delegado ao script-binding `select/select-playlist`. |
| `show-command-palette` | Delegado ao script-binding `select/select-binding`. |
| `show-audio-tracks` | Delegado ao script-binding `select/select-aid`. |
| `show-subtitle-tracks` | Delegado ao script-binding `select/select-sid`. |
| `show-audio-devices` | Delegado ao script-binding `select/select-audio-device`. |

---

# Fontes de comandos

Comandos podem vir de:

- `input.conf`;
- atalhos padrão em `InputHelp`;
- menu de contexto;
- linha de comando;
- scripts Lua;
- scripts JavaScript;
- extensões .NET;
- botões/janelas do frontend.

---

# `input.conf`

Exemplo:

```text
Ctrl+a script-message-to mpvnet show-text Teste 2000 24
MBTN_Right script-message-to mpvnet show-menu
```

O arquivo pode conter comandos mpv nativos e comandos do mpv.net.

Cuidados:

- atalhos duplicados podem sobrescrever comportamento esperado;
- comandos antigos podem continuar funcionando por migração em `InputConf`;
- scripts de usuário podem depender de comandos deprecated.

---

# Linha de comando

`CommandLine` interpreta argumentos `--nome=valor`, incluindo aliases:

| Alias | Normalizado para |
| --- | --- |
| `--script` | `scripts` |
| `--script-opt` | `script-opts` |
| `--audio-file` | `audio-files` |
| `--sub-file` | `sub-files` |
| `--external-file` | `external-files` |

Opções terminadas em `-add`, `-set`, `-append`, `-pre`, `-clr`, `-remove` e `-toggle` são processadas como `change-list` após inicialização.

---

# Áreas críticas

## Compatibilidade

Não remover comando existente sem migração documentada.

## Parsing

Comandos textuais podem quebrar com aspas, espaços e caminhos. Para código C#, prefira `Player.CommandV(...)` quando houver argumentos separados.

## Menu de contexto

O menu depende de bindings e do conteúdo de `input.conf`. Alterações devem validar `MBTN_Right`, fullscreen e arquivos antigos.

## Scripts

Scripts podem chamar comandos do mpv.net diretamente. Mudanças de nome são alto risco.

---

# Recomendações para manutenção

1. Ao adicionar comando novo, registrar documentação no manual e neste arquivo.
2. Validar o comando por atalho, menu e script quando aplicável.
3. Preservar comandos deprecated até existir migração clara.
4. Preferir comandos vetoriais (`CommandV`) para argumentos complexos.
5. Atualizar exemplos em `docs/exemplos/input.conf` quando mudar comportamento esperado.

---

# Pendências

- Gerar tabela automaticamente a partir dos dicionários de `Command.cs` e `GuiCommand.cs`.
- Documentar argumentos aceitos por cada comando.
- Cruzar comandos com atalhos padrão de `InputHelp`.
