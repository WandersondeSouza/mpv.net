# Dependências nativas

O mpv.net usa DLLs e executáveis nativos ao lado de `mpvnet.exe`.

## Arquivos esperados

- `libmpv-2.dll`
- `MediaInfo.dll`
- `ffmpeg.exe`
- `ffplay.exe`
- `ffprobe.exe`
- `yt-dlp.exe`
- `D3DCompiler_47_cor3.dll`
- `vcruntime140_cor3.dll`
- `wpfgfx_cor3.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`

## Origem

- `libmpv-2.dll`, FFmpeg, `yt-dlp.exe` e `MediaInfo.dll` são preparados pelo fluxo de release.
- As DLLs `.NET/WPF` vêm do `dotnet publish --self-contained true --runtime win-x64`.
- `Locale` é gerado a partir de `lang/po` quando necessário.

## Validação

Use o validador antes de publicar:

```powershell
src\Tools\validate-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Debug\win-x64\publish
src\Tools\validate-native-dependencies.ps1 -ZipFile .\artifacts\release\mpv.net-v7.1.2.4-portable-x64.zip
```

O pacote não deve ser publicado se faltar arquivo, se a arquitetura estiver errada ou se alguma dependência vier vazia.
