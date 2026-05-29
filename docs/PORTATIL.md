# Modo portátil

O mpv.net só funciona como portátil real quando existe `portable_config` ao lado de `mpvnet.exe`.

## Layout esperado

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

## O que importa

- `portable_config` faz o player usar a pasta local.
- `mpv.conf` e `input.conf` podem começar a partir dos modelos do fork.
- `scripts/` e `script-opts/` seguem o layout normal do mpv.
- O pacote portável do fork já inclui as DLLs nativas e utilitários esperados.

## Thumbfast

Se usar `thumbfast`, coloque os arquivos em `portable_config/scripts` e `portable_config/script-opts`.
