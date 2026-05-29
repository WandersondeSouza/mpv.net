# Build e release

Resumo do fluxo atual do fork.

## Ambiente

- Windows.
- SDK .NET compatível com o projeto.
- Visual Studio com workload desktop.
- `7z`, Inno Setup e `gh` apenas para release completa.

## Build local

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

O projeto Windows usa `win-x64` por padrão.

## Release

O fluxo de release do fork:

1. publica `win-x64` self-contained;
2. prepara `libmpv-2.dll`, `MediaInfo.dll`, FFmpeg, `yt-dlp.exe` e `Locale`;
3. gera o ZIP portátil;
4. gera o instalador x64;
5. valida as dependências nativas;
6. publica a release no GitHub quando solicitado.

Script principal:

```powershell
src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

## Dependências nativas

Veja [Dependências nativas](native-dependencies.md) para o detalhe dos binários esperados e das fontes usadas no download.
