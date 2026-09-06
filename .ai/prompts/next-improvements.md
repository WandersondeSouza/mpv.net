# Prompt — Próximas melhorias do fork

Use este prompt para iniciar uma rodada curta de melhoria no fork
`WandersondeSouza/mpv.net`.

## Leitura obrigatória

Antes de alterar arquivos, leia `AGENTS.md`, `README.md`, `docs/manual.md`,
`docs/guia-operacional.md`, `.ai/skills/mpvnet-maintainer.md`, o agente de
arquitetura e a documentação técnica da área analisada.

## Objetivo

Analisar o estado atual e propor melhorias pequenas, seguras e documentadas,
priorizando compatibilidade com mpv/libmpv.

## Escopo permitido

- `README.md`, `docs/guia-operacional.md` e `docs/developer/`;
- `.ai/skills/`, `.ai/agents/` e `.ai/prompts/`.

Não altere código, integração libmpv ou scripts de build/release nesta rodada
sem escopo explícito. Preserve configurações existentes.

## Tarefas

1. verificar clareza e atualidade da documentação;
2. separar fatos confirmados, pendências e hipóteses;
3. identificar duplicações entre `docs/` e `.ai/`;
4. listar arquivos e motivo antes de editar;
5. fazer mudanças pequenas e revisáveis;
6. entregar resumo, arquivos alterados, problemas, próximos passos e validação.

## Validação mínima

- `git status --short` antes de editar;
- `git diff --check` ao terminar;
- conferência de links locais, caminhos e comandos;
- não marcar como concluído o que não tiver evidência local;
- não executar commit, push, release ou deploy sem solicitação explícita.
