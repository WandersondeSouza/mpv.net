# Artefatos de IA

Este diretório reúne materiais para tarefas recorrentes do fork. O código e os
documentos em `docs/` continuam sendo a fonte final de verdade; resultados
históricos ou hipóteses de prompts não representam necessariamente o estado atual.

## Conteúdo

- `skills/`: regras e contexto reutilizáveis;
- `agents/`: perfis por tipo de tarefa;
- `prompts/`: prompts prontos para investigação, documentação, release e mudanças técnicas;
- `mcp/`: notas sobre MCPs úteis.

## Regra

Os arquivos aqui ajudam a manter consistência, mas não substituem a leitura do código atual nem a validação do comportamento real.
Atualize documentação técnica apenas quando houver mudança consolidada; prefira documentos existentes e evite criar arquivos redundantes.

Após a refatoração validada, a topologia principal do código usa partials por responsabilidade:

- `src/MpvNet.Windows/WinForms/MainForm.*.cs` para UI Windows por tema;
- `src/MpvNet/Integration/Mpv/Player.*.cs` para player/libmpv por tema.

Consulte `docs/developer/architecture.md`, `docs/developer/windows-ui.md` e `docs/developer/mpv-integration.md` para o mapa consolidado antes de alterar essas áreas.

## Fluxo recomendado

Antes de editar código ou documentação:

1. ler `AGENTS.md`;
2. ler `README.md`;
3. ler `docs/manual.md`;
4. ler `docs/CONFIGURACAO.md` ou a documentação da área tocada;
5. ler `docs/guia-operacional.md` quando a tarefa envolver build, release, scripts ou manutenção geral;
6. ler o `Roadmap` em `docs/guia-operacional.md` e as pendências da documentação técnica da área quando a tarefa envolver planejamento ou priorização;
7. ler `docs/developer/architecture.md` quando a tarefa envolver análise ampla ou refatoração;
8. ler `docs/developer/configuration.md`, `docs/developer/mpv-integration.md`, `docs/developer/windows-ui.md`, `docs/developer/build-release.md` e `docs/developer/localization.md` conforme a área tocada;
9. usar o arquivo de `skills/`, `agents/` e `prompts/` que melhor corresponda ao tipo de trabalho.
10. verificar se `src\MpvNet.Tests\Program.cs` cobre o comportamento antes de alterar parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo;
11. ampliar `src\MpvNet.Tests\Program.cs` quando a mudança criar caso novo nessas áreas;
12. rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore` para validar a cobertura leve;
13. revisar `git diff --check`, links locais e o diff final.

## Limites de execução

- Commit, push, publicação de release, Store ou deploy não são automáticos.
- Só executar essas ações quando forem solicitadas explicitamente e houver evidência de validação local.
- Ao relatar resultados, separar `validado localmente`, `pendente de ambiente` e `não verificado`.

## Observação sobre MCP

O MCP de filesystem já deve incluir este repositório na configuração local do Codex. Em geral não é necessário criar um MCP novo só para trabalhar neste fork.


