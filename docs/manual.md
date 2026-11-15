# Manual do MPV.NET Media Player

O MPV.NET Media Player é um player de mídia para Windows baseado no mpv/libmpv e no mpv.net.

## Tópicos

- [Download](#download)
- [Instalação](#instalação)
- [Controles de mídia do Windows](#controles-de-mídia-do-windows)
- [Capas durante a reprodução de áudio](#capas-durante-a-reprodução-de-áudio)
- [Configuração](#configuração)
- [Atalhos de teclado e mouse](ATALHOS.md#controles-padrão-de-reprodução)
- [Visibilidade dos controles OSC](#visibilidade-dos-controles-osc)
- [Linha de comando](#linha-de-comando)
- [Integração por parâmetros: vídeo, título e idioma](#integração-por-parâmetros-vídeo-título-e-idioma)
- [Modo portátil](#modo-portátil)
- [Documentação técnica](#documentação-técnica)
- [Diferenças em relação ao mpv](#diferenças-em-relação-ao-mpv)
- [Suporte](#suporte)

## Download

Baixe o instalador ou o ZIP portátil nas [releases do GitHub](https://github.com/WandersondeSouza/mpv.net/releases). Você também pode instalar o aplicativo pela [Microsoft Store](https://apps.microsoft.com/detail/9N441SP6XHLD).

## Instalação

- Windows 10 versão 2004 (build 19041) ou posterior, em x64.
- Runtime .NET Desktop 10 para builds dependentes do runtime; o ZIP e o instalador
  self-contained incluem o runtime necessário.
- Para mídia online, o player mantém componentes auxiliares no cache `%LOCALAPPDATA%\mpv.net\Component\current`. É necessário acesso à rede para baixar ou atualizar componentes ausentes ou vencidos. Também é possível fornecer executáveis válidos na pasta do player ou no `PATH`.

O instalador registra os formatos de mídia comuns e playlists IPTV suportados
pelo fork e adiciona a pasta instalada ao `PATH` do Windows, permitindo executar
`mpvnet.exe` pelo terminal após abrir uma nova sessão. Imagens não são
associadas automaticamente na instalação; use `Config > Setup > Register image
file associations` no menu de contexto se quiser associá-las manualmente. O ZIP
portátil não registra associações no Windows. Ao desinstalar, o instalador remove
as associações do mpv.net criadas para imagens e os formatos registrados na
instalação, além de remover a pasta instalada do `PATH`.

### Extensões e associações de arquivos

As listas padrão do projeto incluem os formatos abaixo.

- Vídeo: `.mp4`, `.m4v`, `.mkv`, `.webm`, `.avi`, `.mov`, `.qt`, `.wmv`, `.asf`, `.flv`, `.f4v`, `.mpg`, `.mpeg`, `.mpe`, `.m1v`, `.m2v`, `.vob`, `.ts`, `.mts`, `.m2ts`, `.3gp`, `.3g2`, `.3gpp`, `.ogv`, `.ogg`, `.rm`, `.rmvb`, `.divx`, `.xvid`, `.dv`, `.nut`, `.nsv`, `.264`, `.265`, `.avc`, `.avs`, `.dav`, `.h264`, `.h265`, `.hevc`, `.m2t`, `.mj2`, `.mpv`, `.vpy`, `.y4m`, `.mxf`, `.m2p`, `.m4p`, `.mpeg2`, `.mpegts`;
- Áudio: `.mp3`, `.wav`, `.flac`, `.m4a`, `.m4b`, `.aac`, `.ogg`, `.opus`, `.wma`, `.alac`, `.aiff`, `.aif`, `.ape`, `.wv`, `.mka`, `.ac3`, `.dts`, `.eac3`, `.amr`, `.au`, `.mp2`, `.mpa`, `.mpc`, `.thd`, `.w64`, `.oga`, `.ogm`, `.dtshd`, `.dtshr`, `.dtsma`, `.mid`, `.midi`, `.caf`, `.tta`, `.tak`, `.rmi`, `.ra`, `.ram`, `.voc`, `.spx`;
- Playlists: `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue`, `.jspf`;
- Imagem, apenas quando associada manualmente pelo menu: `.avif`, `.bmp`, `.gif`, `.j2k`, `.jp2`, `.jpeg`, `.jpg`, `.jxl`, `.png`, `.svg`, `.tga`, `.tif`, `.tiff`, `.webp`.

As listas acima descrevem as extensões reconhecidas pelo frontend para filtros,
coleções, associações e tratamento de títulos. Elas não garantem que qualquer
arquivo com aquela extensão use um codec disponível no libmpv; o conteúdo real
e os decoders/demuxers da build determinam a reprodução.

O filtro e o instalador desktop também consultam as propriedades de extensão da
build do libmpv; por isso podem expor tipos adicionais ou diferentes dos
defaults. O manifesto da Store declara os defaults versionados no projeto. A
matriz completa e as diferenças da build auditada estão na
[documentação técnica de integração com mpv](developer/mpv-integration.md).

No pacote da Microsoft Store, as extensões de vídeo, áudio, playlist e imagem
são declaradas no manifesto e aparecem em `Abrir com` e em `Aplicativos padrão`.
O Windows não permite que um aplicativo altere silenciosamente o padrão do
usuário durante a instalação. Para tornar o MPV.NET o padrão, abra
`Configurações > Aplicativos > Aplicativos padrão`, procure o MPV.NET e escolha
as extensões desejadas; também é possível usar `Abrir com > Escolher outro
aplicativo` no Explorador de Arquivos.

## Controles de mídia do Windows

Durante a reprodução, o MPV.NET publica a mídia ativa nos controles de mídia do
Windows 10/11. O painel pode mostrar título, artista/álbum quando o arquivo
fornecer essas tags, artista do álbum, capa disponível, duração e posição,
além de Play, Pause, Stop, Previous, Next e seek. A busca só fica disponível
quando o mpv informa mídia com duração finita e positiva e capacidade de busca fora do cache. Fast
Forward/Rewind, quando oferecidos pelo Windows, saltam 5 segundos dentro dos
limites da mídia, sem mudar a velocidade de reprodução.

O recurso usa o HWND da janela principal e não depende do FluentFlyout. Ele
mantém a sessão e os comandos ativos também em fullscreen. A sessão é limpa
quando não há mídia carregada e durante o encerramento. URLs
com credenciais, query strings ou fragmentos não são publicados como título.

Para áudio local, os controles do Windows podem mostrar a mesma imagem usada na
janela do player. A busca online vem ativada para arquivos locais disponíveis.
O painel usa o ícone do aplicativo quando não há imagem segura. Consulte as
[regras de prioridade e privacidade](#capas-durante-a-reprodução-de-áudio).

A miniatura da janela na barra de tarefas oferece seis botões, na seguinte
ordem: Arquivo anterior, Capítulo anterior, Play/Pause, Próximo capítulo,
Próximo arquivo e apoio ao player. Play/Pause fica no centro dos cinco controles
de reprodução. Os botões de capítulos permanecem visíveis e ficam habilitados
quando a mídia carregada tem pelo menos um capítulo, como no controle do player;
os de arquivos seguem a disponibilidade de anterior/próximo na playlist.
Ao trocar de mídia, o estado é atualizado mesmo quando a reprodução está pausada.
Em tela cheia, os cinco controles de reprodução ficam desabilitados.
Essa barra usa a integração Win32 da miniatura. O botão de apoio abre a página
oficial no idioma da interface. Os outros botões usam os comandos do player. Play/Pause alterna o estado diretamente no mpv.
Os comandos de mídia mantêm a miniatura aberta após o clique, permitindo usar
outro controle em seguida; o botão de apoio fecha a miniatura ao abrir o
navegador. A barra aparece quando o Windows cria a miniatura da janela.

## Capas durante a reprodução de áudio

O player aproveita a capa incorporada ou externa selecionada pelo mpv. A mesma
imagem pode aparecer na janela e nos controles de mídia do Windows. Para áudio
local com seleção automática de imagens, a ordem padrão é capa selecionada pelo
mpv, capa online do álbum, foto verificada do artista e ícone do aplicativo em
um fundo neutro. Escolhas explícitas do mpv têm prioridade, assim como vídeo real.

**Áudio → Buscar capas de áudio online** no editor de configuração vem ativado.
Para desligar novas consultas e downloads, selecione **Não**, salve e reinicie.
Também é possível ajustar `mpvnet.conf` na
[pasta de configuração ativa](#configuração).

```ini
audio-artwork-online=no
```

Com o cache persistente ativado, imagens de sessões anteriores podem ser usadas
sem rede. Para capas, a consulta
ao MusicBrainz e ao Cover Art Archive usa o identificador do álbum ou os nomes do
artista e do álbum. Para fotos, o MusicBrainz identifica o artista pelo ID da
tag ou por uma única correspondência exata de nome. O Wikidata confirma o
vínculo, e o Wikimedia Commons fornece a imagem e os dados de autoria e licença.
Só são aceitas fotos confirmadas em domínio público ou sob CC0. Os serviços
consultados podem receber esses nomes, identificadores e o endereço IP da
conexão. A busca se limita a arquivos disponíveis em discos fixos locais e
usa as tags da mídia como fonte de identidade.

Para dar prioridade à capa online do álbum, use a preferência abaixo.

```ini
audio-artwork-prefer-online=yes
```

A preferência se aplica a capas baixadas ou em cache. As consultas dependem da
opção de busca online. Escolhas explícitas nativas de
imagem/faixa em `mpv.conf`, CLI ou perfis continuam prevalecendo; uma seleção
manual de vídeo durante a reprodução também impede substituição automática.
`audio-display=no` mantém imagens desligadas na janela.

Para escolher uma imagem local, use a opção nativa `cover-art-file` em
`mpv.conf` ou na linha de comando, sem editar o áudio:

```powershell
.\mpvnet.exe --process-instance=multi --cover-art-file="C:\Musicas\minha-capa.jpg" "C:\Musicas\faixa.flac"
```

Também é possível escolher uma das imagens disponíveis como faixa de vídeo no
menu **Track**. Depois de acrescentar uma capa à pasta, o comando nativo
`rescan-external-files` permite redescobri-la sem reiniciar o áudio. A busca de
arquivos e a prioridade incorporada/externa continuam seguindo `cover-art-auto`
e `audio-display` do mpv; uma foto local pode ser escolhida da mesma forma.

Por padrão, imagens validadas ficam em `Artwork` na raiz de dados local do
aplicativo, inclusive no modo portátil. O MSIX usa sua área privada. Para
desativar o cache persistente, abra **Áudio → Manter cache de capas entre
sessões** no editor, selecione **Não** e reinicie o player. A opção equivalente
em `mpvnet.conf` é `audio-artwork-cache=no`. Nesse modo, a sessão usa arquivos
temporários próprios, removidos no fechamento normal. O cache anterior fica
disponível para quando você reativar a opção. **Buscar capas de áudio online**
continua controlando o acesso à rede.

O cache tem limite de 256 MiB e 3.072 arquivos, com retenção de até 30 dias.
Quando o cache persistente está ativo, a limpeza ocorre ao iniciar o player e
durante as gravações, inclusive com a busca desligada. Ausências são lembradas
por um dia e falhas transitórias por cinco minutos. A busca e a decodificação
ocorrem em segundo plano durante a reprodução.

## Configuração

Na área de vídeo, o clique esquerdo alterna Play/Pause quando há mídia
carregada; o duplo clique alterna tela cheia sem pausar ou retomar a mídia.
O clique simples aguarda o intervalo necessário para distinguir o duplo clique
do Windows. `p`, `P` e `Space` também alternam Play/Pause. Personalizações do
`input.conf` prevalecem; menus completos existentes mantêm os atalhos anteriores.
Veja as tabelas de [atalhos de teclado e mouse](ATALHOS.md#controles-padrão-de-reprodução),
os exemplos para [personalizar ou desativar um atalho](ATALHOS.md#personalizar-ou-desativar-um-atalho)
e a configuração [opcional de J/K/L](ATALHOS.md#jkl-como-configuração-opcional),
com os conflitos dos comandos atuais explicados. Os exemplos só são aplicados
se você editar sua configuração; a interface e os controles padrão permanecem
os mesmos.

A barra de título oferece um botão **Tela cheia**, imediatamente antes de
minimizar. Ele executa `cycle fullscreen`, como a opção do menu. O tooltip e
o nome acessível acompanham o idioma da interface. Os três botões do Windows
continuam nativos; o título usa reticências quando falta espaço. O botão
fica oculto em tela cheia, com `border=no` ou `title-bar=no`. `Esc` mantém seu
comportamento atual: fecha primeiro o menu aberto ou sai da tela cheia.

A pasta ativa vale para o mpv e o frontend. Para escolher um perfil, use
`--config-dir=...`, `MPVNET_HOME` ou `portable_config`, conforme a
[ordem de busca](CONFIGURACAO.md#ordem-de-busca). Cache, componentes e
temporários usam a raiz de dados local, inclusive no modo portátil.
`MPVNET_LOCAL_APPDATA` seleciona outra raiz de dados.

Execute `mpvnet.exe --diagnose-paths` para conferir esses locais sem criar
diretórios. Caminhos relativos na linha de comando usam a pasta de lançamento.
Nas variáveis do aplicativo, usam a pasta do executável.

No pacote Store, ações para gerenciar PATH ou associações abrem as configurações
de aplicativos do Windows. O pacote declara os aliases e formatos suportados;
o player não grava seu diretório versionado de WindowsApps no PATH persistente.

Edite `mpv.conf` para opções do motor, `mpvnet.conf` para opções da
interface e `input.conf` para atalhos. O guia de
[configuração rápida](CONFIGURACAO.md#arquivos-principais) descreve os
demais arquivos, incluindo `menu.conf` e `settings.xml`.

Idiomas:

- interface: configure `language` em `mpvnet.conf`, por exemplo `language=english` ou `language=portuguese-brazil`;
- áudio preferido: configure `alang` em `mpv.conf`;
- legenda preferida: configure `slang` em `mpv.conf`;
- legenda desligada: use `sid=no` ou selecione sem legenda na interface.

Se `language` não estiver definido, o mpv.net inicia no idioma do Windows quando houver mapeamento suportado; caso contrário, usa inglês. Variantes regionais usam o idioma base traduzido do projeto: por exemplo, `en-US` e `en-GB` usam inglês, `es-MX` usa espanhol, `pt-AO` usa português de Portugal, e `zh-Hans` usa chinês simplificado.

A interface também oferece 繁體中文（台灣）, 繁體中文（香港）, हिन्दी,
العربية, Українська e Bahasa Indonesia. Os valores de `language` são,
respectivamente, `chinese-taiwan`, `chinese-hongkong`, `hindi`, `arabic`,
`ukrainian` e `indonesian`; códigos como `zh-TW`, `zh-HK`, `hi-IN`, `ar-SA`,
`uk-UA` e `id-ID` também são aceitos. `zh-Hant` usa Taiwan, `zh-MO` usa Hong
Kong e `zh-SG` usa chinês simplificado. Árabe espelha os diálogos e menus, com
caminhos e comandos em direção normal; a formatação regional do Windows é
preservada. É possível pré-visualizar a troca no editor sem perder as opções
editadas. Os nomes dos idiomas na lista e na opção selecionada acompanham o
idioma da interface: por exemplo, `Francês` em português, `French` em inglês e
`Français` em francês. Reinicie para aplicar todas as configurações.

O mpv.net normaliza códigos como `eng`, `por`, `pt_BR`, `es_MX` e nomes comuns
de idioma, mas a seleção final de áudio e legenda continua sendo feita pelo
mpv/libmpv.

## Linha de comando

O frontend aceita arquivos locais, playlists e URLs de streaming compatíveis com o mpv.
Para definir o título visível, informe o título antes da URL ou use `--title`.

```powershell
mpvnet.exe "Nome do vídeo" "https://example.com/video.mp4"
mpvnet.exe --title "Nome do video" "https://example.com/video.mp4"
```

Quando `auto-load-folder=yes` está ativo e o usuário abre um arquivo local de
áudio ou vídeo, o mpv.net acrescenta à playlist interna os outros arquivos de
áudio, vídeo e playlists compatíveis da mesma pasta. Quando o arquivo aberto é
uma imagem, o carregamento automático da pasta adiciona apenas outras imagens
compatíveis da mesma pasta.

Para abrir uma coleção inteira, use **Arquivo > Abrir arquivos de mídia em uma
unidade ou pasta (incluindo subpastas)...**. Essa opção procura áudio, vídeo e
playlists em todos os níveis, em segundo plano, independentemente de
`auto-load-folder`. Imagens, legendas, scripts, ISOs e arquivos desconhecidos
não entram nessa coleção; abrir uma imagem individual continua adicionando
somente imagens da mesma pasta conforme a configuração existente.

A coleção é ordenada naturalmente pelos caminhos relativos à pasta escolhida
(por exemplo, episódio 2 antes de episódio 10), começa no primeiro item e
substitui a fila atual somente quando encontra mídias utilizáveis. Playlists
locais são expandidas; seus títulos têm preferência sobre o nome do mesmo
arquivo encontrado diretamente. URLs conservam a ordem de sua playlist na
posição correspondente ao arquivo de playlist. Títulos passam pela normalização
comum, e caminhos/URLs repetidos são removidos sem eliminar arquivos homônimos
em pastas diferentes.

Todos os itens dessas coleções já entram na playlist com título normalizado,
inclusive os que ainda não foram reproduzidos. A regra remove a extensão
reconhecida, troca pontos, `_`, `-` e outros separadores configurados por
espaços, reduz espaços repetidos e coloca a primeira letra de cada palavra em
maiúscula. Por exemplo, `1.2 - Gerando_o projeto.mp4` aparece como
`1 2 Gerando O Projeto`. Isso também vale para arquivos locais abertos pelo
diálogo, clipboard, drag/drop e linha de comando. Os arquivos não são renomeados.

DVD/Blu-ray reconhecidos pela estrutura `VIDEO_TS/VIDEO_TS.IFO` ou
`BDMV/index.bdmv` usam a reprodução especial de disco. É possível selecionar
a raiz ou a própria pasta `VIDEO_TS`/`BDMV`; nesse último caso, o dispositivo
recebe a pasta pai. Isso vale também para unidades ópticas acessíveis; unidades
de dados e removíveis comuns usam a busca de arquivos. Estruturas ópticas
encontradas dentro de uma coleção são ignoradas por inteiro; abra cada disco
separadamente. Pastas chamadas `VIDEO_TS` e `BDMV` são reservadas e não são
percorridas mesmo quando incompletas, evitando adicionar segmentos VOB/M2TS.
ISOs continuam com o fluxo separado de abertura de arquivo.

Links simbólicos, junctions e outros reparse points não são percorridos nem
incluídos por referência de playlist. Erros em subpastas são registrados e a
busca continua nos locais acessíveis. Uma pasta vazia, sem mídias ou ilegível
mostra uma mensagem no idioma da interface e preserva a reprodução atual.
Entradas locais de playlists mantêm a resolução relativa existente, inclusive
quando apontam para mídias fora da pasta escolhida, respeitando as exclusões
de links e estruturas ópticas.

Cada abertura de arquivo expande a pasta uma única vez. Se um item da playlist
falhar durante a reprodução, o mpv.net registra o erro e continua no próximo
item disponível, sem reconstruir a playlist nem fechar o player.

Playlists locais nos formatos `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`,
`.wpl`, `.cue` e `.jspf` são expandidas pelo frontend antes do envio ao
mpv/libmpv. O mpv.net usa o título informado pela playlist quando disponível,
mantém apenas entradas de áudio ou vídeo ou URLs de streaming e ignora itens
repetidos que apontem para o mesmo caminho ou URL. Se uma instancia do player
já estiver aberta, o modo de instância determina o encaminhamento da mídia. Consulte as opções de instância na [configuração técnica](developer/configuration.md#mpvnetconf).

Arquivos de playlist, arquivos encontrados na mesma pasta e colecoes entregues
pelo mpv passam pela mesma regra: primeiro os títulos são normalizados e depois
as entradas repetidas são removidas pelo caminho ou URL, preservando a ordem.

URLs HTTP/HTTPS novas são enviadas diretamente ao mpv/libmpv, sem uma sondagem
HTTP bloqueante no frontend. Isso evita atraso antes do início do carregamento,
inclusive para URLs sem extensão. A detecção de mídia ou playlist remota fica a
cargo do mpv/libmpv.

Para páginas de serviços de mídia, o MPV.NET utiliza o fluxo nativo do mpv com
o yt-dlp como resolvedor genérico. Esse caminho pode atender, conforme os
extractors disponíveis na versão instalada, serviços como YouTube, Bilibili,
Niconico, Naver, Dailymotion, Twitch, Vimeo e SoundCloud, além de novos serviços
que o yt-dlp passe a reconhecer. O frontend não mantém uma lista de domínios
permitidos nem implementa extractors próprios.

A disponibilidade depende do serviço, do conteúdo, da região e da versão do
yt-dlp. Alguns conteúdos exigem autenticação ou cookies configurados pelo
usuário nos mecanismos do mpv/yt-dlp; conteúdo protegido por DRM pode não ser
reproduzível. O MPV.NET não coleta senhas, não contorna DRM ou paywall e não
promete compatibilidade permanente com serviços externos.

O frontend pode aplicar cache por item de rede. Ajuste a política e o
histórico de URLs em `mpvnet.conf`.

```ini
automatic-network-cache=yes
save-network-urls-in-recent=no
```

URLs de rede não são salvas em `Recent Files` por padrão. A lista fica no
`settings.xml`, em texto legível. Para habilitar o histórico de URLs, configure
`save-network-urls-in-recent=yes` em `mpvnet.conf`. URLs com credenciais na
autoridade, fragmentos ou parâmetros de consulta reconhecidos como credenciais
ou tokens continuam fora da lista. Como nomes de parâmetros opacos podem conter
segredos que não são reconhecidos, habilite a opção apenas para URLs que você
confia não conter dados sigilosos. Essa política não altera a URL usada na
reprodução atual.

No editor, **Cache → Cache automático de streaming** oferece apenas **Sim/Não**.
O padrão é ativado. `automatic-network-cache=no` desliga todas as opções
acrescentadas por essa política; as configurações nativas do mpv continuam válidas.
A mudança vale para o próximo item aberto, inclusive em playlists.

Configurações antigas com `low-latency`, `balanced` ou `resilient` passam a usar
a política automática; `off` e um `automatic-network-cache=no` antigo continuam
desativados. A leitura não regrava o arquivo. Ao salvar uma alteração no editor,
o formato antigo é removido e uma cópia é preservada em
`mpvnet.conf.network-cache.backup`. Valores iguais ao padrão podem ser omitidos.
A opção antiga também continua aceita na linha de comando como alias de migração.

A política se aplica a itens de rede. Os tetos variam de 64 a 512 MiB à
frente, de acordo com a classificação. O preenchimento depende da taxa de
entrada, da janela do servidor e dos limites de leitura antecipada. O player
mantém limites conservadores quando faltam metadados. Uma extensão
`.m3u8`/`.mpd` ou uma janela DVR com duração finita, por si só, não
identifica VOD. Consulte a [tabela de limites](developer/mpv-integration.md#cache-automático-por-item).

Em falha transitória comprovada, streams contínuos RTSP, RTMP, SRT, UDP e TCP
podem tentar reconexão até três vezes, com esperas de 1, 2 e 4 segundos. HLS e
DASH só entram nessa política depois de carregarem com evidência de live pelo
resolvedor, ou no caminho direto já existente sem duração finita.
Arquivos locais, HTTP progressivo, vídeo sob demanda, fim normal e playlists com
próximo item não são reiniciados. Trocar a mídia, usar Stop ou fechar o player
cancela a tentativa pendente.

O cache automático respeita as opções explícitas do mpv e aplica os valores
por item, inclusive em playlists, após próximo/anterior e com `loadlist`.
O diretório padrão do cache é `Cache` na raiz de dados do aplicativo. Apenas
uma opção explícita ativa cache em disco. Em VOD confirmado, a margem inicial
é de 3 segundos. Após interrupções observadas, a margem de retomada pode chegar
a 8 segundos em VOD ou a 2 segundos em live e fontes incertas. Pausa e busca
ficam fora dessa contagem. Para limites, precedência e diagnóstico, consulte
[Cache automático por item](developer/mpv-integration.md#cache-automático-por-item).

No YouTube, uma URL sem `list=` continua sendo tratada como vídeo individual.
Quando a URL contém `list=`, o MPV.NET preserva todos os parâmetros e habilita
a expansão nativa do `ytdl_hook`/`yt-dlp`; a fila resultante pertence ao mpv e
funciona com próximo, anterior e avanço automático. Em URLs
`watch?v=...&list=...&index=...`, o hook atual procura primeiro o vídeo indicado
na playlist e usa `index=` quando ele corresponde, sem criar uma
fila paralela nem reiniciar o item atual. Depois da expansão, o MPV.NET normaliza os títulos
dos itens e remove entradas repetidas pelo caminho ou URL, mantendo a fila nativa
e o item que já estiver em reprodução. Como essa fila só existe depois que o
yt-dlp a entrega ao mpv, a preparação aguarda a expansão ficar estável. O mesmo
estado de caminhos e títulos não é preparado novamente; uma alteração posterior
de título pode exigir nova preparação, preservando o item em reprodução.

Para escolher outro executável do extrator, use
`script-opts-append=ytdl_hook-ytdl_path=C:\Ferramentas\yt-dlp.exe` em `mpv.conf`
ou `ytdl_path=C:\Ferramentas\yt-dlp.exe` em `script-opts/ytdl_hook.conf`.
Esses valores explícitos têm prioridade sobre o caminho automático do player.
`ytdl-path` não é uma opção nativa do mpv; substitua instruções antigas por
uma das formas acima.

Se uma URL do YouTube falhar com `unrecognized file format`, teste o extrator
diretamente e confira a mensagem original:

```powershell
.\yt-dlp.exe --no-playlist --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
.\yt-dlp.exe --no-playlist --cookies-from-browser chrome --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
```

Troque `chrome` por `edge`, `firefox` ou outro navegador que contenha a sessão
autenticada, se necessário. Se preferir exportar cookies para um arquivo, use
um arquivo no formato Mozilla/Netscape e garanta que a primeira linha seja
`# HTTP Cookie File` ou `# Netscape HTTP Cookie File`.

As instruções de cookies e autenticação do YouTube mudam com o `yt-dlp`.
Consulte a [orientação oficial para cookies](https://github.com/yt-dlp/yt-dlp/wiki/Extractors#exporting-youtube-cookies)
antes de exportar. Cookies de sessão dão acesso à conta. Use-os apenas quando
necessário e mantenha arquivos exportados fora de logs e chamados de suporte.

`Sign in to confirm you're not a bot` indica uma verificação exigida pelo
YouTube. Confira o acesso pelo navegador na mesma rede e as orientações oficiais
do extrator antes de configurar cookies. O player informa essa verificação
separadamente de conteúdo privado ou restrito por idade e não tenta reconectar
automaticamente para esse erro. Atualizar FFmpeg não remove essa exigência.
Enumerar uma playlist em modo `--flat-playlist` confirma apenas os metadados
da coleção; a obtenção dos formatos e a reprodução de cada vídeo são verificações
distintas. Os testes usam a URL completa, preservando `v`, `list` e `index`.

O YouTube pode exigir também um PO Token. Nesse caso, consulte o diagnóstico
do extrator e as opções externas descritas na [documentação técnica](developer/mpv-integration.md#diagnóstico-da-cadeia-online).

Falhas auxiliares ao criar ou expandir playlists não devem impedir a tentativa
de reprodução da mídia principal. Quando houver uma URL ou caminho válido, o
frontend tenta enviar a mídia bruta ao mpv/libmpv; o título da linha de comando continua disponível como metadado visual.

Quando uma mídia online falha e não há uma reconexão controlada em andamento,
o player mostra uma mensagem curta no idioma da interface para categorias como
autenticação, verificação antibot, restrição regional, conteúdo removido, playlist, formato e URL.
O detalhe técnico original permanece somente no log, com query strings,
cookies, tokens, sessão e cabeçalhos de autorização sanitizados.

Os componentes auxiliares de runtime usam o cache local em
`%LOCALAPPDATA%\mpv.net\Component\current`. Em instalações pela Microsoft
Store, o cache fica em `LocalCache\Local\mpv.net\Component\current` dentro da
pasta privada do pacote. `libmpv-2.dll`,
`libmpv-2-v3.dll` e `MediaInfo.dll` continuam ao lado do executável ou no pacote
preparado. O player baixa `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`,
`yt-dlp.exe` e `deno.exe` quando necessário. Uma cópia válida no cache `current`
tem prioridade, seguida pelo cache legado, pela pasta do executável e, por
último, pelo `PATH`. Os downloads são preparados em `Component\staging`,
validados e promovidos como uma geração completa.

A janela abre antes de o player verificar os componentes em segundo plano.
O bootstrap valida o SHA-256 publicado e a arquitetura x64, reutiliza
componentes válidos e preserva a geração anterior durante atualizações. Uma
transferência interrompida pode ser retomada na próxima abertura se a URL e
o hash do asset permanecerem iguais. Consulte os [prazos, a validação e os
fallbacks](developer/configuration.md#componentes-auxiliares-de-runtime).

Execute `mpvnet.exe --diagnose-components` para conferir origem, versões,
datas e integridade dos componentes sem iniciar downloads. O helper
`mpvnet.com` acompanha a instalação por compatibilidade.

Exemplo:

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "https://example.com/live/index.m3u8"
mpvnet.exe "Canal IPTV" "https://example.com/live/index.m3u8"
```

### Integração por parâmetros: vídeo, título e idioma

Para outras aplicações abrirem o player portátil com um vídeo, título e idioma
de interface definidos, use este formato no PowerShell:

```powershell
& "C:\Players\MPV.NET\mpvnet.exe" --process-instance=multi --alang= --language=pt-BR --title="Meu vídeo" "C:\Videos\video.mp4"
```

| Argumento | Função |
| --- | --- |
| `--process-instance=multi` | Abre uma nova instância, aplicando o título e o idioma nesta abertura mesmo se já houver outro player aberto. |
| `--language=pt-BR` | Define o idioma da interface; também aceita o nome público, como `--language=portuguese-brazil`. Use `=` entre a opção e o idioma. |
| `--alang=` | Limpa a preferência de áudio nesta execução para impedir que um `alang` salvo substitua o idioma da interface. |
| `--title="Meu vídeo"` | Define o título visual da janela e da mídia. O player aplica sua normalização de títulos. |
| `"C:\Videos\video.mp4"` | Caminho do vídeo. Pode ser substituído por uma URL compatível com o mpv. |

Coloque caminhos e títulos com espaços entre aspas. Use `&` no PowerShell
para chamar o executável quando o caminho estiver entre aspas. No Prompt de
Comando (`cmd.exe`), omita o `&` inicial.

Substitua `pt-BR` por um dos códigos da tabela para abrir o player em outro idioma.

| Idioma da interface | Código |
| --- | --- |
| Alemão | `de` |
| Árabe | `ar-SA` |
| Búlgaro | `bg` |
| Chinês simplificado | `zh-CN` |
| Chinês tradicional (Hong Kong) | `zh-HK` |
| Chinês tradicional (Taiwan) | `zh-TW` |
| Coreano | `ko` |
| Espanhol | `es` |
| Francês | `fr` |
| Hindi | `hi-IN` |
| Indonésio | `id-ID` |
| Inglês | `en` |
| Italiano | `it` |
| Japonês | `ja` |
| Polonês | `pl` |
| Português do Brasil | `pt-BR` |
| Português de Portugal | `pt-PT` |
| Russo | `ru` |
| Turco | `tr` |
| Ucraniano | `uk-UA` |

Para abrir uma URL, mantenha as opções e troque somente a mídia:

```powershell
.\mpvnet.exe --process-instance=multi --alang= --language=pt-BR --title="Canal ao vivo" "https://example.com/live/index.m3u8"
```

**Fallback para inglês:** um idioma explicitamente informado e não reconhecido
usa inglês. Para garantir inglês quando a aplicação não tiver um idioma para
enviar, passe `--language=en`.

```powershell
# Idioma não suportado: interface em inglês.
.\mpvnet.exe --process-instance=multi --alang= --language=xx-ZZ --title="Meu vídeo" "C:\Videos\video.mp4"

# Inglês explícito, independentemente do idioma salvo ou do Windows.
.\mpvnet.exe --process-instance=multi --alang= --language=en --title="Meu vídeo" "C:\Videos\video.mp4"
```

Omitir `--language` permite usar o idioma salvo em `mpvnet.conf`; sem esse valor,
o player usa o idioma do Windows quando suportado e inglês nos demais casos.
O último argumento `--language=...` prevalece sobre os anteriores e sobre
`language` em `mpvnet.conf`. A opção da linha de comando vale para a execução
e não grava essa preferência em `mpvnet.conf`.

`--language` seleciona a interface. As preferências de áudio e legenda são
opções separadas do mpv (`--alang` e `--slang`). Neste fork, um `alang` não vazio
também pode selecionar o idioma da interface após a inicialização. Por isso,
os exemplos usam `--alang=`: isso limpa a preferência de áudio da execução,
sem desativar o áudio nem alterar `mpv.conf`. Se a integração também definir
`--alang`, mantenha seu idioma alinhado ao da interface. `--slang` não participa
dessa escolha. Nomes públicos e variantes aceitas estão em
[Localização](developer/localization.md#idiomas-disponíveis).

No modo padrão de instância única, uma chamada pode encaminhar apenas a mídia
ao player já aberto, sem aplicar o novo título ou idioma. Por isso, mantenha
`--process-instance=multi` nas integrações que precisam definir esses valores
por abertura.

No PowerShell 7.3 ou posterior, monte os argumentos de outra aplicação
como itens separados. O exemplo usa inglês quando o código do idioma está vazio
e executa o player a partir da pasta do executável.

```powershell
$player = 'C:\Players\MPV.NET\mpvnet.exe'
$video = 'C:\Videos\video.mp4'
$titulo = 'Meu vídeo'
$idioma = 'pt-BR'

if ([string]::IsNullOrWhiteSpace($idioma)) {
    $idioma = 'en'
}

$argumentosPlayer = @(
    '--process-instance=multi'
    '--alang='
    "--language=$idioma"
    "--title=$titulo"
    $video
)

Push-Location -LiteralPath (Split-Path -LiteralPath $player)
try {
    & $player @argumentosPlayer
} finally {
    Pop-Location
}
```

Distribua o conteúdo completo do ZIP portátil, incluindo DLLs, `Locale` e
scripts. Copiar apenas `mpvnet.exe` não fornece as dependências nem as
traduções. Para manter a configuração junto do executável, crie
`portable_config` ao lado dele, conforme [Modo portátil](#modo-portátil).

## Visibilidade dos controles OSC

`Del` alterna os controles na tela entre **Sempre visível**, **Oculto** e
**Automático** (partindo do padrão automático). A mesma ação está no menu
**View > Toggle OSC Visibility**, com o nome traduzido no idioma da interface.
A mudança por atalho vale somente para a sessão atual.

Para iniciar sempre com os controles visíveis, abra a pasta de configuração
ativa pelo menu **Config > Open Config Folder** (`Ctrl+f`) e acrescente estas
opções em `script-opts/osc.conf` (crie a pasta e o arquivo se necessário):

```ini
visibility=always
boxvideo=yes
sub_margins=yes
```

No layout padrão `bottombar`, `boxvideo` reserva espaço para o OSC e ajusta o
vídeo a essa área. `sub_margins` solicita o ajuste das legendas de texto; legendas
com posicionamento próprio podem exigir conferência visual. Isso usa o OSC
existente, sem criar uma barra nativa nem trocar o desenho dos controles.

Preserve as demais opções se o arquivo já existir e reinicie o player após
editar. O OSC também aceita opções `osc-*` em `script-opts` do `mpv.conf` ou
da linha de comando, que podem substituir os valores do arquivo. Margens de
vídeo configuradas explicitamente também podem impedir o ajuste por `boxvideo`.
Para voltar ao comportamento padrão, remova apenas as opções acrescentadas
ou use `visibility=auto`, `boxvideo=no` e `sub_margins=no`.

Veja como consultar e diagnosticar
os atalhos em [Atalhos](ATALHOS.md#consultar-e-diagnosticar-atalhos).

## Modo portátil

Para guardar a configuração junto do executável no ZIP portátil, siga a
[estrutura de `portable_config`](CONFIGURACAO.md#estrutura-portátil). Os
dados de cache e diagnóstico ficam na raiz de dados local. Use
`mpvnet.exe --diagnose-paths` para conferir os caminhos da instalação.

## Documentação técnica

Se você for manter ou auditar o fork, os pontos de entrada mais úteis são:

- [Arquitetura](developer/architecture.md)
- [Configuração](developer/configuration.md)
- [Integração com mpv/libmpv](developer/mpv-integration.md)
- [Interface Windows](developer/windows-ui.md)
- [Build e release](developer/build-release.md)
- [Localização](developer/localization.md)

## Diferenças em relação ao mpv

O mpv.net preserva a maior parte do comportamento do mpv e adiciona interface gráfica, menu de contexto, atalhos globais, editor de configuração e integração com o Windows.

## Suporte

A contribuição para o MPV.NET é voluntária e o player continua gratuito.

- **Apoie o player:** [Acessar a página oficial de apoio](https://www.gestaodesistemas.com.br/mpvnet)
- **Repositório:** [WandersondeSouza/mpv.net](https://github.com/WandersondeSouza/mpv.net)
  (também aberto em `Ajuda > Website mpv.net`, atalho `Ctrl+a`)
- **Problemas e sugestões:** [Issues do MPV.NET](https://github.com/WandersondeSouza/mpv.net/issues)
- **Vulnerabilidades:** [Política de segurança](../SECURITY.md)
- **E-mail:** [wanderson_souza@hotmail.com](mailto:wanderson_souza@hotmail.com)
- **Manual oficial do mpv:** [mpv.io/manual/master](https://mpv.io/manual/master/)
