# Roadmap Técnico do Fork mpv.net

## Objetivo

Este roadmap organiza as próximas etapas de manutenção, documentação e evolução do fork `WandersondeSouza/mpv.net`.

O foco inicial é preservar o projeto, melhorar a documentação e preparar o repositório para manutenção segura com apoio de IA/Codex.

---

# Fase 1 — Preservação e documentação base

Status: em andamento

Objetivos:

- manter uma cópia funcional do projeto;
- documentar arquitetura inicial;
- criar documentação em português brasileiro;
- preparar orientações para agentes de IA;
- criar guias para contribuição e build.

Entregas:

- `AGENTS.md`;
- `README.pt-br.md`;
- `docs/contributing-ptbr.md`;
- `docs/developer/architecture-ptbr.md`;
- `docs/developer/build-ptbr.md`;
- `docs/developer/configuration-system-ptbr.md`;
- `docs/developer/commands-ptbr.md`;
- `docs/developer/ui-ptbr.md`;
- `docs/developer/mpv-integration-ptbr.md`.

---

# Fase 2 — Auditoria técnica profunda

Objetivos:

- mapear estrutura real do código;
- identificar solução e projetos;
- identificar entry point;
- mapear fluxo de inicialização;
- mapear integração com mpv/libmpv;
- mapear comandos, configuração e UI.

Entregas esperadas:

- mapa real de pastas;
- lista de projetos `.csproj`;
- documentação de classes principais;
- documentação do fluxo de eventos;
- relatório de riscos técnicos.

---

# Fase 3 — Organização do backlog

Objetivos:

- criar backlog de documentação;
- criar backlog de manutenção;
- criar backlog de UI;
- criar backlog de build/release;
- criar backlog de compatibilidade;
- criar backlog de performance.

---

# Fase 4 — Refatorações seguras

Objetivos:

- aplicar melhorias pequenas;
- evitar mudanças grandes sem testes;
- documentar impacto;
- preservar compatibilidade.

Critério para iniciar:

- build validado;
- fluxo de execução conhecido;
- riscos documentados.

---

# Fase 5 — Melhorias futuras

Possíveis frentes:

- melhorar documentação em português;
- criar guia visual de uso;
- melhorar experiência de contribuição;
- melhorar automação de build;
- documentar release;
- melhorar integração com agentes de IA;
- estudar modernização controlada da UI.

---

# Princípios do roadmap

1. Preservar antes de modificar.
2. Documentar antes de refatorar.
3. Testar antes de publicar.
4. Alterar pouco por vez.
5. Manter compatibilidade com mpv.
