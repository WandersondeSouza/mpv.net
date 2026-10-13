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

## Contribuir

- Preserve a compatibilidade com mpv/libmpv e com os arquivos de configuração existentes.
- Prefira mudanças pequenas e atualize a documentação quando o comportamento mudar.
- Consulte a documentação técnica da área antes de alterar build, configuração,
  interface ou integração com libmpv.

## Validação

Build e validação de pacote não comprovam o comportamento em todas as máquinas
nem a aprovação da Microsoft Store. Registre apenas as verificações realmente
executadas e identifique o pacote e o ambiente usados.

Para mudanças de interface ou integração com libmpv, confira reprodução de
arquivo local e stream, playlist, pasta com mídia, arrastar e soltar, menu de
contexto, tela cheia, áudio, legendas, atalhos e encerramento. Valide assinatura,
instalação e certificação do pacote da Store quando esse fluxo for alterado.

## Manutenção deste guia

Atualize este documento quando os comandos ou scripts operacionais mudarem.
Mantenha aqui o fluxo resumido e deixe detalhes técnicos em `docs/developer/`.
