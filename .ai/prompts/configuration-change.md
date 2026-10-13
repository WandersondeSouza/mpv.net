# Prompt — Mudança em configuração, atalhos ou modo portátil

Leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/manual.md`;
4. `docs/CONFIGURACAO.md`;
5. `docs/ATALHOS.md`;
6. `docs/guia-operacional.md`;
7. `docs/developer/configuration.md`;
8. `.ai/agents/mpvnet-config-agent.md`;
9. `docs/developer/architecture.md` quando a mudança envolver fluxo amplo.

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

Se a mudança tocar cache de mídia de rede, leia também
`docs/developer/mpv-integration.md`. Se tocar componentes ou pacote Store, leia
`docs/developer/build-release.md`.

Regras:

- preservar a precedência `MPVNET_HOME` válido, `portable_config` existente e `AppPaths.DefaultConfig`;
- ao avaliar caminhos, considerar `MPVNET_LOCAL_APPDATA` e a raiz `LocalCache\Local` resolvida por `AppPaths` no MSIX;
- preservar compatibilidade com arquivos existentes;
- não reescrever arquivo do usuário sem necessidade;
- verificar e ampliar `src/MpvNet.Tests/Program.cs` quando a mudança alterar parser, paths, fallback, seleção de idioma ou compatibilidade de configuração;
- documentar qualquer nova opção, fallback ou migração consolidada em documento existente quando possível;
- evitar criar documentação técnica redundante;
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


