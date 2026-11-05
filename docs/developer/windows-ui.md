# Interface Windows do MPV.NET Media Player

## Objetivo

Documentar conceitos da interface gráfica do MPV.NET Media Player.

---

# Visão geral

O mpv.net implementa sua própria janela principal para Windows.

A interface gráfica é uma das principais diferenças entre o mpv original e o mpv.net.

---

# Principais responsabilidades

- janela principal;
- fullscreen;
- menu de contexto;
- overlays;
- temas;
- atalhos;
- mouse;
- controles na tela.

## Topologia da janela principal

- `src/MpvNet.Windows/WinForms/MainForm.cs` - estado principal da janela e ciclo de vida WinForms;
- `src/MpvNet.Windows/WinForms/MainForm.ContextMenu.cs` - menu de contexto, faixas, capítulos e busca de itens;
- `src/MpvNet.Windows/WinForms/MainForm.Commands.cs` - comandos de UI vindos de `GuiCommand`;
- `src/MpvNet.Windows/WinForms/MainForm.Cursor.cs` - cursor e OSC;
- `src/MpvNet.Windows/WinForms/MainForm.DragDrop.cs` - drag/drop;
- `src/MpvNet.Windows/WinForms/MainForm.Fullscreen.cs` - fullscreen;
- `src/MpvNet.Windows/WinForms/MainForm.PlayerEvents.cs` - reação da UI a eventos do player.
- `src/MpvNet.Windows/WinForms/MainForm.MediaTransport.cs` - estado, comandos e
  timeline dos controles de mídia do Windows;
- `src/MpvNet.Windows/Services/MediaTransport/` - contrato, controlador,
  metadata segura e adapter WinRT SMTC.

---

# Áreas críticas

## Fullscreen

Mudanças podem impactar:

- multi-monitor;
- overlays;
- menu de contexto;
- comportamento do cursor.

Quando a janela está em fullscreen, `Esc` é tratado pela camada Windows para
sair desse modo e não é encaminhado ao mpv como `quit`. Com a janela no tamanho
normal, o atalho padrão `Esc` continua sendo encaminhado ao mpv para fechar o
player.

## Botão de tela cheia na barra de título

`MainForm.TitleBar.cs` encaminha o clique para `Player.Command("cycle fullscreen")`.
A propriedade observada do mpv e `MainForm.Fullscreen.cs` continuam controlando
a transição. Esc mantém a prioridade do menu aberto; para sair de fullscreen,
define a propriedade do mpv como `no`, deixando o mesmo observador atualizar
a janela. Isso evita a janela normal com a propriedade ainda em `yes` e permite
entrar novamente pelo próximo clique. Menu, atalhos e duplo clique mantêm seus
comandos existentes.

`TitleBarFullscreenButton.cs` contém uma janela auxiliar WinForms pertencente à
principal, sem ativação, moldura ou entrada na taskbar. Ela ocupa somente a
área do novo botão de tela cheia, preservando o desenho nativo do título e do
ícone do sistema sem sobrepor texto. O texto Win32 da janela principal e seu
HWND permanecem intactos. A área de vídeo,
`WM_NCCALCSIZE` e os três botões nativos não são substituídos. Isso mantém o
roteamento do Windows para os botões, incluindo o recurso de layouts do Windows 11.
Fora do novo botão, `HTTRANSPARENT` entrega os gestos ao proprietário na mesma
thread, preservando arraste, duplo clique e menu de sistema da barra.

O posicionamento consulta `DWMWA_CAPTION_BUTTON_BOUNDS` do DWM e
`WM_GETTITLEBARINFOEX`, em coordenadas de tela com sinal. A superfície termina
antes dos botões nativos e acima da origem do vídeo; seu início reserva a
largura do ícone pequeno do sistema e uma margem ajustada ao DPI. Posição, tamanho,
ativação, título, DPI, tema, visibilidade e display atualizam sua geometria.
Sem geometria válida, sem borda/barra, minimizada ou em fullscreen, ela é
ocultada; não existe um botão flutuante sobre o vídeo. O idioma atualiza
tooltip e `AccessibleName` usando a entrada gettext existente `Fullscreen`.
A ação acessível usa o mesmo clique. O clique ativa o proprietário para que
o foco de teclado permaneça no player. Fontes, tooltip e controles são
descartados com a janela principal; a recriação do HWND oculta e reposiciona
a superfície.

O ícone reproduz em GDI+ o contorno `E108` de `mpv-osd-symbols`, usado pelo
estilo `classic` do OSC distribuído (`bottombar`, padrão `icon_style=layout`).
A fonte interna do libmpv não é uma fonte GDI instalada; por isso o contorno
é desenhado sem acrescentar uma dependência de fonte. O glyph é ampliado para
22 px para melhorar a leitura. Quando a janela perde o foco, a superfície usa
as cores de legenda inativa do Windows no alto contraste; nos temas claro e
escuro, o ícone fica cinza para acompanhar os botões nativos inativos. Ao
recuperar o foco, volta à cor do tema. O OSC e seus ícones não são alterados.
A superfície acompanha `dark-mode` e alto contraste; o tema DWM é reaplicado
após a leitura da configuração inicial.

Referências: [geometria DWM](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute),
[TITLEBARINFOEX](https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-titlebarinfoex)
e [glyph clássico do mpv](https://github.com/mpv-player/mpv/blob/77e8f9cfa1c185e452d1f36c00b8ec569e0f0ea8/TOOLS/mpv-osd-symbols.sfdir/uniE108.glyph).

Os testes `TitleBarFullscreenButtonTests` usam HWNDs reais em thread STA com
PerMonitorV2 para verificar preservação de título, área cliente e hit test
nativo em janela normal/maximizada, limites da superfície, ação acessível,
tradução, ocultação, recriação e descarte. O checklist visual abaixo continua
necessário para outros ambientes, escalas e monitores.

Validação local em 2026-10-27, Windows 11 build 26300, um monitor e escala 100%:
builds Debug/Release sem avisos/erros e 739 testes aprovados. Na aplicação com
perfil isolado foram verificados ícone, tooltip e nome acessível em português,
temas claro/escuro, entrada pelo botão em janela normal, Esc e reentrada,
prioridade do menu no primeiro Esc, duplo clique no título/vídeo, arraste,
minimização/restauração e fechamento nativos, além de ocultação com `border=no`
e `title-bar=no`. A geometria maximizada foi conferida visualmente e nos testes
de HWND; a ação acessível também foi testada nessa condição. HTTP em loopback,
playlist M3U, pasta via IPC do mpv, reprodução de áudio/vídeo e atalhos de troca
de áudio/legenda passaram nas sondas locais.

Continuam pendentes Windows 10, escalas diferentes/mudança de DPI, múltiplos
monitores, arrastar arquivos do Explorer, redimensionamento pela borda e Snap
Layouts por hover no Windows 11. O clique físico no novo botão em janela
maximizada também precisa de verificação manual: a automação local recusou
esse alvo por identificar a janela auxiliar separadamente. As sondas de
pasta/playlist/HTTP não substituem os fluxos correspondentes do Explorer nem
um stream externo; o checklist completo permanece abaixo.

Uma repetição da suíte ficou sem progresso e foi interrompida. A execução
seguinte com diagnóstico por teste aprovou os 739 casos; a causa da ocorrência
intermitente não foi determinada.

## OSC

O OSC depende do comportamento da janela.

O padrão distribuído permanece `visibility=auto`, com `boxvideo=no` e
`sub_margins=no`. `Del` executa `script-binding osc/visibility`; a alternância
não persiste estado. A preferência de inicialização pertence ao próprio OSC,
em `script-opts/osc.conf` ou `script-opts`, sem duplicação em `settings.xml`.
Ao sair de `idle-active`, o script restaura os controles quando a opção é
`always`: o tick ocioso os oculta, inclusive depois de `stop`, e apenas carregar
outra mídia não os tornava visíveis novamente. O callback usa a atualização
existente do OSC para recalcular as margens de `boxvideo`, sem timer adicional
nem mudança no modo automático. `OscVisibilityTests` executa o Lua distribuído
com libmpv em saída nula, cobrindo duas cargas separadas por `stop` e as margens.
O exemplo para controles permanentes está no
[manual](../manual.md#visibilidade-dos-controles-osc).

O comando `show-bindings` consulta `input-bindings` no mpv no momento de uso,
em vez de exibir `Player.UsedInputConfContent`. Este último é apenas a cópia
montada pelo frontend antes de aplicar a CLI e não representa um `--input-conf`
explícito nem bindings adicionados por scripts. `RuntimeInputBindings` exclui
seções inativas e escolhe por tecla o binding forte antes do fraco, depois a
maior prioridade, seguindo o critério de diagnóstico do `stats.lua` do mpv.
Mantém `ignore` na exportação e não altera input nem captura teclas. As
prioridades são dinâmicas e a consulta não comprova o recebimento de uma tecla
física. Veja o contrato de
[`input-bindings`](https://mpv.io/manual/master/#command-interface-input-bindings).

O fork configura o `custom_button_1` para mostrar um coração de apoio. O comando
de clique encaminha para o navegador usando a URL da página de apoio, gerada
pelo C# com o idioma efetivo da interface. O `osc.lua` mantido em
`src/MpvNet.Windows/Scripts/osc.lua`, baseado no commit `304426c39` do mpv,
coloca esse botão na linha inferior do layout `bottombar`, imediatamente antes
do controle de volume. Ele continua usando a visibilidade, hover, autocultação
e roteamento de mouse do próprio OSC.

O `custom_button_2` usa um globo da fonte `Segoe UI Symbol` para abrir a página
oficial. O C# obtém o idioma efetivo de `App.Language`, monta internamente
`https://www.gestaodesistemas.com.br/mpvnet?language=<cultura>` e publica o
resultado em `user-data/mpvnet-website-url`. Os botões são acrescentados a
`script-opts` depois de `mpv_initialize`, preservando as opções do usuário e
reutilizando `script-message-to mpvnet shell-execute`. O OSC embutido é
desabilitado e a cópia compatível distribuída com o aplicativo é carregada em
seguida. A cópia mantém o posicionamento do coração no `bottombar`, os textos
localizados publicados pelo frontend e a restauração do modo permanente ao
sair do estado ocioso. O globo permanece na linha superior.

## Controles de mídia do Windows

O MPV.NET usa `Windows.Media.SystemMediaTransportControlsInterop.GetForWindow`
com o HWND da `MainForm`; a integração é opcional e não faz referência ao
FluentFlyout. O `MainPlayer` continua sendo a autoridade para reprodução: o
SMTC apenas publica estado, metadata e timeline e encaminha Play, Pause, Stop,
Previous, Next e seek como comandos mpv já existentes. Fast Forward e Rewind
usam saltos de 5 segundos, o intervalo padrão dos atalhos Left/Right, sem mudar
a taxa de reprodução. A posição atual é consultada de novo ao executar o comando;
o resultado real de seek é publicado pelos eventos do mpv.

`MediaTransportSnapshot.CanSeek` exige `seekable` do mpv, ausência de
`partially-seekable` (busca somente pelo cache) e duração finita,
positiva e representável. Sem essa capacidade, o adapter limpa a timeline,
desabilita Fast Forward/Rewind e o controller rejeita busca, inclusive eventos
externos tardios. O handler de mudança de posição é registrado somente enquanto
a capacidade de busca está ativa. Posições válidas são limitadas ao intervalo entre zero e a
duração; valores sentinela são rejeitados.

Um único timer WinForms de 5 segundos atualiza a timeline somente durante
reprodução. Carga/fim da mídia, pause, seek/playback-restart, playlist,
fullscreen e propriedades relevantes observadas publicam o estado por evento.
Essa cadência segue a [recomendação oficial da Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/system-media-transport-controls).
O controller elimina snapshots iguais e o adapter compara metadados antes de
executar `ClearAll`/`Update`; atualizar a posição não republica tags ou capa.

Na execução sem pacote (Debug, portátil e instalador), `ApplicationIdentity`
registra o `DisplayName` do produto em
`HKCU\Software\Classes\AppUserModelId\WandersondeSouza.MpvNet` e define esse
AppUserModelID no processo antes de criar a janela. O atalho do instalador e
as associações registradas usam a mesma identidade. Isso fornece ao Shell uma
identificação explícita para resolver o nome da origem do SMTC. O MSIX conserva
a identidade do pacote; falhas no registro ou na API são diagnosticadas sem
impedir a reprodução. A exibição final do nome depende de validação no painel
de mídia do Windows com a build atualizada.

A miniatura da barra de tarefas é uma superfície independente do SMTC. Ela é
configurada por Native/Taskbar.cs usando ITaskbarList3::ThumbBarAddButtons e
ThumbBarUpdateButtons, recebe cliques por WM_COMMAND com THBN_CLICKED e expõe
seis botões nesta ordem: arquivo anterior, capítulo anterior, play/pause,
próximo capítulo, próximo arquivo e apoio ao player. Todos são registrados
na criação da toolbar; as atualizações mantêm IDs, ordem e visibilidade.
Os ícones de capítulos usam uma seta, distinguindo-os das setas duplas de arquivos.
O botão Play/Pause
alterna o estado diretamente no mpv, sem depender de um snapshot intermediário.
Os botões de mídia acompanham o snapshot publicado pelo player; o botão de
apoio usa o link localizado pelo aplicativo.

`Native/TaskbarThumbnailControls.cs` define a composição e a disponibilidade
dos controles sem ampliar o contrato SMTC. Os dois botões de capítulos ficam
habilitados quando há mídia carregada e `chapter-list/count > 0`, inclusive
no primeiro/último capítulo, seguindo a regra do OSC. Os cliques executam
`osd-msg add chapter -1` e `osd-msg add chapter 1`, deixando os limites a cargo
do mpv. A observação de `chapter-list` atualiza a toolbar por evento, também
durante pausa; carga/fim de arquivo continuam limpando o estado nas transições.
Antes de executar o comando, o handler relê a contagem e verifica mídia
carregada, fullscreen, encerramento e a geração de mídia usada ao compor a
toolbar. Isso rejeita comandos de uma toolbar anterior durante a troca de arquivo.
Os cinco controles de mídia não usam `THBF_DISMISSONCLICK`, para manter a
miniatura aberta após o clique e permitir comandos consecutivos. O botão de apoio
usa essa flag porque abre a página externa no navegador. Tooltips reutilizam as
traduções existentes de `Previous Chapter`/`Next Chapter`.

Os glyphs dessa toolbar são desenhados com contraste adaptativo ao tema de
aplicativos do Windows (`AppsUseLightTheme`): usam uma cor escura no tema claro
e uma cor clara no tema escuro. As mensagens `WM_SETTINGCHANGE` e
`WM_THEMECHANGED` invalidam os `Icon` em cache e reaplicam os mesmos botões e
estados quando o tema muda com o player aberto. O cache de configuração evita
`ThumbBarUpdateButtons` quando ícones, tooltips, habilitação e tema não mudaram;
a mudança de idioma continua atualizando os tooltips. Em fullscreen, os botões
de mídia da taskbar ficam desabilitados independentemente da sessão SMTC.

A sessão é limpa quando não existe mídia carregada, durante a transição entre
`start-file` e `file-loaded` e durante o fechamento. Em fullscreen, a mesma
sessão, os metadados, a timeline e os comandos permanecem ativos; as transições
de janela apenas atualizam o estado atual, sem recriar a sessão ou o timer. O
adapter não usa caminho, query, fragmento ou credencial
de URL como título nem inventa tags ausentes. `AlbumArtist` usa `album_artist`
ou `albumartist` do mpv; faixas `albumart` não transformam áudio em vídeo.

Para áudio local, `MainForm.AudioArtwork.cs` resolve a imagem selecionada pelo
mpv depois de `playback-restart`, fora da thread da UI. Capas externas são
copiadas e decodificadas; capas incorporadas usam `screenshot-raw video bgra`
do libmpv, copiando os pixels antes de liberar o resultado nativo. Isso evita
um segundo parser de tags ou dependência adicional e funciona para a imagem
efetivamente selecionada, inclusive em formatos sem miniatura Windows.
`AudioArtworkImage` valida até 4 MiB e 4096 pixels por dimensão, decodifica os
pixels e normaliza em PNG sem metadados. O caminho do cache próprio é publicado
em `ThumbnailPath`; o SMTC abre uma cópia em memória independente.

Uma capa online ou o ícone de fallback é acrescentado como `albumart` por
`video-add` assíncrono, sem trocar `wid`, OSC ou reiniciar áudio. O fallback
usa o ícone centralizado em uma imagem opaca de 512 pixels, para evitar
ampliação excessiva e fundo de transparência. A seleção `vid` é marcada como
local ao arquivo antes da carga e restaurada na próxima mídia. Opções nativas
explícitas e seleção manual prevalecem. A classificação usa as faixas reais:
OGG somente de áudio não é tratado como vídeo só por sua extensão, e vídeo
real desabilitado continua impedindo a inclusão automática de capas.

Quando não há imagem resolvida, `StorageFile.GetThumbnailAsync` usa
`ThumbnailOptions.ReturnOnlyIfCached`, que aceita miniatura em cache ou
incorporada. São aceitos somente arquivos
locais em disco fixo, sem atributo offline/reparse no arquivo ou nos diretórios
pai. Pastas redirecionadas/gerenciadas pela nuvem podem, por isso, usar o ícone
do aplicativo mesmo quando a mídia está disponível. A imagem é validada com
`BitmapDecoder` e limitada a 4 MiB e 4096 pixels por dimensão. O serviço publica
uma cópia em memória com `RandomAccessStreamReference.CreateFromStream`, nunca
um URI do arquivo. Streams de origem são liberados após a cópia.

A leitura ocorre em segundo plano, uma vez por mudança de metadados. Troca de
mídia ou fechamento cancelam a operação e invalidam resultados anteriores;
streams em memória permanecem sob responsabilidade do serviço até serem
substituídos/descartados. Uma falha de capa é diagnosticada só por tipo de
exceção/HRESULT e não desabilita o transporte. Sem imagem segura, a capa fica
com o ícone padrão do aplicativo. Arquivos remotos/offline não têm extração
adicional. O timer para ao pausar, terminar a mídia ou fechar a janela.

`OnlineAudioArtwork` consulta MusicBrainz/Cover Art Archive somente com
`audio-artwork-online=yes`, áudio local disponível e identidade de álbum.
MBIDs válidos prevalecem; a pesquisa por tags exige score >= 95, título e
crédito de artista exatos e um único release-group entre os candidatos. Edições
ambíguas são recusadas. A pesquisa por tags usa a imagem representativa do grupo
de álbum, evitando escolher uma edição específica sem capa. O cliente
usa o contrato JSON tipado de `MusicBrainzReleaseSearch`, que mantém a seleção
separada da consulta e do download da imagem em `OnlineAudioArtwork`. Campos
ausentes não confirmam a identidade; contratos JSON inválidos seguem o fluxo
de falha transitória sem interromper a reprodução. O cliente identifica o
produto no User-Agent, serializa
consultas, mantém intervalo de 1,1 segundo e respeita Retry-After em 429/503.
HTTPS, hosts de MusicBrainz/CAA/Internet Archive, até quatro redirecionamentos,
15 segundos e limites de resposta são verificados antes de publicar a imagem.

`AudioArtworkCache` usa hashes, gravação atômica, validade de 30 dias para
sucesso, um dia para ausência e cinco minutos para falha transitória. Limita o
conjunto a 128 MiB e 512 arquivos, sem gravar junto da mídia. Cancelamento por
troca/fechamento e verificações de geração, caminho e seleção descartam
resultados tardios. `user-data/mpvnet/audio-artwork-source` informa `none`,
`local`, `online` ou `default` para diagnóstico por IPC; não expõe tags/caminhos.
Atualizações de posição não iniciam nova resolução nem republicam a imagem.
Foto automática de artista permanece indisponível por falta de uma fonte com
identidade e licença/atribuição confirmadas; a ausência é uma limitação prevista
no plano, e não uma busca genérica de imagens.

O fallback usa o próprio `mpv-icon.ico`, incorporado como recurso da assembly
Windows, convertido para PNG em memória. Ausência ou falha de capa retorna esse
ícone; trocar de mídia substitui a imagem anterior, sem carregar um arquivo
externo para o fallback. Sem mídia ou durante o encerramento, a miniatura é
removida junto com a sessão. O ícone segue o mesmo cancelamento e descarte das
capas e não é republicado em atualizações de posição.

Na janela embutida, o padrão de `media-controls` do libmpv é `no`: o frontend
já publica a sessão do HWND. Isso evita duas sessões concorrentes no Windows.
Configuração explícita em `mpv.conf`/linha de comando mantém precedência; com
`media-controls=yes`, o frontend não inicializa seu adapter SMTC e deixa a
publicação com o mpv. Nesse modo explícito, controles e metadados seguem o
comportamento nativo da versão de libmpv instalada. A execução sem janela
preserva o padrão anterior do núcleo.

## DPI e escala

Mudanças precisam validar:

- Windows 10;
- Windows 11;
- telas 4K;
- múltiplos monitores.

---

# Temas

O projeto suporta:

- tema claro;
- tema escuro;
- temas customizados.

---

## Smoke test reproduzível

### Capas de áudio — 05/11/2026

Validação local em Windows build 26300.9818, libmpv
`v0.42.0-1-g3138fc8ee`, execução desktop com configuração/dados isolados:

- 940 testes xUnit aprovados, sem falhas ou skips; incluem HTTP loopback real,
  cache/404/timeout/429/redirecionamentos, cancelamento, seleção, imagens
  inválidas/grandes, arquivos offline, libmpv real e inicialização do executável.
- Builds Debug e Release normais, sem avisos/erros e sem novas dependências.
- Matriz de 17 casos via IPC: capas incorporadas em MP3/FLAC/M4A/OGG; ausência
  de capa em WAV/MP3/FLAC/M4A/OGG; dois candidatos locais; capa local inválida;
  preservação de vídeo real; stream HTTP local sem extração/busca; imagens
  desativadas; 12 trocas rápidas; três ciclos de fullscreen; fechamento limpo.
- Consulta HTTPS real ao CAA com MBID conhecido: imagem de 494 x 500 carregada
  como `albumart` e publicada no SMTC com as mesmas dimensões. Capa incorporada
  de 320 x 320 também foi confirmada pela API SMTC; ausência retornou ao ícone.
  Busca real por artista/álbum, sem MBID nas tags, resolveu uma capa de
  500 x 500 por release-group e a publicou nas duas superfícies.
- Conferência visual da janela em tema claro/escuro, menu de contexto, OSC,
  fullscreen, fallback opaco centralizado e fechamento pelo botão da janela.
  `media-controls=yes` explícito preservou uma única sessão nativa com capa.

Na validação foi corrigido o teste `DestroyWaitsForActiveNativeOperation`: ele
mantinha uma lease de `ReaderWriterLockSlim` através de `await`, podendo tentar
liberá-la em outra thread e travar seu próprio cleanup. O teste agora preserva
a thread adquirente; o mecanismo de destruição do player não foi alterado.

Essas evidências não substituem instalação/MSIX/Store, Windows 10, outros DPIs
ou múltiplos monitores. Drag/drop por gesto físico e o painel visual do Windows
não foram exercitados nesta rodada; seus caminhos de entrada e o contrato SMTC
têm testes automatizados. A matriz visual completa abaixo permanece como
checklist para os respectivos ambientes.

### Integração SMTC — 14/10/2026 (histórico)

Validação local das melhorias SMTC em 2026-10-14, Windows build 26300:

- `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-restore`:
  596 testes aprovados, incluindo HWND/WinRT reais, ícone incorporado,
  ausência/falha de capa, descarte tardio, capacidades de busca e cache da taskbar.
- Builds Debug e Release do projeto Windows: zero avisos/erros, com preparação
  normal das dependências e catálogos de idiomas.
- Sonda local da aplicação por SMTC/IPC: áudio, vídeo, AlbumArtist, ícone/capa/ícone,
  Play/Pause/Stop, seek, saltos de 5 segundos, playlist no primeiro/meio/último
  item, três ciclos de fullscreen e encerramento em fullscreen com código zero
  e remoção da sessão. Stream UDP local não pesquisável desabilitou seek e saltos.
- `media-controls=yes` explícito em `mpv.conf`: opção preservada, uma única
  sessão nativa e encerramento com código zero.

A amostra de áudio com imagem incorporada não forneceu miniatura pela API
`ReturnOnlyIfCached`; nesse caso o ícone do aplicativo foi o fallback. A capa
externa local selecionada pelo mpv foi publicada e substituída pelo ícone ao
trocar para mídia sem capa. Esses testes foram automatizados via APIs; não
comprovam apresentação visual no painel do Windows. Windows 10, FluentFlyout,
teclas físicas multimídia, stream externo com credenciais e o checklist visual
completo abaixo ainda precisam de validação nos respectivos ambientes.

O script abaixo verifica se o processo permanece ativo durante as janelas de
inicialização e observação. Por padrão, termina com `Stop-Process -Force`; isso
não comprova fechamento limpo, execução do shutdown nem descarte dos recursos.
Ele também não substitui a verificação visual dos controles:

```powershell
.\src\Tools\test-ui-smoke.ps1 `
  -ExecutablePath .\src\MpvNet.Windows\bin\Debug\win-x64\mpvnet.exe `
  -MediaPath C:\Videos\amostra.mp4
```

Depois do smoke test, validar manualmente:

- arquivo local, URL/stream, playlist e pasta com mídia;
- drag/drop e abertura por linha de comando;
- tema claro/escuro, DPI, múltiplos monitores e fullscreen;
- menu de contexto, atalhos, foco e cursor/OSC;
- alternância de áudio/legenda, pause, seek, controles de mídia do Windows e fechamento;
- persistência de volume, posição, tamanho e pasta inicial.

Use `-KeepOpen` para concluir o checklist visual e fechar a janela pelo próprio
aplicativo, verificando o encerramento normal separadamente.


