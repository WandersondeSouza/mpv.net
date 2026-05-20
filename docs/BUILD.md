# Build e release

Este documento e uma orientacao inicial para estudar o build do fork do mpv.net.

> Status: pendente de validacao em um build real.

## Plataforma

O mpv.net e um projeto para Windows.

O executavel principal e gerado como `win-x64`. O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`, portanto builds normais do projeto da aplicacao devem produzir executavel 64 bits por padrao.

O build deve ser feito com Visual Studio/.NET conforme a estrutura atual do projeto. A versao exata recomendada do Visual Studio, do SDK .NET e das dependencias nativas ainda precisa ser validada neste fork.

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
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained false --configuration Debug --runtime win-x64
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

Durante a geracao do pacote, ele baixa automaticamente as dependencias atualizadas de FFmpeg, libmpv e yt-dlp e copia para a pasta do `mpvnet.exe` os binarios auxiliares esperados pelo pacote portatil:

```text
libmpv-2.dll
MediaInfo.dll
ffmpeg.exe
ffplay.exe
ffprobe.exe
yt-dlp.exe
```

As fontes automaticas usadas pelo script sao:

- `ffmpeg-master-latest-win64-gpl.zip` em `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`;
- `mpv-dev-x86_64-...7z` em `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`;
- `yt-dlp.exe` em `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.

`MediaInfo.dll` e `mpvnet.com` continuam sendo dependencias locais e devem existir previamente em `src/MpvNet.Windows/bin/Debug/win-x64/`. Como alternativa, o script aceita `-MediaInfoFile` e `-MpvNetComFile` para copiar esses arquivos de um local externo durante o empacotamento. Se algum download, extracao ou arquivo obrigatorio falhar, a release deve falhar antes de montar o pacote incompleto. O fluxo completo de release ainda precisa ser validado em execucao real.

Exemplo para gerar artefatos locais sem publicar no GitHub:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Exemplo para gerar apenas o ZIP portatil, sem instalador e sem publicacao:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipInstaller -SkipGitHubRelease
```

Exemplo passando dependencias nativas externas:

```powershell
src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

Tambem existe o workflow manual `.github/workflows/release-packages.yml`, que gera os pacotes no GitHub Actions e pode criar a Release quando executado com `create_release=true`. Para esse workflow, configure os secrets `MEDIAINFO_DLL_BASE64` e `MPVNET_COM_BASE64` se esses binarios nao estiverem versionados no repositorio.

Observacao sobre GitHub Packages: este fork distribui o aplicativo desktop como assets de GitHub Releases e artefatos de workflow. Ele nao publica, por enquanto, um pacote NuGet/container no GitHub Packages.

## Pendencias de validacao

- Versao recomendada do Visual Studio.
- Versao recomendada do SDK .NET.
- Comando exato de build.
- Fluxo de release.
- Validação completa do pacote portatil gerado.
- Dependencias nativas e auxiliares necessarias para executar o player compilado e o pacote portatil.
- Relacao exata entre build do mpv.net e atualizacao de mpv/libmpv.

Este documento deve ser refinado depois de um teste real de build.
