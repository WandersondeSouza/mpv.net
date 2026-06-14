# Prompt — Próximas melhorias do fork

Use este prompt para iniciar uma rodada curta de melhoria no fork `WandersondeSouza/mpv.net`.

## Leitura obrigatória

Antes de alterar qualquer arquivo, leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `.ai/skills/mpvnet-maintainer.md`;
6. `.ai/agents/mpvnet-architecture-agent.md`;
7. `docs/developer/architecture.md`;
8. documentação técnica relacionada à área analisada.

## Objetivo

Analisar o estado atual do fork e propor melhorias pequenas, seguras e bem documentadas para manutenção contínua do MPV.NET Media Player, priorizando compatibilidade com mpv/libmpv.

## Escopo permitido

- `README.md`;
- `docs/guia-operacional.md`;
- `docs/developer/`;
- `.ai/prompts/`;
- `.ai/agents/`;
- `.ai/skills/`.

## Fora de escopo nesta rodada

- Não alterar código-fonte do player.
- Não alterar integração com libmpv.
- Não alterar scripts de build ou release.
- Não fazer refatoração ampla.
- Não remover compatibilidade com configurações existentes.

## Tarefas

1. Verificar se o README está claro para usuário e mantenedor.
2. Identificar tarefas pendentes em `docs/guia-operacional.md` e nos documentos técnicos da área, e tarefas já consolidadas nas descrições das releases do GitHub.
3. Separar melhorias em quatro grupos:
   - documentação;
   - prompts, agentes e fluxo de IA;
   - arquitetura e análise de código;
   - futuras correções técnicas que exigem análise de código.
4. Listar arquivos que pretende alterar e explicar o motivo antes de editar.
5. Fazer mudanças pequenas e revisáveis.
6. Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes e evitando criar arquivos redundantes.
7. Ao final, entregar:
   - resumo do que foi analisado;
   - arquivos alterados;
   - problemas encontrados;
   - próximos passos recomendados;
   - validação executada.

## Formato obrigatório antes de editar

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Problema encontrado:

Mudança proposta:

Riscos:

Plano de teste:
```

## Validação mínima

- Rodar `git status --short` antes de editar.
- Revisar links Markdown locais após alterações.
- Conferir se `README.md`, `docs/guia-operacional.md`, `docs/developer/` e `.ai/prompts/` não duplicam informação com nomes diferentes.
- Não marcar como concluído nada que não tenha evidência no repositório atual ou validação local.


