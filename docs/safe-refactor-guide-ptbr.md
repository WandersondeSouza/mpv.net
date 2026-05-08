# Guia de Refatoração Segura

## Objetivo

Definir regras para refatorações seguras no projeto mpv.net.

---

# Princípios principais

1. Preservar compatibilidade.
2. Alterar pouco por vez.
3. Documentar antes de modificar.
4. Testar antes de publicar.
5. Evitar mudanças amplas sem auditoria.

---

# Antes de refatorar

## Entender o comportamento atual

Responder:

- como funciona hoje;
- quais arquivos participam;
- quais riscos existem;
- quais dependências existem.

---

# Mapear impacto

Validar impacto em:

- fullscreen;
- input;
- scripts;
- extensões;
- configuração;
- menu de contexto;
- temas.

---

# Refatorações recomendadas

## Baixo risco

- documentação;
- comentários;
- organização pequena;
- nomes mais claros;
- extração pequena de métodos.

## Médio risco

- reorganização de UI;
- melhoria de configuração;
- melhorias de build.

## Alto risco

- integração com libmpv;
- fullscreen;
- input;
- parser de configuração;
- eventos.

---

# Estratégia recomendada

1. Criar branch específica.
2. Fazer mudanças pequenas.
3. Criar commits pequenos.
4. Testar incrementalmente.
5. Atualizar documentação.

---

# Checklist antes do merge

- [ ] build funcionando;
- [ ] fullscreen validado;
- [ ] reprodução validada;
- [ ] input validado;
- [ ] scripts validados;
- [ ] documentação atualizada.
