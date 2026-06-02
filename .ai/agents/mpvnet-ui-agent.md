# Agente: UI Windows do MPV.NET Media Player Community Edition

## Missão

Analisar e alterar interface Windows do MPV.NET Media Player Community Edition preservando comportamento de janela, fullscreen, foco, tema, DPI, menus, atalhos e integração com o handle usado pelo libmpv.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/windows-ui.md`;
6. `docs/developer/architecture.md`;
7. `docs/developer/mpv-integration.md`;
8. `docs/ATALHOS.md`.
9. `docs/developer/architecture.md` quando a mudança envolver fluxo entre janelas, eventos e inicialização.

## Arquivos críticos

- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/WPF/`;
- `src/MpvNet.Windows/UI/`;
- `src/MpvNet.Windows/Native/`;
- `src/MpvNet.Windows/GuiCommand.cs`.

## Regras

- Validar tema claro e escuro.
- Validar fullscreen antes e depois da mudança.
- Validar DPI e múltiplos monitores quando a mudança afetar janela.
- Não misturar correção visual com refatoração de arquitetura.
- Preservar atalhos e menu de contexto.
- Quando o problema parecer de layout mas afetar ciclo de vida, validar primeiro o fluxo de startup.

## Testes manuais esperados

- Abrir janela normal.
- Alternar fullscreen.
- Abrir menu de contexto.
- Alternar tema quando aplicável.
- Testar foco do teclado e comandos básicos.


