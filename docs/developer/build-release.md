# Build e release do MPV.NET Media Player

Use este guia para configurar o SDK, compilar o fork e preparar os pacotes x64.

## Tópicos

- [Requisitos](#requisitos-principais)
- [Build pelo terminal](#build-via-terminal)
- [Release e empacotamento](#release-e-empacotamento)
- [Microsoft Store e WACK](#empacotamento-microsoft-store)
- [Versão e manifesto](#versão)
- [Checklist manual](#checklist-de-validação-manual)

<a name="objetivo"></a>

## Requisitos principais

Para desenvolvimento:

- Windows 10 versão 2004 (build 19041) ou posterior, em x64;
- SDK estável do .NET 10. Para o WAP, instale também o Windows SDK `10.0.26100.0`;
- Visual Studio com desenvolvimento para desktop com .NET, se for usar a IDE, e Desktop Bridge/MSIX para o projeto WAP;
- Git;
- acesso ao repositório no GitHub.

Execute `dotnet --version` a partir da raiz para confirmar o SDK ativo.
O `global.json` aceita as atualizações estáveis da série 10.0 a partir da
10.0.100. Se houver apenas um SDK mais recente instalado, instale o 10.0
lado a lado ou selecione a instalação 10.0 no PATH da sessão de build.
`src/Directory.Build.props` fixa C# 14.0 e mantém `AnalysisLevel=10`
para as regras de análise do .NET 10. O projeto WAP empacota o executável
compilado pelo projeto Windows. Consulte o
[baseline da modernização](reports/csharp14-modernization-baseline.md)
para a matriz de diagnósticos e os comandos de comparação.

Para execução:

- Windows x64, com o mesmo mínimo de versão indicado acima;
- .NET Desktop Runtime 10 compatível com a arquitetura nas builds dependentes
  do runtime; ZIP e instalador publicados como self-contained incluem o runtime;
- `libmpv-2.dll` e `libmpv-2-v3.dll` x64, ambas da mesma revisão upstream do mpv/libmpv;
- `MediaInfo.dll` x64 baixada da MediaArea oficial durante a release;
- `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe` e `deno.exe` gerenciados pelo player em [Componentes auxiliares de runtime](configuration.md#componentes-auxiliares-de-runtime);
- arquivos de `Locale`, quando aplicável.

Para release:

- 7-Zip em `C:\Program Files\7-Zip\7z.exe`;
- Inno Setup 6 em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`, exceto quando `-SkipInstaller` for usado;
- GitHub CLI (`gh`), exceto quando `-SkipGitHubRelease` for usado;
- autenticação no GitHub CLI para criação de release. A variável `GH_TOKEN` é uma opção para CI;
- acesso à internet para baixar libmpv, MediaInfo, `mpvnet.com` e `Gettext.Tools` no momento da release, quando esses arquivos/ferramentas ainda não estiverem disponíveis localmente.

Os downloads de dependências nativas e auxiliares de todos os fluxos ficam em
`artifacts\native-dependencies\downloads`, incluindo Debug, Release, ZIP,
instalador e Microsoft Store. Um arquivo ausente, vazio ou com mais de dois dias é
baixado novamente; os demais são reutilizados. As extrações ficam em diretórios
isolados por execução e o cache é protegido por mutex entre processos.
O parâmetro legado `-MpvBuildVariant` continua aceito para compatibilidade. O fluxo padrão prepara ambas as variantes.

## Solução e projetos

A solução principal é [MpvNet.sln](../../src/MpvNet.sln).

| Projeto | Tipo | Target | Saída |
| --- | --- | --- | --- |
| `src/MpvNet/MpvNet.csproj` | biblioteca | `net10.0-windows10.0.19041.0` | `libmpvnet` |
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | aplicação Windows | `net10.0-windows10.0.19041.0`, `win-x64` | `mpvnet.exe` x64 |
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | biblioteca | `net10.0-windows10.0.19041.0`, `PackageReference` | suporte WPF/NGettext |
| `src/MpvNet.Tests/MpvNet.Tests.csproj` | testes | `net10.0-windows10.0.19041.0` | executável xUnit v3 |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | exemplo | `net10.0-windows10.0.19041.0` | exemplo carregável |
| `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` | empacotamento MSIX/WAP | payload .NET 10 do projeto Windows, mínimo Windows 10.0.19041.0 | pacote para Microsoft Store |

Consulte [Directory.Packages.props](../../src/Directory.Packages.props) para as versões de `CommunityToolkit.Mvvm`,
`NGettext`, `Microsoft.Xaml.Behaviors.Wpf`, `Microsoft.Windows.SDK.BuildTools`,
`xunit.v3`, `xunit.runner.visualstudio` e `TngTech.ArchUnitNET.xUnitV3`.

### Scripts de `src/Tools`

O conteúdo gerenciado obrigatório de toda distribuição é verificado por
`validate-package-contents.ps1`. A geração falha quando `mpvnet.exe` ou
`Scripts/osc.lua` não está presente. O fluxo central valida o diretório de
publicação, a pasta portátil e o ZIP; o Inno Setup impede a compilação do
instalador sem o OSC. O projeto WAP inclui o script explicitamente no payload e
`publish-store-package.ps1` inspeciona o pacote final da Microsoft Store,
inclusive contêineres e bundles aninhados.
No fluxo Microsoft Store, `mpvnet.com` também é obrigatório: o projeto WAP
valida o helper na pasta de build e o inclui explicitamente em
`MpvNet.Windows\mpvnet.com`, ao lado de `mpvnet.exe`. O script de publicação
rejeita o pacote final se o helper estiver ausente ou vazio.
Quando um payload incluir auxiliares opcionais, o validador também rejeita
arquivos vazios e um bundle FFmpeg parcial; a ausência completa continua
permitida para a política de download sob demanda.

Principais scripts de build, diagnóstico e empacotamento do fork.

| Script | Uso |
| --- | --- |
| `build-release-package.ps1` | fluxo completo de release local |
| `generate-portable-zip.ps1` | ZIP portátil |
| `generate-installer-exe.ps1` | instalador Inno Setup |
| `prepare-native-dependencies.ps1` | dependências nativas e auxiliares |
| `native-dependencies-config.ps1` | política central de caminho e validade do cache |
| `libmpv-validation.ps1` | funções de validação PE x64, exports, revisões e hashes das duas DLLs; contrato em `libmpv-build-contract.psd1` |
| `download-mediainfo-dependency.ps1` | entrada legada de compatibilidade para o preparo central de dependências |
| `prepare-build-output.ps1` | preparo automático do output no build do app Windows |
| `validate-native-dependencies.ps1` | validação de DLLs nativas em pasta ou ZIP |
| `validate-package-contents.ps1` | validação de `mpvnet.exe`, OSC e demais arquivos gerenciados obrigatórios em pastas e pacotes |
| `set-release-version.ps1` | atualiza a versão pública em `BuildVersion.props` e grava a versão Store em `Package.appxmanifest` com revisão zero |
| `publish-emergency-release.ps1` | release emergencial com bump de versão |
| `update-mpv-runtime.ps1` | atualização do runtime mpv |
| `test-mpv-build-variants.ps1` | contrato dual offline; com `-Online`, baixa e valida as duas builds upstream |
| `test-libmpv-build-contract.ps1` | valida o contrato dual e os padrões de nomes de assets sem rede |
| `publish-store-package.ps1` | geração e validação local do pacote da Microsoft Store |
| `test-native-dependency-cache.ps1` | smoke test offline do cache compartilhado |
| `test-native-dependency-download-validation.ps1` | teste offline da validação do SHA-256 publicado |
| `validate-windows-baseline.ps1` | verifica TFMs e manifestos do baseline Windows 10 build 19041 |
| `test-ui-smoke.ps1` | verifica permanência do processo; por padrão força seu término, sem comprovar fechamento limpo |
| `test-localization-package.ps1` | contrato de catálogos em diretório, ZIP e contêiner Store aninhado, incluindo rejeição de catálogos ausentes/vazios |

## Clonando o fork

```powershell
git clone https://github.com/WandersondeSouza/mpv.net.git
cd mpv.net
```

## Abrindo no Visual Studio

1. Abra o Visual Studio.
2. Selecione **Open a project or solution**.
3. Abra `src/MpvNet.sln`.
4. Restaure os pacotes NuGet.
5. Compile em Debug.
6. O build do projeto Windows prepara os binários e os catálogos com
   `src\Tools\prepare-build-output.ps1`. Use `/p:EnsureBuildAssets=false`
   para compilar sem essa preparação quando já tiver os recursos necessários.

<a name="validacao-do-pacote-microsoft-store-no-visual-studio"></a>

### Validação do pacote Microsoft Store no Visual Studio

O projeto `src\MpvNet.Pacote\MpvNet.Pacote.wapproj` já valida automaticamente:

- assinatura de distribuição;
- alinhamento entre `src\BuildVersion.props` e `src\MpvNet.Pacote\Package.appxmanifest`, considerando que a Microsoft Store exige revisão zero no manifesto MSIX.

Para usar o Visual Studio nesse fluxo:

1. Instale o workload/componente de Desktop Bridge/MSIX no Visual Studio.
2. Abra `src\MpvNet.sln`.
3. Carregue o projeto `MpvNet.Pacote`.
4. Se for publicar de verdade, crie `src\MpvNet.Pacote\Packaging.Distribution.props` com `PackagePublisher` e `PackageCertificateKeyFile`.
5. Compile `Release|x64` no projeto de pacote.

Com Desktop Bridge/MSIX instalado, o projeto valida a configuração antes de
empacotar. Na ausência dos targets, o projeto emite um aviso e pula o build do
pacote.

## Build via terminal

Na raiz do repositório:

```powershell
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln
```

Para compilar apenas a aplicação Windows:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

O projeto da aplicação define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`. O build padrão gera um executável x64.

Para compilar e baixar/validar automaticamente os binários nativos e auxiliares esperados ao lado de `mpvnet.exe`:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj -c Release
```

O build executa `src\Tools\prepare-build-output.ps1`, que chama
`src\Tools\prepare-native-dependencies.ps1` e garante `MediaInfo.dll`, `libmpv-2.dll`, `libmpv-2-v3.dll` e o helper legado `mpvnet.com` na pasta da configuração compilada. Os assets baixados do GitHub são comparados com o SHA-256 publicado pela API quando disponível. `mpvnet.com` usa o hash fixado para sua release imutável. MediaInfo continua vindo do site oficial por HTTPS e é validado como PE x64. O script reutiliza os arquivos válidos, mantém as duas DLLs libmpv com revisões upstream idênticas e aceita `-UpdateExisting` para renovar os downloads. Ele não baixa DLLs Microsoft/.NET/WPF de sites externos; essas DLLs continuam vindo do publish self-contained.

Os executáveis auxiliares de reprodução online são atualizados pelo aplicativo
após exibir a janela. Veja [Componentes auxiliares de runtime](configuration.md#componentes-auxiliares-de-runtime)
para caminhos desktop/MSIX, precedência, validação do bundle FFmpeg e fallback.
O bootstrap grava na raiz privada do pacote sem solicitar
`broadFileSystemAccess`. Antes de submeter um pacote, confira no Partner
Center as regras vigentes para executáveis auxiliares baixados. A validação
local do MSIX não certifica o pacote.

Para gerar uma publicação local self-contained, com a preparação padrão de assets do projeto Windows, execute:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained true --configuration Release --runtime win-x64 /p:IncludeNativeLibrariesForSelfExtract=false
```

### Seleção dual de libmpv

As duas DLLs x64 vêm da mesma revisão do mpv. O carregador tenta a v3 em
CPU compatível e usa a normal como fallback obrigatório. Confira as instruções
de diagnóstico, as extensões de CPU e os valores de
`MPVNET_FORCE_LIBMPV_VARIANT` em [Carregamento dual de libmpv](mpv-integration.md#carregamento-dual-de-libmpv).

## Empacotamento Microsoft Store

O [projeto WAP](../../src/MpvNet.Pacote/MpvNet.Pacote.wapproj) empacota o
executável do projeto Windows. Ele lê a versão de `src/BuildVersion.props`
e inclui os recursos de `Images\`.

| Campo do pacote | Valor |
| --- | --- |
| `Identity Name` | `24183GestodeSistemas.MPV.NETMediaPlayer` |
| `Publisher` | `CN=6581967D-2DE4-48DE-A846-C6F69ECA7701` |
| `PublisherDisplayName` | Gestão de Sistemas |
| `Package Family Name` | `24183GestodeSistemas.MPV.NETMediaPlayer_ex0zyz39hzsk6` |
| ID da Store | [9N441SP6XHLD](https://apps.microsoft.com/detail/9N441SP6XHLD) |

O manifesto usa `RuntimeBehavior="packagedClassicApp"`,
`TrustLevel="mediumIL"` e `runFullTrust` para empacotar a aplicação desktop
WinForms/WPF. Preserve esse modelo para manter o acesso à rede, ao AppData
e aos componentes auxiliares na raiz privada. Consulte o
[versionamento do MSIX](#versão) antes de gerar o pacote.

Pontos importantes:

- mantenha o `Identity Name` e o `Publisher` de `Package.appxmanifest` alinhados
  aos valores reservados no Partner Center e ao certificado de distribuição; os
  valores atuais do projeto estão registrados acima;
- o payload x64 inclui as duas DLLs libmpv; a seleção ocorre apenas entre arquivos já instalados, sem exigir CPU v3 para iniciar;
- como o idioma pode ser alterado dentro do player, todos os catálogos `.mo` ficam no pacote principal; o WAP separa recursos por escala e DirectX, mas não por idioma. Mantenha `AppxBundleAutoResourcePackageQualifiers` e `AppxDefaultResourceQualifiers` alinhados aos idiomas declarados em `Package.appxmanifest`.
- o manifesto declara formatos para `Abrir com` e `Aplicativos padrão`. A escolha do aplicativo padrão fica com o usuário. Veja as [associações de arquivos](../manual.md#extensões-e-associações-de-arquivos);
- o pacote MSIX não grava a pasta instalada diretamente no `PATH` como o Inno Setup; para terminal, ele declara o alias `mpvnet.exe`, resolvido pelo Windows via App Execution Alias.
- o arquivo real `src/MpvNet.Pacote/Packaging.Distribution.props` fica fora do Git por padrão e deve conter apenas dados locais/secretos do certificado.
- se o `.pfx` tiver senha, use `MPVNET_STORE_CERTIFICATE_PASSWORD` no CI ou `-PackageCertificatePassword` no script local.
- o script procura um `.pfx` em `src/MpvNet.Pacote` ou em `src/` e informa o certificado usado quando encontra um candidato.

Gere e valide o pacote localmente a partir da raiz do repositório.

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\publish-store-package.ps1 .\src .\artifacts\store
```

O script executa `ValidateStorePackage`, compila o WAP e inspeciona o
conteúdo do pacote gerado. O manifesto Store precisa ter revisão zero. O envio
e a certificação são feitos separadamente no Partner Center.
Para envio real, copie `src\MpvNet.Pacote\Packaging.Distribution.props.example` para `src\MpvNet.Pacote\Packaging.Distribution.props` e ajuste `PackagePublisher` e `PackageCertificateKeyFile` para o certificado usado na publicação.
No CI ou em máquina local, o script também aceita `MPVNET_STORE_CERTIFICATE_KEYFILE`, `MPVNET_STORE_CERTIFICATE_PASSWORD` e `MPVNET_STORE_PUBLISHER` como variáveis de ambiente.

<a name="validacao-wack"></a>

### Validação WACK

Ao validar o MSIX com o Windows App Certification Kit, confira:

- `MpvNet.Windows.csproj` declara explicitamente `app.manifest`, garantindo que o executável empacotado preserve `PerMonitorV2`;
- `BadgeLogo.png` e `BadgeLogo.scale-200.png` usam fundo transparente e glifo branco, conforme a regra de badge da Store;
- o teste opcional de executáveis bloqueados pode listar chamadas intencionais do app e referências do runtime .NET/WPF. Remova somente chamadas próprias desnecessárias; não tente limpar assemblies do runtime nem quebrar comandos compatíveis com mpv como `shell-execute`, abertura de URLs/manuais e pasta de configuração.

## Pacotes de diagnóstico

O fluxo de release usa `Release` e publica o projeto com
`/p:EnsureBuildAssets=false` para preparar e validar os binários em etapa
própria. `EnableFileLogging` vem desativado por padrão. Pacotes de suporte
usam a mesma versão pública com sufixo `-diagnostic`. No workflow, use
`enable_file_logging=true` e `create_release=false` para obter apenas o
artefato de diagnóstico. Veja os [comandos e cuidados com logs](../logging.md#como-habilitar).

## Execução em debug

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

## Release e empacotamento

Para os comandos operacionais resumidos, veja [Guia operacional](../guia-operacional.md).

### Comandos prontos para copiar e colar

O [guia operacional](../guia-operacional.md#gerar-pacotes-locais) contém os
comandos para gerar o ZIP e o instalador juntos ou em separado. Execute-os na
raiz do repositório. `-SourceDir` aponta para `src` e os exemplos usam
`artifacts\release` como pasta de saída. O script cria a pasta se necessário.

### Parâmetros dos scripts

`build-release-package.ps1` gera o ZIP e o instalador x64. Use
`-SkipInstaller` ou `-SkipPortableZip` para omitir um pacote. O script
tenta publicar no GitHub por padrão. Indique `-SkipGitHubRelease` para
restringir o fluxo à geração local.

Para usar cópias locais de `MediaInfo.dll` e `mpvnet.com`, indique os arquivos no comando.

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

O script segue este fluxo.

1. valida `MpvNet.sln`;
2. valida 7-Zip e, quando aplicável, Inno Setup;
3. publica `MpvNet.Windows.csproj` self-contained para `win-x64`;
4. cria nomes com base na versão do `mpvnet.exe`;
5. copia arquivos publicados;
6. chama `src\Tools\prepare-native-dependencies.ps1` para baixar ou validar `MediaInfo.dll`, `libmpv-2.dll` e `libmpv-2-v3.dll`, reutilizando downloads com até 2 dias em `artifacts\native-dependencies\downloads`;
7. valida e copia as DLLs Microsoft/.NET `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` e `PresentationNative_cor3.dll` vindas do publish self-contained;
8. copia `MediaInfo.dll`, `libmpv-2.dll` e `libmpv-2-v3.dll` x64;
9. copia `Locale`;
10. cria `portable_config` com modelos comentados de `mpv.conf` e `input.conf`;
11. valida as DLLs nativas no publish, na pasta portátil e no ZIP com `validate-native-dependencies.ps1`;
12. gera ZIP portátil x64;
13. executa `Setup/Inno/build-windows-installer.iss` para gerar o instalador x64, exceto com `-SkipInstaller`;
14. o instalador adiciona a pasta instalada ao `PATH` do Windows e remove essa
    entrada durante a desinstalação;
15. o instalador executa `mpvnet.exe --register-file-associations video`,
    `mpvnet.exe --register-file-associations audio` e
    `mpvnet.exe --register-file-associations playlist` para registrar as
    associações de vídeo, áudio e playlists após a instalação. Para registrar imagens, use o menu `Config > Setup > Register image file associations`;
16. cria release no GitHub usando `gh release create`, exceto com `-SkipGitHubRelease`; quando publicar, use `-ReleaseNotes` ou `-ReleaseNotesFile` para colocar a descrição diretamente no corpo da release.

O build recompila os catálogos da fonte atual. O contrato de conteúdo exige os
19 `.mo` correspondentes a `lang/po`, incluindo Taiwan, Hong Kong, hindi,
árabe, ucraniano e indonésio. Essa verificação vale para diretório de publish,
portátil, instalador e arquivos Store aninhados. O payload WAP e o manifesto
declaram os novos idiomas; inglês continua nativo e não tem arquivo `.mo`.
Para verificar a rejeição de catálogos ausentes ou vazios sem criar uma release:

```powershell
& .\src\Tools\test-localization-package.ps1
```

As dependências baixadas automaticamente usam estas fontes:

- libmpv: `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, asset normal `mpv-dev-x86_64-[data]-git-[hash].7z` ou asset v3 `mpv-dev-x86_64-v3-[data]-git-[hash].7z`;
- MediaInfo: `https://mediaarea.net/en/MediaInfo/Download/Windows`, asset `MediaInfo_DLL_[versao]_Windows_x64_WithoutInstaller.7z`;
- DLLs WPF/.NET e `vcruntime140_cor3.dll`: publish self-contained oficial do SDK .NET Desktop/WPF.
- Gettext.Tools: `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, usado para obter `msgfmt.exe` e gerar `Locale` quando `msgfmt.exe` não está no `PATH`.

O script resolve um asset normal e um asset v3 da mesma revisão upstream, mantém os nomes físicos distintos e preserva a ABI `libmpv-2` do P/Invoke. Se o GitHub mudar os nomes dos assets, se a MediaArea mudar o link de download, se o NuGet mudar a resolução de `Gettext.Tools`, se o download falhar, se a extração falhar, se algum arquivo baixado estiver vazio, se as DLLs forem idênticas ou se uma DLL obrigatória não for x64, o script deve abortar antes de gerar um pacote parcial. `MediaInfo.dll` pode ser pinada por `-MediaInfoVersion`/`MPVNET_MEDIAINFO_VERSION` ou sobrescrita por `-MediaInfoFile`.

Validação offline do contrato das duas variantes:

```powershell
.\src\Tools\test-mpv-build-variants.ps1
```

Para também baixar e inspecionar as duas DLLs reais, execute o mesmo script com
`-Online`. O resultado offline não comprova os binários de uma distribuição.

Smoke test offline do cache compartilhado:

```powershell
.\src\Tools\test-native-dependency-cache.ps1
```

Depois de gerar um pacote dual, execute a revisão manual com seleção automática e com `MPVNET_FORCE_LIBMPV_VARIANT=normal`: inicialização, reprodução de arquivo local, pause/play, seek, fullscreen, legenda, áudio e fechamento. Em CPU compatível, confirme no log a tentativa da v3 e o fallback para a normal quando a v3 não estiver disponível.

O workflow manual `.github/workflows/release-packages.yml` executa esse mesmo script no GitHub Actions. Ele sempre publica os pacotes como artefato do workflow e, quando executado com `create_release=true`, também cria a Release no repositório. O workflow roda `validate-native-dependencies.ps1` antes do upload dos artefatos. Pacotes de diagnóstico com `enable_file_logging=true` só podem ser gerados como artefato do workflow; o workflow bloqueia a combinacao `create_release=true` e `enable_file_logging=true`.

Este fork não publica um pacote NuGet/container no GitHub Packages por enquanto; os pacotes de distribuição do aplicativo são assets de GitHub Releases e artefatos do workflow.

Para criar e publicar uma release emergencial a partir do branch atual:

```powershell
.\src\Tools\publish-emergency-release.ps1
```

Para incluir o instalador no workflow emergencial:

```powershell
.\src\Tools\publish-emergency-release.ps1 -CreateInstaller
```

Esse script exige árvore Git limpa e atualiza os arquivos de versão antes do
commit. Ele não substitui a revisão das notas da release, da interface e da
compatibilidade. Como publica assets no GitHub, não aceita
`-EnableFileLogging`; gere pacotes de diagnóstico separadamente, sem publicação.

## Versão

A versão pública está centralizada em `src/BuildVersion.props`
(`MpvNetVersion`). Consulte esse arquivo para identificar a versão configurada
na branch.

O projeto `src/MpvNet.Windows/MpvNet.Windows.csproj` usa `MpvNetVersion` para
`FileVersion`, `AssemblyVersion` e `InformationalVersion`. O script de release
usa essa versão para nomear os artefatos e a tag.
O manifesto MSIX exige um valor literal em `Package.appxmanifest`. Atualize os
dois arquivos juntos com:

```powershell
$versaoPublica = Read-Host 'Informe a versão pública'
.\src\Tools\set-release-version.ps1 -Version $versaoPublica
```

O script grava a versão pública em `BuildVersion.props` e aplica revisão zero
ao `Identity Version` do manifesto MSIX, conforme a regra da Store. Use
`-StoreVersion <versao-com-revisao-zero>` para definir uma versão independente
da Store; o script registra esse valor em `MpvNetStoreVersion` e no manifesto. Por exemplo, use
`-Version 7.1.32.0 -StoreVersion 7.1.31.0` para preparar a versão pública
7.1.32.0 e manter a versão do MSIX em 7.1.31.0. O aumento automático de
revisão do WAP fica desativado. Gerar uma release GitHub não altera o pacote
publicado no Partner Center.

## Possíveis problemas comuns

### SDK ou runtime ausente

Confira os `TargetFramework` dos projetos e instale o SDK/runtime correspondente.

### Dependência nativa ausente

Se o aplicativo compilar mas não abrir, verifique as DLLs x64 e o diretório de
execução. Execute o [diagnóstico de libmpv](mpv-integration.md#carregamento-dual-de-libmpv)
para conferir a seleção. Se o problema ocorrer ao abrir mídia online, use o
[diagnóstico de componentes](configuration.md#componentes-auxiliares-de-runtime).

### Ferramenta de release ausente

O script falha se 7-Zip, Inno Setup ou `gh` não estiverem nos locais esperados.

### Caminho fixo do Inno Setup

`src/Setup/Inno/build-windows-installer.iss` usa `artifacts\release` relativo ao
repositório como saída padrão. O fluxo oficial continua passando `/O` com
`OutputRootDir`, que tem precedência. A compilação direta não depende de uma
unidade ou pasta pessoal do desenvolvedor.

## Checklist de validação manual

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

## Validações que exigem ambiente específico

- Revalidar `dotnet build src\MpvNet.sln` quando houver mudança relevante no
  build.
- Revalidar `dotnet publish` x64 quando houver mudança relevante no fluxo de
  publish.
- Validar revisão manual completa de UI, fullscreen, menu, atalhos, temas e persistência.
- Validar o workflow manual `.github/workflows/release-packages.yml`.
