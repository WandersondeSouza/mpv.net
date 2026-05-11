# Artefatos de IA do fork mpv.net

Este diretório reúne materiais para orientar agentes de IA na manutenção do fork.

## Estrutura

| Pasta | Conteúdo | Uso |
| --- | --- | --- |
| `skills/` | Skill base do mantenedor mpv.net | Contexto e regras reutilizáveis para manutenção conservadora. |
| `agents/` | Perfis por área crítica | Especializar o agente conforme o tipo de tarefa. |
| `prompts/` | Prompts operacionais | Iniciar auditorias, bugs, release, documentação e mudanças técnicas. |
| `mcp/` | Guia de MCPs úteis | Planejar quais servidores MCP ativar no ambiente do agente. |

## Fluxo recomendado

1. Ler `AGENTS.md`.
2. Escolher um perfil em `agents/`.
3. Usar a skill `skills/mpvnet-maintainer.md` como base.
4. Escolher um prompt em `prompts/` para a tarefa.
5. Ativar MCPs somente quando forem úteis para a investigação.

## Regra principal

Os arquivos deste diretório ajudam a orientar agentes, mas não substituem a leitura do código atual. Toda mudança deve ser confirmada no código e validada com o menor teste suficiente para o risco da área alterada.
