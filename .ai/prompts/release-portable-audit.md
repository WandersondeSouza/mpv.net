# Prompt — Auditoria de build, release e ZIP portátil

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/build-release.md`;
6. `docs/developer/architecture.md`;
7. descricao da release no GitHub, quando houver publicacao;
8. documentação da área de build ou release que esteja ligada à mudança.

Objetivo:

```text
[DESCREVA A AUDITORIA OU MUDANÇA DE RELEASE]
```

Se a mudança tocar a experiência de uso imediato, confira também se os artefatos de release continuam coerentes com `docs/guia-operacional.md` e `docs/developer/build-release.md`.

Verifique:

- solução e projetos envolvidos;
- target frameworks;
- runtime identifiers;
- dependências nativas;
- scripts `src/Tools/*.ps1`;
- empacotamento ZIP;
- instalador Inno Setup;
- estrutura `portable_config`;
- requisitos externos como 7-Zip, Inno Setup e GitHub CLI.

Antes de alterar:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```

Regras:

- não commitar binários gerados sem pedido explícito;
- separar build local de pacote final;
- validar x64 sempre; validar ARM64 somente se esse alvo for explicitamente reintroduzido;
- atualizar documentação técnica apenas quando o processo consolidado mudar;
- preferir documentos existentes e evitar criar arquivos redundantes.


