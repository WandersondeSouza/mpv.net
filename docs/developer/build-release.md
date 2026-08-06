# Build e release do MPV.NET Media Player

## Objetivo

Este documento orienta como preparar o ambiente para estudar, compilar e manter o fork **MPV.NET Media Player**.

> Status: estrutura real do projeto mapeada. O build local da aplicacao Windows e o fluxo local de release foram validados em Windows, incluindo ZIP portatil, instalador, Locale, validacao de dependencias nativas e validacao do pacote MSIX/WAP no Visual Studio 2026 Community. A versao atual preparada para publicacao e `7.1.13.1`. Ainda falta fechar a revisao manual completa de UI/compatibilidade em maquina de uso final.

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
- `libmpv-2.dll` e `libmpv-2-v3.dll` x64, ambas da mesma revisao upstream do mpv/libmpv;
- `MediaInfo.dll` x64 baixada da MediaArea oficial durante a release;
- `ffmpeg.exe`, `ffplay.exe` e `ffprobe.exe` baixados sob demanda para `%LOCALAPPDATA%\mpv.net\Component` pelo player;
- arquivos de `Locale`, quando aplicável.

Para release:

- 7-Zip em `C:\Program Files\7-Zip\7z.exe`;
- Inno Setup 6 em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`, exceto quando `-SkipInstaller` for usado;
- GitHub CLI (`gh`), exceto quando `-SkipGitHubRelease` for usado;
- variável `GH_TOKEN` configurada para criação de release;
- acesso a internet para baixar libmpv, MediaInfo, `mpvnet.com` e `Gettext.Tools` no momento da release, quando esses arquivos/ferramentas ainda nao estiverem disponiveis localmente ou para semear o cache de componentes na primeira execucao do player.

Observacao: Inno Setup, GitHub CLI e `GH_TOKEN` deixam de ser obrigatorios quando o script e executado, respectivamente, com `-SkipInstaller` e `-SkipGitHubRelease`.
Os downloads de dependencias nativas e auxiliares ficam em `artifacts\native-dependencies\downloads` e sao reutilizados por ate 20 dias. Se o arquivo nao existir ou estiver mais antigo, o script baixa novamente a versao mais recente encontrada nas fontes configuradas.
Os fluxos de build, release, portátil, instalador e Store sempre preparam as duas variantes. O parâmetro legado `-MpvBuildVariant` continua aceito pelos scripts para compatibilidade, mas não seleciona uma distribuição exclusiva; não o use para tentar gerar um pacote somente v3.

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
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | biblioteca | `net10.0-windows7.0`, `PackageReference` | suporte WPF/NGettext |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | exemplo | extensão .NET | exemplo carregável |
| `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` | empacotamento MSIX/WAP | `net10.0-windows10.0.26100.0` | pacote para Microsoft Store |

Pacotes NuGet versionados em `src/Directory.Packages.props`:

- `CommunityToolkit.Mvvm` `8.4.2`;
- `NGettext` `0.6.7`;
- `Microsoft.Xaml.Behaviors.Wpf` `1.1.142`.

## Scripts de `src/Tools`

O conteúdo gerenciado obrigatório de toda distribuição é verificado por
`validate-package-contents.ps1`. A geração falha quando `mpvnet.exe` ou
`Scripts/osc.lua` não está presente. O fluxo central valida o diretório de
publicação, a pasta portátil e o ZIP; o Inno Setup impede a compilação do
instalador sem o OSC. O projeto WAP inclui o script explicitamente no payload e
`publish-store-package.ps1` inspeciona o pacote final da Microsoft Store,
inclusive contêineres e bundles aninhados.
Quando um payload incluir auxiliares opcionais, o validador também rejeita
arquivos vazios e um bundle FFmpeg parcial; a ausência completa continua
permitida para a política de download sob demanda.

Esta e a lista canonica de scripts do fork. Os demais documentos devem apontar para esta secao em vez de repetir a lista inteira.

| Script | Uso |
| --- | --- |
| `build-release-package.ps1` | fluxo completo de release local |
| `generate-portable-zip.ps1` | ZIP portatil |
| `generate-installer-exe.ps1` | instalador Inno Setup |
| `prepare-native-dependencies.ps1` | dependencias nativas e auxiliares |
| `prepare-build-output.ps1` | preparo automatico do output no build do app Windows |
| `validate-native-dependencies.ps1` | validacao de DLLs nativas em pasta ou ZIP |
| `validate-package-contents.ps1` | validação de `mpvnet.exe`, OSC e demais arquivos gerenciados obrigatórios em pastas e pacotes |
| `set-release-version.ps1` | atualiza a versao publica em `BuildVersion.props` e grava a versao Store em `Package.appxmanifest` com revisao zero |
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
- alinhamento entre `src\BuildVersion.props` e `src\MpvNet.Pacote\Package.appxmanifest`, considerando que a Microsoft Store exige revisao zero no manifesto MSIX.

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

Esse alvo chama `src\Tools\prepare-native-dependencies.ps1` e garante `MediaInfo.dll`, `libmpv-2.dll` e `libmpv-2-v3.dll` na pasta da configuração compilada. Ele baixa apenas o que estiver faltando e mantém as duas DLLs com revisões upstream idênticas. O fluxo novo do player passa a manter `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `mpvnet.com` em `%LOCALAPPDATA%\mpv.net\Component` quando necessario. O `yt-dlp.exe` fica sob responsabilidade do bootstrap do player em runtime. Para forcar atualizacao dos arquivos ja presentes, chame o script direto com `-UpdateExisting`. Ele nao baixa DLLs Microsoft/.NET/WPF de sites externos; essas DLLs continuam vindo do publish self-contained.

No runtime, o player passa a usar `%LOCALAPPDATA%\mpv.net\Component` como cache de componentes baixados antes da interface abrir, com fallback para os binarios que vierem junto da instalacao. O contrato desta etapa preserva `libmpv-2.dll`, `libmpv-2-v3.dll`, `MediaInfo.dll` e as DLLs do runtime ao lado do executavel, enquanto `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `mpvnet.com` migram para a pasta de componente quando a rede estiver disponivel. O bootstrap trata `ffmpeg.exe`, `ffplay.exe` e `ffprobe.exe` como um bundle unico: valida o digest do ZIP compartilhado uma vez, extrai os tres binarios do mesmo archive e grava a freshness desse grupo em `ffmpeg-bundle.json`. O `yt-dlp.exe` continua com validação direta do proprio binario quando e renovado pelo player.
Em pacote MSIX/Microsoft Store, esse caminho deve ser entendido como a visao de LocalAppData disponivel para o aplicativo empacotado; o Windows pode virtualizar escritas em AppData para uma area privada do pacote. O aplicativo nao deve solicitar `broadFileSystemAccess` para esse fluxo, porque os componentes pertencem ao proprio app e continuam sob o cache `Component`. A pasta do pacote e somente leitura: o bootstrap nunca a atualiza. Antes de cada submissão Store, confirme no Partner Center que a atualização de executáveis auxiliares baixados está coerente com a funcionalidade declarada e com as políticas vigentes; a aprovação de um build MSIX local não é prova de certificação.

Para publicar como o script de release atual faz:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Release --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

## Seleção dual de libmpv

`libmpv-2.dll` é a build normal, compatível com o escopo x64 suportado pelo aplicativo. `libmpv-2-v3.dll` é uma build x86-64-v3; o ganho potencial depende do conteúdo, decodificação, filtros, drivers e demais partes do pipeline, portanto não há promessa de melhoria fixa.

O carregador usa caminhos absolutos a partir de `AppContext.BaseDirectory`. Ele verifica SSE3, SSSE3, SSE4.1, SSE4.2, POPCNT, AVX, AVX2, F16C, FMA, BMI1, BMI2, LZCNT e MOVBE. Em CPU compatível, tenta a v3; se o arquivo ou o carregamento falhar, registra o motivo e carrega a normal. Em CPU incompatível, carrega a normal diretamente. A normal é obrigatória em toda distribuição oficial.

O log de depuração registra `libmpv selection completed`, com compatibilidade da CPU, DLL preferida/carregada, caminho e eventual fallback. Para diagnosticar uma pasta de distribuição sem abrir a interface completa, execute:

```powershell
.\mpvnet.exe --diagnose-libmpv
```

Esse modo carrega a DLL selecionada, chama `mpv_client_api_version` e cria/destroi um contexto mpv. Em desenvolvimento, `MPVNET_FORCE_LIBMPV_VARIANT=normal`, `auto` ou `x86_64-v3` serve apenas para diagnóstico temporário; não é uma preferência persistida. O valor `x86_64-v3` falha em CPU incompatível.

## Empacotamento Microsoft Store

O fork agora inclui um projeto WAP/MSIX separado em `src/MpvNet.Pacote/MpvNet.Pacote.wapproj`.
Ele referencia `src/MpvNet.Windows/MpvNet.Windows.csproj`, usa `src/BuildVersion.props` como fonte da versao e inclui um conjunto basico de assets `Images\` para a Store.
A identidade reservada atual do pacote e `24183GestodeSistemas.MPV.NETMediaPlayer`, com `Publisher` `CN=6581967D-2DE4-48DE-A846-C6F69ECA7701`.
O pacote publicado na Microsoft Store usa o `Package/Properties/PublisherDisplayName` `Gestão de Sistemas`, o `Package Family Name` `24183GestodeSistemas.MPV.NETMediaPlayer_ex0zyz39hzsk6` e o `ID da Store` `9N441SP6XHLD`.
Link profundo da Store: `ms-windows-store://pdp/?productid=9N441SP6XHLD`
URL da Web Store: [https://apps.microsoft.com/detail/9N441SP6XHLD](https://apps.microsoft.com/detail/9N441SP6XHLD)
O auto incremento de revisao do MSIX fica desativado. A Microsoft Store nao aceita pacote com quarto componente diferente de zero no `Identity Version`, entao `BuildVersion.props` mantem a versao publica completa para executavel, ZIP e instalador, enquanto `Package.appxmanifest` usa a mesma versao com revisao zero. Exemplo: release `7.1.3.15` gera manifesto Store `7.1.3.0`. Quando precisar alterar a versao, use `src\Tools\set-release-version.ps1` para atualizar `BuildVersion.props` e `Package.appxmanifest` juntos.
O manifesto declara a aplicacao como desktop empacotada full trust, com `RuntimeBehavior="packagedClassicApp"` e `TrustLevel="mediumIL"`. Nao use `RuntimeBehavior="windowsApp"` para este projeto WinForms/WPF, pois isso aproxima o pacote do modelo UWP e pode interferir no acesso esperado a rede, AppData e componentes auxiliares.

Pontos importantes:

- o `Identity Name` e o `Publisher` em `Package.appxmanifest` ainda precisam ser trocados pelos valores reais do Partner Center/certificado antes da publicação;
- o payload x64 inclui as duas DLLs libmpv; a seleção ocorre apenas entre arquivos já instalados, sem exigir CPU v3 para iniciar;
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

O script primeiro executa `ValidateStorePackage` e depois faz o `Build` do projeto WAP, para falhar cedo quando a assinatura ou a versao do manifest estao incorretas. Essa validacao rejeita manifesto com revisao diferente de zero para Store.
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
Pacotes de diagnostico usam a mesma versao publica do pacote normal e recebem
apenas o sufixo `-diagnostic` no nome do artefato.
Eles sao artefatos de suporte e nao devem ser publicados como assets da Release
do GitHub. No workflow manual, `create_release=true` com
`enable_file_logging=true` e bloqueado; gere diagnostico com
`create_release=false` e baixe pelo artefato do workflow.
Detalhes: `docs/logging.md`.

---

# Execução em debug

O projeto de inicialização deve ser `MpvNet.Windows`.

Pontos a validar:

- o executável localiza `libmpv-2.dll`;
- o executável localiza `libmpv-2-v3.dll`;
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

Quando for necessario sobrescrever `MediaInfo.dll`, fornecer um `mpvnet.com` local ou informar uma descricao explicita para a publicacao do GitHub, passe os parametros correspondentes:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com -ReleaseNotesFile .\release-notes.md
```

O script:

1. valida `MpvNet.sln`;
2. valida 7-Zip e, quando aplicavel, Inno Setup;
3. publica `MpvNet.Windows.csproj` self-contained para `win-x64`;
4. cria nomes com base na versão do `mpvnet.exe`;
5. copia arquivos publicados;
6. chama `src\Tools\prepare-native-dependencies.ps1` para baixar ou validar `MediaInfo.dll`, `libmpv-2.dll` e `libmpv-2-v3.dll`, reutilizando downloads com ate 20 dias em `artifacts\native-dependencies\downloads`;
7. valida e copia as DLLs Microsoft/.NET `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll` vindas do publish self-contained;
8. copia `MediaInfo.dll`, `libmpv-2.dll` e `libmpv-2-v3.dll` x64;
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
16. cria release no GitHub usando `gh release create`, exceto com `-SkipGitHubRelease`; quando publicar, use `-ReleaseNotes` ou `-ReleaseNotesFile` para colocar a descricao diretamente no corpo da release.

As dependencias baixadas automaticamente usam estas fontes:

- libmpv: `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, asset normal `mpv-dev-x86_64-[data]-git-[hash].7z` ou asset v3 `mpv-dev-x86_64-v3-[data]-git-[hash].7z`;
- MediaInfo: `https://mediaarea.net/en/MediaInfo/Download/Windows`, asset `MediaInfo_DLL_[versao]_Windows_x64_WithoutInstaller.7z`;
- DLLs WPF/.NET e `vcruntime140_cor3.dll`: publish self-contained oficial do SDK .NET Desktop/WPF.
- Gettext.Tools: `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, usado para obter `msgfmt.exe` e gerar `Locale` quando `msgfmt.exe` nao esta no `PATH`.

O script resolve um asset normal e um asset v3 da mesma revisão upstream, mantém os nomes físicos distintos e preserva a ABI `libmpv-2` do P/Invoke. Se o GitHub mudar os nomes dos assets, se a MediaArea mudar o link de download, se o NuGet mudar a resolucao de `Gettext.Tools`, se o download falhar, se a extracao falhar, se algum arquivo baixado estiver vazio, se as DLLs forem idênticas ou se uma DLL obrigatoria nao for x64, o script deve abortar antes de gerar um pacote parcial. `MediaInfo.dll` pode ser pinada por `-MediaInfoVersion`/`MPVNET_MEDIAINFO_VERSION` ou sobrescrita por `-MediaInfoFile`.

Smoke test das duas variantes de dependencias:

```powershell
.\src\Tools\test-mpv-build-variants.ps1
```

Depois de gerar um pacote dual, execute a revisão manual com seleção automática e com `MPVNET_FORCE_LIBMPV_VARIANT=normal`: inicialização, reprodução de arquivo local, pause/play, seek, fullscreen, legenda, áudio e fechamento. Em CPU compatível, confirme no log a tentativa da v3 e o fallback para a normal quando a v3 não estiver disponível.

O workflow manual `.github/workflows/release-packages.yml` executa esse mesmo script no GitHub Actions. Ele sempre publica os pacotes como artefato do workflow e, quando executado com `create_release=true`, tambem cria a Release no repositorio. O workflow roda `validate-native-dependencies.ps1` antes do upload dos artefatos. Pacotes de diagnostico com `enable_file_logging=true` so podem ser gerados como artefato do workflow; o workflow bloqueia a combinacao `create_release=true` e `enable_file_logging=true`.

Este fork nao publica um pacote NuGet/container no GitHub Packages por enquanto; os pacotes de distribuicao do aplicativo sao assets de GitHub Releases e artefatos do workflow.

Para uma release emergencial, com incremento automatico do ultimo numero da versao, commit, push e disparo do workflow:

```powershell
src\Tools\publish-emergency-release.ps1
```

Para incluir o instalador no workflow emergencial:

```powershell
src\Tools\publish-emergency-release.ps1 -CreateInstaller
```

Esse script exige arvore Git limpa antes de alterar a versao. Ele usa
`src\Tools\set-release-version.ps1 -IncrementRevision`, portanto atualiza
`src\BuildVersion.props` e `src\MpvNet.Pacote\Package.appxmanifest` no mesmo
commit. Ele nao substitui a revisao manual de descricao da release, UI e compatibilidade;
e uma rota curta para publicar uma nova versao do branch atual quando
necessario. Como a release emergencial publica assets no GitHub, ela nao aceita
`-EnableFileLogging`; pacotes de diagnostico devem ser gerados separadamente
sem publicacao.

A partir de 2026-06-02, o fluxo de release publica em `Release` e nomeia os artefatos como `MPV.NET-Media-Player-v<versao>-setup-x64.exe` para o instalador e `MPV.NET-Media-Player-v<versao>-portable-x64.zip` para o ZIP portatil, baixa MediaInfo/libmpv, gera `Locale` para todos os catalogos ativos, inclui `portable_config` e valida as DLLs nativas obrigatorias no publish, na pasta portatil e dentro do ZIP. Quando `-EnableFileLogging` e usado, os artefatos recebem `-diagnostic` antes do tipo de pacote sem alterar a versao publica.

Validacao de release registrada em 2026-06-23: a versao `7.1.4.2` consolida
as mudancas posteriores a `7.1.4.1`, incluindo refatoracoes conservadoras de
organizacao, nullability, excecoes, descarte de recursos nativos, async/task e
blocos grandes, alem de ajustes no cache de URL remota, logs e documentacao.
Antes da publicacao, a versao publica foi alinhada com
`src\Tools\set-release-version.ps1`, mantendo `BuildVersion.props` em
`7.1.4.2` e o manifesto MSIX em `7.1.4.0`, conforme a regra de revisao zero da
Microsoft Store.

Validacao local registrada em 2026-06-12: `build-release-package.ps1` concluiu
com `-SkipGitHubRelease`, gerando ZIP portatil e instalador x64, compilando
`Locale` e validando dependencias nativas no publish, na pasta portatil e no
ZIP.

Validacao da versao `7.1.13.1` registrada em 2026-08-05: a release consolida
ajustes recentes de cache de rede com precedencia para configuracoes explicitas,
carregamento de playlists e URLs, retorno de `Esc` do fullscreen, melhorias do
OSC com botoes localizados do site oficial e doacao, alem da validacao de
conteudo obrigatorio do pacote. A versao publica foi alinhada com
`src\Tools\set-release-version.ps1`, mantendo `BuildVersion.props` em
`7.1.13.1` e o manifesto MSIX em `7.1.13.0`, conforme a regra de revisao zero
da Microsoft Store.

Pendente real: validar o workflow manual do GitHub Actions, o pacote MSIX/WAP em
ambiente com Desktop Bridge/MSIX instalado e a revisao manual completa da UI no
pacote gerado.

---

# Versão

A versão publica está centralizada em `src/BuildVersion.props`:

```xml
<MpvNetVersion>7.1.13.1</MpvNetVersion>
```

O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` importa essa propriedade e
usa `MpvNetVersion` para `FileVersion`, `AssemblyVersion` e
`InformationalVersion`. O script de release usa a versão do arquivo publicado
para montar os nomes dos artefatos e a tag da release.
O manifesto MSIX ainda exige um valor literal em `Package.appxmanifest`; por
isso a alteracao de versao deve ser feita por:

```powershell
.\src\Tools\set-release-version.ps1 -Version 7.1.13.1
```

Esse comando grava `7.1.13.1` em `BuildVersion.props` e `7.1.13.0` no
`Identity Version` do manifesto MSIX, atendendo a regra da Microsoft Store de
revisao zero no pacote.

Para apenas incrementar o ultimo numero:

```powershell
.\src\Tools\set-release-version.ps1 -IncrementRevision
```

Nao edite `BuildVersion.props` e `Package.appxmanifest` separadamente.

---

# Possíveis problemas comuns

## SDK ou runtime ausente

Confira os `TargetFramework` dos projetos e instale o SDK/runtime correspondente.

## Dependência nativa ausente

Se a aplicação compilar mas não abrir ou falhar ao iniciar reprodução, verifique `libmpv-2.dll`, `libmpv-2-v3.dll`, `MediaInfo.dll`, arquitetura x64 e diretório de execução. Rode `mpvnet.exe --diagnose-libmpv` para registrar a seleção e o fallback. `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `mpvnet.com` passam a ser obtidos pelo cache de componentes do player em `%LOCALAPPDATA%\mpv.net\Component` quando necessário. O `yt-dlp.exe` e renovado pelo bootstrap do player em runtime. O bootstrap baixa e extrai cada componente em sequência, e se algum item falhar ele fica ausente do cache para ser tentado novamente na próxima abertura do player. No fluxo de build/release, as duas DLLs libmpv e `MediaInfo.dll` devem continuar sendo preparados automaticamente por `src\Tools\prepare-native-dependencies.ps1`.

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




