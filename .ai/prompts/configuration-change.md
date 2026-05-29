# Prompt — Mudança em configuração, atalhos ou modo portátil

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/CONFIGURACAO.md`;
5. `docs/ATALHOS.md`;
6. `docs/guia-operacional.md`;
7. `docs/developer/configuration-system-ptbr.md`;
8. `docs/developer/configuration-flow-ptbr.md`.

Objetivo:

```text
[DESCREVA A MUDANÇA OU BUG DE CONFIGURAÇÃO]
```

Antes de alterar código, confirme no código:

- ordem de resolução da pasta de configuração;
- impacto em `mpv.conf`;
- impacto em `mpvnet.conf`;
- impacto em `input.conf`;
- impacto em `settings.xml`;
- impacto no modo portátil;
- necessidade de migração ou backup.

Regras:

- preservar `MPVNET_HOME`, `portable_config` e `%APPDATA%\mpv.net`;
- preservar compatibilidade com arquivos existentes;
- não reescrever arquivo do usuário sem necessidade;
- documentar qualquer nova opção, fallback ou migração;
- testar caminho instalado e portátil.

Entrega esperada:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```
