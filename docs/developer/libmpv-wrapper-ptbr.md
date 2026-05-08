# Wrapper e Integração com libmpv

## Objetivo

Documentar como o mpv.net se comunica com libmpv.

> Documento inicial. Deve ser refinado após auditoria completa do código.

---

# Responsabilidades esperadas

A camada de integração deve:

- inicializar libmpv;
- enviar comandos;
- ler propriedades;
- receber eventos;
- sincronizar estado;
- expor comportamento para UI.

---

# Áreas esperadas

## Inicialização

- criação do contexto mpv;
- aplicação de configurações;
- registro de eventos.

---

## Propriedades

Exemplos esperados:

- pause;
- fullscreen;
- volume;
- chapter;
- subtitle;
- audio-track.

---

## Eventos

Exemplos esperados:

- file-loaded;
- end-file;
- pause;
- seek;
- shutdown.

---

## Comandos

Exemplos esperados:

- loadfile;
- cycle;
- set;
- show-text;
- script-message.

---

# Áreas críticas

## Threading

Mudanças podem causar deadlock ou congelamento.

## Sincronização

Mudanças podem quebrar atualização da UI.

## Compatibilidade

Mudanças podem quebrar scripts e comportamento esperado do mpv.

---

# Próxima etapa

Durante auditoria real:

- localizar wrapper real;
- localizar chamadas nativas;
- localizar marshaling;
- localizar eventos;
- mapear ciclo de vida.
