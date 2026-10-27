# Skills

## Disponível

- `mpvnet-maintainer.md`: skill base para manutenção conservadora do fork MPV.NET Media Player.

## Uso recomendado

Use esta skill quando a tarefa envolver:

- auditoria de documentação;
- análise de bug;
- alteração de configuração;
- integração com mpv/libmpv;
- componentes auxiliares, cache de runtime ou cache de rede;
- UI Windows;
- build ou release;
- preparação do repositório para agentes de IA;
- análise arquitetural ampla antes de mudanças grandes.

Para tarefas muito específicas, combine esta skill com um perfil em `.ai/agents/`
e um prompt em `.ai/prompts/`. Para componentes auxiliares, use também
`.ai/agents/mpvnet-runtime-components-agent.md`. Eles devem complementar, e não
contradizer, `AGENTS.md`.

Topologia atual consolidada:

- UI Windows: `src/MpvNet.Windows/WinForms/MainForm.cs` mais partials `MainForm.*.cs`;
- Player/libmpv: `src/MpvNet/Integration/Mpv/Player.cs` mais partials `Integration/Mpv/Player.*.cs`.

Para caminhos de configuração e runtime, confira `src/MpvNet/Infrastructure/Folder.cs`
e `docs/developer/configuration.md`; para cache de mídia de rede, confira
`src/MpvNet/Integration/Mpv/Player.NetworkCache.cs` e
`docs/developer/mpv-integration.md`.

Para mudanças em parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo, verifique os testes da área em `src/MpvNet.Tests/`, incluindo `Program.cs` quando aplicável. Amplie a cobertura quando houver comportamento novo e valide com `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`. Para documentação e `.ai`, use `git diff --check` e verificação de links/referências.

## Leitura que esta skill assume

- `AGENTS.md`;
- `README.md`;
- `docs/manual.md`;
- `docs/guia-operacional.md`;
- a documentação técnica da área tocada em `docs/developer/`.

