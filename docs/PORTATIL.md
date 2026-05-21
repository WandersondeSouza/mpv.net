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

Arquivos auxiliares esperados ao lado de `mpvnet.exe` no pacote portatil:

- `libmpv-2.dll`: biblioteca nativa usada diretamente pelo mpv.net.
- `MediaInfo.dll`: biblioteca nativa usada pelo recurso de informacoes de midia.
- `ffmpeg.exe`, `ffplay.exe` e `ffprobe.exe`: ferramentas auxiliares do ecossistema FFmpeg baixadas do release latest do BtbN durante a geracao do pacote portatil.
- `yt-dlp.exe`: ferramenta usada pelo mpv/libmpv para streaming de sites suportados; tambem pode estar no `PATH`, mas o pacote portatil do fork baixa e inclui o executavel oficial ao lado do `mpvnet.exe`.

## Quando a pasta portable_config não existe

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

O pacote portatil tambem deve deixar `libmpv-2.dll`, `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` ao lado de `mpvnet.exe`. Durante a release, o script baixa `libmpv-2.dll`, FFmpeg e `yt-dlp.exe` das fontes configuradas no proprio script. `MediaInfo.dll` vem da copia versionada em `src/Native/win-x64/MediaInfo.dll`. O codigo do mpv.net carrega diretamente `libmpv-2.dll` e `MediaInfo.dll`; os executaveis FFmpeg e `yt-dlp.exe` sao auxiliares usados pelo mpv/libmpv e por fluxos de streaming, nao chamadas diretas do codigo C# do mpv.net.

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
