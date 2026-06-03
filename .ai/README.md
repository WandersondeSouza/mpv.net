# Artefatos de IA

Este diretório reúne materiais para tarefas recorrentes do fork.

## Conteúdo

- `skills/`: regras e contexto reutilizáveis;
- `agents/`: perfis por tipo de tarefa;
- `prompts/`: prompts prontos para investigação, documentação, release e mudanças técnicas;
- `mcp/`: notas sobre MCPs úteis.

## Regra

Os arquivos aqui ajudam a manter consistência, mas não substituem a leitura do código atual nem a validação do comportamento real.
Atualize documentação técnica apenas quando houver mudança consolidada; prefira documentos existentes e evite criar arquivos redundantes.

## Fluxo recomendado

Antes de editar código ou documentação:

1. ler `AGENTS.md`;
2. ler `README.md`;
3. ler `docs/manual.md`;
4. ler `docs/CONFIGURACAO.md` ou a documentação da área tocada;
5. ler `docs/guia-operacional.md` quando a tarefa envolver build, release, scripts ou manutenção geral;
6. ler `docs/proximos-trabalhos.md` quando a tarefa envolver planejamento ou priorização;
7. ler `docs/developer/architecture.md` quando a tarefa envolver análise ampla ou refatoração;
8. ler `docs/developer/configuration.md`, `docs/developer/mpv-integration.md`, `docs/developer/windows-ui.md`, `docs/developer/build-release.md` e `docs/developer/localization.md` conforme a área tocada;
9. usar o arquivo de `skills/`, `agents/` e `prompts/` que melhor corresponda ao tipo de trabalho.

## Observação sobre MCP

O MCP de filesystem já deve incluir este repositório na configuração local do Codex. Em geral não é necessário criar um MCP novo só para trabalhar neste fork.


