# Build e release do mpv.net

## Objetivo

Este documento orienta como preparar o ambiente para estudar, compilar e manter o fork **mpv.net**.

> Status: estrutura real do projeto mapeada. O build local da aplicacao Windows e o fluxo local de release da versao `7.1.2.5` foram validados em Windows, incluindo ZIP portatil, instalador, Locale e validacao de dependencias nativas. A versao atual preparada para publicacao e `7.1.2.6`. Ainda faltam fechar a revisao manual completa de UI/compatibilidade e validar o workflow manual do GitHub Actions.

---

# Requisitos principais

Para desenvolvimento:

- Windows 10 ou Windows 11;
- SDK do .NET compatível com `net10.0` e `net10.0-windows7.0`;
- Visual Studio com workload de desenvolvimento desktop .NET;
- Git;
- acesso ao repositório no GitHub.

Para execução:

- Windows;
- SDK .NET 10.0 para publicar self-contained `win-x64`;
- `libmpv-2.dll` x64;
- `MediaInfo.dll` x64 baixada da MediaArea oficial durante a release;
- `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` no pacote portatil do fork;
- arquivos de `Locale`, quando aplicável.

Para release:

- 7-Zip em `C:\Program Files\7-Zip\7z.exe`;
- Inno Setup 6 em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`, exceto quando `-SkipInstaller` for usado;
- GitHub CLI (`gh`), exceto quando `-SkipGitHubRelease` for usado;
- variável `GH_TOKEN` configurada para criação de release;
- acesso a internet para baixar FFmpeg, libmpv, yt-dlp, MediaInfo, `mpvnet.com` e `Gettext.Tools` no momento da release, quando esses arquivos/ferramentas ainda nao estiverem disponiveis localmente.

Observacao: Inno Setup, GitHub CLI e `GH_TOKEN` deixam de ser obrigatorios quando o script e executado, respectivamente, com `-SkipInstaller` e `-SkipGitHubRelease`.
Os downloads de dependencias nativas e auxiliares ficam em `artifacts\native-dependencies\downloads` e sao reutilizados por ate 2 dias. Se o arquivo nao existir ou estiver mais antigo, o script baixa novamente a versao mais recente encontrada nas fontes configuradas.

---

# Solução e projetos

Solução principal:

```text
src/MpvNet.sln
```

Projetos principais:

| Projeto | Tipo | Target | Saída |
| --- | --- | --- | --- |
| `src/MpvNet/MpvNet.csproj` | biblioteca | `net10.0` | `libmpvnet` |
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | aplicação Windows | `net10.0-windows7.0`, `win-x64` | `mpvnet.exe` x64 |
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | biblioteca | legado/packages.config | suporte WPF/NGettext |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | exemplo | extensão .NET | exemplo carregável |

Pacotes NuGet versionados em `src/Directory.Packages.props`:

- `CommunityToolkit.Mvvm` `8.4.2`;
- `NGettext` `0.6.7`;
- `Microsoft.Xaml.Behaviors.Wpf` `1.1.142`.

---

# Clonando o fork

```powershell
git clone https://github.com/WandersondeSouza/mpv.net.git
cd mpv.net
```

---

# Abrindo no Visual Studio

1. Abra o Visual Studio.
2. Selecione **Open a project or solution**.
3. Abra `src/MpvNet.sln`.
4. Restaure os pacotes NuGet.
5. Compile em Debug.
6. Builds Debug/Release do projeto Windows preparam automaticamente os binarios nativos/auxiliares e `Locale` com `src\Tools\prepare-build-output.ps1`; para uma compilacao rapida sem preparar assets, use `/p:EnsureBuildAssets=false`.

---

# Build via terminal

Na raiz do repositório:

```powershell
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln
```

Para compilar apenas a aplicação Windows:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

O projeto da aplicação define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`, portanto o build normal gera o executável como x64.

Para compilar e baixar/validar automaticamente os binarios nativos e auxiliares esperados ao lado de `mpvnet.exe`:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj -c Release
```

Esse alvo opt-in chama `src\Tools\prepare-native-dependencies.ps1` e garante `MediaInfo.dll`, `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com` na pasta Debug, baixando apenas o que estiver faltando. Para forcar atualizacao dos arquivos ja presentes, chame o script direto com `-UpdateExisting`. Ele nao baixa DLLs Microsoft/.NET/WPF de sites externos; essas DLLs continuam vindo do publish self-contained.

Para publicar como o script de release atual faz:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Debug --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

Observação: o script de release atual publica em `Debug`. Não documente uma release como `Release` sem ajustar e validar o script.

---

# Execução em debug

O projeto de inicialização deve ser `MpvNet.Windows`.

Pontos a validar:

- o executável localiza `libmpv-2.dll`;
- o executável localiza `MediaInfo.dll`;
- `Locale` está disponível quando necessário;
- `mpv.conf`, `mpvnet.conf` e `input.conf` são resolvidos pela pasta correta;
- a janela principal abre;
- reprodução de mídia local funciona;
- fullscreen, menu de contexto e atalhos funcionam.

---

# Release e empacotamento

Para a referência completa dos scripts PowerShell do fork, veja `docs/guia-operacional.md`.

Script principal:

```text
src/Tools/build-release-package.ps1
```

Uso esperado pelo cabeçalho do script:

```powershell
src\Tools\build-release-package.ps1 <diretorio-src> <diretorio-saida>
```

Exemplo:

```powershell
src\Tools\build-release-package.ps1 C:\repo\mpv.net\src C:\saida
```

Por padrao, o script publica em `WandersondeSouza/mpv.net`. Para gerar apenas artefatos locais, use:

```powershell
src\Tools\build-release-package.ps1 C:\repo\mpv.net\src C:\saida -SkipGitHubRelease
```

Para gerar apenas o ZIP portatil, sem instalador e sem publicacao:

```powershell
src\Tools\generate-portable-zip.ps1 -SourceDir C:\repo\mpv.net\src -OutputRootDir C:\saida
```

Para gerar apenas o instalador executavel, sem ZIP e sem publicacao:

```powershell
src\Tools\generate-installer-exe.ps1 -SourceDir C:\repo\mpv.net\src -OutputRootDir C:\saida
```

Quando for necessario sobrescrever `MediaInfo.dll` ou fornecer um `mpvnet.com` local, informe os arquivos explicitamente:

```powershell
src\Tools\build-release-package.ps1 C:\repo\mpv.net\src C:\saida -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

O script:

1. valida `MpvNet.sln`;
2. valida 7-Zip e, quando aplicavel, Inno Setup;
3. publica `MpvNet.Windows.csproj` self-contained para `win-x64`;
4. cria nomes com base na versão do `mpvnet.exe`;
5. copia arquivos publicados;
6. chama `src\Tools\prepare-native-dependencies.ps1` para baixar ou validar `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `libmpv-2.dll`, `yt-dlp.exe` e `mpvnet.com`, reutilizando downloads com ate 2 dias em `artifacts\native-dependencies\downloads`;
7. valida e copia as DLLs Microsoft/.NET `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll` vindas do publish self-contained;
8. copia `MediaInfo.dll`, `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com` x64;
9. copia `Locale`;
10. cria `portable_config` com modelos comentados de `mpv.conf` e `input.conf`;
11. valida as DLLs nativas no publish, na pasta portatil e no ZIP com `validate-native-dependencies.ps1`;
12. gera ZIP portatil x64;
13. executa `Setup/Inno/build-windows-installer.iss` para gerar o instalador x64, exceto com `-SkipInstaller`;
14. o instalador executa `mpvnet.exe --register-file-associations video` e
    `mpvnet.exe --register-file-associations audio` para registrar as
    associacoes de video, audio e playlists IPTV apos a instalacao;
15. cria release no GitHub usando `gh release create`, exceto com `-SkipGitHubRelease`.

As dependencias baixadas automaticamente usam estas fontes:

- FFmpeg: `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`, asset `ffmpeg-master-latest-win64-gpl.zip`;
- libmpv: `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, asset `mpv-dev-x86_64-[data]-git-[hash].7z`;
- yt-dlp: `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`;
- MediaInfo: `https://mediaarea.net/en/MediaInfo/Download/Windows`, asset `MediaInfo_DLL_[versao]_Windows_x64_WithoutInstaller.7z`;
- DLLs WPF/.NET e `vcruntime140_cor3.dll`: publish self-contained oficial do SDK .NET Desktop/WPF.
- Gettext.Tools: `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, usado para obter `msgfmt.exe` e gerar `Locale` quando `msgfmt.exe` nao esta no `PATH`.

O script usa o asset x64 generico de libmpv, nao `x86_64-v3`, para preservar compatibilidade com mais CPUs x64. Se o GitHub mudar os nomes dos assets, se a MediaArea mudar o link de download, se o NuGet mudar a resolucao de `Gettext.Tools`, se o download falhar, se a extracao falhar, se algum arquivo baixado estiver vazio ou se uma DLL obrigatoria nao for x64, o script deve abortar antes de gerar um pacote parcial. `MediaInfo.dll` pode ser pinada por `-MediaInfoVersion`/`MPVNET_MEDIAINFO_VERSION` ou sobrescrita por `-MediaInfoFile`.

O workflow manual `.github/workflows/release-packages.yml` executa esse mesmo script no GitHub Actions. Ele sempre publica os pacotes como artefato do workflow e, quando executado com `create_release=true`, tambem cria a Release no repositorio. O workflow roda `validate-native-dependencies.ps1` antes do upload dos artefatos.

Este fork nao publica um pacote NuGet/container no GitHub Packages por enquanto; os pacotes de distribuicao do aplicativo sao assets de GitHub Releases e artefatos do workflow.

Para uma release emergencial, com incremento automatico do ultimo numero da versao, commit, push e disparo do workflow:

```powershell
src\Tools\publish-emergency-release.ps1
```

Para incluir o instalador no workflow emergencial:

```powershell
src\Tools\publish-emergency-release.ps1 -CreateInstaller
```

Esse script exige arvore Git limpa antes de alterar `src\BuildVersion.props`. Ele nao substitui a revisao manual de changelog, UI e compatibilidade; e uma rota curta para publicar uma nova versao do branch atual quando necessario.

Validado em 2026-05-31: execucao local de `src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease` gerou o ZIP portatil `mpv.net-v7.1.2.5-portable-x64.zip` e o instalador `MPV.NET-Media-Player-Community-Edition-v7.1.2.5-setup-x64.exe`, baixou MediaInfo/FFmpeg/libmpv/yt-dlp, gerou `Locale` para todos os catalogos ativos, incluiu `portable_config` e validou as DLLs nativas obrigatorias no publish, na pasta portatil e dentro do ZIP.

Pendente real: validar o workflow manual do GitHub Actions e a revisao manual completa da UI no pacote gerado.

---

# Versão

A versão atual do executável está centralizada em `src/BuildVersion.props`:

```xml
<MpvNetVersion>7.1.2.6</MpvNetVersion>
```

O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` importa essa propriedade e
usa `MpvNetVersion` para `FileVersion`, `AssemblyVersion` e
`InformationalVersion`. O script de release usa a versão do arquivo publicado
para montar os nomes dos artefatos e a tag da release.

---

# Possíveis problemas comuns

## SDK ou runtime ausente

Confira os `TargetFramework` dos projetos e instale o SDK/runtime correspondente.

## Dependência nativa ausente

Se a aplicação compilar mas não abrir ou falhar ao iniciar reprodução, verifique `libmpv-2.dll`, `MediaInfo.dll`, arquitetura x64 e diretório de execução. Para o pacote portatil, verifique tambem `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` ao lado de `mpvnet.exe`. No fluxo de build/release, FFmpeg, libmpv, yt-dlp e `MediaInfo.dll` devem ser baixados ou atualizados automaticamente por `src\Tools\prepare-native-dependencies.ps1`.

## Ferramenta de release ausente

O script falha se 7-Zip, Inno Setup ou `gh` não estiverem nos locais esperados.

## Caminho fixo do Inno Setup

`src/Setup/Inno/build-windows-installer.iss` usa `OutputDir=E:\Desktop`. Ajustar esse caminho exige cuidado para não quebrar o fluxo original.

---

# Checklist de validação manual

Após compilar:

- abrir aplicação sem argumentos;
- abrir arquivo de vídeo;
- abrir arquivo de áudio;
- abrir imagem;
- abrir URL;
- abrir múltiplos arquivos;
- testar play/pause;
- testar fullscreen;
- testar menu de contexto;
- testar editor de configuração;
- testar editor de input;
- abrir pasta de configuração;
- alterar uma opção simples;
- fechar e abrir novamente;
- verificar persistência;
- validar tema claro/escuro;
- validar modo portátil com `portable_config`;
- validar `MPVNET_HOME`.

---

# Pendências deste guia

- Rodar e registrar resultado de `dotnet build src\MpvNet.sln`.
- Rodar e registrar resultado de `dotnet publish` x64 em rodada futura.
- Validar revisão manual completa de UI, fullscreen, menu, atalhos, temas e persistência.
- Validar o workflow manual `.github/workflows/release-packages.yml`.
