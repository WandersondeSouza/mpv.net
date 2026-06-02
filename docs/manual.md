# Manual do MPV.NET Media Player Community Edition

O MPV.NET Media Player Community Edition é um player de mídia para Windows baseado no mpv/libmpv e no mpv.net.

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

O instalador registra os formatos de mídia comuns e playlists IPTV suportados pelo fork.

As associações atuais incluem:

- Vídeo e playlists: `.mp4`, `.m4v`, `.mkv`, `.webm`, `.avi`, `.mov`, `.qt`, `.wmv`, `.asf`, `.flv`, `.f4v`, `.mpg`, `.mpeg`, `.mpe`, `.m1v`, `.m2v`, `.vob`, `.ts`, `.mts`, `.m2ts`, `.3gp`, `.3g2`, `.ogv`, `.ogg`, `.rm`, `.rmvb`, `.divx`, `.xvid`, `.dv`, `.nut`, `.nsv`, `.m3u`, `.m3u8`, `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue`, `.jspf`;
- Áudio: `.mp3`, `.wav`, `.flac`, `.m4a`, `.aac`, `.ogg`, `.opus`, `.wma`, `.alac`, `.aiff`, `.aif`, `.ape`, `.wv`, `.mka`, `.ac3`, `.dts`, `.eac3`, `.amr`, `.au`, `.mp2`, `.mpa`, `.mpc`, `.thd`, `.w64`, `.oga`, `.ogm`, `.dtshd`, `.dtshr`, `.dtsma`.

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

## Linha de comando

O frontend aceita arquivos locais, playlists e URLs de streaming compatíveis com o mpv.

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
mpv/libmpv.

Exemplo:

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "https://example.com/live/index.m3u8"
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
- manual oficial do mpv: `https://mpv.io/manual/master/`

## Diferenças em relação ao mpv

O mpv.net preserva a maior parte do comportamento do mpv e adiciona interface gráfica, menu de contexto, atalhos globais, editor de configuração e integração com o Windows.
