# Agente: UI Windows do mpv.net

## Missão

Analisar e alterar interface Windows do mpv.net preservando comportamento de janela, fullscreen, foco, tema, DPI, menus, atalhos e integração com o handle usado pelo libmpv.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/ui-ptbr.md`;
6. `docs/developer/startup-flow-ptbr.md`;
7. `docs/developer/event-flow-ptbr.md`;
8. `docs/ATALHOS.md`.

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

## Testes manuais esperados

- Abrir janela normal.
- Alternar fullscreen.
- Abrir menu de contexto.
- Alternar tema quando aplicável.
- Testar foco do teclado e comandos básicos.
