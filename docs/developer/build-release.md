# Build e release do MPV.NET Media Player

## Objetivo

Este documento orienta como preparar o ambiente para estudar, compilar e manter o fork **MPV.NET Media Player**.

> Status: estrutura real do projeto mapeada. O build local da aplicacao Windows e o fluxo local de release foram validados em Windows, incluindo ZIP portatil, instalador, Locale, validacao de dependencias nativas e validacao do pacote MSIX/WAP no Visual Studio 2026 Community. A versao atual preparada para publicacao e `7.1.3.13`. Ainda falta fechar a revisao manual completa de UI/compatibilidade em maquina de uso final.

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
- `libmpv-2.dll` x64, usando por padrao a build 64bit-v3 do mpv/libmpv;
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
O parametro `-MpvBuildVariant x86_64-v3` e o padrao do fork e baixa a build 64bit-v3 do mpv/libmpv, que continua fornecendo `libmpv-2.dll`, mas exige CPU compativel com x86_64-v3, como Intel Haswell/AMD Excavator ou mais recente. Use `-MpvBuildVariant normal` apenas quando precisar gerar pacote para CPUs x64 mais antigas.

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
| `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` | empacotamento MSIX/WAP | `net10.0-windows10.0.26100.0` | pacote para Microsoft Store |

Pacotes NuGet versionados em `src/Directory.Packages.props`:

- `CommunityToolkit.Mvvm` `8.4.2`;
- `NGettext` `0.6.7`;
- `Microsoft.Xaml.Behaviors.Wpf` `1.1.142`.

## Scripts de `src/Tools`

Esta e a lista canonica de scripts do fork. Os demais documentos devem apontar para esta secao em vez de repetir a lista inteira.

| Script | Uso |
| --- | --- |
| `build-release-package.ps1` | fluxo completo de release local |
| `generate-portable-zip.ps1` | ZIP portatil |
| `generate-installer-exe.ps1` | instalador Inno Setup |
| `prepare-native-dependencies.ps1` | dependencias nativas e auxiliares |
| `prepare-build-output.ps1` | preparo automatico do output no build do app Windows |
| `validate-native-dependencies.ps1` | validacao de DLLs nativas em pasta ou ZIP |
| `publish-emergency-release.ps1` | release emergencial com bump de versao |
| `update-mpv-runtime.ps1` | atualizacao do runtime mpv |
| `test-mpv-build-variants.ps1` | smoke test das variantes de build |
| `publish-store-package.ps1` | publicacao do pacote Microsoft Store |

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

## Validacao do pacote Microsoft Store no Visual Studio

O projeto `src\MpvNet.Pacote\MpvNet.Pacote.wapproj` ja valida automaticamente:

- assinatura de distribuicao;
- alinhamento entre `src\BuildVersion.props` e `src\MpvNet.Pacote\Package.appxmanifest`.

Para usar o Visual Studio nesse fluxo:

1. Instale o workload/componente de Desktop Bridge/MSIX no Visual Studio.
2. Abra `src\MpvNet.sln`.
3. Carregue o projeto `MpvNet.Pacote`.
4. Se for publicar de verdade, crie `src\MpvNet.Pacote\Packaging.Distribution.props` com `PackagePublisher` e `PackageCertificateKeyFile`.
5. Compile `Release|x64` no projeto de pacote.

Se os targets do Desktop Bridge/MSIX estiverem instalados, o build do projeto de pacote passa pela validacao antes de gerar o pacote. Se o ambiente nao tiver esses componentes, o projeto apenas avisa e ignora a compilacao do pacote.
No ambiente atual de manutencao, os arquivos `Microsoft.DesktopBridge.props` e `Microsoft.DesktopBridge.targets` estao presentes em `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Microsoft\DesktopBridge\`, e a validacao `ValidateStorePackage` concluiu com sucesso apos o alinhamento da versao do manifesto.

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

Esse alvo opt-in chama `src\Tools\prepare-native-dependencies.ps1` e garante `MediaInfo.dll`, `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `mpvnet.com` na pasta da configuracao compilada, baixando apenas o que estiver faltando. Para forcar atualizacao dos arquivos ja presentes, chame o script direto com `-UpdateExisting`. Ele nao baixa DLLs Microsoft/.NET/WPF de sites externos; essas DLLs continuam vindo do publish self-contained. Para testar a variante otimizada localmente, use `dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj /p:MpvBuildVariant=x86_64-v3`.

Para publicar como o script de release atual faz:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Release --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

## Empacotamento Microsoft Store

O fork agora inclui um projeto WAP/MSIX separado em `src/MpvNet.Pacote/MpvNet.Pacote.wapproj`.
Ele referencia `src/MpvNet.Windows/MpvNet.Windows.csproj`, usa `src/BuildVersion.props` como fonte da versao e inclui um conjunto basico de assets `Images\` para a Store.
A identidade reservada atual do pacote e `24183GestodeSistemas.MPV.NETMediaPlayer`, com `Publisher` `CN=6581967D-2DE4-48DE-A846-C6F69ECA7701`.

Pontos importantes:

- o `Identity Name` e o `Publisher` em `Package.appxmanifest` ainda precisam ser trocados pelos valores reais do Partner Center/certificado antes da publicação;
- os assets de Store foram gerados a partir do `mpv-icon.ico` atual para permitir a montagem do projeto no fork;
- o projeto foi adicionado à solução `src/MpvNet.sln`, mas depende do Desktop Bridge/MSIX instalado no Visual Studio para gerar o pacote de fato.
- o manifesto MSIX declara associacoes de video, audio e playlists para o Windows listar o app como abridor desses arquivos.
- o pacote MSIX nao grava a pasta instalada diretamente no `PATH` como o Inno Setup; para terminal, ele declara o alias `mpvnet.exe`, resolvido pelo Windows via App Execution Alias.
- o arquivo real `src/MpvNet.Pacote/Packaging.Distribution.props` fica fora do Git por padrao e deve conter apenas dados locais/secretos do certificado.
- se o `.pfx` tiver senha, use `MPVNET_STORE_CERTIFICATE_PASSWORD` no CI ou `-PackageCertificatePassword` no script local.
- se existir um `.pfx` comum ao lado de `src/MpvNet.Pacote` ou em `src/`, o script tenta descobri-lo automaticamente antes de exigir parametros.

Script de publicacao dedicado:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\publish-store-package.ps1 .\src .\artifacts\store
```

O script primeiro executa `ValidateStorePackage` e depois faz o `Build` do projeto WAP, para falhar cedo quando a assinatura ou a versao do manifest estao incorretas.
Para envio real, copie `src\MpvNet.Pacote\Packaging.Distribution.props.example` para `src\MpvNet.Pacote\Packaging.Distribution.props` e ajuste `PackagePublisher` e `PackageCertificateKeyFile` para o certificado usado na publicacao.
No CI ou em maquina local, o script tambem aceita `MPVNET_STORE_CERTIFICATE_KEYFILE`, `MPVNET_STORE_CERTIFICATE_PASSWORD` e `MPVNET_STORE_PUBLISHER` como variaveis de ambiente.
O script resolve automaticamente um `.pfx` local em caminhos comuns e mostra o certificado usado quando encontra um candidato.

## Validacao WACK

O relatorio do Windows App Certification Kit de 2026-06-10 para o pacote `7.1.3.12` retornou aprovado com avisos. Os ajustes consolidados foram:

- `MpvNet.Windows.csproj` declara explicitamente `app.manifest`, garantindo que o executavel empacotado preserve `PerMonitorV2`;
- `BadgeLogo.png` e `BadgeLogo.scale-200.png` usam fundo transparente e glifo branco, conforme a regra de badge da Store;
- o teste opcional de executaveis bloqueados pode listar chamadas intencionais do app e referencias do runtime .NET/WPF. Remova somente chamadas proprias desnecessarias; nao tente limpar assemblies do runtime nem quebrar comandos compativeis com mpv como `shell-execute`, abertura de URLs/manuais e pasta de configuracao.

Observacao: o script de release publica em `Release`. Ele chama o publish com `/p:EnsureBuildAssets=false`, porque prepara e valida os binarios nativos em uma etapa propria depois do publish.
Logs detalhados em arquivo ficam desligados por padrao com `/p:EnableFileLogging=false`.
Erros continuam sendo gravados em qualquer build. Para pacote de diagnostico,
use `/p:EnableFileLogging=true` ou `-EnableFileLogging` nos scripts de release.
Detalhes: `docs/logging.md`.

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

## Comandos prontos para copiar e colar

Abra o PowerShell ou o Terminal do Windows e cole um dos blocos abaixo.
O comando cria `C:\Users\<usuario>\source\repos\mpv.net` se o repositório ainda
não existir, entra na raiz do projeto e usa `artifacts\release` como saída. A
pasta de saída também é criada automaticamente pelo script.

Gerar apenas o ZIP portátil:

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Para gerar um pacote avancado com mpv/libmpv 64bit-v3:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -MpvBuildVariant x86_64-v3
```

Resultado esperado:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64.zip
```

Gerar apenas o instalador:

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Resultado esperado:

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-setup-x64.exe
```

Gerar ZIP e instalador localmente, sem publicar no GitHub:

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Se você já usa outro diretório, abra o terminal na raiz do repositório e execute
apenas a última linha do bloco escolhido. O valor de `-SourceDir` deve apontar
para a pasta `src` do repositório, não para a raiz.

## Parâmetros dos scripts

Script principal:

```text
src\Tools\build-release-package.ps1
```

Uso:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Por padrão, o script principal tenta publicar em `WandersondeSouza/mpv.net`.
Para gerar apenas artefatos locais, mantenha `-SkipGitHubRelease`.

Para gerar apenas o ZIP portátil, sem instalador e sem publicação:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Para gerar apenas o instalador executável, sem ZIP e sem publicação:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Quando for necessario sobrescrever `MediaInfo.dll` ou fornecer um `mpvnet.com` local, informe os arquivos explicitamente:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
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
14. o instalador adiciona a pasta instalada ao `PATH` do Windows e remove essa
    entrada durante a desinstalacao;
15. o instalador executa `mpvnet.exe --register-file-associations video`,
    `mpvnet.exe --register-file-associations audio` e
    `mpvnet.exe --register-file-associations playlist` para registrar as
    associacoes de video, audio e playlists IPTV apos a instalacao; imagens
    continuam opt-in pelo menu `Config > Setup > Register image file associations`;
16. cria release no GitHub usando `gh release create`, exceto com `-SkipGitHubRelease`.

As dependencias baixadas automaticamente usam estas fontes:

- FFmpeg: `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`, asset `ffmpeg-master-latest-win64-gpl.zip`;
- libmpv: `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, asset normal `mpv-dev-x86_64-[data]-git-[hash].7z` ou asset v3 `mpv-dev-x86_64-v3-[data]-git-[hash].7z`;
- yt-dlp: `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`;
- MediaInfo: `https://mediaarea.net/en/MediaInfo/Download/Windows`, asset `MediaInfo_DLL_[versao]_Windows_x64_WithoutInstaller.7z`;
- DLLs WPF/.NET e `vcruntime140_cor3.dll`: publish self-contained oficial do SDK .NET Desktop/WPF.
- Gettext.Tools: `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, usado para obter `msgfmt.exe` e gerar `Locale` quando `msgfmt.exe` nao esta no `PATH`.

O script usa o asset `x86_64-v3` de libmpv por padrao. Para preservar compatibilidade com CPUs x64 mais antigas, gere explicitamente com `-MpvBuildVariant normal` ou `MPVNET_MPV_BUILD_VARIANT=normal`. A variante escolhida nao altera o nome da DLL nem a ABI esperada pelo P/Invoke. Se o GitHub mudar os nomes dos assets, se a MediaArea mudar o link de download, se o NuGet mudar a resolucao de `Gettext.Tools`, se o download falhar, se a extracao falhar, se algum arquivo baixado estiver vazio ou se uma DLL obrigatoria nao for x64, o script deve abortar antes de gerar um pacote parcial. `MediaInfo.dll` pode ser pinada por `-MediaInfoVersion`/`MPVNET_MEDIAINFO_VERSION` ou sobrescrita por `-MediaInfoFile`.

Smoke test das duas variantes de dependencias:

```powershell
.\src\Tools\test-mpv-build-variants.ps1
```

Depois de gerar os pacotes normal e `x86_64-v3`, execute a revisao manual em uma CPU compativel com a variante escolhida: inicializacao, reproducao de arquivo local, pause/play, seek, fullscreen, legenda, audio e fechamento.

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

A partir de 2026-06-02, o fluxo de release publica em `Release` e nomeia os artefatos como `MPV.NET-Media-Player-v<versao>-setup-x64.exe` para o instalador e `MPV.NET-Media-Player-v<versao>-portable-x64.zip` para o ZIP portatil, baixa MediaInfo/FFmpeg/libmpv/yt-dlp, gera `Locale` para todos os catalogos ativos, inclui `portable_config` e valida as DLLs nativas obrigatorias no publish, na pasta portatil e dentro do ZIP.

Validacao local registrada em 2026-06-12: `build-release-package.ps1` concluiu
com `-SkipGitHubRelease`, gerando ZIP portatil e instalador x64, compilando
`Locale` e validando dependencias nativas no publish, na pasta portatil e no
ZIP.

Pendente real: validar o workflow manual do GitHub Actions, o pacote MSIX/WAP em
ambiente com Desktop Bridge/MSIX instalado e a revisao manual completa da UI no
pacote gerado.

---

# Versão

A versão atual do executável está centralizada em `src/BuildVersion.props`:

```xml
<MpvNetVersion>7.1.3.13</MpvNetVersion>
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

Se a aplicação compilar mas não abrir ou falhar ao iniciar reprodução, verifique `libmpv-2.dll`, `MediaInfo.dll`, arquitetura x64, variante de CPU e diretório de execução. Para o pacote portatil, verifique tambem `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` ao lado de `mpvnet.exe`. No fluxo de build/release, FFmpeg, libmpv, yt-dlp e `MediaInfo.dll` devem ser baixados ou atualizados automaticamente por `src\Tools\prepare-native-dependencies.ps1`.

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

- Revalidar `dotnet build src\MpvNet.sln` quando houver mudanca relevante no
  build.
- Revalidar `dotnet publish` x64 quando houver mudanca relevante no fluxo de
  publish.
- Validar revisão manual completa de UI, fullscreen, menu, atalhos, temas e persistência.
- Validar o workflow manual `.github/workflows/release-packages.yml`.


