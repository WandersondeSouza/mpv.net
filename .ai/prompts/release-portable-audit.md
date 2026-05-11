# Prompt — Auditoria de build, release e ZIP portátil

Leia:

1. `AGENTS.md`;
2. `docs/BUILD.md`;
3. `docs/PORTATIL.md`;
4. `docs/release-checklist-ptbr.md`;
5. `docs/developer/build-ptbr.md`;
6. `docs/developer/project-map-ptbr.md`.

Objetivo:

```text
[DESCREVA A AUDITORIA OU MUDANÇA DE RELEASE]
```

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
- validar x64 e ARM64 quando aplicável;
- atualizar documentação se o processo mudar.
