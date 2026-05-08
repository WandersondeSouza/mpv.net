# Sistema de Comandos do mpv.net

## Objetivo

Documentar o funcionamento do sistema de comandos do mpv.net.

---

# Visão geral

O mpv.net herda grande parte da arquitetura de comandos do mpv.

Além disso, implementa comandos próprios relacionados à interface gráfica e integração com Windows.

---

# Tipos de comandos

## Comandos do mpv

Compatíveis com o mpv original.

Exemplos:

- pause;
- seek;
- fullscreen;
- volume;
- cycle.

---

## Comandos específicos do mpv.net

Implementados pelo frontend Windows.

Exemplos documentados:

- open-conf-folder;
- show-menu;
- show-conf-editor;
- show-input-editor;
- move-window;
- show-about.

---

# Fontes de comandos

Comandos podem vir de:

- teclado;
- mouse;
- menu de contexto;
- terminal;
- linha de comando;
- scripts Lua;
- scripts JavaScript;
- extensões .NET.

---

# input.conf

Arquivo responsável por atalhos e mapeamento.

Exemplo:

```text
Ctrl+a show-text Teste
```

---

# Menu de contexto

O menu de contexto pode ser customizado.

Versões antigas utilizavam `#menu:`.

Versões mais novas utilizam estrutura interna mais flexível.

---

# Áreas críticas

## Compatibilidade com atalhos antigos

Mudanças no parser podem quebrar configurações existentes.

## Scripts

Scripts podem depender de comandos específicos.

## Menu de contexto

Mudanças podem afetar usabilidade.

---

# Recomendações para manutenção

1. Não remover comandos existentes sem migração.
2. Documentar novos comandos.
3. Validar integração com scripts.
4. Validar funcionamento em fullscreen.
5. Testar teclado e mouse.

---

# Melhorias futuras sugeridas

- documentação automática dos comandos;
- editor visual de atalhos;
- exportação/importação de atalhos;
- detecção de conflitos;
- perfis de atalhos.
