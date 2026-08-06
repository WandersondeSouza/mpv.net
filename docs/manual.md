# Manual do MPV.NET Media Player

O MPV.NET Media Player é um player de mídia para Windows baseado no mpv/libmpv e no mpv.net.

Este manual cobre apenas o que importa para o uso cotidiano do fork.

## Tópicos

- [Download](#download)
- [Instalação](#instalação)
- [Configuração](#configuração)
- [Linha de comando](#linha-de-comando)
- [Modo portátil](#modo-portátil)
- [Documentação técnica](#documentação-técnica)
- [Suporte](#suporte)
- [Diferenças em relação ao mpv](#diferenças-em-relação-ao-mpv)

## Download

Use as releases do fork em:

`https://github.com/WandersondeSouza/mpv.net/releases`

O aplicativo também está publicado na Microsoft Store:

- Link profundo: `ms-windows-store://pdp/?productid=9N441SP6XHLD`
- URL da Web Store: [https://apps.microsoft.com/detail/9N441SP6XHLD](https://apps.microsoft.com/detail/9N441SP6XHLD)

## Instalação

- Windows 10 ou superior.
- Runtime .NET Desktop compatível com o projeto.
- Para streaming, o player baixa e atualiza `yt-dlp.exe` no cache de componentes em `%LOCALAPPDATA%\mpv.net\Component`. Se quiser testar manualmente, também vale deixá-lo ao lado do executável ou no `PATH`.

O instalador registra os formatos de mídia comuns e playlists IPTV suportados
pelo fork e adiciona a pasta instalada ao `PATH` do Windows, permitindo executar
`mpvnet.exe` pelo terminal após abrir uma nova sessão. Imagens não são
associadas automaticamente na instalação; use `Config > Setup > Register image
file associations` no menu de contexto se quiser associá-las manualmente. Ao
desinstalar, o instalador remove também as associações do mpv.net criadas
manualmente para imagens e remove a pasta instalada do `PATH`.

As associações automáticas atuais incluem:

- Vídeo: `.mp4`, `.m4v`, `.mkv`, `.webm`, `.avi`, `.mov`, `.qt`, `.wmv`, `.asf`, `.flv`, `.f4v`, `.mpg`, `.mpeg`, `.mpe`, `.m1v`, `.m2v`, `.vob`, `.ts`, `.mts`, `.m2ts`, `.3gp`, `.3g2`, `.3gpp`, `.ogv`, `.ogg`, `.rm`, `.rmvb`, `.divx`, `.xvid`, `.dv`, `.nut`, `.nsv`, `.264`, `.265`, `.avc`, `.avs`, `.dav`, `.h264`, `.h265`, `.hevc`, `.m2t`, `.mj2`, `.mpv`, `.vpy`, `.y4m`, `.mxf`, `.m2p`, `.m4p`, `.mpeg2`, `.mpegts`;
- Áudio: `.mp3`, `.wav`, `.flac`, `.m4a`, `.m4b`, `.aac`, `.ogg`, `.opus`, `.wma`, `.alac`, `.aiff`, `.aif`, `.ape`, `.wv`, `.mka`, `.ac3`, `.dts`, `.eac3`, `.amr`, `.au`, `.mp2`, `.mpa`, `.mpc`, `.thd`, `.w64`, `.oga`, `.ogm`, `.dtshd`, `.dtshr`, `.dtsma`, `.mid`, `.midi`, `.caf`, `.tta`, `.tak`, `.rmi`, `.ra`, `.ram`, `.voc`, `.spx`;
- Playlists: `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue`, `.jspf`;
- Imagem, apenas quando associada manualmente pelo menu: `.avif`, `.bmp`, `.gif`, `.j2k`, `.jp2`, `.jpeg`, `.jpg`, `.jxl`, `.png`, `.svg`, `.tga`, `.tif`, `.tiff`, `.webp`.

## Configuração

O mpv.net lê a configuração nesta ordem:

1. `MPVNET_HOME`
2. `portable_config`
3. `%LOCALAPPDATA%\mpv.net`

Arquivos principais:

- `mpv.conf`
- `mpvnet.conf`
- `input.conf`
- `global-input.conf`
- `settings.xml`
- `theme.conf`

Idiomas:

- interface: configure `language` em `mpvnet.conf`, por exemplo `language=english` ou `language=portuguese-brazil`;
- audio preferido: configure `alang` em `mpv.conf`;
- legenda preferida: configure `slang` em `mpv.conf`;
- legenda desligada: use `sid=no` ou selecione sem legenda na interface.

Se `language` não estiver definido, o mpv.net inicia no idioma do Windows quando houver mapeamento suportado; caso contrário, usa inglês. Variantes regionais usam o idioma base traduzido do projeto: por exemplo, `en-US` e `en-GB` usam inglês, `es-MX` usa espanhol, `pt-AO` usa português de Portugal, e `zh-Hans` usa chinês simplificado.

O mpv.net normaliza codigos como `eng`, `por`, `pt_BR`, `es_MX` e nomes comuns
de idioma, mas a selecao final de audio e legenda continua sendo feita pelo
mpv/libmpv.

## Linha de comando

O frontend aceita arquivos locais, playlists e URLs de streaming compativeis com o mpv.
Tambem aceita um titulo separado antes de uma URL. Esse texto e usado apenas
como metadado visual:

```powershell
mpvnet.exe "Nome do video" "https://example.com/video.mp4"
mpvnet.exe --title "Nome do video" "https://example.com/video.mp4"
```

Quando `auto-load-folder=yes` esta ativo e o usuario abre um arquivo local de
audio ou video, o mpv.net adiciona a playlist interna os outros arquivos de
audio, video e playlists compativeis da mesma pasta. Quando o arquivo aberto e
uma imagem, o carregamento automatico da pasta adiciona apenas outras imagens
compativeis da mesma pasta.

Cada abertura de arquivo expande a pasta uma única vez. Se um item da playlist
falhar durante a reprodução, o mpv.net registra o erro e continua no próximo
item disponível, sem reconstruir a playlist nem fechar o player.

Playlists locais nos formatos `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`,
`.wpl`, `.cue` e `.jspf` sao expandidas pelo frontend antes do envio ao
mpv/libmpv. O mpv.net usa o titulo informado pela playlist quando disponivel,
mantem apenas entradas de audio/video ou URLs de streaming e ignora itens
repetidos que apontem para o mesmo caminho ou URL. Se uma instancia do player
ja estiver aberta, os itens da playlist sao adicionados a playlist atual.

URLs HTTP/HTTPS novas são enviadas diretamente ao mpv/libmpv, sem uma sondagem
HTTP bloqueante no frontend. Isso evita atraso antes do início do carregamento,
inclusive para URLs sem extensão. A detecção de mídia ou playlist remota fica a
cargo do mpv/libmpv.

Ao abrir uma URL de streaming, o frontend pode aplicar opcoes locais e
conservadoras de cache. A politica e controlada por `mpvnet.conf`:

```text
automatic-network-cache=yes
network-cache-profile=balanced
```

Os perfis sao `off`, `low-latency`, `balanced` e `resilient`. Arquivos locais
nao recebem opcoes automaticas de rede. HTTP progressivo, HLS/DASH, arquivos
FTP/SFTP e transmissao ao vivo (RTSP/RTMP/UDP/TCP) usam limites diferentes.
RTSP nao recebe `network-timeout`, pois essa opcao pode quebrar o transporte.

Opcoes explicitamente definidas no `mpv.conf` ou na linha de comando tem
precedencia sobre os valores automaticos. O diretorio temporario do cache
continua em `%LOCALAPPDATA%\mpv.net\Cache`, mas `cache` e `cache-on-disk` nao
sao mais forcados globalmente pelo frontend.

Playlists locais sao expandidas pelo frontend e seus itens sao enviados
individualmente por `loadfile`, preservando ordem, duplicatas filtradas,
titulos e politica especifica do item.

Se uma URL do YouTube falhar com `unrecognized file format`, teste o extrator
diretamente. Em muitos casos o problema e autenticacao ou cookies do navegador,
nao o frontend:

```powershell
.\yt-dlp.exe --no-playlist --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
.\yt-dlp.exe --no-playlist --cookies-from-browser chrome --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
```

Troque `chrome` por `edge`, `firefox` ou outro navegador que contenha a sessao
autenticada, se necessario. Se preferir exportar cookies para um arquivo, use
um arquivo no formato Mozilla/Netscape e garanta que a primeira linha seja
`# HTTP Cookie File` ou `# Netscape HTTP Cookie File`.

Para reduzir o risco de cookies rotacionados pelo YouTube, a wiki do `yt-dlp`
recomenda abrir uma janela privada/incognita, fazer login nela, acessar
`https://www.youtube.com/robots.txt` na mesma aba e exportar os cookies logo em
seguida. Evite manter essa mesma sessao privada aberta depois da exportacao.

Se o video continuar falhando mesmo com cookies validos, o YouTube pode estar
exigindo PO Token em vez de apenas autenticacao. O `yt-dlp` nao gera esse
token sozinho, entao esse caso ainda pode exigir ajuste externo no lado do
extrator.

Falhas auxiliares ao criar ou expandir playlists nao devem impedir a tentativa
de reproducao da midia principal. Quando houver uma URL ou caminho valido, o
frontend tenta enviar a midia bruta ao mpv/libmpv; o titulo informado por linha
de comando e aplicado como metadado visual, nao como requisito de playlist.

Os componentes auxiliares de runtime usam o cache local em
`%LOCALAPPDATA%\mpv.net\Component\current`. Nesse fluxo, `libmpv-2.dll`,
`libmpv-2-v3.dll` e `MediaInfo.dll` continuam ao lado do executavel ou no pacote preparado, enquanto
`ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com` podem
ser baixados e atualizados pelo player quando necessario. Uma cópia válida no
cache é preferida; depois o player tenta a pasta do executável e, por último,
o `PATH`. Downloads são feitos em `Component\staging`, validados e promovidos
como uma geração completa, sem substituir diretamente arquivos em uso.

Exemplo:

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "https://example.com/live/index.m3u8"
mpvnet.exe "Canal IPTV" "https://example.com/live/index.m3u8"
```

## Modo portátil

Crie `portable_config` ao lado de `mpvnet.exe` para manter as configurações dentro da pasta do player.

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

Veja [Guia operacional](guia-operacional.md) para o layout recomendado.

## Documentação técnica

Se você for manter ou auditar o fork, os pontos de entrada mais úteis são:

- [Arquitetura](developer/architecture.md)
- [Configuração](developer/configuration.md)
- [Integração com mpv/libmpv](developer/mpv-integration.md)
- [Interface Windows](developer/windows-ui.md)
- [Build e release](developer/build-release.md)
- [Localização](developer/localization.md)

## Suporte

- issues do fork: `https://github.com/WandersondeSouza/mpv.net/issues`
- GitHub Sponsors: `https://github.com/sponsors/stax76`
- doação: `https://www.gestaodesistemas.com.br/mpvnet?language=<idioma>`; a página centralizada recebe o idioma da interface e usa valores de US$ 1 a US$ 20, com moeda local no Stripe Checkout quando disponível
- menu de suporte: GitHub Sponsors, página oficial de doação e e-mail
- e-mail: `mailto:wanderson_souza@hotmail.com`
- manual oficial do mpv: `https://mpv.io/manual/master/`

## Diferenças em relação ao mpv

O mpv.net preserva a maior parte do comportamento do mpv e adiciona interface gráfica, menu de contexto, atalhos globais, editor de configuração e integração com o Windows.



