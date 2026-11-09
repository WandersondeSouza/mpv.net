# Prompt mestre — Auditoria de qualidade do código (mpv.net)
> Escopo: APENAS inventário, configuração de analisadores de baixo risco e auditoria. Nenhuma refatoração, deleção ou alteração de código de produção. Revisão obrigatória antes de aplicar correções.

## Contexto
Projeto: WPF / .NET / libmpv. Base de referência: branch atual e AGENTS.md; nunca inferir estado de relatório antigo.
Riscos e foco: P/Invoke libmpv, disposal do player, handlers, eventos, yt-dlp, processo filho, buffer streaming, opções nativas e compatibilidade com fork upstream.

## Fase A — inventariar sem alterar
1. Inspecionar status, solução/módulos, composição de projetos, testes, branches, arquivos gerados e arquitetura documentada.
2. Identificar configurações existentes: Roslyn/EditorConfig, NuGetAudit, CodeQL, Android Lint, detekt, ESLint, Oxlint, TypeScript, cobertura, SAST, dependency review, GitHub Actions, SonarQube/Qodana.
3. Confirmar versões reais e compatibilidade ANTES de propor plugins. Não instalar globalmente nem baixar executáveis sem origem/versionamento verificável.
4. Preservar convenções e fluxo de build. Não editar arquivos de runtime, produção, migrations, banco, recursos de localização ou manifests.

## Fase B — analisar com evidência
- Duplicação: blocos semânticos e textuais; separar duplicação legítima de oportunidade de reutilização, comparar métodos e padrões em múltiplas áreas. Escolher jscpd/SonarQube/Qodana se disponível e versão validada; não inventar índice.
- Código não usado: referências por IDE/compiler + busca textual + chamadas indiretas (DI, reflection, callbacks, XAML, serializadores, rotas e ferramentas de build); tratar suspeitas como hipóteses.
- Obsolescência: warnings do SDK, APIs [Obsolete]/deprecated, dependências e riscos de atualização sem upgrade automático.
- Design: responsabilidade única, tamanho de classes/métodos, complexidade cognitiva/ciclomática, acoplamento, dependências circulares, duplicidade de DTOs, código UI integrado com persistência.
- Qualidade operacional: async/await/coroutines, cancellation, Dispose/close, memory leaks, exception swallowing, logs sem segredos, performance e segurança.
- Testes: cobertura por módulo e no código novo, regressão, testes de integração e UI quando aplicável; nunca equiparar cobertura a qualidade.
- Não enviar tokens, urls privadas, credenciais, dados pessoais, conteúdo de stream ou logs sensíveis a serviços externos.

## Fase C — executar ferramentas já presentes
Verificar existência de SDKs e comandos; executar individualmente, capturar exit code e separar erro do ambiente de erro do projeto.
Comandos/candidatos existentes a validar: verificar .sln/.slnx e executar build/testes adequados; dotnet format whitespace --verify-no-changes; CodeQL existente.
SonarQube Cloud/Server: opcional, somente após validar permissões, cobertura de idiomas, licença, política de privacidade e configuração de token em secrets.
Qodana: opcional, não instalar/ativar sem verificar edição, licença, runtime e sobreposição com analisadores já existentes.
CodeQL: complementar segurança e não substitui manutenção/duplicidade.
Nunca habilitar --fix, auto-format ou qualquer tarefa que escreva código durante esta etapa.

## Fase D — métricas e baseline
Produzir arquivo `docs/quality/reports/YYYY-MM-DD-baseline.md` APÓS executar ferramentas; se não executou, registrar N/A e razão, nunca transformar meta em resultado.
Registrar: branch/commit e data; ferramenta/versão/comando/exit code; LOC (com exclusões); duplicação % (global e novas alterações); cobertura (por módulo e escopo); critical/high security; bug/reliability; maintainability; dívida técnica; complexidade p95/máximo; arquivos e classes grandes; código possivelmente morto; pacotes obsoletos/vulneráveis; testes/pass/fail.
Metas INICIAIS para novo código: 0 issue critical/blocker introduzida, 0 nova vulnerabilidade alta/crítica, <=3% duplicação, >=80% de cobertura nas partes testáveis do novo código. Metas não são medidas; qualificar exclusões e justificar exceções. Nenhuma regra retroativa bloqueante por padrão.

## Fase E — relatório de achados
Para cada achado, apresentar ID, gravidade P0–P3, confirmação (confirmado/provável/indeterminado), caminho:linha, explicação, evidência/referências, impacto, dependência, falso positivo possível, correção proposta (somente plano), teste de regressão, esforço e risco. Priorizar por risco real e frequência em vez de estilo pessoal.
Separar: (1) problemas confirmados; (2) suspeitas; (3) dependências; (4) gaps de testes; (5) limites de execução/ferramenta; (6) plano incremental por PR.
Gerar sumário executivo e comparação com baseline anterior se houver.

## Fase F — proteção
Não alterar código produtivo. Criar PR só com documentação, skills, configuração de análise e workflow informativo. Não reescrever `AGENTS.md` apagando regras existentes; adicionar seção. Nunca sobrescrever relatórios de execuções anteriores. Encerrar com: o que foi EXECUTADO vs somente CONFIGURADO, resultados reais e pendências. Pedir aprovação separada para qualquer refatoração.

## Critérios de aceite
- [ ] Arquivos de runtime/produção inalterados.
- [ ] Respeitado AGENTS.md e arquitetura real.
- [ ] Auditoria reproduzível (comandos/versões/logs e status).
- [ ] Métricas sem invenção e achados rastreáveis a código.
- [ ] CI original preservado e novo fluxo não bloqueante.
- [ ] Nenhum auto-fix, remoção de código ou atualização de dependências.
