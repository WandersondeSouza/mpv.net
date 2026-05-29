# Prompt — Próximas melhorias do fork

Use este prompt para iniciar uma rodada curta de melhoria no fork `WandersondeSouza/mpv.net`.

## Leitura obrigatória

Antes de alterar qualquer arquivo, leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/proximos-trabalhos.md`;
6. `.ai/skills/mpvnet-maintainer.md`;
7. `.ai/agents/mpvnet-architecture-agent.md`;
8. `docs/developer/project-map-ptbr.md`;
9. `docs/developer/architecture-ptbr.md`;
10. `docs/developer/source-audit-ptbr.md`;
11. documentação técnica relacionada à área analisada.

## Objetivo

Analisar o estado atual do fork e propor melhorias pequenas, seguras e bem documentadas para manutenção contínua do mpv.net, priorizando compatibilidade com mpv/libmpv.

## Escopo permitido

- `README.md`;
- `docs/guia-operacional.md`;
- `docs/proximos-trabalhos.md`;
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
2. Identificar tarefas pendentes e tarefas já consolidadas em `docs/proximos-trabalhos.md` e `docs/changelog.md`.
3. Separar melhorias em quatro grupos:
   - documentação;
   - prompts, agentes e fluxo de IA;
   - arquitetura e análise de código;
   - futuras correções técnicas que exigem análise de código.
4. Listar arquivos que pretende alterar e explicar o motivo antes de editar.
5. Fazer mudanças pequenas e revisáveis.
6. Ao final, entregar:
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
- Conferir se `README.md`, `docs/proximos-trabalhos.md` e `.ai/prompts/` não duplicam informação com nomes diferentes.
- Não marcar como concluído nada que não tenha evidência no repositório atual ou validação local.
