# Prompt — Auditoria de build, release e ZIP portátil

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/guia-operacional.md`;
5. `docs/developer/build-release.md`;
6. `docs/developer/architecture.md`;
7. `.ai/agents/mpvnet-release-agent.md`;
8. descrição da release no GitHub, quando houver publicação;
9. documentação da área de build ou release que esteja ligada à mudança.

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
- pacote WAP/MSIX e validação de payload Store, quando aplicável;
- estrutura `portable_config`;
- requisitos externos como 7-Zip, Inno Setup e GitHub CLI.

Avalie separadamente os fluxos GitHub e Microsoft Store. O pacote Store usa
revisão zero no `Identity Version` e deve ser validado com o projeto WAP e
`publish-store-package.ps1`; a release GitHub não comprova assinatura,
submissão ou certificação Store. Confira o conteúdo obrigatório em
`docs/developer/build-release.md`, incluindo `Scripts/osc.lua` e o helper
`mpvnet.com` no payload Store.

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
- não criar branch, commit, push ou publicar sem solicitação explícita;
- separar build local de pacote final;
- validar x64 sempre; validar ARM64 somente se esse alvo for explicitamente reintroduzido;
- atualizar documentação técnica apenas quando o processo consolidado mudar;
- preferir documentos existentes e evitar criar arquivos redundantes.



