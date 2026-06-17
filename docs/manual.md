# Manual do MPV.NET Media Player

O MPV.NET Media Player Ã© um player de mÃ­dia para Windows baseado no mpv/libmpv e no mpv.net.

Este manual cobre apenas o que importa para o uso cotidiano do fork.

## TÃ³picos

- [Download](#download)
- [InstalaÃ§Ã£o](#instalaÃ§Ã£o)
- [ConfiguraÃ§Ã£o](#configuraÃ§Ã£o)
- [Linha de comando](#linha-de-comando)
- [Modo portÃ¡til](#modo-portÃ¡til)
- [DocumentaÃ§Ã£o tÃ©cnica](#documentaÃ§Ã£o-tÃ©cnica)
- [Suporte](#suporte)
- [DiferenÃ§as em relaÃ§Ã£o ao mpv](#diferenÃ§as-em-relaÃ§Ã£o-ao-mpv)

## Download

Use as releases do fork em:

`https://github.com/WandersondeSouza/mpv.net/releases`

O aplicativo tambÃ©m estÃ¡ publicado na Microsoft Store:

- Link profundo: `ms-windows-store://pdp/?productid=9N441SP6XHLD`
- URL da Web Store: [https://apps.microsoft.com/detail/9N441SP6XHLD](https://apps.microsoft.com/detail/9N441SP6XHLD)

## InstalaÃ§Ã£o

- Windows 10 ou superior.
- Runtime .NET Desktop compatÃ­vel com o projeto.
- Para streaming, o player baixa e atualiza `yt-dlp.exe` no cache de componentes em `%LOCALAPPDATA%\mpv.net\Component`. Se quiser testar manualmente, tambÃ©m vale deixÃ¡-lo ao lado do executÃ¡vel ou no `PATH`.

O instalador registra os formatos de mÃ­dia comuns e playlists IPTV suportados
pelo fork e adiciona a pasta instalada ao `PATH` do Windows, permitindo executar
`mpvnet.exe` pelo terminal apÃ³s abrir uma nova sessÃ£o. Imagens nÃ£o sÃ£o
associadas automaticamente na instalaÃ§Ã£o; use `Config > Setup > Register image
file associations` no menu de contexto se quiser associÃ¡-las manualmente. Ao
desinstalar, o instalador remove tambÃ©m as associaÃ§Ãµes do mpv.net criadas
manualmente para imagens e remove a pasta instalada do `PATH`.

As associaÃ§Ãµes automÃ¡ticas atuais incluem:

- VÃ­deo: `.mp4`, `.m4v`, `.mkv`, `.webm`, `.avi`, `.mov`, `.qt`, `.wmv`, `.asf`, `.flv`, `.f4v`, `.mpg`, `.mpeg`, `.mpe`, `.m1v`, `.m2v`, `.vob`, `.ts`, `.mts`, `.m2ts`, `.3gp`, `.3g2`, `.ogv`, `.ogg`, `.rm`, `.rmvb`, `.divx`, `.xvid`, `.dv`, `.nut`, `.nsv`, `.264`, `.265`, `.avc`, `.avs`, `.dav`, `.h264`, `.h265`, `.hevc`, `.m2t`, `.mj2`, `.mpv`, `.vpy`, `.y4m`;
- Ãudio: `.mp3`, `.wav`, `.flac`, `.m4a`, `.aac`, `.ogg`, `.opus`, `.wma`, `.alac`, `.aiff`, `.aif`, `.ape`, `.wv`, `.mka`, `.ac3`, `.dts`, `.eac3`, `.amr`, `.au`, `.mp2`, `.mpa`, `.mpc`, `.thd`, `.w64`, `.oga`, `.ogm`, `.dtshd`, `.dtshr`, `.dtsma`;
- Playlists: `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue`, `.jspf`;
- Imagem, apenas quando associada manualmente pelo menu: `.avif`, `.bmp`, `.gif`, `.j2k`, `.jp2`, `.jpeg`, `.jpg`, `.jxl`, `.png`, `.svg`, `.tga`, `.tif`, `.tiff`, `.webp`.

## ConfiguraÃ§Ã£o

O mpv.net lÃª a configuraÃ§Ã£o nesta ordem:

1. `MPVNET_HOME`
2. `portable_config`
3. `%APPDATA%\mpv.net`

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

Se `language` nÃ£o estiver definido, o mpv.net inicia no idioma do Windows quando houver mapeamento suportado; caso contrÃ¡rio, usa inglÃªs.

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

Playlists locais nos formatos `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`,
`.wpl`, `.cue` e `.jspf` sao expandidas pelo frontend antes do envio ao
mpv/libmpv. O mpv.net usa o titulo informado pela playlist quando disponivel,
mantem apenas entradas de audio/video ou URLs de streaming e ignora itens
repetidos que apontem para o mesmo caminho ou URL. Se uma instancia do player
ja estiver aberta, os itens da playlist sao adicionados a playlist atual.

URLs HTTP/HTTPS sem extensao reconhecida tambem sao verificadas de forma
conservadora. Quando o conteudo comeca com `#EXTM3U`, a playlist remota e
baixada para um arquivo temporario e expandida pelo frontend antes do envio ao
mpv/libmpv. Essa verificacao usa timeout de pelo menos 60 segundos; se expirar
ou falhar, a URL original ainda e enviada ao mpv/libmpv.

Ao abrir uma URL de streaming diretamente, o frontend aplica opcoes locais de
cache e rede para tolerar quedas curtas de fluxo sem alterar o comportamento
de arquivos locais.

Essa regra usa o protocolo da entrada (`http`, `https`, `ftp`, `rtsp`, `rtmp`
e similares), nao perfis especificos de IPTV. `cache-pause-wait=60` permite
aguardar ate cerca de 60 segundos para rebuffer antes de retomar, enquanto
`network-timeout=60` da mais tempo para operacoes de rede HTTP antes de o mpv
considerar a conexao encerrada.

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
`%LOCALAPPDATA%\mpv.net\Component`. Nesse fluxo, `libmpv-2.dll` e
`MediaInfo.dll` continuam ao lado do executavel ou no pacote preparado, enquanto
`ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com` podem
ser baixados e atualizados pelo player quando necessario. O bootstrap grava
primeiro em `%LOCALAPPDATA%\mpv.net\Temp\RuntimeComponents`, limpa esse staging
na abertura e apaga os arquivos temporarios depois que o processo termina.

Exemplo:

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "https://example.com/live/index.m3u8"
mpvnet.exe "Canal IPTV" "https://example.com/live/index.m3u8"
```

## Modo portÃ¡til

Crie `portable_config` ao lado de `mpvnet.exe` para manter as configuraÃ§Ãµes dentro da pasta do player.

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

Veja [Guia operacional](guia-operacional.md) para o layout recomendado.

## DocumentaÃ§Ã£o tÃ©cnica

Se vocÃª for manter ou auditar o fork, os pontos de entrada mais Ãºteis sÃ£o:

- [Arquitetura](developer/architecture.md)
- [ConfiguraÃ§Ã£o](developer/configuration.md)
- [IntegraÃ§Ã£o com mpv/libmpv](developer/mpv-integration.md)
- [Interface Windows](developer/windows-ui.md)
- [Build e release](developer/build-release.md)
- [LocalizaÃ§Ã£o](developer/localization.md)

## Suporte

- issues do fork: `https://github.com/WandersondeSouza/mpv.net/issues`
- GitHub Sponsors: `https://github.com/sponsors/stax76`
- Stripe donation: configure `MPVNET_STRIPE_DONATION_URL` with your Stripe Payment Link
- menu de suporte: GitHub Sponsors, Stripe donation e e-mail
- e-mail: `mailto:wanderson_souza@hotmail.com`
- manual oficial do mpv: `https://mpv.io/manual/master/`

## DiferenÃ§as em relaÃ§Ã£o ao mpv

O mpv.net preserva a maior parte do comportamento do mpv e adiciona interface grÃ¡fica, menu de contexto, atalhos globais, editor de configuraÃ§Ã£o e integraÃ§Ã£o com o Windows.

