# Interface Gráfica do mpv.net

## Objetivo

Documentar conceitos da interface gráfica do mpv.net.

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

---

# Áreas críticas

## Fullscreen

Mudanças podem impactar:

- multi-monitor;
- overlays;
- menu de contexto;
- comportamento do cursor.

## OSC

O OSC depende do comportamento da janela.

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

---

# Melhorias futuras sugeridas

- documentação visual;
- guia de temas;
- guia de acessibilidade;
- testes automatizados de UI;
- modo compacto;
- perfis visuais.
