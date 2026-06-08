# Prompt — Investigação de bug no MPV.NET Media Player

Leia `AGENTS.md`, `README.md`, `docs/manual.md` e `docs/guia-operacional.md`.
Se o bug parecer amplo, leia também `docs/developer/architecture.md` antes de alterar.

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

Se a causa estiver espalhada por mais de um subsistema, proponha primeiro um recorte menor.

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
- antes de corrigir parser, paths, playlist, título, logs, configuração, seleção de idioma ou MediaInfo, verificar se `src/MpvNet.Tests/Program.cs` cobre o caso e ampliar quando necessário;
- atualizar documentação técnica apenas quando houver mudança consolidada, preferindo documentos existentes;
- ao final, informar arquivos alterados e validação feita.

