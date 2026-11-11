# PROMPT MESTRE — Modernização conservadora do MPV.NET Media Player para C# 14 mantendo .NET 10 LTS

> **Execução futura pelo Codex.** Este documento é uma especificação de trabalho, não evidência de que a modernização foi realizada.
>
> Repositório: `WandersondeSouza/mpv.net` · Branch-base: `master` · Plataforma: Windows x64 · Meta: **.NET 10 + C# 14**.
>
> **Não migrar para .NET 11, não ativar C# 15 e não trocar o mecanismo mpv/libmpv.**

## 0. Papel e missão

Atue como engenheiro principal de C#/.NET, especialista em WPF/WinForms, interoperabilidade C/native, libmpv, diagnóstico de performance, qualidade de código, testes xUnit v3, segurança e distribuição Windows/MSIX.

Sua missão é modernizar **de verdade** o código existente, hoje direcionado a .NET 10 com `LangVersion=12.0`, para **C# 14**, mantendo a base estável, reduzindo avisos legítimos, código obsoleto, duplicação, fragilidade de recursos e complexidade desnecessária. Não transformar a tarefa em mera substituição de `LangVersion` ou formatação em massa.

**Prioridade:** comportamento preservado > segurança e interoperabilidade > testes > clareza e manutenção > desempenho mensurável > sintaxe nova. C# 14 é um meio, não uma meta para reescrever arquivos indiscriminadamente.

## 1. Evidências iniciais e arquivos a examinar

Antes de tocar no código, atualize a leitura do estado REAL da branch e identifique diferenças em relação aos exemplos abaixo. Consulte:
- `AGENTS.md`, `README.md`, `docs/manual.md`, `docs/guia-operacional.md`, `docs/developer/architecture.md`, `docs/developer/mpv-integration.md`, `docs/developer/windows-ui.md`, `docs/developer/build-release.md`, `docs/developer/localization.md`.
- `src/Directory.Build.props` (observado: `LangVersion=12.0`, `Nullable=enable`, `AnalysisLevel=10`, analisadores e estilo ativos).
- `src/Directory.Packages.props` (gerenciamento central de pacotes).
- `src/MpvNet/MpvNet.csproj` — biblioteca central `net10.0-windows10.0.19041.0`.
- `src/MpvNet.Windows/MpvNet.Windows.csproj` — WPF + WinForms, executável e scripts de assets.
- `src/NGettext.Wpf/NGettext.Wpf.csproj` — localização.
- `src/MpvNet.Tests/MpvNet.Tests.csproj` — xUnit v3, ArchUnitNET, cópia de libs nativas.
- `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` — exemplo de extensão.
- `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` e `Package.appxmanifest` — MSIX/Store, assinatura e identidade.
- `src/.editorconfig` — regras e exceções, incluindo CA1401, SYSLIB1054, WFO1000.
- `src/MpvNet/Native/LibMpv.cs` — DllImport, marshalling, layouts de structs e callbacks.
- `.github/workflows/dotnet-desktop.yml`, `release-packages.yml`, `codeql.yml` — automação, SDK 10.0.x e publicação.
- `.ai/prompts/` — tarefas planejadas; **não presuma que estão executadas** nem implemente prompts paralelos por acidente.

O projeto distribui libmpv normal e x86-64-v3, inclui yt-dlp, MediaInfo, OSC Lua, cache automático de streaming, configurações mpv, SMTC, várias línguas e distribuição Store/portátil/instalador. Trate essas integrações como contratos de compatibilidade.

## 2. Restrições inegociáveis

1. Preservar `TargetFramework=net10.0-windows10.0.19041.0` para os projetos atuais; preservar Windows 10 build 19041+ e x64. **Não** introduzir dependência de APIs exclusivas do .NET 11 ou Windows 11.
2. Alterar a configuração explícita de linguagem para **`<LangVersion>14.0</LangVersion>`**, somente após confirmar que o SDK .NET 10 instalado suporta C# 14, e validar todos os projetos, inclusive extensão e pacote.
3. Preferir compilador e recursos estáveis. Proibido `preview`, experimentais, C# 15, `NoWarn` genérico, ou remover verificações de qualidade para obter compilação verde.
4. Preservar comportamento público: CLI, extensões, arquivos de configuração e migrações, opções nativas do mpv, atalhos, localização em tempo real, fullscreen, menu de contexto, tema claro/escuro, DPI, SMTC, associação de arquivos, playlists, yt-dlp e streaming.
5. Proibido reescrever o subsistema libmpv, trocar mpv por outro player, introduzir downloader/reconnect watchdog concorrente, ou modificar política de cache sem demanda e provas. Preservar `AutomaticNetworkCache`, precedência de configurações nativas, hooks e `LiveStreamRecoveryPolicy`.
6. Não introduzir alterações globais de formatação, renomeações sem finalidade, novas camadas artificiais ou abstrações sem consumidores. Não converter automaticamente todos os `DllImport` em `LibraryImport`.
7. Não tocar em certificados, credenciais, secrets, identidade do pacote, Publisher, assinatura ou versões comerciais sem necessidade comprovada; não publicar release na Store nem GitHub sem autorização separada.
8. Respeitar `AGENTS.md` e documentação. Se houver conflito de requisitos, documente e escolha a opção mais conservadora.

## 3. Etapa A — Auditoria e baseline ANTES da refatoração

Produza relatório inicial rastreável, incluindo:
- SDK/compilador e `dotnet --info`, `dotnet --list-sdks`; build Debug/Release com configuração Windows x64; `dotnet test` ou comando xUnit v3 apropriado conforme o projeto.
- Inventário de targets, pacotes, APIs depreciadas/obsoletas, avisos por ID e arquivo, warnings de nullable, async, análise, segurança, possíveis supressões injustificadas.
- Inspeção de arquivos grandes/classes extensas, responsabilidades misturadas, duplicação real (incluindo métodos utilitários semelhantes), código inacessível/não referenciado, exceções engolidas, `async void` fora de eventos, bloqueios síncronos, `Task.Run` e `ConfigureAwait` inadequados.
- Ciclo de vida de recursos: handles libmpv, `Dispose`, `IAsyncDisposable` onde cabível, streams, timers, assinaturas de eventos, WPF/WinForms dispatcher, cancelamento de operações, callbacks e shutdown.
- Benchmark objetivo disponível: cold/warm startup, abrir arquivo local, playlist grande, troca de mídia, consumo gerenciado/nativo, fechamento e streams offline/online quando o ambiente permitir. Registre ambiente e metodologia; não invente números.
- Lista de pontos em que C# 14 traz benefício concreto, distinguindo mudanças de sintaxe de melhorias semânticas. **Não** forçar uso de todos os recursos da linguagem.
- Evidência de cobertura existente, lacunas de testes e riscos por área; categorizar P0/P1/P2 com impacto, confiança e custo estimado relativo.
- Conferir código implementado versus prompts existentes de `.ai/prompts/`; não duplicar atividades planejadas.

Salve o baseline em `docs/developer/reports/csharp14-modernization-baseline.md` (ou diretório de relatórios já padronizado, se existir). Se o ambiente não puder rodar aplicações GUI/nativas, indique explicitamente o que ficou **não validado**, sem afirmar sucesso.

## 4. Etapa B — Configuração segura de linguagem e tooling

- Manter SDK 10 e TFM 10; fixar ou documentar SDK compatível (usar `global.json` só se reduzir deriva sem quebrar CI local). Trocar `LangVersion` de 12.0 para 14.0 no ponto central adequado; conferir herança por todos os `.csproj`.
- Preservar `Nullable=enable`, `EnableNETAnalyzers=true`, `EnforceCodeStyleInBuild=true`. Revisar `AnalysisLevel` compatível com SDK 10 e documentar escolha. Não confundir `AnalysisLevel` e `LangVersion`.
- Revisar `.editorconfig` progressivamente, sem transformar milhares de avisos históricos em bloqueio inesperado; criar baseline para avisos preexistentes e regra **zero novos avisos** em arquivos modificados, salvo justificativa explícita.
- Avaliar NuGet/Dependabot: atualizar somente dependências compatíveis com .NET 10 após análise de changelog e testes; não atualizar tudo indiscriminadamente. Verificar xUnit v3, ArchUnitNET, CommunityToolkit.Mvvm, NGettext, Microsoft.Xaml.Behaviors.Wpf e ferramentas Windows SDK.
- Workflow CI deve continuar com `dotnet-version: 10.0.x`; assegurar restore/build/test, CodeQL e pipeline de release preservados. Não alterar workflows de publicação por estética.
- Conferir que `dotnet format --verify-no-changes` ou regras equivalentes podem ser executadas com escopo claro; evite diffs de estilo globais.

## 5. Etapa C — Refatoração orientada a valor

Trabalhe em lotes pequenos, independentes e reversíveis; para cada mudança: problema comprovado → arquivos → alternativa mínima → riscos → testes → resultados.

### C1. C# 14 e práticas modernas
Aplicar recursos compatíveis com C# 14 somente quando aumentarem legibilidade, segurança ou diminuírem duplicação. Considerar (quando adequados) melhor tratamento de nullability, pattern matching, expressões de coleção, membros de extensão, melhorias de propriedades e outras funcionalidades estáveis suportadas. Evitar alterar APIs públicas ou serialização/configurações apenas para mostrar nova sintaxe. Revisar suporte a compilador/geradores/IDE, inclusive bindings WPF.

### C2. Obsolescência e warnings
Localizar `[Obsolete]`, avisos SYSLIB/CA/IDE/CS, APIs do Windows e SDK, e usos de interop potencialmente frágeis. Para cada caso, separar:
- substituição segura com equivalência comprovada;
- supressão específica e documentada tecnicamente (incluindo `SYSLIB1054`, `CA1401` e as supressões locais de `LibMpv.cs`);
- incompatibilidade de terceiro, ainda pendente, com mitigação.
Não apagar enumeração/eventos nativos marcados como deprecated sem confirmar compatibilidade com versões libmpv e seus consumidores. Nunca mascarar warnings no nível do repositório.

### C3. Código duplicado, morto e responsabilidades
Identificar duplicação verificável; unificar apenas se tiver comportamento equivalente. Confirmar referências por código, reflection, XAML, comandos do mpv, scripts Lua, extensões e serialização antes de remover membros supostamente não usados. Preferir serviços pequenos/coerentes e DTOs quando justificáveis; não redesenhar arquitetura inteira.

### C4. Assincronismo e recursos
Rever cancelamento, concorrência, exceções, retries, timeout, `HttpClient`, streams, operações de disco, UI thread, `Dispatcher`, timers e shutdown. Evitar `async void` exceto event handlers apropriados. Melhorar descartes, ordem de desligamento e remoção de eventos; não causar deadlock, race condition, vazamento ou fechamento lento. Instrumentar e provar mudanças relevantes.

### C5. Native / libmpv — tratamento especial
Mapear todas as entradas `DllImport`, assinaturas, `CallingConvention.Cdecl`, `StructLayout`, offsets, strings UTF-8, ponteiros e ownership das alocações; revisar callbacks, `GCHandle`, `SafeHandle` e `mpv_*free*` caso presentes. **Não** converter em massa para `LibraryImport`; somente mudanças pontuais com prova de ABI e testes de execução nativa, x64 normal/v3, ciclos de inicialização/encerramento e falha de carregamento da DLL. Validar comportamento com CPU sem AVX2/x86-64-v3 quando houver equipamento/ambiente real. Se não houver, registrar bloqueio.

### C6. UI Windows, i18n e mídia
Preservar WPF, WinForms, SMTC, ícones, drag/drop, DPI, múltiplos monitores, tela cheia, atalhos, OSC, feedback visual, comandos de janela, player de áudio/vídeo, seleção de legenda/faixa e localização de strings. Não introduzir nova biblioteca de UI sem justificativa e aceite. Verificar arquivos `.po/.mo`, troca de idioma e ausência de textos não traduzidos criados pela refatoração. Proteger características de streams ao vivo, YouTube/yt-dlp e playlists.

## 6. Etapa D — Testes obrigatórios e portas de qualidade

Executar o máximo possível no Windows x64; acrescentar testes para comportamentos alterados e não reescrever testes só para fazê-los passar.

**Automatizados**:
- Compilar solução/projetos Debug e Release; validar projeto de extensão.
- Executar a suíte xUnit v3 e regras ArchUnitNET. Não afirmar `dotnet test` como único caminho se o projeto exigir `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj ...`.
- Verificar testes existentes: `LibMpvInteropTests`, `LibMpvRuntimeTests`, `MpvClientReliabilityTests`, `ResourceLifecycleTests`, `NetworkCachePolicyTests`, `NetworkCacheSessionTests`, `NetworkCacheNativeTests`, `LiveStreamRecoveryPolicyTests`, `StreamingFailureDiagnosticsTests`, `MediaTransportTests`, `ArchitectureTests`, testes de localização/SMTC/inputs e novas áreas atingidas.
- Conferir versões de assets nativos, `libmpv-2.dll` e `libmpv-2-v3.dll`, `MediaInfo.dll`, `osc.lua`, catálogos de tradução e integridade da distribuição.
- Executar validação estática, análise de segurança, dependências, arquivo formatado e `git diff --check`.

**Manuais/smoke tests quando houver interface**:
1. Arquivos locais MP4/MKV/MP3/FLAC; legendas externas e internas; faixas áudio.
2. M3U/M3U8/URLs HTTP, HLS/VOD e live quando disponíveis; cache automático com precedência de mpv.
3. YouTube/yt-dlp em diversos cenários, com atenção a vídeo congelado enquanto áudio continua; não atribuir correção sem evidência.
4. Play/pause, seek, próxima/anterior, atalhos, clique simples/duplo, fullscreen, OSC, menu contexto, volume.
5. Tema claro/escuro, DPI, multi-monitor, drag-and-drop, SMTC e taskbar.
6. Idiomas e alternância dinâmica, Unicode, configuração persistida e configuração legada.
7. Carregamento nativo, recuperação de falha, encerramento, múltiplos ciclos e ausência de crescimento de memória anômalo.
8. Instalação/atualização Store MSIX, pacote portátil e instalador quando o ambiente e autorização permitirem. Apenas construir e validar, **não publicar**.

**Critérios de aceite**:
- Todos os projetos relevantes continuam em .NET 10, compilam com C# 14 estável; nada em .NET 11/C# 15/preview.
- Nenhuma alteração comportamental não aprovada; APIs, configurações, CLI, arquivos e conteúdo de pacote preservados.
- Testes existentes passam ou há bloqueio/defeito anterior documentado com evidência; novos testes adicionados para regressões.
- Nenhum aviso novo sem justificativa; diminuição mensurável de warnings obsoletos/duplicação/defeitos na área modificada.
- Sem regressão material de startup/memória/latência nas medições comparáveis; declarar limitações se benchmarks indisponíveis.
- Libmpv normal/v3, Store/MSIX e idiomas sem mudanças inesperadas.
- Baseline e relatório final com evidências, comandos, resultados, riscos residuais e instruções de rollback.

## 7. Estratégia de commits e entrega

1. Criar branch de trabalho dedicada, por exemplo `codex/modernize-csharp14-net10`, a partir da `master` atual; não fazer commit direto em `master` durante a execução deste prompt.
2. Fazer commits pequenos por etapa/área com mensagens objetivas. Não combinar upgrade de tooling, grande refatoração, native e UI em um único commit.
3. Antes de cada commit: revisar diff, rodar verificações pertinentes, registrar resultado. Não incluir binários gerados, caches, arquivos temporários, segredos nem artefatos de teste.
4. Abrir PR para `master` com resumo, tabela antes/depois, testes, riscos e checklist. Não mesclar nem publicar release automaticamente.
5. Relatório final sugerido: `docs/developer/reports/csharp14-modernization-result.md` (adapte à convenção existente).
6. Se o trabalho for extenso ou o ambiente não possuir ferramentas Windows, complete com segurança etapas independentes, deixe pendências explícitas e não simule execução bem-sucedida.

## 8. Formato obrigatório da resposta final do Codex

Apresentar, em português do Brasil:
1. Estado inicial confirmado (TFM, LangVersion, SDK, projetos e diferenças da premissa).
2. Alterações por etapa, citando arquivos e motivo.
3. Matriz de avisos antes/depois por ID e total; métricas de cobertura/duplicação/performance **somente se realmente medidas**.
4. Resultado dos comandos de build/test/analyzers/smoke, sistema operacional e limitações.
5. Riscos residuais, especialmente libmpv, WPF, CPU x64, yt-dlp, cache e Store.
6. Branch, commits e link do PR; pendências para aprovação humana.
7. Garantia verificável de que o target **permanece .NET 10** e a linguagem está em **C# 14**, não C# 15.

**Comece pela auditoria e pelo baseline. Não declare sucesso da modernização sem as evidências de compilação/testes e comparação antes/depois.**
