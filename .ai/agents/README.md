# Agentes recomendados para o fork MPV.NET Media Player Community Edition

Este diretório descreve perfis de agentes úteis para trabalhar no fork sem alterar o comportamento do MPV.NET Media Player Community Edition por acidente.

Use estes perfis como instruções de especialização para Codex, Copilot, agentes locais ou automações.

## Regras comuns

Todo agente deve seguir:

1. preservar compatibilidade com mpv/libmpv;
2. ler `AGENTS.md`, `README.md` e `docs/manual.md`;
3. consultar `docs/guia-operacional.md` quando a tarefa envolver build, release, scripts ou manutenção geral;
4. consultar `docs/proximos-trabalhos.md` quando a tarefa envolver priorização ou planejamento;
5. consultar a documentação técnica em `docs/developer/` que corresponda à área tocada;
6. confirmar comportamento no código antes de editar;
7. propor mudança pequena;
8. atualizar documentação quando o comportamento mudar;
9. informar plano de teste.

## Perfis disponíveis

| Perfil | Arquivo | Uso |
| --- | --- | --- |
| Mantenedor geral | `mpvnet-maintainer-agent.md` | Tarefas amplas de manutenção, documentação e correções pequenas. |
| Arquitetura e análise | `mpvnet-architecture-agent.md` | Mapa arquitetural, análise de módulos, refatoração segura e mudanças amplas. |
| Configuração e atalhos | `mpvnet-config-agent.md` | `mpv.conf`, `mpvnet.conf`, `input.conf`, modo portátil e migrações. |
| Integração mpv/libmpv | `mpvnet-libmpv-agent.md` | Player, eventos, propriedades, comandos e wrappers nativos. |
| UI Windows | `mpvnet-ui-agent.md` | WinForms, WPF, fullscreen, tema, DPI, menus e foco. |
| Build e release | `mpvnet-release-agent.md` | Scripts de release, ZIP portátil, Inno Setup e dependências. |

## Como escolher

- Use o mantenedor geral para triagem, documentação e alinhamento inicial.
- Use o agente de arquitetura quando a tarefa envolver mapa do código, classes críticas ou refatoração em etapas.
- Use um agente especializado quando a tarefa tocar uma área crítica.
- Para mudanças grandes, peça primeiro relatório de impacto e divida em etapas.
