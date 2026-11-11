# Qualidade de código — mpv.net

Este diretório guarda metodologia e relatórios auditáveis. A presente implantação configura o processo; **não constitui uma auditoria executada do código**.

- Prompt mestre: [code-quality-master.md](../../.agents/prompts/code-quality-master.md)
- Skill: [SKILL.md](../../.agents/skills/code-quality-auditor/SKILL.md)
- Regras de agentes: [AGENTS.md](../../AGENTS.md)
- Workflow informativo: [code-quality-audit.yml](../../.github/workflows/code-quality-audit.yml)

## Escopo
WPF / .NET / libmpv. Focos: P/Invoke libmpv, disposal do player, handlers, eventos, yt-dlp, processo filho, buffer streaming, opções nativas e compatibilidade com fork upstream.

## Métricas a coletar
| Métrica | Fonte/definição | Meta inicial para código novo | Estado inicial |
| --- | --- | --- | --- |
| Duplicação | SonarQube/jscpd ou ferramenta versionada; reportar exclusões | <=3% | N/A — não medida |
| Cobertura | Relatório de testes por módulo + diff coverage quando disponível | >=80% da lógica testável modificada | N/A — não medida |
| Issues severidade crítica | Analisadores/validação manual | 0 novos | N/A — não medida |
| Vulnerabilidades altas/críticas | SAST e dependências; triagem | 0 novas | N/A — não medida |
| Complexidade | Analisador específico; p95, máximo e localização | Tendência sem piora injustificada | N/A — não medida |
| Código suspeito de desuso | Busca de referências + reflexão/registro dinâmico | Revisão humana obrigatória | N/A — não medida |
| Dívida técnica | Issues confirmadas por módulo, tendência | Redução incremental | N/A — não medida |

## Relatórios de baseline
Criar `reports/YYYY-MM-DD-baseline.md` somente após auditoria REAL. Incluir commit, branch, runner, versões, comandos, exit codes, métricas e achados com arquivo:linha. Não editar um relatório antigo para adequá-lo a resultado novo. Diferenciar _configurado_, _executado_ e _validado_.

## Segurança e implantação
Não publicar segredos, logs completos de clientes ou URLs privadas. O CI é inicialmente não bloqueante. Após coleta de baseline e triagem, pode-se propor Quality Gate e SonarQube/Qodana em PR distinto, com autorização e verificação de licenciamento/permissões.
