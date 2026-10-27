# Guia operacional do MPV.NET Media Player

Este guia reúne os comandos mais usados para compilar, preparar dependências e gerar pacotes.
Os detalhes de ambiente, arquitetura e empacotamento da Store estão em
[Build e release](developer/build-release.md).

## Referências

- [Manual do usuário](manual.md)
- [Logs](logging.md)
- [Build e release](developer/build-release.md)
- [Arquitetura](developer/architecture.md)
- [Configuração](developer/configuration.md)
- [Integração com mpv/libmpv](developer/mpv-integration.md)

## Build

Execute os comandos a partir da raiz do repositório. Consulte
[Build e release](developer/build-release.md) para os requisitos do ambiente.

```powershell
dotnet restore .\src\MpvNet.sln
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj
```

Para conferir os projetos e manifestos do Windows:

```powershell
.\src\Tools\validate-windows-baseline.ps1
```

## Dependências nativas

As distribuições x64 incluem `libmpv-2.dll` e `libmpv-2-v3.dll`. O player seleciona a
variante compatível e mantém a versão normal como fallback obrigatório. Não gere
pacotes que contenham apenas a variante v3.

O build prepara as dependências necessárias automaticamente. Para prepará-las manualmente:

```powershell
.\src\Tools\prepare-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Release\win-x64
```

Valide uma pasta publicada antes de distribuir o pacote:

```powershell
.\src\Tools\validate-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Release\win-x64\publish
.\src\Tools\validate-package-contents.ps1 -Path .\src\MpvNet.Windows\bin\Release\win-x64\publish
```

Os validadores também aceitam arquivos de pacote. Consulte os parâmetros de
`validate-native-dependencies.ps1` e `validate-package-contents.ps1` para validar
um ZIP ou pacote da Store.

## Gerar pacotes locais

Para gerar o ZIP portátil e o instalador sem publicar no GitHub:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
```

Para gerar somente o ZIP portátil:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Para gerar somente o instalador:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release
```

Esses scripts validam o conteúdo gerado. Para criar um pacote de diagnóstico com
logs detalhados, adicione `-EnableFileLogging`. Pacotes de diagnóstico são para
suporte e não devem ser publicados como assets de uma release.

## Publicar uma release

Defina a versão pública pelo script oficial; não altere os arquivos de versão
separadamente:

```powershell
$VersaoPublica = Read-Host 'Informe a versão pública'
.\src\Tools\set-release-version.ps1 -Version $VersaoPublica
```

Gere e valide os pacotes localmente antes de iniciar a publicação. Use o workflow
`release-packages.yml` e revise a branch, as notas e os assets configurados. Para
regras de versionamento e publicação da Microsoft Store, siga
[Build e release](developer/build-release.md).

O script `publish-emergency-release.ps1` faz mais que gerar pacotes locais: ele
atualiza a versão, cria um commit, envia a branch ao GitHub e inicia o workflow
de release. Execute-o somente quando a publicação estiver planejada e a árvore
Git estiver limpa.

## Modo portátil

O diretório `portable_config` guarda as configurações usadas pelo pacote
portátil. Você pode incluir nele `mpv.conf`, `input.conf`, scripts e opções de
scripts, seguindo o layout do mpv.

## Diagnóstico de caminhos

```powershell
mpvnet.exe --diagnose-paths
mpvnet.exe --diagnose-paths --config-dir="C:\Meu perfil MPV"
mpvnet.exe --diagnose-components
```

O primeiro diagnóstico não cria diretórios nem inicia downloads. Confira a
origem da configuração e as raízes de cache, componentes, temporários, logs,
DLLs e recursos. A matriz de portátil/instalado/Store está em
[Configuração técnica](developer/configuration.md). Em Store, arquivos graváveis
pertencem à raiz privada de dados, não ao diretório instalado em WindowsApps.

## Contribuir

- Preserve a compatibilidade com mpv/libmpv e com os arquivos de configuração existentes.
- Prefira mudanças pequenas e atualize a documentação quando o comportamento mudar.
- Consulte a documentação técnica da área antes de alterar build, configuração,
  interface ou integração com libmpv.

## Validação

### Testes automatizados

O projeto `src/MpvNet.Tests` usa xUnit v3. Execute a suíte a partir da raiz:

```powershell
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
```

Se os assets NuGet ainda não existirem, restaure o projeto antes de usar
`--no-restore`. Os testes referenciam o projeto Windows, cujo build normal
prepara dependências nativas e catálogos. `/p:EnsureBuildAssets=false` apenas
pula essa preparação; exige assets válidos já disponíveis para testes que os usam.

Depois do build, o projeto de testes copia da saída efetiva do projeto Windows
as DLLs `libmpv-2.dll`, `libmpv-2-v3.dll`, `libmpvnet.dll` e `MediaInfo.dll`, os
metadados de libmpv, os catálogos `Locale` e `Scripts/osc.lua`. A origem é resolvida
pelo MSBuild para a configuração e plataforma atuais. Isso permite iniciar a
cópia de `mpvnet.exe` da pasta de testes sem depender de uma execução anterior
ou de cópias manuais das DLLs; a ausência da libmpv normal interrompe o build.

`Program.cs` conserva casos de comportamento existente; a cobertura adicional
fica nos arquivos por área, como `ApplicationPathTests.cs`, `MediaInputTests.cs`,
`NativePlaylistTitleTests.cs`, `LocalizationExpansionTests.cs`,
`GitHubReleaseClientTests.cs` e `TitleBarFullscreenButtonTests.cs`. Consulte a
suíte completa antes de escolher onde acrescentar uma regressão. O runner
configurado em `AssemblyInfo.cs` executa sem paralelismo; evite também builds
concorrentes sobre a mesma saída.

`StartupLocalizationTests.cs` inicia o executável com configuração isolada e
consulta `osd-msg1` pela IPC nativa do mpv. A cobertura inclui os 20 idiomas,
idioma salvo, aliases, fallback, último argumento `--language` e preservação da
mensagem personalizada em `mpv.conf` e na linha de comando.

`TestEnvironment.cs` define `MPVNET_LOCAL_APPDATA` para `test-runtime` na pasta
do executável de testes se não houver override explícito. Isso isola os dados
do player. O comando da solução `dotnet test src\MpvNet.sln` não substitui esse
fluxo, pois a solução também contém o projeto WAP.

### Limites das evidências

Build e validação de pacote não comprovam o comportamento em todas as máquinas
nem a aprovação da Microsoft Store. Registre apenas as verificações realmente
executadas e identifique o pacote e o ambiente usados.

Para mudanças de interface ou integração com libmpv, confira reprodução de
arquivo local e stream, playlist, pasta com mídia, arrastar e soltar, menu de
contexto, tela cheia, áudio, legendas, atalhos e encerramento. Valide assinatura,
instalação e certificação do pacote da Store quando esse fluxo for alterado.

Para mudanças de localização, execute também `python lang/test-language-expansion.py`
e `src/Tools/test-localization-package.ps1`.
O inventário, as regras RTL/regionais e os resultados da expansão estão em
[Localização](developer/localization.md). O pacote deve conter todos os 19
catálogos; inglês usa o texto-fonte. Uma compilação ou um MSIX local sem
assinatura não comprova instalação, atualização nem certificação da Store.

## Manutenção deste guia

Atualize este documento quando os comandos ou scripts operacionais mudarem.
Mantenha aqui o fluxo resumido e deixe detalhes técnicos em `docs/developer/`.
