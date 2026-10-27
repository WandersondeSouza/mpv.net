# Agente: UI Windows do MPV.NET Media Player

## Missão

Analisar e alterar interface Windows do MPV.NET Media Player preservando comportamento de janela, fullscreen, foco, tema, DPI, menus, atalhos e integração com o handle usado pelo libmpv.

## Ler primeiro

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/windows-ui.md`;
6. `docs/developer/architecture.md`;
7. `docs/developer/mpv-integration.md`;
8. `docs/ATALHOS.md`.
9. documentação técnica relacionada quando a mudança envolver fluxo entre janelas, eventos e inicialização.
10. `docs/developer/localization.md` quando a mudança tocar texto, cultura ou layout localizado.

## Arquivos críticos

- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/WinForms/MainForm.*.cs`;
- `src/MpvNet.Windows/WinForms/TitleBarFullscreenButton.cs`, para o botão da barra de título;
- `src/MpvNet.Windows/WPF/InterfaceLayout.cs`, para direção e recursos de layout;
- `src/MpvNet.Windows/WPF/`;
- `src/MpvNet.Windows/UI/`;
- `src/MpvNet.Windows/Native/`;
- `src/MpvNet.Windows/Commands/GuiCommand.cs`.

## Regras

- Validar tema claro e escuro.
- Validar fullscreen antes e depois da mudança.
- Validar DPI e múltiplos monitores quando a mudança afetar janela.
- Não misturar correção visual com refatoração de arquitetura.
- Preservar atalhos e menu de contexto.
- Usar gettext para textos visíveis; preservar RTL/LTR, campos técnicos LTR,
  formatação regional, tooltips e nomes acessíveis. Validar troca de idioma no
  editor sem perder opções não salvas.
- Para a barra de título, preservar HWND, área cliente, hit test e os botões
  nativos do Windows; consultar os testes `TitleBarFullscreenButtonTests.cs`.
- Quando o problema parecer de layout mas afetar ciclo de vida, validar primeiro o fluxo de startup.
- Atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes.

## Testes manuais esperados

- Abrir janela normal.
- Alternar fullscreen.
- Abrir menu de contexto.
- Abrir arquivo local, URL/stream, playlist e pasta com mídia.
- Testar drag/drop.
- Testar cursor/OSC.
- Alternar faixa/legenda quando aplicável.
- Testar comandos de janela quando a mudança tocar `GuiCommand`.
- Fechar o player e confirmar encerramento normal.
- Alternar tema quando aplicável.
- Testar foco do teclado e comandos básicos.



