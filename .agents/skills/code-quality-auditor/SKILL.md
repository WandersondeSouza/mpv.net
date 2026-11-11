---
name: code-quality-auditor
description: Auditoria conservadora, baseada em evidências, de qualidade de código em mpv.net; sem alterações de produção.
---
# code-quality-auditor

## Quando utilizar
Sempre que solicitado diagnóstico de duplicação, dead code, complexidade, obsolescência, design, cobertura, arquitetura, segurança ou qualidade de PR.
Leia primeiro `AGENTS.md`, `.agents/prompts/code-quality-master.md`, e a documentação de arquitetura.

## Sequência operacional
1. `git status --short`, identificar HEAD, ambiente, SDKs; não descartar mudanças locais.
2. Ler workflow e analisadores existentes; usar ferramentas já disponíveis e identificar lacunas antes de instalar qualquer plugin.
3. Analisar em lotes por módulo: interfaces públicas, dependências, classes/métodos, testes e chamadas indiretas.
4. Executar verificações REPRODUZÍVEIS sem `--fix`; conservar logs, exit codes e versões. Para WPF / .NET / libmpv: verificar .sln/.slnx e executar build/testes adequados; dotnet format whitespace --verify-no-changes; CodeQL existente.
5. Inspecionar manualmente evidências antes de rotular dead code ou violação. Duplicações textuais não são automaticamente defeitos arquiteturais.
6. Escrever relatório segundo `docs/quality/README.md`, com achados P0-P3, confirmações, arquivos:linhas, evidência, riscos e testes.
7. Propor correções em PRs pequenos APENAS após autorização específica. Nunca refatorar durante a auditoria.

## Saída obrigatória
- Matriz de verificações: executada / falhou / indisponível / não configurada.
- Baseline com métricas reais ou N/A, escopo e exclusões.
- Tabela priorizada por P0-P3 e confiança; sem resultado fabricado.
- Riscos de falso positivo e próximos testes.
- Nenhum segredo, URL privada de stream, token, PII ou output confidencial no relatório.

## Regras de avaliação
Metas sugeridas para NOVO código: bloqueadores/críticos 0, vulnerabilidades altas/críticas novas 0, duplicação <=3%, cobertura >=80% na lógica testável modificada. Não classificar dívida legada como falha nova. Não excluir código aparentemente não usado sem confirmar DI/reflection/XAML/serialização/rotas/manifest.
