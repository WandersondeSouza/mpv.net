# Prompt — Auditoria editorial e atualização da documentação do MPV.NET Media Player

## Missão

Realize uma auditoria técnica, editorial, estrutural e linguística de toda a documentação de `WandersondeSouza/mpv.net`. Atualize os documentos para refletirem o comportamento **comprovado pelo código atual**, em português brasileiro correto, direto e objetivo, sem perda de instruções úteis. **Conciso não significa incompleto.** Não estipule meta percentual de redução de palavras.

Este prompt é para execução posterior pelo Codex. Não execute suas tarefas ao apenas ler este arquivo.

## Escopo e prioridades

Analise `README.md`, `SECURITY.md`, `docs/manual.md`, `docs/ATALHOS.md`, `docs/CONFIGURACAO.md`, `docs/guia-operacional.md`, `docs/logging.md`, `docs/developer/*.md` e os relatórios em `docs/developer/reports/`. Examine `.github/`, `.ai/`, `.agents/` e `AGENTS.md` como referências de políticas, restrições e links, sem reescrever indiscriminadamente prompts e registros históricos.

Priorize `docs/developer/localization.md` (acentuação e exemplo C#), `docs/manual.md` (exemplo C#), `docs/developer/build-release.md` (acentuação e organização), `docs/developer/mpv-integration.md` (conteúdo operacional versus histórico). O inventário inicial é indicativo: procure também outros arquivos e defeitos.

## 1. Qualidade textual

- Corrija ortografia, acentuação, concordância nominal e verbal, regência, crase, pontuação, número, tempo verbal, coesão e ambiguidade.
- Corrija, quando forem palavras da prosa em português, grafias como `validacao` → `validação`, `traducoes` → `traduções`, `implementacao` → `implementação`, `configuracao` → `configuração`, `disponiveis` → `disponíveis`.
- Adote português brasileiro técnico, profissional, claro e voz ativa quando apropriado. Evite frases circulares, introduções vazias, adjetivos excessivos, jargão sem necessidade e parágrafos excessivamente longos.
- Mantenha intactos nomes de classes, métodos, namespaces, argumentos de CLI, parâmetros, identificadores, tags, códigos BCP 47, extensões, arquivos e caminhos.
- Para documentos em outros idiomas, preserve o idioma e corrija a gramática correspondente; não traduza comandos ou identificadores.

## 2. Preservação e consolidação

- Identifique redundâncias **dentro do mesmo arquivo** e **entre arquivos**, retirando somente repetições que não agregam contexto. Não retire requisitos, procedimentos, pré-requisitos, cautelas de segurança, limitações, diagnósticos, explicações necessárias nem detalhes de compatibilidade.
- Prefira uma fonte canônica por assunto e links internos para detalhes, mantendo resumos locais quando úteis: `README.md` (visão geral), `docs/manual.md` (uso), `docs/ATALHOS.md` (atalhos), `docs/CONFIGURACAO.md` (configuração rápida), `docs/logging.md` (logs), `docs/guia-operacional.md` (comandos operacionais), `docs/developer/architecture.md` (arquitetura), `docs/developer/configuration.md` (configuração interna), `docs/developer/build-release.md` (build/release), `docs/developer/mpv-integration.md` (mpv/libmpv), `docs/developer/localization.md` (idiomas) e `docs/developer/windows-ui.md` (UI).
- Separe estado atual e registros históricos. Preserve relatórios datados, evidências, medições e decisões relevantes; não transforme hipóteses ou prompts pendentes em funcionalidades implementadas. Quando adequado, referencie registros existentes em vez de duplicá-los. Não apague documentação histórica sem justificativa e análise de dependências.
- Preserve UTF-8 e, sempre que possível, âncoras existentes. Evite links cruzados circulares.

## 3. Exemplos de código e PowerShell

- **Remova exemplos de código-fonte C# dos documentos de uso e documentação técnica**, tanto blocos ```csharp como exemplos C# sem marcador. Isso não autoriza apagar código do repositório.
- Quando o exemplo demonstrar um procedimento executável externamente, substitua-o por **comandos PowerShell reais**, com blocos ```powershell, verificados contra argumentos, scripts e caminhos existentes. Exemplos: iniciar `mpvnet.exe`, abrir mídia local ou URLs, argumentos, diagnóstico, build, release e operações de arquivos.
- Quando não houver equivalente PowerShell correto, substitua por descrição técnica concisa e referência ao caminho real do código; **não invente** comandos nem force uma equivalência inadequada.
- Preserve caminhos de arquivos `.cs`, nomes de tipos, métodos e interfaces na explicação arquitetural: referências não são exemplos de código-fonte.
- Preserve **formatos necessários de configuração** (INI, XML, JSON, gettext, `.conf`, comandos do mpv, esquemas e tabelas). O objetivo não é transformar configurações não-PowerShell em comandos PowerShell falsos. Use linguagem de cerca de código adequada (`powershell`, `ini`, `xml`, `text` etc.).
- Não inclua novos exemplos em C#.

## 4. Confronto com a implementação

Verifique informações relevantes nos arquivos do código, nos projetos, scripts e workflows antes de corrigir alegações sobre:
- versão de .NET/C#, SDK, Windows e arquiteturas suportadas;
- dependências `mpv/libmpv`, seleção de DLL e CPU;
- reprodução de mídias locais, playlists, streaming, `yt-dlp`, cache e fallbacks;
- integração SMTC, controles OSC, teclado, mouse, legendas e áudio;
- localização/gettext, idiomas e variantes regionais;
- configuração, logs, modo portátil e caminhos;
- builds, MSIX, Microsoft Store, scripts e releases.

Distingua funcionalidades implementadas, planejadas, testadas e não verificadas. Não presuma que uma versão, opção, idioma ou recurso esteja correto apenas por constar de outro Markdown. Registre incertezas com evidências e não fabrique testes ou resultados.

## 5. Markdown e navegação

Padronize hierarquia de títulos com um único `#` por página e níveis coerentes, listas, tabelas, links, referências, nomenclatura, cerca de código e trechos de comando. Preserve a semântica do texto e corrija links ou âncoras afetados por títulos editados. Não crie um documento novo se uma seção existente resolver a necessidade.

## 6. Fases de execução

**A — Inventário e diagnóstico:** liste arquivos e seções, duplicidades, erros linguísticos, blocos C#, discrepâncias com o código, links quebrados, riscos e severidade com evidência de caminho e seção.

**B — Revisão documentada:** corrija linguagem, clareza, estrutura, conteúdo técnico comprovadamente incorreto, redundâncias e exemplos C#; substitua por PowerShell quando viável. Evite mudanças em partes não relacionadas.

**C — Validação:** execute `git diff --check`; verifique links/âncoras locais, referências de arquivos/branches/frameworks, blocos C# ainda presentes no escopo, acentuação e codificação UTF-8. Examine sintaxe PowerShell sem executar publicação, instalações, scripts destrutivos nem comandos com efeitos externos. Registre testes impossíveis no ambiente, sem alegar execução.

**D — Relatório:** crie `docs/developer/reports/documentation-editorial-audit.md` com inventário, problemas classificados, evidências, lista de alterações, exemplos C# removidos e suas substituições, decisões de consolidação, informações essenciais preservadas, verificações e resultados, pendências, riscos e comparação qualitativa antes/depois. Não use quantidade de texto removida como único indicador de qualidade.

## Critérios de aceite

- Português brasileiro correto e terminologia consistente.
- Informação útil, completa, atual e comprovável; ausência de repetições sem valor.
- Documentação de uso e técnica sem exemplos de código-fonte C#; comandos PowerShell genuínos quando cabíveis.
- Configurações e referências arquiteturais importantes preservadas.
- Markdown, links locais e âncoras conferidos.
- Relatório final com evidências e limitações.
- **Somente arquivos de documentação alterados**. Não modifique classes C#, arquivos de projeto, dependências, traduções consumidas em runtime, binários, workflows ou scripts operacionais.
- Não faça commit, push, release nem publicação **sem pedido explícito do usuário**.

Antes de começar as alterações, registre o escopo e os principais riscos. Ao terminar, apresente o resumo das mudanças e os resultados de validação.
