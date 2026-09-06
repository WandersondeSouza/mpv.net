# Skills

## Disponível

- `mpvnet-maintainer.md`: skill base para manutenção conservadora do fork MPV.NET Media Player.

## Uso recomendado

Use esta skill quando a tarefa envolver:

- auditoria de documentação;
- análise de bug;
- alteração de configuração;
- integração com mpv/libmpv;
- UI Windows;
- build ou release;
- preparação do repositório para agentes de IA;
- análise arquitetural ampla antes de mudanças grandes.

Para tarefas muito específicas, combine esta skill com um perfil em `.ai/agents/` e um prompt em `.ai/prompts/`. Eles devem complementar, e não contradizer, `AGENTS.md`.

Topologia atual consolidada:

- UI Windows: `src/MpvNet.Windows/WinForms/MainForm.cs` mais partials `MainForm.*.cs`;
- Player/libmpv: `src/MpvNet/Integration/Mpv/Player.cs` mais partials `Integration/Mpv/Player.*.cs`.

Para mudanças em parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo, verifique primeiro a cobertura em `src/MpvNet.Tests/Program.cs`, amplie o harness quando houver comportamento novo e valide com `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`. Para documentação e `.ai`, use `git diff --check` e verificação de links/referências.

## Leitura que esta skill assume

- `AGENTS.md`;
- `README.md`;
- `docs/manual.md`;
- `docs/guia-operacional.md`;
- a documentação técnica da área tocada em `docs/developer/`.

