# Agentes recomendados para o fork mpv.net

Este diretório descreve perfis de agentes úteis para trabalhar no fork sem alterar o comportamento do mpv.net por acidente.

Use estes perfis como instruções de especialização para Codex, Copilot, agentes locais ou automações.

## Regras comuns

Todo agente deve seguir:

1. preservar compatibilidade com mpv/libmpv;
2. ler `AGENTS.md`, `README.md` e `docs/ROADMAP.md`;
3. consultar a documentação técnica em `docs/developer/`;
4. confirmar comportamento no código antes de editar;
5. propor mudança pequena;
6. atualizar documentação quando comportamento mudar;
7. informar plano de teste.

## Perfis disponíveis

| Perfil | Arquivo | Uso |
| --- | --- | --- |
| Mantenedor geral | `mpvnet-maintainer-agent.md` | Tarefas amplas de manutenção, documentação e correções pequenas. |
| Configuração e atalhos | `mpvnet-config-agent.md` | `mpv.conf`, `mpvnet.conf`, `input.conf`, modo portátil e migrações. |
| Integração mpv/libmpv | `mpvnet-libmpv-agent.md` | Player, eventos, comandos, propriedades e wrappers nativos. |
| UI Windows | `mpvnet-ui-agent.md` | WinForms, WPF, fullscreen, tema, DPI, menus e foco. |
| Build e release | `mpvnet-release-agent.md` | Scripts de release, ZIP portátil, Inno Setup e dependências. |

## Como escolher

- Use o mantenedor geral para triagem e documentação.
- Use um agente especializado quando a tarefa tocar uma área crítica.
- Para mudanças grandes, peça primeiro relatório de impacto e divida em etapas.
