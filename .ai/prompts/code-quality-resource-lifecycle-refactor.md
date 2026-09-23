# Refatoração profunda de qualidade, recursos e lifecycle do MPV.NET

Você está trabalhando no repositório **WandersondeSouza/mpv.net**, projeto **MPV.NET Media Player**.

## OBJETIVO

Executar uma refatoração profissional, profunda e segura do projeto, visando principalmente:

- eliminar código redundante;
- eliminar código realmente morto ou classes/métodos/propriedades realmente sem uso;
- eliminar recursos não descartados;
- impedir arquivos, streams, logs, processos ou handles de permanecerem bloqueados;
- melhorar gerenciamento de processos externos;
- melhorar lifecycle de libmpv e componentes nativos;
- melhorar gerenciamento de tarefas assíncronas;
- melhorar tratamento de erros;
- eliminar catches indevidamente silenciosos;
- reduzir possibilidades de memory leak;
- eliminar event handlers que sobrevivam ao objeto proprietário;
- garantir shutdown limpo;
- preservar compatibilidade funcional;
- fortalecer testes automatizados;
- deixar o projeto pronto para produção.

Esta tarefa NÃO consiste em reescrever a aplicação nem em alterar comportamento funcional sem necessidade.

Primeiro diagnostique. Depois refatore.

## 1. PREPARAÇÃO

Antes de alterar qualquer arquivo:

1. atualizar a branch `main`;
2. verificar que o working tree está limpo;
3. executar restore;
4. executar build completo;
5. executar todos os testes existentes;
6. registrar o baseline;
7. criar uma branch dedicada, por exemplo:

`refactor/code-quality-resource-lifecycle`

Nunca trabalhar diretamente na `main`.

Leia também:

- `AGENTS.md`
- `.editorconfig`
- `Directory.Build.props`
- `Directory.Packages.props`
- projetos da solution
- workflows GitHub Actions
- testes existentes

Respeite integralmente as convenções atuais do repositório.

## 2. MAPEAR A ARQUITETURA ANTES DE MODIFICAR

Analise completamente:

- `src/MpvNet`
- `src/MpvNet.Windows`
- `src/MpvNet.Extension`
- `src/NGettext.Wpf`
- `src/MpvNet.Tests`
- scripts relacionados ao build/runtime
- integração libmpv
- integração yt-dlp
- MediaInfo
- SMTC
- WinForms
- WPF
- IPC
- extensões
- runtime components
- configuração
- playlists
- streaming
- arquivos temporários
- logging.

Monte internamente uma matriz:

`recurso -> proprietário -> criação -> utilização -> cancelamento -> Dispose/Close/Destroy -> teste`

Nenhum recurso deve ser modificado sem conhecer seu owner.

## 3. CÓDIGO MORTO E REDUNDÂNCIA

Identifique:

- classes não referenciadas;
- interfaces sem implementações úteis;
- métodos privados nunca chamados;
- propriedades sem consumidores;
- helpers duplicados;
- wrappers antigos;
- código `[Obsolete]`;
- caminhos impossíveis;
- campos nunca lidos;
- branches permanentemente mortas;
- abstrações substituídas por versões novas.

Use:

- análise do compilador;
- IDE/analyzers;
- busca de referências;
- reflexão utilizada pelo projeto;
- XAML;
- extensões;
- P/Invoke;
- chamadas dinâmicas;
- serialização;
- nomes usados pelo mpv/Lua;
- API pública.

NÃO considere código morto apenas porque uma busca textual não encontra referências.

Código público, reflection, XAML, COM, scripts Lua e extensões podem depender dele.

## 4. CANDIDATOS OBSOLETOS JÁ PRESENTES

### TaskHelp

Arquivo: `src/MpvNet/Infrastructure/BackgroundTaskRunner.cs`

Existe `[Obsolete] public static class TaskHelp`.

Determine se ainda existe consumidor. Se não houver compatibilidade externa necessária:

- migrar referências restantes;
- remover.

Se for mantido por compatibilidade:

- documentar claramente o motivo;
- não duplicar implementação.

### ExtensionLoader

Arquivo: `src/MpvNet/Services/ExtensionLoader.cs`

`ExtensionLoader` é uma fachada `[Obsolete]` para `ExtensionService`.

Determinar se alguma extensão externa pode depender publicamente desse tipo.

Não quebrar compatibilidade binária/public API sem justificar.

## 5. AUDITORIA DE IDisposable / IAsyncDisposable

Faça uma análise completa de toda criação de objetos que implementem:

- `IDisposable`
- `IAsyncDisposable`.

Investigar especialmente:

- Stream
- FileStream
- StreamReader
- StreamWriter
- TextWriter
- TextReader
- XmlReader
- XmlWriter
- JsonDocument
- Process
- RegistryKey
- Timer
- CancellationTokenSource
- SemaphoreSlim
- ReaderWriterLockSlim
- AutoResetEvent
- ManualResetEvent
- WaitHandle
- HttpClient quando houver ownership local
- SafeHandle
- bitmap/icon/image
- COM wrappers
- MediaInfo
- objetos WinRT/SMTC quando aplicável.

Para cada ocorrência, determinar quem é o owner.

Aplicar `using`, `await using`, `Dispose` ou lifecycle explícito conforme apropriado.

Nunca adicionar `Dispose` apenas para satisfazer analyzer se o objeto não pertence à classe.

## 6. CORREÇÃO PRIORITÁRIA — DEBUG LOG FILE LOCK

Investigar `src/MpvNet/App.cs`.

Hoje, no DebugMode, há criação semelhante a:

`Trace.Listeners.Add(new TextWriterTraceListener(filePath));`

O listener não deve ficar sem ownership explícito.

Refatorar para que:

- o listener tenha owner claro;
- exista uma referência armazenada;
- inicialização seja idempotente;
- nenhum listener duplicado seja criado;
- no shutdown seja executado o flush necessário;
- seja removido de `Trace.Listeners`;
- seja descartado;
- o arquivo seja liberado imediatamente;
- uma nova execução no mesmo processo consiga excluir/substituir o arquivo;
- shutdown excepcional também tente liberar o listener.

Adicionar teste comprovando que depois do encerramento o arquivo pode ser:

- aberto com `FileShare.None`;
- renomeado;
- ou excluído.

Não deixar `MpvNet-debug.log` preso pelo player.

## 7. MAINPLAYER — RECURSOS DE SINCRONIZAÇÃO

Revisar `src/MpvNet/Integration/Mpv/Player.State.cs`.

Existem recursos como:

- `_playerCancellation`
- `_playerTaskGate`
- `ShutdownAutoResetEvent`
- `_playlistNormalizationDebounce`.

Revisar lifecycle completo.

O `Destroy()` já:

- marca destroyed;
- cancela;
- aguarda tasks;
- destrói clients;
- destrói handles libmpv.

Após garantir que nenhuma task usa esses recursos, determinar o local seguro para:

- Dispose do CancellationTokenSource;
- Dispose do SemaphoreSlim;
- Dispose do AutoResetEvent;
- Dispose de outros recursos similares.

`Destroy()` deve permanecer:

- seguro;
- idempotente;
- thread-safe.

Executar `Destroy()` duas vezes não deve lançar exceção.

Não descartar recursos enquanto alguma task puder utilizá-los.

## 8. MPVCLIENT — ReaderWriterLockSlim

Revisar `src/MpvNet/Integration/Mpv/MpvClient.cs`.

Existe `ReaderWriterLockSlim _nativeLifetimeGate`.

A solução atual protege corretamente o handle nativo durante shutdown.

Preservar essa garantia.

Entretanto, analisar o descarte do `ReaderWriterLockSlim`.

Somente descartá-lo quando:

- `BeginShutdown()` já impedir novas operações;
- todas as operações existentes tiverem terminado;
- nenhum caminho puder entrar novamente.

Se necessário:

- implementar lifecycle explícito;
- integrar ao `DestroyHandle`;
- ou adotar `IDisposable` internamente.

Evitar:

- ObjectDisposedException durante shutdown;
- corrida entre EventLoop e Destroy;
- deadlock;
- double Dispose.

Adicionar testes de concorrência/lifecycle.

## 9. JSONDOCUMENT

Pesquisar TODAS as chamadas `JsonDocument.Parse(...)`.

Há ocorrências que acessam diretamente `JsonDocument.Parse(json).RootElement...` sem manter explicitamente o documento.

Converter, onde necessário, para algo semelhante a:

`using JsonDocument document = JsonDocument.Parse(json);`

e utilizar `document.RootElement`.

Verificar pelo menos:

- lista de dispositivos de áudio;
- leitura da playlist;
- qualquer diagnóstico/runtime component que use JSON.

Adicionar testes existentes ou novos para os caminhos modificados.

## 10. FILE HANDLES E FILE LOCKS

Auditar TODAS as operações:

- `File.Open`
- `File.OpenRead`
- `File.OpenWrite`
- `File.Create`
- `FileStream`
- `StreamReader`
- `StreamWriter`
- `TextWriterTraceListener`
- XML
- JSON
- arquivos temporários.

Verificar se algum objeto mantém handle aberto após sua utilização.

O projeto já possui `RuntimeComponentFileSystem.IsFileLocked`.

Aproveitar esta infraestrutura para testes.

Criar testes específicos garantindo que arquivos envolvidos em:

- settings;
- logs;
- playlists temporárias;
- runtime components;
- config;
- input.conf;
- downloads temporários

não permaneçam bloqueados depois que a operação termina.

## 11. ESCRITA ATÔMICA

O projeto já possui padrões corretos como `FileHelp.WriteAllTextAtomic` e escrita temporária de settings.

Não criar implementações concorrentes da mesma ideia.

Pesquisar duplicações de:

1. criar `.tmp`;
2. escrever;
3. mover;
4. excluir em erro.

Se houver abstração realmente compartilhável, consolidar.

Mas não generalizar prematuramente operações com semânticas diferentes.

## 12. PROCESSOS EXTERNOS

Auditar todas as ocorrências de:

- `new Process`
- `Process.Start`
- `Process.GetProcessesByName`
- `ProcessStartInfo`
- stdout/stderr redirect;
- WaitForExit;
- Kill.

`ProcessHelp.Execute()` já usa `using Process`.

Preservar esse comportamento se estiver correto.

Verificar, entretanto, todos os demais pontos do projeto.

Em especial, `Process.GetProcessesByName("mpvnet")` retorna objetos `Process` descartáveis.

Garantir o descarte de cada instância retornada após a utilização.

Não deixar handles de processos Windows pendurados.

Para processos cujo stdout/stderr for redirecionado:

- consumir ambos corretamente;
- evitar deadlocks;
- suportar CancellationToken quando aplicável;
- matar árvore de processos apenas quando ownership for realmente da aplicação.

## 13. HANDLES NATIVOS

Auditar:

- libmpv handles;
- Win32 handles;
- LoadLibrary;
- registry;
- taskbar;
- COM;
- icons;
- bitmaps.

Não substituir P/Invoke estável sem necessidade.

Quando ownership existir, preferir `SafeHandle` se isso simplificar e tornar seguro.

## 14. AVISYNTH

Revisar `Player.MediaLoading.cs`.

`LoadAviSynth()` executa `LoadLibrary`.

Atualmente `_wasAviSynthLoaded` impede carregamento repetido, mas o handle retornado não é mantido.

Determinar semanticamente se a DLL deve:

A. permanecer carregada durante toda a vida do processo;

ou

B. ser descarregada durante Destroy.

Se A for obrigatório, documentar explicitamente esse process-lifetime ownership.

Se B for seguro, armazenar o handle usando um padrão seguro e fazer `FreeLibrary` no lifecycle correto.

Não chamar `FreeLibrary` se o mpv/AviSynth ainda puder executar código da DLL.

## 15. COM — WScript.Shell

Revisar `GetShortcutTarget()`, que utiliza `WScript.Shell` via COM/dynamic.

Analisar os RCWs envolvidos:

- Shell;
- Shortcut.

Garantir que não exista retenção desnecessária de recursos COM.

NÃO aplicar `Marshal.FinalReleaseComObject` de forma automática.

Somente utilizar liberação explícita se ownership e segurança estiverem comprovados.

Se uma alternativa moderna e simples sem COM existir e for compatível, avaliar.

## 16. EVENT HANDLERS

Criar uma auditoria de todos os `+=` e seus respectivos `-=`.

Especial atenção para:

- eventos estáticos;
- singleton;
- StrongReferenceMessenger;
- Player;
- GUI commands;
- SMTC;
- timers;
- WPF;
- WinForms;
- extensions.

O `MainForm.DisposeManagedResources()` já contém bom padrão de limpeza.

Preservá-lo e utilizá-lo como referência.

Não adicionar unsubscribe desnecessário em objetos com lifecycle idêntico.

## 17. STRONGREFERENCEMESSENGER

Investigar registros em `StrongReferenceMessenger.Default.Register(...)`.

Há registro em `AppClass`.

Determinar lifecycle do destinatário.

Se o objeto viver exatamente durante todo o processo, documentar.

Caso contrário, fazer `Unregister` no lifecycle apropriado.

Garantir que nenhuma janela, viewmodel ou serviço de curta duração seja mantido vivo pelo messenger.

## 18. EXTENSION SERVICE

Revisar `ExtensionService`.

Hoje assemblies são carregados com `Assembly.LoadFile` e instâncias são guardadas em `_refs`.

Determinar explicitamente a política:

- extensões vivem durante toda a execução;
- extensões podem ser descarregadas.

Se process-lifetime for deliberado, documentar.

Não tentar descarregar assemblies carregados no `AssemblyLoadContext.Default`.

Se hot reload/unload não fizer parte da aplicação, não introduzir complexidade desnecessária.

Melhorar tratamento de:

- assembly inválido;
- nenhum tipo IExtension;
- múltiplos tipos IExtension;
- ReflectionTypeLoadException;
- dependência ausente.

Não deixar uma extensão quebrada derrubar o player.

## 19. BACKGROUNDTASKRUNNER

Revisar `BackgroundTaskRunner`.

O overload assíncrono inicia `RunAsync(...)` sem aguardar o Task.

Isso parece ser fire-and-forget deliberado.

Tornar a intenção explícita.

Garantir:

- captura de exceções;
- CancellationToken;
- shutdown;
- nenhuma UnobservedTaskException;
- nenhuma task importante sobrevivendo ao encerramento.

Quando uma tarefa fizer parte do lifecycle de um objeto, preferir tracking semelhante ao usado em `MainPlayer` em vez de fire-and-forget global.

Não transformar tudo em `await` se isso bloquear UI/startup.

## 20. THREAD.SLEEP

Pesquisar `Thread.Sleep`.

Onde estiver em UI/background orchestration, avaliar substituição por `Task.Delay` com CancellationToken.

Exemplo conhecido:

`MainForm.OnActivated` usa BackgroundTaskRunner + `Thread.Sleep(200)`.

Verificar possibilidade de fazer delay assíncrono cancelável, preservando acesso ao UI thread.

Não mudar código apenas por estética se a mudança trouxer risco de race condition.

## 21. TRATAMENTO DE ERROS

Classificar exceções em:

### recuperáveis

Exemplo:

- arquivo temporariamente indisponível;
- metadata inválida;
- integração opcional indisponível;
- SMTC indisponível.

Registrar contexto suficiente e continuar.

### fatais

Exemplo:

- falha ao criar libmpv;
- inicialização impossível;
- invariantes fundamentais quebradas.

Não esconder.

### cancelamento

`OperationCanceledException` esperada não deve ser registrada como erro.

### bugs

Não capturar `Exception` apenas para ignorar um defeito de programação.

## 22. CATCH SILENCIOSO

Pesquisar TODOS `catch { }` e `catch (Exception)` sem ação útil.

Não aplicar substituição automática.

Há uma exceção importante:

o writer do próprio logger pode precisar falhar silenciosamente para impedir:

`logger -> erro -> logger -> erro -> recursão infinita`.

Nesses pontos, documentar claramente o fail-safe.

Nos demais, decidir entre:

- tratamento específico;
- logging;
- propagação;
- remoção do catch.

## 23. LOGGING

Revisar a infraestrutura de `Log`.

Preservar `Log.SafeValue` e proteção de dados sensíveis.

Não registrar:

- tokens;
- cookies;
- Authorization;
- credenciais;
- URLs completas com query sensível.

Avaliar se `File.AppendAllText` por mensagem continua adequado.

O objetivo principal NÃO é criar um framework de logging novo.

Evitar manter streams de log persistentemente abertos se isso voltar a criar file locks.

## 24. REDUNDÂNCIA

Pesquisar lógica repetida em:

- FileHelp;
- RuntimeComponentFileSystem;
- SettingsStore;
- temporary cleanup;
- config parsing;
- process execution;
- logging;
- playlist normalization;
- media loading;
- localization.

Somente extrair código quando houver:

- mesma responsabilidade;
- mesma regra;
- mesma semântica.

Evitar criar helpers genéricos gigantes.

## 25. CLASSES GRANDES

Analisar particularmente:

- `MainForm.cs`
- `MpvClient.cs`
- `Player.*`
- `GuiCommand.cs`
- janelas WPF maiores.

Já existem partial classes em algumas áreas.

Não fragmentar arquivos apenas para diminuir número de linhas.

Separar apenas responsabilidades claras.

## 26. ANALYZERS

Verificar quais analyzers estão ativos.

Avaliar habilitar/reforçar regras relacionadas a:

- disposal;
- async;
- nullability;
- dead code;
- unused members;
- exception handling;
- interop;
- performance.

Em especial avaliar regras equivalentes a:

- CA2000
- CA2213
- CA1816
- CA1001
- CA2016
- CA1849
- CA1851

Não ativar uma avalanche de warnings sem tratar corretamente o projeto.

Se uma regra não for aplicável, justificar suppression localmente.

Nunca desligar globalmente uma regra só para deixar o build verde.

## 27. TESTES OBRIGATÓRIOS

Expandir `MpvNet.Tests`.

Criar testes para, no mínimo:

1. debug Trace listener libera arquivo;
2. settings não deixam arquivo aberto;
3. atomic write não deixa `.tmp`;
4. JsonDocument lifecycle atualizado continua funcionando;
5. RuntimeComponent file lock;
6. cancellation de tasks;
7. Destroy idempotente;
8. Destroy aguarda background tasks;
9. eventos não executam depois do Dispose;
10. lifecycle do SMTC quando possível sem depender de sessão real;
11. BackgroundTaskRunner captura exceção;
12. cleanup de temporários;
13. processo/handles onde testável;
14. playlist normalization debounce;
15. shutdown concorrente do MpvClient.

Para testes de arquivo, sempre tentar abrir após a operação com `FileShare.None` quando apropriado.

## 28. TESTES DE ARQUITETURA

Aproveitar ArchUnitNET já utilizado pelo projeto.

Adicionar regras úteis, apenas quando forem estáveis.

Exemplos:

- Native não depender de UI;
- Infrastructure não depender de WinForms/WPF;
- Core não depender do assembly Windows;
- evitar dependências circulares entre camadas.

Não criar regras artificiais apenas para aumentar quantidade de testes.

## 29. TESTE DE REGRESSÃO FUNCIONAL

Validar que continuam funcionando:

- abrir arquivo local;
- abrir múltiplos arquivos;
- playlist;
- URLs;
- YouTube/yt-dlp;
- playlists do YouTube;
- streaming HTTP/HLS;
- subtitles;
- MediaInfo;
- atalhos `.lnk`;
- file association;
- single instance;
- queue instance;
- IPC WM_COPYDATA;
- fullscreen;
- taskbar;
- SMTC;
- idiomas;
- configurações;
- debug mode;
- extensões;
- runtime component bootstrap;
- shutdown.

Não alterar comportamento funcional sem necessidade.

## 30. BUILD E TESTES

Executar:

`dotnet restore`

`dotnet build src/MpvNet.sln -c Debug`

`dotnet build src/MpvNet.sln -c Release`

`dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --configuration Debug --no-restore`

`dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --configuration Release --no-restore`

ou comandos equivalentes corretos para a solution.

Todos devem terminar sem erro.

Verificar warnings novos.

Não considerar concluído com warnings introduzidos pela refatoração.

## 31. VERIFICAÇÃO DE FILE LOCK REAL

Criar um teste/manual smoke test específico:

1. iniciar player;
2. ativar debug quando possível;
3. gerar escrita de log;
4. fechar player normalmente;
5. confirmar término das tasks;
6. confirmar liberação de resources;
7. tentar renomear/excluir logs/config/temp;
8. iniciar novamente;
9. confirmar ausência de sharing violation.

Repetir ciclos de abrir/fechar várias vezes.

O player não deve impedir:

- rebuild;
- update;
- troca de versão;
- exclusão de temporário;
- atualização de componente.

## 32. VERIFICAÇÃO DE MEMÓRIA E HANDLES

Quando possível executar teste de stress:

- abrir/fechar mídia repetidamente;
- trocar playlist;
- trocar idioma;
- usar context menu;
- ativar/desativar SMTC;
- reproduzir URL;
- fechar/reabrir player.

Observar:

- handle count;
- threads;
- memória privada;
- arquivos abertos;
- processos filhos.

Não é obrigatório atingir zero crescimento absoluto devido a caches/runtime, mas crescimento continuamente monotônico deve ser investigado.

## 33. NÃO QUEBRAR LIBMPV

A camada de lifecycle do libmpv possui sincronização importante.

Preservar:

- `BeginShutdown`;
- bloqueio de novas operações;
- espera das operações existentes;
- event loops;
- `mpv_destroy`;
- `mpv_terminate_destroy`;
- client handles.

Qualquer modificação nessa região deve possuir testes.

Nunca liberar handle enquanto outro thread puder executar chamada nativa.

## 34. NÃO QUEBRAR YT-DLP

Esta refatoração NÃO pode desfazer o trabalho recente de modernização do yt-dlp.

Preservar:

- resolução de `yt-dlp.exe`;
- runtime components;
- `ytdl-path`;
- playlists do YouTube;
- JS runtime;
- atualização de componente;
- fallback;
- diagnósticos.

Executar testes existentes relacionados a online media/runtime components.

## 35. DOCUMENTAÇÃO

Ao concluir, criar ou atualizar documentação técnica curta contendo:

- resources cujo lifetime é de processo;
- resources cujo lifetime é do player;
- resources cujo lifetime é da janela;
- regra de cancellation;
- regra de file ownership;
- regra de event subscription;
- regra de native handles.

Evitar documentação redundante.

## 36. COMMITS

Não produzir um commit gigantesco se houver divisões naturais.

Sugestão de sequência:

- `test: add resource lifecycle regression coverage`
- `refactor: harden disposable and native resource lifecycle`
- `refactor: remove verified dead and redundant code`
- `fix: release trace and file resources on shutdown`
- `refactor: improve async and exception handling`
- `test: expand shutdown and file locking coverage`

Ajuste os commits aos trabalhos realmente realizados.

Cada commit deve:

- compilar;
- ser coerente;
- não misturar mudanças sem relação.

## 37. REVISÃO FINAL DO DIFF

Antes do push:

- revisar `git diff`;
- revisar `git diff --check`;
- verificar arquivos acidentalmente modificados;
- verificar formatação;
- verificar código morto criado pela própria refatoração;
- verificar TODO temporário;
- verificar debug code;
- verificar dados sensíveis;
- verificar bin/obj;
- verificar arquivos temporários;
- verificar que nenhuma dependência desnecessária foi adicionada.

## 38. CRITÉRIOS DE ACEITE

A tarefa só está concluída se:

- build Debug passar;
- build Release passar;
- todos os testes passarem;
- novos testes de lifecycle passarem;
- nenhum arquivo permanecer bloqueado indevidamente;
- nenhum processo próprio ficar órfão;
- resources descartáveis tiverem ownership claro;
- libmpv shutdown permanecer thread-safe;
- nenhum regression do yt-dlp;
- nenhum regression de SMTC;
- código morto confirmado for removido;
- duplicações justificáveis forem reduzidas;
- catches indevidos forem corrigidos;
- não houver novas exceções engolidas;
- não houver warnings novos relevantes;
- comportamento público permanecer compatível.

## 39. COMMIT E PUSH

Depois de TODAS as validações:

1. confirmar branch correta;
2. criar os commits finais;
3. executar novamente a bateria completa de testes;
4. fazer push da branch para o `origin`.

Não fazer merge automático na `main`.

Se o repositório utiliza pull requests, criar PR após o push com:

- resumo das mudanças;
- problemas encontrados;
- resources corrigidos;
- código morto removido;
- testes adicionados;
- comandos executados;
- resultados;
- riscos restantes.

## 40. RELATÓRIO FINAL OBRIGATÓRIO

Ao finalizar, apresentar:

- branch criada;
- arquivos principais modificados;
- código morto encontrado;
- código removido;
- redundâncias eliminadas;
- disposables corrigidos;
- handles nativos corrigidos;
- file locks encontrados;
- file locks corrigidos;
- tratamento de erros alterado;
- testes adicionados;
- resultado Debug;
- resultado Release;
- resultado do harness `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-restore`;
- commits criados;
- SHA do último commit;
- branch enviada;
- URL do PR, se criado.

Também listar qualquer item que tenha sido analisado mas deliberadamente NÃO modificado, explicando tecnicamente o motivo.

## REGRA PRINCIPAL

**Não faça refatoração cosmética em massa.**

Cada alteração deve resolver um problema comprovado de:

- qualidade;
- redundância;
- lifecycle;
- estabilidade;
- segurança;
- manutenção;
- performance;
- testabilidade.

Preserve o comportamento estável existente do MPV.NET Media Player.
