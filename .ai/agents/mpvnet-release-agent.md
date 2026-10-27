# Agente: Build e release do MPV.NET Media Player

## Missão

Analisar build, empacotamento, release, ZIP portátil, instalador e dependências nativas do fork MPV.NET Media Player.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/build-release.md`;
6. `docs/developer/architecture.md`;
7. descricao da release no GitHub, quando a tarefa envolver publicacao;
8. pendências registradas em `docs/guia-operacional.md` ou `docs/developer/build-release.md` quando a mudança afetar a priorização do fluxo;
9. documentação técnica relacionada quando o release expuser dependências de fluxo amplo.

## Arquivos críticos

- `src/MpvNet.sln`;
- `src/Directory.Build.props`;
- `src/Directory.Packages.props`;
- `src/MpvNet.Windows/MpvNet.Windows.csproj`;
- `src/MpvNet.Pacote/MpvNet.Pacote.wapproj`;
- `src/MpvNet.Pacote/Package.appxmanifest`;
- `src/Tools/build-release-package.ps1`;
- `src/Tools/prepare-native-dependencies.ps1`;
- `src/Tools/validate-native-dependencies.ps1`;
- `src/Tools/validate-package-contents.ps1`;
- `src/Tools/publish-store-package.ps1`;
- `src/Tools/set-release-version.ps1`;
- `.github/workflows/release-packages.yml`;
- `src/Tools/update-mpv-runtime.ps1`;
- `src/Setup/Inno/build-windows-installer.iss`;

## Regras

- Não assumir que build local equivale a pacote final.
- Validar x64 sempre que a mudança afetar release; validar ARM64 somente se esse alvo for explicitamente reintroduzido.
- Verificar se o ZIP portátil contém estrutura esperada.
- Conferir as duas DLLs libmpv da mesma revisão, a normal obrigatória e todos
  os catálogos exigidos por `validate-package-contents.ps1`. O teste de variantes
  é offline por padrão; `-Online` é necessário para baixar e inspecionar DLLs reais.
- Tratar a release GitHub (ZIP/instalador) e o pacote WAP/MSIX da Store como fluxos separados; um não comprova o outro.
- Validar `Scripts/osc.lua` nos pacotes aplicáveis e `mpvnet.com` no payload Store; esse helper é empacotado por compatibilidade, não baixado nem iniciado pelo player.
- Manter `BuildVersion.props` e `Package.appxmanifest` alinhados pelo script oficial; a versão MSIX usa revisão zero.
- Não commitar artefatos binários gerados sem pedido explícito.
- Documentar pré-requisitos externos como 7-Zip, Inno Setup e GitHub CLI.
- Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes.
- Se o empacotamento falhar por fluxo anterior, localizar o estágio exato antes de mexer no script final.
- Tratar build, pacote, assinatura, upload e publicação como evidências diferentes; sucesso local não prova que a Store ou o GitHub foram atualizados.
- Não executar `git commit`, `git push` ou publicação automaticamente; registrar recomendações e aguardar autorização explícita.

## Testes esperados

- Build/publish x64 do projeto Windows e validação do ZIP e das dependências nativas.
- Conferência do instalador quando aplicável.
- Para Store, validar o WAP/MSIX e inspecionar o payload final; assinatura, submissão e certificação são evidências separadas.
- Executar testes automatizados relevantes com `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`; não usar `dotnet test src\MpvNet.sln` como substituto neste repositório.




