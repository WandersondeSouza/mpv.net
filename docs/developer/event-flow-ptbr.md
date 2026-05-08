# Fluxo de Eventos do mpv.net

## Objetivo

Documentar como eventos circulam entre UI, comandos e libmpv.

> Documento inicial baseado na arquitetura esperada.

---

# Fluxo esperado

## Entrada do usuário

Eventos podem vir de:

- teclado;
- mouse;
- menu de contexto;
- linha de comando;
- scripts;
- extensões.

---

## Camada de comandos

A entrada do usuário normalmente passa por:

- parser;
- sistema de atalhos;
- sistema de comandos.

---

## Integração com libmpv

Após interpretação:

- comandos são enviados;
- propriedades são alteradas;
- eventos retornam para UI.

---

## Atualização da UI

A UI deve reagir a:

- pausa;
- seek;
- fullscreen;
- mudança de mídia;
- áudio;
- legenda;
- estado do player.

---

# Áreas críticas

## Sincronização

Mudanças podem causar:

- UI dessincronizada;
- fullscreen incorreto;
- overlays incorretos.

## Eventos assíncronos

Eventos podem ocorrer fora da thread principal.

## Scripts

Scripts podem depender de ordem específica de eventos.

---

# Próxima etapa

Durante auditoria real do código:

- identificar classes de eventos;
- identificar handlers;
- identificar fluxo de propriedades;
- identificar eventos assíncronos;
- documentar riscos reais.
