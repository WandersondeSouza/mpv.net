# Configuração do MPV.NET Media Player

## Objetivo

Documentar como o MPV.NET Media Player localiza, carrega, migra e utiliza arquivos de configuração.

---

# Visão geral

O mpv.net mantém compatibilidade com o modelo do mpv: arquivos de texto simples, pasta de configuração previsível e suporte a scripts.

O núcleo da resolução de configuração está em:

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/Settings.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

---

# Ordem real da pasta de configuração

Implementação: `Player.ConfigFolder`.

Ordem:

1. variável de ambiente `MPVNET_HOME`, quando aponta para um diretório existente;
2. pasta `portable_config` ao lado do executável;
3. `%LOCALAPPDATA%\mpv.net`, criada automaticamente no startup.

Essa ordem é crítica para compatibilidade com instalações existentes e modo portátil.

---

# Arquivos principais

| Arquivo | Origem no código | Função |
| --- | --- | --- |
| `mpv.conf` | `Player.ConfPath` | Opções do mpv e algumas opções processadas por `Player.ProcessProperty`. |
| `mpvnet.conf` | `App.ConfPath` | Opções específicas do frontend mpv.net. |
| `input.conf` | `App.InputConf` | Atalhos, comandos e menu. |
| `menu.conf` | `App.EnsureInitialSelectMenuConf()` | Menu OSD do `select.lua` com rótulos localizados por idioma. |
| `settings.xml` | `SettingsManager.SettingsFile` | Preferências persistidas pelo mpv.net. |
| `theme.conf` | `Theme.Init()` | Tema visual customizado. |
| `global-input.conf` | `GlobalHotkey` | Hotkeys globais. |

Pastas:

| Pasta | Função |
| --- | --- |
| `scripts` | Scripts Lua/JavaScript do mpv. |
| `script-opts` | Configuração dos scripts. |
| `extensions` | Extensões .NET carregadas pelo mpv.net. |

Cache e logs:

| Pasta | Função |
| --- | --- |
| `%LOCALAPPDATA%\mpv.net\Cache` | Cache temporário do mpv para demuxer, ICC e shaders. |
| `%LOCALAPPDATA%\mpv.net\Component` | Componentes nativos baixados/atualizados em segundo plano, com fallback para os binários que vierem junto do app. |
| `%LOCALAPPDATA%\mpv.net\Temp` | Arquivos temporários criados pelo frontend, como playlists normalizadas. |
| `%LOCALAPPDATA%\mpv.net\Temp\RuntimeComponents` | Downloads e extrações temporárias antes da instalação dos arquivos em `Component`. |
| `%LOCALAPPDATA%\mpv.net\Logs` | Logs diários quando o build é gerado com logging em arquivo habilitado. |

Esses diretórios são centralizados em `AppPaths` e criados no início da
aplicação, antes da inicialização de logs, cache e atualização de componentes.

Para scripts como `thumbfast`, a versão portátil deve usar `portable_config/scripts` e `portable_config/script-opts`. No mpv.net v7, `thumbfast` tem suporte direto; `mpv_path` para um `mpv.exe` separado deve ser tratado como fallback para versões antigas ou casos específicos documentados pelo próprio script.

Na primeira execução, o `mpv.conf` inicial criado pelo aplicativo não cria
perfis específicos para IPTV. `Player.MediaLoading` aplica tolerância de rede
como opções locais do comando `loadfile` quando a entrada aberta diretamente é
uma URL de streaming, detectada pelo protocolo (`http`, `https`, `ftp`, `rtsp`,
`rtmp` e similares). Essa aplicação automática não altera arquivos locais nem
torna as opções globais para o próximo item carregado.

---

# `mpv.conf`

Arquivo de opções do mpv.

Além de ser lido pelo mpv, o mpv.net também processa algumas opções em `Player.ProcessProperty`, incluindo:

- `autofit`;
- `autofit-smaller`;
- `autofit-larger`;
- `border`;
- `fs` / `fullscreen`;
- `gpu-api`;
- `keepaspect-window`;
- `screen`;
- `snap-window`;
- `taskbar-progress`;
- `vo`;
- `window-maximized`;
- `window-minimized`;
- `title-bar`.

Cuidados:

- preservar sintaxe compatível com mpv;
- evitar mudar o parser sem validar arquivos antigos;
- lembrar que linhas sem `=` podem ser interpretadas como `=yes` quando o nome é simples.

---

# `mpvnet.conf`

Arquivo de opções específicas do mpv.net, processado por `App.ProcessProperty`.

Opções observadas:

| Opção | Valor padrão no código | Função |
| --- | --- | --- |
| `auto-load-folder` | `no` | Controla carregamento automático da pasta. |
| `autofit-audio` | `70%` | Tamanho de janela para áudio. |
| `autofit-image` | `80%` | Tamanho de janela para imagem. |
| `dark-mode` | `always` | Modo escuro/claro. |
| `dark-theme` | `dark` | Tema escuro. |
| `debug-mode` | `no` | Ativa log `MpvNet-debug.log`. |
| `language` | `english` | Idioma da interface. Se ausente, o app usa o idioma do Windows quando suportado e cai para ingles. |
| `light-theme` | `light` | Tema claro. |
| `media-info` | `yes` | Controla recurso de media info. |
| `menu-syntax` | `#menu:` | Sintaxe antiga/nova para menu em `input.conf`. |
| `minimum-aspect-ratio` | `0` | Limite de proporção para vídeo. |
| `minimum-aspect-ratio-audio` | `0` | Limite de proporção para áudio. |
| `process-instance` | `single` | Controle de instância: single/queue/multi. |
| `queue` | `no` | Modo fila. |
| `recent-count` | `15` | Quantidade de recentes. |
| `remember-audio-device` | `yes` | Persiste dispositivo de áudio. |
| `remember-volume` | `yes` | Persiste volume/mute. |
| `remember-window-position` | `no` | Persiste posição da janela. |
| `start-size` | `height-session` | Tamanho inicial da janela. |

Opções desconhecidas são registradas como erro quando `App.Init()` processa o arquivo.

`language` controla apenas a interface. Preferencias de audio e legenda continuam
no `mpv.conf`, usando as opcoes nativas do mpv:

| Opção | Arquivo | Função |
| --- | --- | --- |
| `alang` | `mpv.conf` | Lista de prioridade de idiomas de audio. |
| `slang` | `mpv.conf` | Lista de prioridade de idiomas de legenda. |
| `aid` | `mpv.conf` ou runtime | Seleciona faixa de audio manualmente. |
| `sid` | `mpv.conf` ou runtime | Seleciona legenda manualmente; `no` mantem legenda desligada. |
| `sub-auto` | `mpv.conf` | Controla carregamento automatico de legendas externas. |
| `track-auto-selection` | `mpv.conf` | Controla a selecao automatica de faixas pelo mpv. |

O mpv.net normaliza e compara codigos de idioma em um ponto central, mas nao
reescreve `mpv.conf` nem sobrescreve configuracoes avancadas do usuario para
audio/legenda.

---

# `input.conf`

Responsável por atalhos, comandos e menu.

Arquivos envolvidos:

- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Utilities/InputHelp.cs`;
- `src/MpvNet/Binding.cs`;
- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/Commands/GuiCommand.cs`.

Comportamento importante:

- se o arquivo usa a sintaxe de menu (`App.MenuSyntax`), o conteúdo é usado como base de menu;
- caso contrário, os atalhos padrão de `InputHelp.GetDefaults()` são combinados com os atalhos do usuário;
- atalhos do usuário podem substituir atalhos padrão;
- o conteúdo final pode ser passado ao mpv via `input-conf=memory://...`.
- os rótulos padrão do menu são resolvidos no momento da renderização, usando o idioma ativo da interface;
- rótulos personalizados continuam sendo respeitados como estão no `input.conf`.

Migração automática observada:

- `script-message mpv.net` vira `script-message-to mpvnet`;
- `/docs/Manual.md` vira `/docs/manual.md`;
- link antigo `stax76/mpv.net` vira `mpvnet-player/mpv.net`;
- antes de alterar o arquivo, é criado backup em `input.conf.backup`.

---

# `settings.xml`

Arquivo persistido por `SettingsManager`.

Usado para preferências internas como:

- volume;
- mute;
- dispositivo de áudio;
- arquivos recentes;
- flags de migração já aplicadas.

Esse arquivo não substitui `mpv.conf` ou `mpvnet.conf`; ele armazena estado interno do frontend.
Se ainda não existir `settings.xml`, o volume inicial usado pelo frontend começa em `100`.

---

# Temas e hotkeys globais

`theme.conf` é lido por `Theme.Init()` quando existe na pasta de configuração.

`global-input.conf` é lido por `GlobalHotkey` para registrar hotkeys globais no Windows.

Mudanças nesses fluxos devem ser validadas com tema claro/escuro, permissões de registro de hotkey e conflito com outros programas.

---

# Linha de comando e configuração

`CommandLine` processa argumentos `--nome=valor`.

Algumas opções são aplicadas antes de `mpv_initialize`, por exemplo:

- `config-dir`;
- `demuxer-cache-dir`;
- `icc-cache-dir`;
- `gpu-shader-cache-dir`;
- `input-conf`;
- `scripts`;
- `script-opts`;
- `terminal`;
- `input-terminal`;
- `idle`;
- `log-file`.

Quando `--config-dir` é usado, o caminho do `input.conf` também é ajustado para essa pasta.

---

# Fluxo resumido de configuração

1. A aplicação inicia.
2. `Player.ConfigFolder` resolve `MPVNET_HOME`, `portable_config` ou `%LOCALAPPDATA%\mpv.net`.
3. Arquivos antigos em `%LOCALAPPDATA%\mpv.net\Cache` e `%LOCALAPPDATA%\mpv.net\Temp`
   com mais de 1 dia são removidos de forma não bloqueante.
4. O cache do mpv é direcionado para `%LOCALAPPDATA%\mpv.net\Cache`.
5. Os arquivos de configuração são lidos.
6. `mpv.conf`, `mpvnet.conf` e `input.conf` recebem tratamento específico.
7. `settings.xml`, `theme.conf` e `global-input.conf` completam o estado do frontend.
8. A UI e o libmpv usam esse estado inicial.
9. Alterações podem ser persistidas em `settings.xml` ou nos arquivos do usuário quando a migração for necessária.

---

# Áreas críticas

## Compatibilidade

Não alterar nomes de arquivos, ordem de resolução ou sintaxe sem migração.

## Modo portátil

`portable_config` só é usado quando a pasta existe ao lado do executável.
Arquivos de configuração ficam em `portable_config`, mas cache temporário e logs
de diagnóstico continuam em `%LOCALAPPDATA%\mpv.net`, para não misturar estado
descartável com configuração portátil.

## Limpeza de estado descartável

Na inicialização, o aplicativo tenta limpar arquivos e diretórios vazios com
mais de 1 dia em `%LOCALAPPDATA%\mpv.net\Cache` e
`%LOCALAPPDATA%\mpv.net\Temp`.

Essa limpeza não altera arquivos de configuração e não deve bloquear a abertura
do player. Falhas individuais ou gerais são capturadas e registradas apenas
quando o build foi gerado com logging em arquivo habilitado.

## Migrações automáticas

Migrações em `App.ApplyShowMenuFix`, `App.ApplyInputDefaultBindingsFix` e `InputConf.UpdateContent` alteram arquivos do usuário. Qualquer ajuste deve ser conservador.

## Performance

A configuração é lida no startup. Evite leituras repetidas ou validações pesadas.

---

# Recomendações para manutenção

1. Validar `MPVNET_HOME`, `portable_config` e `%LOCALAPPDATA%\mpv.net`.
2. Testar `mpv.conf`, `mpvnet.conf` e `input.conf` separadamente.
3. Preservar compatibilidade com arquivos antigos.
4. Criar backup antes de qualquer migração que altere arquivo do usuário.
5. Documentar novas opções no manual e neste arquivo.
6. Validar o editor visual quando alterar opções expostas na UI.

---

# Pendências

- Gerar documentação automática a partir de `App.ProcessProperty`, `Player.ProcessProperty` e comandos registrados.
- Validar manualmente o ZIP portátil gerado com `portable_config`.
- Documentar exemplos avançados de `theme.conf` e `global-input.conf`.


