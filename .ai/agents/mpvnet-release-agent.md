# Agente: Build e release do MPV.NET Media Player Community Edition

## Missão

Analisar build, empacotamento, release, ZIP portátil, instalador e dependências nativas do fork MPV.NET Media Player Community Edition.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/build-release.md`;
6. `docs/developer/architecture.md`;
7. `docs/changelog.md`;
8. `docs/proximos-trabalhos.md` quando a mudança afetar a priorização do fluxo.
9. `docs/developer/architecture.md` quando o release expuser dependências de fluxo amplo.

## Arquivos críticos

- `src/MpvNet.sln`;
- `src/Directory.Build.props`;
- `src/Directory.Packages.props`;
- `src/MpvNet.Windows/MpvNet.Windows.csproj`;
- `src/Tools/build-release-package.ps1`;
- `src/Tools/prepare-native-dependencies.ps1`;
- `src/Tools/validate-native-dependencies.ps1`;
- `src/Tools/update-mpv-runtime.ps1`;
- `src/Setup/Inno/build-windows-installer.iss`;

## Regras

- Não assumir que build local equivale a pacote final.
- Validar x64 sempre que a mudança afetar release; validar ARM64 somente se esse alvo for explicitamente reintroduzido.
- Verificar se o ZIP portátil contém estrutura esperada.
- Não commitar artefatos binários gerados sem pedido explícito.
- Documentar pré-requisitos externos como 7-Zip, Inno Setup e GitHub CLI.
- Se o empacotamento falhar por fluxo anterior, localizar o estágio exato antes de mexer no script final.

## Testes esperados

- Build da solução.
- Publish do projeto Windows.
- Geração do ZIP portátil.
- Conferência de DLLs nativas.
- Conferência do instalador quando aplicável.


