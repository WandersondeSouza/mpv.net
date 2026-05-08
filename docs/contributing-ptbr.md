# Guia de Contribuição do mpv.net

## Objetivo

Este documento orienta contribuidores e mantenedores do fork.

---

# Antes de contribuir

Leia:

- README.md
- docs/manual.md
- AGENTS.md
- documentação em `docs/developer`

---

# Filosofia do projeto

O mpv.net prioriza:

1. compatibilidade com mpv;
2. estabilidade;
3. simplicidade de configuração;
4. performance;
5. flexibilidade.

---

# Regras importantes

1. Não quebrar compatibilidade sem justificativa.
2. Evitar refatorações gigantes.
3. Fazer mudanças pequenas.
4. Atualizar documentação.
5. Validar impacto em scripts.
6. Validar impacto em configurações antigas.

---

# Fluxo recomendado

## 1. Entender o problema

Antes de alterar código:

- identificar comportamento atual;
- localizar arquivos envolvidos;
- entender impacto.

---

## 2. Criar plano

Descrever:

- problema;
- solução;
- riscos;
- testes.

---

## 3. Implementar

Preferir:

- mudanças pequenas;
- commits organizados;
- código legível.

---

## 4. Testar

Validar:

- abertura do player;
- reprodução;
- fullscreen;
- atalhos;
- menu de contexto;
- temas;
- persistência de configuração.

---

# Checklist para Pull Request

- [ ] código compila;
- [ ] funcionalidade foi testada;
- [ ] documentação foi atualizada;
- [ ] compatibilidade preservada;
- [ ] riscos conhecidos documentados.

---

# Prioridades atuais deste fork

1. documentação técnica;
2. documentação em português brasileiro;
3. preparação para IA/agentes;
4. entendimento da arquitetura;
5. melhorias futuras controladas.
