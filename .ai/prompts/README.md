# Índice de prompts do MPV.NET

Os prompts são moldes de trabalho ou planos preparados em momentos específicos.
`AGENTS.md`, o código e a documentação atual em `docs/` são a fonte de verdade.
Antes de executar um plano longo, marque cada requisito como implementado,
pendente, não validado ou fora do escopo; não repita trabalho já consolidado.

Instruções antigas de branch, commit, push ou publicação não autorizam essas
ações. Siga `.ai/README.md` e o pedido explícito do usuário.

## Moldes de uso geral

- [Investigação de bug](bug-investigation.md)
- [Mudança de configuração](configuration-change.md)
- [Análise profunda de código-fonte](deep-source-analysis.md)
- [Auditoria de documentação](documentation-audit.md)
- [Mudança em mpv/libmpv](libmpv-change.md)
- [Próximas melhorias](next-improvements.md)
- [Tradução e documentação pt-BR](ptbr-translation-docs.md)
- [Auditoria de build, release, ZIP e Store](release-portable-audit.md)

## Planos por área

- [Auditoria incremental de confiabilidade de reprodução](playback-reliability-deep-audit.md)
- [Extensão de componentes de runtime e armazenamento](playback-runtime-components-storage-extension.md), complementar ao plano de reprodução
- [Modernização de mídia online e YouTube](online-media-youtube-streaming-modernization.md), com o [complemento de serviços regionais](online-media-ytdlp-regional-services-extension.md)
- [Refatoração de qualidade e ciclo de vida](code-quality-resource-lifecycle-refactor.md)
- [Interações de mouse e teclado](player-input-click-pause-fullscreen.md) e [evolução de atalhos/gestos/OSC](player-input-future-shortcuts-gestures-osc.md)
- [Migração SMTC/FluentFlyout/.NET 10](windows-smtc-fluentflyout-net10-migration.md)
- [Melhorias de integração SMTC com o Windows](windows-smtc-integration-improvements.md), complemento incremental ao plano de migração
- [Tooltip de doação na taskbar](taskbar-donation-tooltip-localization.md)

Os planos por área descrevem escopos específicos e podem estar parcial ou
totalmente concluídos. Consulte `docs/guia-operacional.md` e a documentação
técnica relacionada para distinguir validações concluídas de pendências atuais.
