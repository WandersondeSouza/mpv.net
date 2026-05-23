# Prompt — Investigação de bug no mpv.net

Leia `AGENTS.md`, `README.md` e `docs/ROADMAP.md`.

Vamos investigar apenas este bug:

```text
[DESCREVA O BUG, LOG, ISSUE OU REPRODUÇÃO]
```

Antes de alterar qualquer arquivo:

1. identifique a área afetada;
2. leia a documentação técnica relacionada em `docs/developer/`;
3. localize os arquivos do código envolvidos;
4. explique o comportamento atual confirmado no código;
5. compare com o comportamento esperado;
6. liste riscos e plano de teste.

Use este formato:

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Problema encontrado:

Mudança proposta:

Riscos:

Plano de teste:
```

Regras:

- não fazer refatoração ampla;
- não alterar comportamento não relacionado;
- preservar compatibilidade com mpv;
- atualizar documentação se o comportamento mudar;
- ao final, informar arquivos alterados e validação feita.
