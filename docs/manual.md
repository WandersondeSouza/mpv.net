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
- Para streaming, `yt-dlp.exe` precisa estar disponÃ­vel no `PATH` ou ao lado do executÃ¡vel.

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

O frontend aceita arquivos locais, playlists e URLs de streaming compatÃ­veis com o mpv.
TambÃ©m aceita um tÃ­tulo separado antes de uma URL, usando esse texto apenas
como metadado visual:

```powershell
mpvnet.exe "Nome do video" "https://example.com/video.mp4"
mpvnet.exe --title "Nome do video" "https://example.com/video.mp4"
```

Quando `auto-load-folder=yes` estÃ¡ ativo e o usuÃ¡rio abre um arquivo local de
Ã¡udio ou vÃ­deo, o mpv.net adiciona Ã  playlist interna os outros arquivos de
Ã¡udio, vÃ­deo e playlists compatÃ­veis da mesma pasta. Quando o arquivo aberto Ã©
uma imagem, o carregamento automÃ¡tico da pasta adiciona apenas outras imagens
compatÃ­veis da mesma pasta.

Playlists locais nos formatos `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`,
`.wpl`, `.cue` e `.jspf` sÃ£o expandidas pelo frontend antes do envio ao
mpv/libmpv. O mpv.net usa o tÃ­tulo informado pela playlist quando disponÃ­vel
em todos esses formatos, inclusive no seletor de playlist antes de o item ser reproduzido, mantÃ©m
apenas entradas de Ã¡udio/vÃ­deo ou URLs de streaming e ignora itens repetidos
que apontem para o mesmo caminho ou URL. Se uma instÃ¢ncia do player jÃ¡ estiver
aberta, os itens da playlist sÃ£o adicionados Ã  playlist atual.

URLs HTTP/HTTPS sem extensÃ£o reconhecida tambÃ©m sÃ£o verificadas de forma
conservadora. Quando o conteÃºdo comeÃ§a com `#EXTM3U`, a playlist remota Ã©
baixada para um arquivo temporÃ¡rio e expandida pelo frontend antes do envio ao
mpv/libmpv. Essa verificaÃ§Ã£o usa timeout de pelo menos 60 segundos; se expirar
ou falhar, a URL original ainda Ã© enviada ao mpv/libmpv.

Ao abrir uma URL de streaming diretamente, o frontend aplica automaticamente
opÃ§Ãµes locais de cache e rede para tolerar quedas curtas de fluxo sem alterar
o comportamento de arquivos locais.

Essa regra Ã© baseada no protocolo da entrada (`http`, `https`, `ftp`, `rtsp`,
`rtmp` e similares), nÃ£o em perfis especÃ­ficos de IPTV. `cache-pause-wait=60`
permite aguardar atÃ© cerca de 60 segundos para rebuffer antes de retomar,
enquanto `network-timeout=60` dÃ¡ mais tempo para operaÃ§Ãµes de rede HTTP antes
de o mpv considerar a conexÃ£o encerrada.

Se uma URL do YouTube falhar com `unrecognized file format`, teste o extrator
diretamente. Em muitos casos o problema Ã© autenticaÃ§Ã£o ou cookies do navegador,
nÃ£o o frontend:

```powershell
.\yt-dlp.exe --no-playlist --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
.\yt-dlp.exe --no-playlist --cookies-from-browser chrome --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
```

Troque `chrome` por `edge`, `firefox` ou outro navegador que contenha a sessÃ£o
autenticada, se necessÃ¡rio. Se preferir exportar cookies para um arquivo, use
um arquivo no formato Mozilla/Netscape e garanta que a primeira linha seja
`# HTTP Cookie File` ou `# Netscape HTTP Cookie File`.

Para reduzir o risco de cookies rotacionados pelo YouTube, a wiki do `yt-dlp`
recomenda abrir uma janela privada/incognita, fazer login nela, acessar
`https://www.youtube.com/robots.txt` na mesma aba e exportar os cookies logo em
seguida. Evite manter essa mesma sessÃ£o privada aberta depois da exportaÃ§Ã£o.

Se o vÃ­deo continuar falhando mesmo com cookies vÃ¡lidos, o YouTube pode estar
exigindo PO Token em vez de apenas autenticaÃ§Ã£o. O `yt-dlp` nÃ£o gera esse token
sozinho, entÃ£o esse caso ainda pode exigir ajuste externo no lado do extrator.

Falhas auxiliares ao criar ou expandir playlists nao devem impedir a tentativa
de reproducao da midia principal. Quando houver uma URL ou caminho valido, o
frontend tenta enviar a midia bruta ao mpv/libmpv; o titulo informado por linha
de comando e aplicado como metadado visual, nao como requisito de playlist.

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

