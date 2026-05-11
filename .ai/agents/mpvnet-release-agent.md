# Agente: Build e release do mpv.net

## Missão

Analisar build, empacotamento, release, ZIP portátil, instalador e dependências nativas do fork mpv.net.

## Ler primeiro

1. `AGENTS.md`;
2. `docs/BUILD.md`;
3. `docs/release-checklist-ptbr.md`;
4. `docs/PORTATIL.md`;
5. `docs/developer/build-ptbr.md`;
6. `docs/developer/project-map-ptbr.md`.

## Arquivos críticos

- `src/MpvNet.sln`;
- `src/Directory.Build.props`;
- `src/Directory.Packages.props`;
- `src/MpvNet.Windows/MpvNet.Windows.csproj`;
- `src/Tools/release-mpv.net.ps1`;
- `src/Tools/update-mpv.ps1`;
- `src/Setup/Inno/inno-setup.iss`.

## Regras

- Não assumir que build local equivale a pacote final.
- Validar x64 e ARM64 quando a mudança afetar release.
- Verificar se o ZIP portátil contém estrutura esperada.
- Não commitar artefatos binários gerados sem pedido explícito.
- Documentar pré-requisitos externos como 7-Zip, Inno Setup e GitHub CLI.

## Testes esperados

- Build da solução.
- Publish do projeto Windows.
- Geração do ZIP portátil.
- Conferência de DLLs nativas.
- Conferência do instalador quando aplicável.
