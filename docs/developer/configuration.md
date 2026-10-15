# Configuração do MPV.NET Media Player

## Objetivo

Descrever como o MPV.NET localiza e carrega as configurações e como trata seus arquivos.

---

## Visão geral

O mpv.net preserva o modelo de configuração do mpv: arquivos de texto, uma pasta previsível e suporte a scripts.

O núcleo da resolução de configuração está em:

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/Settings.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

---

## Ordem de resolução da pasta de configuração

Implementação: `Player.ConfigFolder`.

Ordem:

1. variável de ambiente `MPVNET_HOME`, quando aponta para um diretório existente;
2. pasta `portable_config`, se existir ao lado do executável;
3. diretório padrão de `AppPaths`, criado automaticamente na inicialização. Em instalações comuns, é `%LOCALAPPDATA%\mpv.net`.

A ordem define qual pasta prevalece e deve ser preservada para manter a compatibilidade com instalações existentes e com o modo portátil.

---

## Arquivos principais

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
| `AppPaths.Cache` | Cache temporário do mpv para demuxer, ICC e shaders; o cache de rede em disco é usado durante a reprodução e removido ao fechar a mídia. |
| `AppPaths.Components\current` | Geração válida atual dos componentes auxiliares baixados e seus manifestos. |
| `AppPaths.Components\staging` | Downloads e extrações temporárias, removidos depois de promover ou falhar. |
| `AppPaths.Components` | Local legado: continua sendo lido como fallback até uma atualização válida migrar os arquivos para `current`. |
| `AppPaths.Temp` | Arquivos temporários criados pelo frontend, como playlists normalizadas. |
| `AppPaths.Logs` | Erros e logs detalhados somente quando o build é gerado com `EnableFileLogging=true`. |

Esses diretórios são centralizados em `AppPaths` e criados no início da
aplicação; a pasta `Logs` só é criada em builds com logging habilitado.
Em instalações comuns, `AppPaths.LocalAppData` é `%LOCALAPPDATA%`;
no pacote MSIX/Microsoft Store, é
`%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalCache\Local`. `AppPaths.LocalRoot`
acrescenta `mpv.net` a essa raiz. A variável `MPVNET_LOCAL_APPDATA` pode definir
outra raiz nos dois casos. Essa resolução mantém os caminhos do bootstrap na
pasta física do pacote, sem depender da virtualização do AppData.

## Componentes auxiliares de runtime

Os componentes auxiliares de runtime `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`,
`yt-dlp.exe` e `deno.exe` não dependem do diretório de trabalho. A resolução
preserva a política histórica de preferir o cache validado e segue esta ordem:

1. `%LOCALAPPDATA%\mpv.net\Component\current`;
2. cache legado `%LOCALAPPDATA%\mpv.net\Component`;
3. pasta de `mpvnet.exe` (`AppContext.BaseDirectory`);
4. `PATH` do processo, apenas como último fallback.

Cada candidato precisa existir e é convertido para um caminho absoluto. Os cinco
executáveis baixados também precisam ser arquivos PE x64 não vazios. Arquivos
HTML, truncados, x86 ou corrompidos são rejeitados. Nomes de componentes
inválidos não geram caminhos alternativos. Manifestos JSON ilegíveis são
preservados para diagnóstico e tratados como cache inválido até uma atualização
concluída.

O `yt-dlp.exe` resolvido é informado ao mpv por `ytdl-path`. Um valor definido na
linha de comando ou em `mpv.conf` tem precedência. Quando `current` existe, o
processo o coloca no início do `PATH` para que ferramentas como `yt-dlp`
encontrem `ffmpeg` e `ffprobe` da mesma geração. Antes da primeira promoção, o
diretório legado continua sendo usado como fallback.

As atualizações são preparadas em `staging`. Antes de promover os arquivos para
`current`, o aplicativo valida HTTPS, host permitido, tamanho, SHA-256 publicado
pelo GitHub, ZIP Slip e arquitetura PE x64. O conjunto FFmpeg é promovido junto
com o manifesto `ffmpeg-bundle.json`, hashes individuais e fingerprints de
tamanho e data de alteração.

Nas inicializações seguintes, um fingerprint inalterado evita reler o arquivo
inteiro. Se o tamanho ou a data mudar, o aplicativo recalcula o SHA-256 completo
e compara o resultado com o manifesto. Downloads antigos sem fingerprint também
passam por hash completo antes da atualização do manifesto. Esse atalho reduz a
leitura de disco; `--diagnose-components` continua recalculando todos os hashes
para o diagnóstico completo.

Um mutex nomeado por usuário impede duas instâncias de alterarem o cache ao
mesmo tempo. Uma thread dedicada o libera, e o bloqueio pode ser recuperado se
uma instância terminar inesperadamente. Em caso de falha, a geração anterior é
mantida. Os manifestos `yt-dlp.exe.json` e `deno.exe.json` ficam ao lado dos
binários e registram versão/asset, hash, URL, data, tamanho, arquitetura e
fingerprint.

Na primeira execução, `yt-dlp.exe` e `deno.exe` são baixados antes do ZIP do
FFmpeg e ficam disponíveis mesmo se esse download for interrompido. O bundle
FFmpeg só é promovido depois que os três executáveis forem validados. O bootstrap
começa em segundo plano após a exibição da janela e, quando termina, atualiza o
`ytdl-path` para as próximas reproduções sem substituir uma configuração
explícita do usuário.

Cada download tem limite de 10 minutos. Ao fechar o aplicativo, o cancelamento
remove o arquivo parcial e só aparece nos logs de diagnóstico (`Debug`). Timeout
e falhas de transporte são registrados como erros; o timeout também informa o
limite e os bytes recebidos. O cache válido anterior é preservado, e componentes
ausentes ou vencidos podem ser baixados na próxima inicialização.

O `yt-dlp.exe` oficial já inclui os scripts EJS. Para resolver os desafios
JavaScript atuais do YouTube, o bootstrap também baixa o runtime `deno.exe`
x64 (Deno 2.3 ou posterior) e o coloca na mesma geração do cache. O Deno fica
no `PATH` do processo e é encontrado pelo `yt-dlp` sem alterar o `ytdl-path`
configurado pelo usuário. O MPV.NET não habilita `--remote-components` para
baixar scripts EJS de fontes externas sem uma configuração explícita.

Para diagnóstico sem download nem escrita, execute:

```powershell
mpvnet.exe --diagnose-components
```

O comando informa a origem, caminho absoluto, versão e resultado da validação.
`mpvnet.com` continua podendo ser resolvido e verificado quando vem junto do
pacote, mas não é baixado pelo bootstrap nem iniciado pelo frontend, pois não há
consumidor de runtime que justifique sua execução.

Para scripts como `thumbfast`, use `portable_config/scripts` e
`portable_config/script-opts` no pacote portátil. O mpv.net v7 oferece suporte
direto ao `thumbfast`; configure `mpv_path` para um `mpv.exe` separado apenas
como fallback para versões antigas ou casos específicos documentados pelo
próprio script.

Na primeira execução, o `mpv.conf` inicial criado pelo aplicativo não cria
perfis específicos para IPTV. `Player.MediaLoading` aplica, quando habilitada,
uma política conservadora como opções locais do comando `loadfile`. A política
classifica a URI antes de escolher o perfil, não altera arquivos locais e não
torna as opções globais para o próximo item. Opções explícitas no `mpv.conf` ou
na linha de comando são excluídas da lista automática.

---

## `mpv.conf`

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

## `mpvnet.conf`

Arquivo de opções específicas do mpv.net, processado por `App.ProcessProperty`.

Opções reconhecidas pelo mpv.net:

| Opção | Valor padrão no código | Função |
| --- | --- | --- |
| `auto-load-folder` | `no` | Controla carregamento automático da pasta. |
| `automatic-network-cache` | `yes` | Ativa a política automática de cache por entrada de rede. |
| `autofit-audio` | `70%` | Tamanho de janela para áudio. |
| `autofit-image` | `80%` | Tamanho de janela para imagem. |
| `dark-mode` | `always` | Modo escuro/claro. |
| `dark-theme` | `dark` | Tema escuro. |
| `debug-mode` | `no` | Ativa o diagnóstico em runtime; `MpvNet-debug.log` e verbose automático do yt-dlp exigem também `EnableFileLogging=true` na compilação. |
| `language` | `english` | Idioma da interface. Se ausente, o app usa o idioma do Windows quando houver tradução e, caso contrário, usa inglês. |
| `light-theme` | `light` | Tema claro. |
| `media-info` | `yes` | Ativa ou desativa as informações de mídia. |
| `menu-syntax` | `#menu:` | Define a sintaxe de menu reconhecida em `input.conf`. |
| `network-cache-profile` | `resilient` | Perfil `off`, `low-latency`, `balanced` ou `resilient`; valor inválido usa `resilient`. |
| `minimum-aspect-ratio` | `0` | Limite de proporção para vídeo. |
| `minimum-aspect-ratio-audio` | `0` | Limite de proporção para áudio. |
| `process-instance` | `single` | Controle de instância: single/queue/multi. |
| `queue` | `no` | Modo fila. |
| `recent-count` | `15` | Quantidade de recentes. |
| `save-network-urls-in-recent` | `no` | Permite salvar URLs de rede em Recent Files; credenciais conhecidas, parâmetros sensíveis e fragmentos são sempre omitidos. A lista é armazenada em texto legível em `settings.xml`. |
| `remember-audio-device` | `yes` | Persiste dispositivo de áudio. |
| `remember-volume` | `yes` | Persiste volume/mute. |
| `remember-window-position` | `no` | Persiste posição da janela. |
| `start-size` | `height-session` | Tamanho inicial da janela. |

`App.Init()` registra como erro cada opção desconhecida encontrada no arquivo.

`language` controla apenas o idioma da interface. As preferências de áudio e
legenda continuam no `mpv.conf`, com as opções nativas do mpv:

| Opção | Arquivo | Função |
| --- | --- | --- |
| `alang` | `mpv.conf` | Lista de prioridade dos idiomas de áudio. |
| `slang` | `mpv.conf` | Lista de prioridade dos idiomas de legenda. |
| `aid` | `mpv.conf` ou execução | Seleciona manualmente uma faixa de áudio. |
| `sid` | `mpv.conf` ou execução | Seleciona manualmente uma legenda; `no` mantém as legendas desligadas. |
| `sub-auto` | `mpv.conf` | Controla o carregamento automático de legendas externas. |
| `track-auto-selection` | `mpv.conf` | Controla a seleção automática de faixas pelo mpv. |

O mpv.net normaliza e compara códigos de idioma, mas não reescreve o
`mpv.conf` nem substitui as configurações de áudio e legenda definidas pelo
usuário.

---

## `input.conf`

Responsável por atalhos, comandos e menu.

Arquivos envolvidos:

- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Utilities/InputHelp.cs`;
- `src/MpvNet/Binding.cs`;
- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/Commands/GuiCommand.cs`.

Como o arquivo é processado:

- Se o arquivo usa a sintaxe de menu (`App.MenuSyntax`), seu conteúdo serve de base para o menu.
- Caso contrário, o mpv.net combina os atalhos padrão de `InputHelp.GetDefaults()` com os atalhos do usuário. Os atalhos do usuário podem substituir os padrões.
- O conteúdo final pode ser enviado ao mpv por `input-conf=memory://...`.
- Os rótulos padrão do menu usam o idioma ativo da interface. Rótulos definidos pelo usuário permanecem como estão no `input.conf`.

Migrações automáticas:

- `script-message mpv.net` é substituído por `script-message-to mpvnet`.
- `/docs/Manual.md` é substituído por `/docs/manual.md`.
- Links de `stax76/mpv.net` são atualizados para `WandersondeSouza/mpv.net`.
- Links do manual com `/blob/main/docs/` são atualizados para `/blob/master/docs/`, branch publicado do repositório.
- Quando uma migração altera o arquivo, o mpv.net cria o backup `input.conf.backup`.

---

## `settings.xml`

Arquivo persistido por `SettingsManager`.

Usado para preferências internas como:

- volume;
- mute;
- dispositivo de áudio;
- arquivos recentes;
- flags de migração já aplicadas.

Esse arquivo guarda o estado interno do frontend e não substitui `mpv.conf` nem
`mpvnet.conf`. Se ainda não existir, o volume inicial será `100`.

---

## Temas e atalhos globais

`Theme.Init()` lê `theme.conf` quando o arquivo existe na pasta ativa de
configuração.

`GlobalHotkey` lê `global-input.conf` para registrar atalhos globais no Windows.

Ao alterar esses recursos, valide os temas claro e escuro, o registro de atalhos
globais e possíveis conflitos com outros programas.

---

## Opções iniciais da linha de comando

`CommandLine` processa argumentos no formato `--nome=valor`.

Algumas opções são aplicadas antes de `mpv_initialize`, incluindo:

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

Quando o usuário define `--config-dir`, o mpv.net também procura o `input.conf`
nessa pasta.

---

## Fluxo de configuração

1. `Player.ConfigFolder` escolhe `MPVNET_HOME`, `portable_config` ou `AppPaths.DefaultConfig`, nessa ordem.
2. A limpeza tenta remover arquivos e diretórios vazios cuja última alteração
   ocorreu há mais de três dias em `AppPaths.Cache` e `AppPaths.Temp`.
3. O cache do mpv é direcionado para `AppPaths.Cache`.
4. O aplicativo lê os arquivos de configuração. `mpv.conf`, `mpvnet.conf` e `input.conf` recebem tratamento específico.
5. `settings.xml`, `theme.conf` e `global-input.conf` completam as preferências do frontend.
6. A interface e o libmpv usam esse estado inicial. O aplicativo salva as alterações em `settings.xml` e, quando necessário, atualiza arquivos durante migrações.

---

## Áreas críticas

### Compatibilidade

Não altere nomes de arquivos, ordem de resolução ou sintaxe sem uma migração compatível.

### Modo portátil

O mpv.net usa `portable_config` quando essa pasta existe ao lado do executável.
Os arquivos de configuração ficam nela; o cache temporário e os logs continuam
na raiz de dados local definida por `AppPaths`.

Registre a validação do ZIP portátil junto ao artefato e à versão testada. Veja
[Build e release](build-release.md).

### Limpeza de estado descartável

Na inicialização, o aplicativo tenta remover arquivos e diretórios vazios cuja
última alteração ocorreu há mais de três dias em `AppPaths.Cache` e
`AppPaths.Temp`.

Essa limpeza não altera arquivos de configuração nem deve bloquear a abertura do
player. Falhas são registradas como erros. Logs detalhados dependem de um build
com registro em arquivo habilitado.

### Migrações automáticas

`App.ApplyShowMenuFix`, `App.ApplyInputDefaultBindingsFix` e
`InputConf.UpdateContent` podem alterar arquivos do usuário. Preserve o conteúdo
existente e mantenha essas alterações restritas ao necessário.

### Desempenho

O aplicativo lê a configuração na inicialização. Evite leituras repetidas e
processamento pesado nesse fluxo.


