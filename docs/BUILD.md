# Build e release

Este documento e uma orientacao inicial para estudar o build do fork do mpv.net.

> Status: build local, ZIP portatil e instalador validados em 2026-05-22; publicacao GitHub continua pendente de validacao real.

## Plataforma

O mpv.net e um projeto para Windows.

O executavel principal e gerado como `win-x64`. O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`, portanto builds normais do projeto da aplicacao devem produzir executavel 64 bits por padrao.

O build deve ser feito com Visual Studio/.NET conforme a estrutura atual do projeto. A versao exata recomendada do Visual Studio, do SDK .NET e das dependencias nativas ainda precisa ser validada neste fork.

## Versao

A versao do fork fica centralizada em:

```text
src/BuildVersion.props
```

O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` importa essa propriedade e
usa `MpvNetVersion` para `FileVersion`, `AssemblyVersion` e
`InformationalVersion`. O script de release le a versao do `mpvnet.exe`
publicado para montar nomes de artefatos e tag, por exemplo
`mpv.net-v7.1.2.1-portable-x64.zip` e `v7.1.2.1`.

## Como abrir o projeto

Orientacao inicial:

1. Usar Windows.
2. Abrir a solucao/projeto no Visual Studio.
3. Restaurar pacotes NuGet.
4. Compilar em Debug antes de tentar empacotamento ou release.

Comando principal esperado para a aplicacao:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

Publicacao x64 usada pelo fluxo de release:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Debug --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

## Scripts relacionados

Existem scripts em:

```text
src/Tools
```

Scripts relevantes para analise futura:

- `src/Tools/release-mpv.net.ps1`: relacionado ao fluxo de build/release e à criação do ZIP portátil com `portable_config`.
- `src/Tools/update-mpv.ps1`: relacionado a atualizacao de mpv/libmpv.

O script de release foi ajustado para publicar e empacotar a aplicacao como x64, incluindo `portable_config` no pacote portatil. Por padrao, a publicacao GitHub aponta para o fork `WandersondeSouza/mpv.net`; use `-Repo outro-dono/outro-repo` se precisar publicar em outro repositorio.

Durante a geracao do pacote, ele publica o aplicativo como self-contained `win-x64`, baixa automaticamente as dependencias atualizadas de FFmpeg, libmpv, yt-dlp e MediaInfo, e copia para a pasta do `mpvnet.exe` os binarios auxiliares esperados pelo pacote portatil:

```text
libmpv-2.dll
MediaInfo.dll
ffmpeg.exe
ffplay.exe
ffprobe.exe
yt-dlp.exe
```

As fontes automaticas usadas pelo script sao:

- `ffmpeg-N-...-win64-gpl.zip` em `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`;
- `mpv-dev-x86_64-...7z` em `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`;
- `yt-dlp.exe` em `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.
- `MediaInfo_DLL_..._Windows_x64_WithoutInstaller.7z` a partir da pagina oficial `https://mediaarea.net/en/MediaInfo/Download/Windows`;
- `Gettext.Tools` em `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, quando `msgfmt.exe` nao estiver no `PATH`, para gerar `Locale`.

As DLLs Microsoft/.NET `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll` vem do proprio `dotnet publish` self-contained. O fork nao baixa essas DLLs de sites externos.

`MediaInfo.dll` e baixada/atualizada por `src/Tools/download-native-dependencies.ps1`. O parametro `-MediaInfoVersion`, ou a variavel `MPVNET_MEDIAINFO_VERSION`, permite pinar uma versao especifica. O parametro `-MediaInfoFile` continua existindo no release script apenas como override manual. `mpvnet.com` pode ser fornecido por `-MpvNetComFile`; se nao for informado e nao existir no build output, o script baixa o arquivo auxiliar do host original usado pelo projeto. A pasta `Locale` e gerada automaticamente a partir de `lang/po` quando necessario. Se algum download, extracao ou arquivo obrigatorio falhar, a release deve falhar antes de montar o pacote incompleto.

Validacao local de 2026-05-22: `src\Tools\release-mpv.net.ps1 .\src .\artifacts\release` gerou `mpv.net-v7.1.2.1-portable-x64.zip` e `mpv.net-v7.1.2.1-setup-x64.exe`, baixou MediaInfo 26.05 da MediaArea, baixou FFmpeg/libmpv/yt-dlp, gerou `Locale`, incluiu `portable_config` e validou as DLLs nativas obrigatorias no publish, na pasta portatil e dentro do ZIP. A criacao da GitHub Release exige `GH_TOKEN` ou `gh auth login`.

Exemplo para gerar artefatos locais sem publicar no GitHub:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Exemplo para gerar apenas o ZIP portatil, sem instalador e sem publicacao:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipInstaller -SkipGitHubRelease
```

Exemplo passando dependencias nativas externas como override:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

Exemplo pinando uma versao especifica do MediaInfo:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -MediaInfoVersion 26.05 -SkipGitHubRelease
```

Validacao manual de dependencias nativas:

```powershell
src\Tools\test-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Debug\win-x64\publish
src\Tools\test-native-dependencies.ps1 -ZipFile .\artifacts\release\mpv.net-v7.1.2.1-portable-x64.zip
```

Tambem existe o workflow manual `.github/workflows/release-packages.yml`, que gera os pacotes no GitHub Actions e pode criar a Release quando executado com `create_release=true`. O workflow executa o mesmo release script e roda `test-native-dependencies.ps1` antes de publicar os artefatos.

Observacao sobre GitHub Packages: este fork distribui o aplicativo desktop como assets de GitHub Releases e artefatos de workflow. Ele nao publica, por enquanto, um pacote NuGet/container no GitHub Packages.

## Pendencias de validacao

- Versao recomendada do Visual Studio.
- Versao recomendada do SDK .NET.
- Fluxo de publicacao GitHub.
- Validacao manual completa do pacote portatil gerado, incluindo fullscreen, menu, atalhos, persistencia de configuracao e temas.
- Relacao exata entre build do mpv.net e atualizacao de mpv/libmpv.

Este documento deve ser refinado depois de um teste real do instalador e da publicacao.
