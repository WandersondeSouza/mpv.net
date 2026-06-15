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

## Instalação

- Windows 10 ou superior.
- Runtime .NET Desktop compatível com o projeto.
- Para streaming, `yt-dlp.exe` precisa estar disponível no `PATH` ou ao lado do executável.

O instalador registra os formatos de mídia comuns e playlists IPTV suportados
pelo fork e adiciona a pasta instalada ao `PATH` do Windows, permitindo executar
`mpvnet.exe` pelo terminal após abrir uma nova sessão. Imagens não são
associadas automaticamente na instalação; use `Config > Setup > Register image
file associations` no menu de contexto se quiser associá-las manualmente. Ao
desinstalar, o instalador remove também as associações do mpv.net criadas
manualmente para imagens e remove a pasta instalada do `PATH`.

As associações automáticas atuais incluem:

- Vídeo: `.mp4`, `.m4v`, `.mkv`, `.webm`, `.avi`, `.mov`, `.qt`, `.wmv`, `.asf`, `.flv`, `.f4v`, `.mpg`, `.mpeg`, `.mpe`, `.m1v`, `.m2v`, `.vob`, `.ts`, `.mts`, `.m2ts`, `.3gp`, `.3g2`, `.ogv`, `.ogg`, `.rm`, `.rmvb`, `.divx`, `.xvid`, `.dv`, `.nut`, `.nsv`, `.264`, `.265`, `.avc`, `.avs`, `.dav`, `.h264`, `.h265`, `.hevc`, `.m2t`, `.mj2`, `.mpv`, `.vpy`, `.y4m`;
- Áudio: `.mp3`, `.wav`, `.flac`, `.m4a`, `.aac`, `.ogg`, `.opus`, `.wma`, `.alac`, `.aiff`, `.aif`, `.ape`, `.wv`, `.mka`, `.ac3`, `.dts`, `.eac3`, `.amr`, `.au`, `.mp2`, `.mpa`, `.mpc`, `.thd`, `.w64`, `.oga`, `.ogm`, `.dtshd`, `.dtshr`, `.dtsma`;
- Playlists: `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue`, `.jspf`;
- Imagem, apenas quando associada manualmente pelo menu: `.avif`, `.bmp`, `.gif`, `.j2k`, `.jp2`, `.jpeg`, `.jpg`, `.jxl`, `.png`, `.svg`, `.tga`, `.tif`, `.tiff`, `.webp`.

## Configuração

O mpv.net lê a configuração nesta ordem:

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

Se `language` não estiver definido, o mpv.net inicia no idioma do Windows quando houver mapeamento suportado; caso contrário, usa inglês.

O mpv.net normaliza codigos como `eng`, `por`, `pt_BR`, `es_MX` e nomes comuns
de idioma, mas a selecao final de audio e legenda continua sendo feita pelo
mpv/libmpv.

## Linha de comando

O frontend aceita arquivos locais, playlists e URLs de streaming compatíveis com o mpv.
Também aceita um título separado antes de uma URL, usando esse texto apenas
como metadado visual:

```powershell
mpvnet.exe "Nome do video" "https://example.com/video.mp4"
mpvnet.exe --title "Nome do video" "https://example.com/video.mp4"
```

Quando `auto-load-folder=yes` está ativo e o usuário abre um arquivo local de
áudio ou vídeo, o mpv.net adiciona à playlist interna os outros arquivos de
áudio, vídeo e playlists compatíveis da mesma pasta. Quando o arquivo aberto é
uma imagem, o carregamento automático da pasta adiciona apenas outras imagens
compatíveis da mesma pasta.

Playlists locais nos formatos `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`,
`.wpl`, `.cue` e `.jspf` são expandidas pelo frontend antes do envio ao
mpv/libmpv. O mpv.net usa o título informado pela playlist quando disponível
em todos esses formatos, inclusive no seletor de playlist antes de o item ser reproduzido, mantém
apenas entradas de áudio/vídeo ou URLs de streaming e ignora itens repetidos
que apontem para o mesmo caminho ou URL. Se uma instância do player já estiver
aberta, os itens da playlist são adicionados à playlist atual.

URLs HTTP/HTTPS sem extensão reconhecida também são verificadas de forma
conservadora. Quando o conteúdo começa com `#EXTM3U`, a playlist remota é
baixada para um arquivo temporário e expandida pelo frontend antes do envio ao
mpv/libmpv. Essa verificação usa timeout de pelo menos 60 segundos; se expirar
ou falhar, a URL original ainda é enviada ao mpv/libmpv.

Ao abrir uma URL de streaming diretamente, o frontend aplica automaticamente
opções locais de cache e rede para tolerar quedas curtas de fluxo sem alterar
o comportamento de arquivos locais.

Essa regra é baseada no protocolo da entrada (`http`, `https`, `ftp`, `rtsp`,
`rtmp` e similares), não em perfis específicos de IPTV. `cache-pause-wait=60`
permite aguardar até cerca de 60 segundos para rebuffer antes de retomar,
enquanto `network-timeout=60` dá mais tempo para operações de rede HTTP antes
de o mpv considerar a conexão encerrada.

Se uma URL do YouTube falhar com `unrecognized file format`, teste o extrator
diretamente. Em muitos casos o problema é autenticação ou cookies do navegador,
não o frontend:

```powershell
.\yt-dlp.exe --no-playlist --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
.\yt-dlp.exe --no-playlist --cookies-from-browser chrome --dump-single-json "https://www.youtube.com/watch?v=DuVaLWf2114"
```

Troque `chrome` por `edge`, `firefox` ou outro navegador que contenha a sessão
autenticada, se necessário. Se preferir exportar cookies para um arquivo, use
um arquivo no formato Mozilla/Netscape e garanta que a primeira linha seja
`# HTTP Cookie File` ou `# Netscape HTTP Cookie File`.

Para reduzir o risco de cookies rotacionados pelo YouTube, a wiki do `yt-dlp`
recomenda abrir uma janela privada/incognita, fazer login nela, acessar
`https://www.youtube.com/robots.txt` na mesma aba e exportar os cookies logo em
seguida. Evite manter essa mesma sessão privada aberta depois da exportação.

Se o vídeo continuar falhando mesmo com cookies válidos, o YouTube pode estar
exigindo PO Token em vez de apenas autenticação. O `yt-dlp` não gera esse token
sozinho, então esse caso ainda pode exigir ajuste externo no lado do extrator.

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
- Stripe donation: configure `MPVNET_STRIPE_DONATION_URL` with your Stripe Payment Link
- menu de suporte: GitHub Sponsors, Stripe donation e e-mail
- e-mail: `mailto:wanderson_souza@hotmail.com`
- manual oficial do mpv: `https://mpv.io/manual/master/`

## Diferenças em relação ao mpv

O mpv.net preserva a maior parte do comportamento do mpv e adiciona interface gráfica, menu de contexto, atalhos globais, editor de configuração e integração com o Windows.
