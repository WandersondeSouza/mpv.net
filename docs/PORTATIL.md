# Modo portátil do mpv.net

Este documento explica como usar o mpv.net em modo portátil real.

## Diferença entre ZIP e portátil real

A versão em ZIP não usa instalador. Porém, isso não significa que todas as configurações ficarão automaticamente dentro da pasta do programa.

Para que o mpv.net grave as configurações dentro da própria pasta do player, é necessário criar uma pasta chamada:

```text
portable_config
```

ao lado do arquivo:

```text
mpvnet.exe
```

## Estrutura recomendada

```text
mpvnet.exe
libmpv-2.dll
MediaInfo.dll
D3DCompiler_47_cor3.dll
vcruntime140_cor3.dll
wpfgfx_cor3.dll
PenImc_cor3.dll
PresentationNative_cor3.dll
ffmpeg.exe
ffplay.exe
ffprobe.exe
yt-dlp.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

## Para que serve cada item

- `mpv.conf`: configurações gerais do mpv/mpv.net.
- `input.conf`: atalhos de teclado, mouse e comandos do menu.
- `scripts/`: scripts Lua ou JavaScript usados pelo mpv.
- `script-opts/`: arquivos de configuração dos scripts.

Arquivos auxiliares esperados ao lado de `mpvnet.exe` no pacote portátil:

- `libmpv-2.dll`: biblioteca nativa usada diretamente pelo mpv.net.
- `MediaInfo.dll`: biblioteca nativa usada pelo recurso de informações de mídia; o release baixa a DLL x64 da fonte oficial MediaArea.
- `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll`: DLLs nativas vindas do publish self-contained oficial do .NET Desktop/WPF.
- `ffmpeg.exe`, `ffplay.exe` e `ffprobe.exe`: ferramentas auxiliares do ecossistema FFmpeg baixadas do release latest do BtbN durante a geração do pacote portátil.
- `yt-dlp.exe`: ferramenta usada pelo mpv/libmpv para streaming de sites suportados; também pode estar no `PATH`, mas o pacote portátil do fork baixa e inclui o executável oficial ao lado do `mpvnet.exe`.

## Quando a pasta `portable_config` não existe

Se a pasta `portable_config` não existir, o mpv.net pode usar a pasta de configuração do usuário no Windows, por exemplo:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

Isso pode confundir usuários que esperam que a versão ZIP seja totalmente portátil.

## Comportamento do pacote gerado pelo fork

O script de release do fork inclui automaticamente uma estrutura básica no ZIP portátil:

```text
portable_config/
portable_config/mpv.conf
portable_config/input.conf
portable_config/scripts/
portable_config/script-opts/
```

Os arquivos `mpv.conf` e `input.conf` são modelos comentados, sem opções ativas por padrão, copiados de:

```text
docs/exemplos/portable_config/mpv.conf
docs/exemplos/portable_config/input.conf
```

Assim o usuário entende imediatamente onde colocar suas configurações, e o mpv.net passa a usar a pasta portátil porque `portable_config` existe ao lado de `mpvnet.exe`.

O `portable_config/mpv.conf` também inclui o perfil `[iptv-media-center]`. Esse perfil só é usado quando o player recebe `--profile=iptv-media-center`; portanto ele não muda a abertura normal do mpv.net pelo usuário.

O pacote portátil também deve deixar `libmpv-2.dll`, `MediaInfo.dll`, as DLLs `.NET/WPF` nativas, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` ao lado de `mpvnet.exe`. Durante a release, o script baixa `libmpv-2.dll`, MediaInfo, FFmpeg e `yt-dlp.exe` das fontes configuradas no próprio script. As DLLs Microsoft/.NET vêm do `dotnet publish` self-contained, não de sites externos. O código do mpv.net carrega diretamente `libmpv-2.dll` e `MediaInfo.dll`; os executáveis FFmpeg e `yt-dlp.exe` são auxiliares usados pelo mpv/libmpv e por fluxos de streaming, não chamadas diretas do código C# do mpv.net.

## thumbfast no modo portátil

Para usar o `thumbfast` na versão portátil, coloque os arquivos dentro da própria pasta `portable_config`:

```text
mpvnet.exe
portable_config/
  scripts/
    thumbfast.lua
  script-opts/
    thumbfast.conf
```

O arquivo de exemplo deste fork fica em:

```text
docs/exemplos/thumbfast.conf
```

No mpv.net v7, o `thumbfast` tem suporte direto e não deve precisar de `mpv_path` apontando para um `mpv.exe` separado. Use `mpv_path` apenas como fallback para versões antigas ou quando a documentação atual do próprio `thumbfast` indicar essa necessidade.

O pacote portátil não inclui `thumbfast.lua` nem `mpv.exe` separado. Esses arquivos devem ser instalados pelo usuário conforme a documentação do script.

`thumbfast` funciona no modo portátil com `portable_config/scripts/thumbfast.lua` e `portable_config/script-opts/thumbfast.conf`. A exibição visual dos thumbnails ainda depende de uma UI/script compatível consumindo as mensagens do script.
