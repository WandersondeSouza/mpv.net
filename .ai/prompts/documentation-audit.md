# Prompt — Auditoria de documentação

Analise profundamente este repositório.

Objetivo: realizar uma auditoria completa da documentação técnica e funcional.

Verifique:

- links quebrados e arquivos ausentes;
- documentação desatualizada, redundante ou sem fonte clara;
- cobertura de uso, configuração, build, release e arquitetura;
- inconsistências entre README, manual, `docs/developer/` e `.ai/`;
- consistência entre documentos técnicos e os agentes/prompts que os citam;
- codificação UTF-8, caminhos reais e comandos ainda suportados;
- se uma nova página é necessária ou se o conteúdo pertence a documento existente.

Entregue:

1. problemas encontrados e severidade;
2. arquivos afetados e evidência;
3. correção sugerida;
4. roadmap de melhoria;
5. riscos de manutenção;
6. validação executada e pendências de ambiente.

Antes de alterar qualquer arquivo, resuma o entendimento, liste os arquivos
relevantes e identifique as áreas críticas. Atualize documentação técnica
somente quando houver mudança consolidada, preferindo documentos existentes.

Validação mínima: `git diff --check`, conferência de links locais e busca por
referências a caminhos, branches, TFMs e fluxos que não existem mais. Não faça
commit, push ou publicação sem solicitação explícita.
