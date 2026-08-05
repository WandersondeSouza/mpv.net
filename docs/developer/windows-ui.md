# Interface Windows do MPV.NET Media Player

## Objetivo

Documentar conceitos da interface gráfica do MPV.NET Media Player.

---

# Visão geral

O mpv.net implementa sua própria janela principal para Windows.

A interface gráfica é uma das principais diferenças entre o mpv original e o mpv.net.

---

# Principais responsabilidades

- janela principal;
- fullscreen;
- menu de contexto;
- overlays;
- temas;
- atalhos;
- mouse;
- controles na tela.

## Topologia da janela principal

- `src/MpvNet.Windows/WinForms/MainForm.cs` - estado principal da janela e ciclo de vida WinForms;
- `src/MpvNet.Windows/WinForms/MainForm.ContextMenu.cs` - menu de contexto, faixas, capítulos e busca de itens;
- `src/MpvNet.Windows/WinForms/MainForm.Commands.cs` - comandos de UI vindos de `GuiCommand`;
- `src/MpvNet.Windows/WinForms/MainForm.Cursor.cs` - cursor e OSC;
- `src/MpvNet.Windows/WinForms/MainForm.DragDrop.cs` - drag/drop;
- `src/MpvNet.Windows/WinForms/MainForm.Fullscreen.cs` - fullscreen;
- `src/MpvNet.Windows/WinForms/MainForm.PlayerEvents.cs` - reação da UI a eventos do player.

---

# Áreas críticas

## Fullscreen

Mudanças podem impactar:

- multi-monitor;
- overlays;
- menu de contexto;
- comportamento do cursor.

Quando a janela está em fullscreen, `Esc` é tratado pela camada Windows para
sair desse modo e não é encaminhado ao mpv como `quit`. Com a janela no tamanho
normal, o atalho padrão `Esc` continua sendo encaminhado ao mpv para fechar o
player.

## OSC

O OSC depende do comportamento da janela.

O fork configura o `custom_button_1` nativo do `osc.lua` para mostrar um pequeno
coração de apoio. O comando de clique encaminha para o navegador usando a URL
de doação gerada pelo C# com o idioma efetivo da interface. Como o botão é
criado pelo próprio OSC, ele usa a mesma visibilidade, hover, autocultação e
roteamento de mouse dos controles de volume e fullscreen. Em janelas muito
pequenas o próprio layout do OSC decide quando os controles cabem.

O `custom_button_2` usa um globo da fonte `Segoe UI Symbol` para abrir a página
oficial. O C# obtém o idioma efetivo de `App.Language`, monta internamente
`https://www.gestaodesistemas.com.br/mpvnet?language=<cultura>` e publica o
resultado em `user-data/mpvnet-website-url`. Os botões são acrescentados a
`script-opts` depois de `mpv_initialize`, preservando as opções do usuário e
reutilizando `script-message-to mpvnet shell-execute`. O `osc.lua` original não
é alterado nem copiado: layout, escala, hover, clique e visibilidade continuam
sob responsabilidade do OSC nativo.

## DPI e escala

Mudanças precisam validar:

- Windows 10;
- Windows 11;
- telas 4K;
- múltiplos monitores.

---

# Temas

O projeto suporta:

- tema claro;
- tema escuro;
- temas customizados.

---

# Recomendações de manutenção

1. Evitar alterações amplas de UI sem testes.
2. Validar fullscreen.
3. Validar menu de contexto.
4. Validar temas.
5. Validar comportamento do mouse.
6. Preservar compatibilidade visual.
7. Quando tocar `MainForm.*.cs`, validar arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento do player.

## Smoke test reproduzível

O script abaixo verifica inicialização, permanência do processo e fechamento
limpo. Ele não substitui a verificação visual dos controles:

```powershell
.\src\Tools\test-ui-smoke.ps1 `
  -ExecutablePath .\src\MpvNet.Windows\bin\Debug\win-x64\mpvnet.exe `
  -MediaPath C:\Videos\amostra.mp4
```

Depois do smoke test, validar manualmente:

- arquivo local, URL/stream, playlist e pasta com mídia;
- drag/drop e abertura por linha de comando;
- tema claro/escuro, DPI, múltiplos monitores e fullscreen;
- menu de contexto, atalhos, foco e cursor/OSC;
- alternância de áudio/legenda, pause, seek e fechamento;
- persistência de volume, posição, tamanho e pasta inicial.

O script encerra o processo ao final por padrão. Use `-KeepOpen` quando for
necessário concluir o checklist visual antes de fechar a janela.

---

# Melhorias futuras sugeridas

- documentação visual;
- guia de temas;
- guia de acessibilidade;
- testes automatizados de UI;
- modo compacto;
- perfis visuais.


