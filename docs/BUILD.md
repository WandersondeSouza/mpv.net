# Build e release

Este documento é uma orientação inicial para estudar o build do fork do mpv.net.

> Status: publicação GitHub continua pendente de validação real.

## Plataforma

O mpv.net é um projeto para Windows.

O executável principal é gerado como `win-x64`. O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`, portanto builds normais do projeto da aplicação devem produzir executável 64 bits por padrão.

O build deve ser feito com Visual Studio/.NET conforme a estrutura atual do projeto. A versão exata recomendada do Visual Studio, do SDK .NET e das dependências nativas ainda precisa ser validada neste fork.

## Versão

A versão do fork fica centralizada em:

```text
src/BuildVersion.props
```

O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` importa essa propriedade e usa `MpvNetVersion` para `FileVersion`, `AssemblyVersion` e `InformationalVersion`. O script de release lê a versão do `mpvnet.exe` publicado para montar nomes de artefatos e tag, por exemplo `mpv.net-v7.1.2.1-portable-x64.zip` e `v7.1.2.1`.

## Como abrir o projeto

Orientação inicial:

1. Usar Windows.
2. Abrir a solução/projeto no Visual Studio.
3. Restaurar pacotes NuGet.
4. Compilar em Debug antes de tentar empacotamento ou release.

Comando principal esperado para a aplicação:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

Publicação x64 usada pelo fluxo de release:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Debug --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

## Scripts relacionados

Existem scripts em:

```text
src/Tools
```

Scripts relevantes para análise futura:

- `src/Tools/release-mpv.net.ps1`: relacionado ao fluxo de build/release e à criação do ZIP portátil com `portable_config`.
- `src/Tools/update-mpv.ps1`: relacionado a atualização de mpv/libmpv.

O script de release foi ajustado para publicar e empacotar a aplicação como x64, incluindo `portable_config` no pacote portátil. Por padrão, a publicação GitHub aponta para o fork `WandersondeSouza/mpv.net`; use `-Repo outro-dono/outro-repo` se precisar publicar em outro repositório.

Durante a geração do pacote, ele publica o aplicativo como self-contained `win-x64`, baixa automaticamente as dependências atualizadas de FFmpeg, libmpv, yt-dlp e MediaInfo, e copia para a pasta do `mpvnet.exe` os binários auxiliares esperados pelo pacote portátil:

```text
libmpv-2.dll
MediaInfo.dll
ffmpeg.exe
ffplay.exe
ffprobe.exe
yt-dlp.exe
```

As fontes automáticas usadas pelo script são:

- `ffmpeg-N-...-win64-gpl.zip` em `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`;
- `mpv-dev-x86_64-...7z` em `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`;
- `yt-dlp.exe` em `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.
- `MediaInfo_DLL_..._Windows_x64_WithoutInstaller.7z` a partir da página oficial `https://mediaarea.net/en/MediaInfo/Download/Windows`;
- `Gettext.Tools` em `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, quando `msgfmt.exe` não estiver no `PATH`, para gerar `Locale`.

As DLLs Microsoft/.NET `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll` vêm do próprio `dotnet publish` self-contained. O fork não baixa essas DLLs de sites externos.

`MediaInfo.dll` é baixada/atualizada por `src/Tools/download-native-dependencies.ps1`. O parâmetro `-MediaInfoVersion`, ou a variável `MPVNET_MEDIAINFO_VERSION`, permite pinagem de uma versão específica. O parâmetro `-MediaInfoFile` continua existindo no release script apenas como override manual. `mpvnet.com` pode ser fornecido por `-MpvNetComFile`; se não for informado e não existir no build output, o script baixa o arquivo auxiliar do host original usado pelo projeto. A pasta `Locale` é gerada automaticamente a partir de `lang/po` quando necessário. Se algum download, extração ou arquivo obrigatório falhar, a release deve falhar antes de montar o pacote incompleto.

O fluxo de release gera `mpv.net-v7.1.2.1-portable-x64.zip` e `mpv.net-v7.1.2.1-setup-x64.exe`, baixa MediaInfo da MediaArea, baixa FFmpeg/libmpv/yt-dlp, gera `Locale`, inclui `portable_config` e valida as DLLs nativas obrigatórias no publish, na pasta portátil e dentro do ZIP. A criação da GitHub Release exige `GH_TOKEN` ou `gh auth login`.

Exemplo para gerar artefatos locais sem publicar no GitHub:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Exemplo para gerar apenas o ZIP portátil, sem instalador e sem publicação:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipInstaller -SkipGitHubRelease
```

Exemplo passando dependências nativas externas como override:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

Exemplo pinando uma versão específica do MediaInfo:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -MediaInfoVersion 26.05 -SkipGitHubRelease
```

Validação manual de dependências nativas:

```powershell
src\Tools\test-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Debug\win-x64\publish
src\Tools\test-native-dependencies.ps1 -ZipFile .\artifacts\release\mpv.net-v7.1.2.1-portable-x64.zip
```

Também existe o workflow manual `.github/workflows/release-packages.yml`, que gera os pacotes no GitHub Actions e pode criar a Release quando executado com `create_release=true`. O workflow executa o mesmo release script e roda `test-native-dependencies.ps1` antes de publicar os artefatos.

Observação sobre GitHub Packages: este fork distribui o aplicativo desktop como assets de GitHub Releases e artefatos de workflow. Ele não publica, por enquanto, um pacote NuGet/container no GitHub Packages.

## Pendências de validação

- Versão recomendada do Visual Studio.
- Versão recomendada do SDK .NET.
- Fluxo de publicação GitHub.
- Validação manual completa do pacote portátil gerado, incluindo fullscreen, menu, atalhos, persistência de configuração e temas.
- Relação exata entre build do mpv.net e atualização de mpv/libmpv.

Este documento deve ser refinado depois de um teste real do instalador e da publicação.
