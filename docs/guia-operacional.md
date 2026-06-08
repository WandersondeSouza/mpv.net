# Guia operacional do MPV.NET Media Player

Documento único para build, dependências nativas, scripts, modo portátil, contribuição, release e direção do fork.

## Como usar este guia

- Para build local, siga a seção `Build`.
- Para preparar ou validar binários nativos, siga `Dependências nativas`.
- Para executar scripts, use os comandos com caminho completo mostrados em `Scripts`.
- Para empacotar e publicar, siga `Release`.
- Para entender o modo portátil, leia `Portátil`.
- Para contribuir com mudanças pequenas e compatíveis, leia `Contribuição`.
- Para saber o que ainda está pendente, leia `Roadmap`.
- Para entender a estrutura maior do fork, leia `docs/developer/architecture.md`.
- Para mudanças de configuração, leia `docs/developer/configuration.md`.
- Para mudanças de build e release, leia `docs/developer/build-release.md`.

## Build

Gerar apenas o ZIP portatil:

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Gerar apenas o instalador executavel:

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

O comando cria `C:\Users\<usuario>\source\repos\mpv.net` se o repositório ainda
não existir. Se você já usa outro diretório, abra o terminal na raiz do
repositório e execute apenas a última linha do bloco. A pasta `artifacts\release`
é criada automaticamente quando ainda não existe.

Se você quiser apenas compilar a solução principal, use:

```powershell
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj
```

O alvo Windows usa `win-x64` por padrão.

## Dependências nativas

Arquivos nativos esperados ao lado de `mpvnet.exe`:

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

O script de dependencias reutiliza downloads em `artifacts\native-dependencies\downloads`. Se o arquivo esperado nao existir ou tiver mais de 2 dias, ele baixa novamente a versao mais recente encontrada nas fontes oficiais configuradas no script.

Por padrao, o fork usa a build 64bit-v3 do mpv/libmpv. Essa variante mantem o nome `libmpv-2.dll`, mas exige CPU compativel com x86_64-v3, como Intel Haswell/AMD Excavator ou mais recente. Use `-MpvBuildVariant normal` apenas quando precisar gerar pacote para CPUs x64 mais antigas.

Validação:

```powershell
.\src\Tools\validate-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Release\win-x64\publish
.\src\Tools\validate-native-dependencies.ps1 -ZipFile .\artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64.zip
```

## Scripts

Use sempre os comandos a partir da raiz do repositório. O repositório atual expõe
os scripts em `src\Tools\`, então este guia referencia esses caminhos
diretamente.

### Gerar pacote de release

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Usar quando:

- gerar ZIP portátil;
- gerar instalador;
- preparar a release localmente.

Por padrao, pacotes de release sao gerados com logs detalhados em arquivo desabilitados.
Erros continuam sendo gravados em arquivo. Para gerar um pacote de diagnostico
com logs diarios completos em `%LOCALAPPDATA%\mpv.net\Logs`, adicione
`-EnableFileLogging`.

### Gerar apenas o ZIP portátil

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Gerar ZIP portatil com mpv/libmpv 64bit-v3:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -MpvBuildVariant x86_64-v3
```

Usar quando:

- quiser somente `MPV.NET-Media-Player-v<versao>-portable-x64.zip`;
- nao quiser gerar instalador;
- nao quiser publicar no GitHub.

Para ZIP de diagnostico, adicione `-EnableFileLogging`.

O script também cria a pasta extraída
`artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\`.

### Gerar apenas o instalador executável

```powershell
$RepoDir = Join-Path $env:USERPROFILE 'source\repos\mpv.net'
if (-not (Test-Path $RepoDir)) { git clone https://github.com/WandersondeSouza/mpv.net.git $RepoDir }
Set-Location $RepoDir
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Usar quando:

- quiser somente `MPV.NET-Media-Player-v<versao>-setup-x64.exe`;
- nao quiser gerar ZIP portatil;
- nao quiser publicar no GitHub.

Para instalador de diagnostico, adicione `-EnableFileLogging`.

O script também prepara a pasta portátil base
`artifacts\release\MPV.NET-Media-Player-v<versao>-portable-x64\`, usada pelo instalador.

### Preparar dependências nativas

```powershell
.\src\Tools\prepare-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Release\win-x64
```

Preparar dependencias com mpv/libmpv 64bit-v3:

```powershell
.\src\Tools\prepare-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Release\win-x64 -MpvBuildVariant x86_64-v3 -UpdateExisting
```

Usar quando:

- precisar baixar ou validar `MediaInfo.dll`, `libmpv-2.dll`, FFmpeg e `yt-dlp.exe`;
- preparar a pasta de execução antes do empacotamento.
- reutilizar downloads recentes em `artifacts\native-dependencies\downloads`;
- baixar novamente arquivos ausentes ou baixados ha mais de 2 dias.
- alternar explicitamente entre `normal` e `x86_64-v3` sem mudar o `DllImport`.

Smoke test de preparacao das duas variantes:

```powershell
.\src\Tools\test-mpv-build-variants.ps1
```

Para validar execucao real dos pacotes normal e v3, abrir cada pacote na maquina de destino e testar: inicializacao, reproducao, pause/play, seek, fullscreen, legendas, audio e fechamento.

### Validar dependências nativas

```powershell
.\src\Tools\validate-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Release\win-x64\publish
```

Usar quando:

- confirmar que a pasta publicada está completa;
- validar o ZIP portátil antes de publicar.

### Atualizar runtime do mpv

```powershell
.\src\Tools\update-mpv-runtime.ps1
```

Usar quando:

- precisar atualizar o runtime do mpv do fork;
- revisar dependências do player sem alterar o resto do pacote.

## Release

Fluxo resumido:

1. compilar o projeto;
2. preparar dependências nativas;
3. gerar pacote portátil;
4. gerar instalador;
5. validar o conteúdo final;
6. publicar a release quando solicitado.

### Release de emergência pelo GitHub

Use este script apenas quando a arvore Git estiver limpa e voce quiser gerar uma nova versao a partir do branch atual:

```powershell
.\src\Tools\publish-emergency-release.ps1
```

O script:

1. valida `git diff --check`;
2. exige arvore Git limpa;
3. incrementa o ultimo numero de `src\BuildVersion.props`;
4. compila `src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore`;
5. commita a alteracao de versao;
6. faz `git push origin <branch-atual>`;
7. dispara `.github/workflows/release-packages.yml` com `create_release=true`.

Para tambem gerar o instalador no workflow:

```powershell
.\src\Tools\publish-emergency-release.ps1 -CreateInstaller
```

## Portátil

Estrutura esperada do pacote portátil:

```text
mpvnet.exe
libmpv-2.dll
MediaInfo.dll
D3DCompiler_47_cor3.dll
vcruntime140_cor3.dll
wpfgfx_cor3.dll
PenImc_cor3.dll
PresentationNative_cor3.dll
ffmpeg.exe
ffplay.exe
ffprobe.exe
yt-dlp.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

Regras práticas:

- `portable_config` faz o player usar a pasta local;
- `mpv.conf` e `input.conf` podem partir dos modelos do fork;
- `scripts/` e `script-opts/` seguem o layout normal do mpv;
- o pacote portátil do fork já inclui os binários nativos esperados.

## Contribuição

Antes de mexer:

- leia `README.md`;
- leia `docs/manual.md`;
- leia este guia;
- leia a documentação da área tocada.

Regras:

- preserve compatibilidade com mpv;
- faça mudanças pequenas;
- atualize a documentação quando o comportamento mudar;
- valide impacto em configuração, atalhos e UI;
- evite refatorações amplas sem necessidade clara.

Fluxo recomendado:

1. entender o comportamento atual;
2. localizar os arquivos envolvidos;
3. propor uma mudança pequena e verificável;
4. testar o resultado;
5. registrar o que mudou.

## Roadmap

Pontos ainda abertos ou reservados:

- validar instalador, ZIP portátil e release do GitHub;
- validar UI, fullscreen, menu, atalhos e persistência de configuração;
- validar caminhos longos, `input.conf` e `thumbfast`;
- evitar mudanças grandes sem bug ou necessidade clara;
- manter a documentação alinhada ao que foi consolidado aqui.

Quando um item estiver corrigido e validado, ele sai desta lista e entra no `docs/changelog.md`.

## Tradução e manutenção

- Este guia substitui a documentação espalhada sobre scripts e fluxo operacional.
- Sempre que um script mudar, ajuste esta página primeiro.
- Se um comando aqui deixar de funcionar, atualize o caminho completo e a descrição do uso.
- Para mudanças amplas de arquitetura ou módulos, comece pela documentação técnica em `docs/developer/`.


