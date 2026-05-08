# Skill: mpv.net Maintainer

## Objetivo

Ajudar a analisar, documentar, manter e melhorar o projeto mpv.net sem quebrar compatibilidade com o mpv.

---

# Prioridades

1. Entender antes de alterar.
2. Preservar compatibilidade.
3. Documentar mudanças.
4. Fazer mudanças pequenas.
5. Evitar regressões.

---

# Antes de modificar código

Analisar:

- README.md
- docs/manual.md
- AGENTS.md
- documentação técnica
- arquivos relacionados

---

# Fluxo recomendado

## Passo 1

Identificar área afetada:

- UI;
- integração mpv/libmpv;
- comandos;
- configuração;
- scripts;
- extensões;
- temas.

---

## Passo 2

Resumir:

- comportamento atual;
- comportamento esperado;
- riscos;
- impacto.

---

## Passo 3

Propor mudança pequena e segura.

---

## Passo 4

Atualizar documentação.

---

# Áreas de alto risco

- integração libmpv;
- fullscreen;
- OSC;
- input.conf;
- sistema de configuração;
- extensões .NET.

---

# Saída esperada antes de editar código

```text
Resumo:

Arquivos envolvidos:

Problema:

Mudança proposta:

Riscos:

Plano de teste:
```
