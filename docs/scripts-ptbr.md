# Scripts de manutencao

Este documento lista os scripts PowerShell mantidos pelo fork, o que cada um faz e exemplos de execucao a partir da raiz do repositorio.

## Build e release

### `src/Tools/prepare-build-output.ps1`

Prepara a pasta de saida de um build Debug ou Release para que `mpvnet.exe` possa ser aberto diretamente pelo Visual Studio ou por `dotnet build`. O script chama `prepare-native-dependencies.ps1`, garante os binarios auxiliares ao lado do executavel e compila os catalogos gettext em `Locale`.

Normalmente ele e chamado automaticamente por `src/MpvNet.Windows/MpvNet.Windows.csproj` depois do build:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

Execucao direta:

```powershell
src\Tools\prepare-build-output.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Debug\win-x64
```

### `src/Tools/prepare-native-dependencies.ps1`

Baixa, copia e valida as dependencias nativas e auxiliares esperadas ao lado de `mpvnet.exe`: `MediaInfo.dll`, `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com`. Quando `-PublishDir` e informado, tambem valida as DLLs Microsoft/.NET/WPF vindas do publish self-contained.

```powershell
src\Tools\prepare-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Debug\win-x64
```

Para forcar atualizacao de arquivos ja existentes:

```powershell
src\Tools\prepare-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Debug\win-x64 -UpdateExisting
```

### `src/Tools/validate-native-dependencies.ps1`

Valida se uma pasta publicada ou um ZIP portatil contem as DLLs nativas obrigatorias e se os binarios PE esperados sao x64. Use depois de publish, release local ou alteracoes no fluxo de dependencias.

```powershell
src\Tools\validate-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Debug\win-x64\publish
src\Tools\validate-native-dependencies.ps1 -ZipFile .\artifacts\release\mpv.net-v7.1.2.3-portable-x64.zip
```

### `src/Tools/build-release-package.ps1`

Fluxo principal de release local. Publica `MpvNet.Windows` self-contained `win-x64`, prepara dependencias nativas, gera `Locale`, inclui `portable_config`, valida dependencias, cria o ZIP portatil, opcionalmente gera o instalador Inno Setup e opcionalmente cria a GitHub Release com `gh`.

Artefatos locais sem publicar no GitHub:

```powershell
src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Somente ZIP portatil, sem instalador e sem GitHub Release:

```powershell
src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipInstaller -SkipGitHubRelease
```

### `src/Tools/download-mediainfo-dependency.ps1`

Script legado e mais estreito para baixar e validar `MediaInfo.dll` a partir da MediaArea oficial, copiando o resultado para uma pasta de publish e, opcionalmente, para uma pasta de build. O fluxo atual normalmente usa `prepare-native-dependencies.ps1`, que cobre mais arquivos.

```powershell
src\Tools\download-mediainfo-dependency.ps1 -SourceDir .\src -PublishDir .\src\MpvNet.Windows\bin\Debug\win-x64\publish
```

### `src/Tools/update-mpv-runtime.ps1`

Atualiza uma instalacao local de `mpv.exe` x64 e/ou `libmpv-2.dll` a partir dos assets mais recentes de `shinchiro/mpv-winbuild-cmake`. Passe `-` nos argumentos posicionais que devem ser ignorados.

```powershell
src\Tools\update-mpv-runtime.ps1 C:\mpv C:\mpvnet-bin -
```

## Localizacao gettext

### `lang/update-gettext-catalogs.ps1`

Atualiza `lang/source.pot` a partir do codigo C#, XAML e `editor_conf.txt`, faz backup temporario dos `.po` e mescla os catalogos traduzidos com o template atualizado.

```powershell
lang\update-gettext-catalogs.ps1
```

### `lang/validate-po-files.ps1`

Normaliza e valida os arquivos `.po` contra `lang/source.pot`. Com `-ValidateOnly`, funciona como gate de paridade sem reescrever os catalogos.

```powershell
lang\validate-po-files.ps1 -ValidateOnly
```

### `lang/compile-mo-files.ps1`

Compila `lang/po/*.po` em arquivos `mpvnet.mo` no layout `Locale/<cultura>/LC_MESSAGES/mpvnet.mo`. Usa ferramentas gettext quando disponiveis e fallback Python quando necessario.

```powershell
lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
```

## Observacoes

- Execute os comandos a partir da raiz do repositorio.
- Use PowerShell em Windows.
- O ingles permanece nativo em `lang/source.pot`; nao crie `lang/po/en.po` para o fluxo normal.
- Para compilacao rapida sem preparar assets, use `dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj /p:EnsureBuildAssets=false`.
