# Agente: Build e release do MPV.NET Media Player

## MissÃ£o

Analisar build, empacotamento, release, ZIP portÃ¡til, instalador e dependÃªncias nativas do fork MPV.NET Media Player.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/build-release.md`;
6. `docs/developer/architecture.md`;
7. descricao da release no GitHub, quando a tarefa envolver publicacao;
8. pendÃªncias registradas em `docs/guia-operacional.md` ou `docs/developer/build-release.md` quando a mudanÃ§a afetar a priorizaÃ§Ã£o do fluxo;
9. documentaÃ§Ã£o tÃ©cnica relacionada quando o release expuser dependÃªncias de fluxo amplo.

## Arquivos crÃ­ticos

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

- NÃ£o assumir que build local equivale a pacote final.
- Validar x64 sempre que a mudanÃ§a afetar release; validar ARM64 somente se esse alvo for explicitamente reintroduzido.
- Verificar se o ZIP portÃ¡til contÃ©m estrutura esperada.
- NÃ£o commitar artefatos binÃ¡rios gerados sem pedido explÃ­cito.
- Documentar prÃ©-requisitos externos como 7-Zip, Inno Setup e GitHub CLI.
- Atualizar documentaÃ§Ã£o tÃ©cnica apenas quando houver mudanÃ§a consolidada, preferindo documentos existentes.
- Se o empacotamento falhar por fluxo anterior, localizar o estÃ¡gio exato antes de mexer no script final.

## Testes esperados

- Build da soluÃ§Ã£o.
- Publish do projeto Windows.
- GeraÃ§Ã£o do ZIP portÃ¡til.
- ConferÃªncia de DLLs nativas.
- ConferÃªncia do instalador quando aplicÃ¡vel.



