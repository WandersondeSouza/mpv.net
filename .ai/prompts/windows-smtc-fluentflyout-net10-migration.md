# Prompt Codex — SMTC/FluentFlyout e migração integral para .NET 10 Windows 10 19041

## Missão

Trabalhe no repositório `WandersondeSouza/mpv.net` e implemente, de ponta a ponta, a integração do **MPV.NET Media Player** com os controles de mídia do Windows por meio de **System Media Transport Controls (SMTC)**, permitindo que Windows 10/11, teclas multimídia e consumidores de sessões como o **FluentFlyout** exibam e controlem a reprodução.

A implementação deve oferecer, conforme o estado real do player:

- Play;
- Pause;
- Previous;
- Next;
- estado Playing/Paused/Stopped/Closed;
- título e metadados da mídia atual;
- duração e posição, quando confiáveis;
- capa/thumbnail apenas quando disponível de forma segura;
- habilitação de Previous/Next conforme a playlist;
- suporte tanto a vídeo quanto a áudio;
- remoção/desabilitação da sessão e dos controles externos enquanto o MPV.NET estiver em fullscreen;
- restauração correta da sessão ao sair do fullscreen.

Como pré-requisito da integração, migre **todos os projetos .NET do repositório** para o TFM exato:

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

A entrega deve ser feita em **branch nova**, com alterações pequenas e verificáveis, testes em cada etapa, **commit e push após cada etapa aprovada**, sem fazer merge em `main`.

---

## Resultado esperado

Ao final:

1. o MPV.NET compila e executa em WPF/.NET 10 com suporte mínimo declarado ao Windows 10 versão 2004, build 19041;
2. todos os projetos SDK-style usam `net10.0-windows10.0.19041.0`;
3. o projeto WAP/MSIX, o manifesto do aplicativo, o manifesto do pacote, scripts, documentação e CI estão coerentes com o novo piso de Windows;
4. uma mídia carregada cria uma sessão SMTC vinculada à janela real do MPV.NET;
5. Play/Pause enviados pelo Windows alteram o estado do libmpv;
6. Previous/Next navegam pela playlist somente quando o item correspondente existe;
7. mudanças feitas dentro do MPV.NET são refletidas no Windows sem polling agressivo;
8. áudio e vídeo publicam metadados coerentes;
9. fullscreen desabilita a exposição SMTC e sair de fullscreen a restaura;
10. encerrar, trocar arquivo repetidamente ou reabrir a janela não deixa eventos, threads, timers, objetos COM/WinRT ou sessão de mídia presos;
11. os pacotes portátil, instalador e Microsoft Store continuam válidos;
12. o MPV.NET passa a ser detectável por consumidores de SMTC, inclusive o FluentFlyout quando ele estiver instalado, sem dependência direta dele.

---

## Regras obrigatórias do repositório

Antes de modificar qualquer arquivo:

1. leia integralmente o `AGENTS.md`;
2. leia `README.md`, `docs/manual.md`, `docs/guia-operacional.md` e os documentos relevantes em `docs/developer/`;
3. leia os materiais aplicáveis em `.ai/skills/`, `.ai/agents/` e `.ai/prompts/`;
4. inspecione o código atual. Os documentos auxiliam, mas não substituem o código;
5. preserve compatibilidade com mpv/libmpv, configurações existentes, OSC, atalhos, menus, temas, DPI, múltiplos monitores, linha de comando, drag/drop, playlists, Store e distribuição portátil;
6. não faça refatoração ampla que não seja necessária para esta funcionalidade;
7. não altere versão pública do produto;
8. não faça merge em `main`;
9. não use `push --force`, não reescreva histórico e não apague alterações alheias;
10. nunca faça commit com build ou testes conhecidos como quebrados;
11. não inclua certificados, tokens, chaves, segredos, binários gerados, `bin/`, `obj/` ou arquivos temporários;
12. atualize documentação existente quando houver mudança consolidada; evite documentos redundantes.

Antes de cada etapa, registre no relatório de trabalho:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```

---

## Escopo técnico real já identificado

A solução principal é:

```text
src/MpvNet.sln
```

Projetos que devem ser inventariados e tratados:

| Projeto | Estado atual conhecido | Estado exigido |
| --- | --- | --- |
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | `net10.0-windows7.0`; WPF + WinForms; executável | `net10.0-windows10.0.19041.0` |
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | `net10.0-windows7.0`; WPF | `net10.0-windows10.0.19041.0` |
| `src/MpvNet/MpvNet.csproj` | `net10.0`; biblioteca principal | `net10.0-windows10.0.19041.0` |
| `src/MpvNet.Tests/MpvNet.Tests.csproj` | `net10.0`; testes xUnit v3 | `net10.0-windows10.0.19041.0` |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | `net10.0`; exemplo de extensão | `net10.0-windows10.0.19041.0` |
| `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` | TFM `net10.0-windows10.0.26100.0`; plataforma mínima antiga | alinhar TFM e piso suportado a 19041 sem quebrar o SDK de empacotamento |

Não presuma que esta lista é completa. Antes da alteração, procure todos os arquivos:

- `*.csproj`;
- `*.wapproj`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- `global.json`, se existir;
- `*.sln`;
- manifests;
- workflows;
- scripts de build, publish, Store, instalador e release;
- documentação que cite `net10.0`, `windows7.0`, versões do Windows ou requisitos mínimos.

Se encontrar outro projeto, inclua-o na análise e aplique o mesmo critério.

---

## Decisão arquitetural obrigatória para SMTC

Este é um aplicativo desktop WPF/WinForms baseado em uma janela Win32. Portanto:

- use a integração desktop oficial vinculada ao **HWND** da janela principal;
- obtenha o `SystemMediaTransportControls` por `ISystemMediaTransportControlsInterop` ou pela projeção .NET oficial equivalente que efetivamente use `GetForWindow(HWND)`;
- inicialize somente depois que o HWND válido existir;
- não use `SystemMediaTransportControls.GetForCurrentView()`, pois esse padrão depende de `CoreWindow/ApplicationView` e não é o caminho suportado para este desktop app;
- isole COM/WinRT e os detalhes de HWND no projeto `MpvNet.Windows`;
- não contamine a biblioteca principal `MpvNet` com dependências de UI/WinRT quando um contrato simples e testável resolver;
- faça marshaling explícito para o Dispatcher/contexto correto quando eventos do SMTC chegarem fora da thread de UI ou da thread esperada pelo player;
- trate falha ou indisponibilidade do SMTC de forma segura: o player deve continuar reproduzindo normalmente;
- não adicione dependência direta do FluentFlyout;
- não copie código, XAML, assets ou implementação do FluentFlyout;
- não crie nesta tarefa um widget próprio fisicamente embutido na taskbar.

O objetivo é publicar corretamente uma sessão SMTC. O FluentFlyout, quando instalado, será apenas um consumidor externo dessa sessão.

Consulte e siga primeiro a documentação oficial atual da Microsoft para:

- WinRT em desktop apps;
- APIs WinRT não suportadas diretamente em desktop;
- `ISystemMediaTransportControlsInterop`;
- `SystemMediaTransportControls`;
- `SystemMediaTransportControlsDisplayUpdater`;
- `SystemMediaTransportControlsTimelineProperties`.

Use o repositório do FluentFlyout apenas para entender o comportamento esperado como consumidor de sessões; não o use como fonte de código a copiar.

---

## Arquitetura proposta

Crie uma integração pequena, isolada e substituível no projeto Windows. Os nomes podem ser ajustados à convenção real após a análise, mas a responsabilidade deve permanecer clara.

Exemplo:

```text
src/MpvNet.Windows/Services/MediaTransport/
    ISystemMediaTransportService.cs
    WindowsSystemMediaTransportService.cs
    SystemMediaTransportInterop.cs
    MediaTransportState.cs
    MediaTransportMetadata.cs
```

Separe:

1. **interoperação Windows/HWND/SMTC**;
2. **tradução do estado do MPV.NET para um modelo simples**;
3. **roteamento de comandos SMTC para comandos já existentes do player**;
4. **ciclo de vida e descarte**;
5. **testes da lógica sem exigir uma sessão real do Windows**.

Não crie duplicação de estado desnecessária. O estado autoritativo continua sendo o player/libmpv.

Se a arquitetura atual favorecer outro local ou outro padrão, adapte os nomes, mas preserve isolamento, testabilidade e descarte determinístico.

---

## Mapeamento funcional obrigatório

### Estado do player para SMTC

| Estado MPV.NET/libmpv | Estado SMTC esperado |
| --- | --- |
| sem mídia carregada | sessão desabilitada ou fechada/limpa |
| mídia carregando | estado conservador; não anunciar Playing prematuramente |
| reproduzindo | `PlaybackStatus.Playing` |
| pausado | `PlaybackStatus.Paused` |
| encerrado/fim sem próximo item | `PlaybackStatus.Stopped` ou sessão limpa conforme o ciclo real |
| aplicativo fechando | eventos removidos, SMTC desabilitado e recursos liberados |
| fullscreen | `IsEnabled = false`; não expor controles externos |
| saiu do fullscreen com mídia | reconstruir/sincronizar estado e `IsEnabled = true` |

A reprodução não deve parar ao entrar em fullscreen. Apenas a exposição externa da sessão deve ser desabilitada.

### Comandos do Windows para MPV.NET

- Play: retirar pause somente se houver mídia;
- Pause: pausar somente se houver mídia;
- Play/Pause toggle: usar a mesma semântica já consolidada pelo player;
- Next: avançar somente se `PlaylistPos < PlaylistCount - 1`;
- Previous: voltar somente se `PlaylistPos > 0`;
- Stop, se habilitado: usar a semântica real existente e testada, sem transformar Stop em Quit acidentalmente;
- ignorar com segurança comandos inválidos, duplicados, recebidos durante shutdown ou sem mídia.

Reutilize os comandos já existentes do MPV.NET/libmpv. Não implemente uma segunda playlist.

### Botões habilitados

Atualize dinamicamente:

```text
Play/Pause = existe mídia reproduzível
Previous   = PlaylistCount > 1 e PlaylistPos > 0
Next       = PlaylistCount > 1 e PlaylistPos >= 0 e PlaylistPos < PlaylistCount - 1
Stop       = apenas se houver semântica segura e requisito real
```

Recalcule ao:

- carregar arquivo;
- trocar posição da playlist;
- alterar playlist;
- finalizar arquivo;
- parar;
- entrar/sair de fullscreen;
- iniciar shutdown.

### Eventos e propriedades do player

Investigue e reutilize os eventos/propriedades reais, incluindo os equivalentes atuais de:

- inicialização do player/janela;
- `FileLoaded`;
- pause/`Paused`;
- `PlaylistPosChanged`;
- contagem da playlist;
- `Duration`;
- posição de reprodução;
- `Path`;
- `media-title`;
- fullscreen;
- end-file/stop;
- shutdown/dispose.

Não crie timer para consultar pause ou playlist se esses estados já são orientados a eventos.

---

## Metadados

### Vídeo

Prioridade sugerida:

1. título de mídia fornecido pelo mpv;
2. título da playlist;
3. nome amigável do arquivo/URL;
4. nome do aplicativo como subtítulo ou identificador quando apropriado.

Preencha apenas propriedades válidas para vídeo. Não invente artista, álbum, série, temporada ou episódio.

### Áudio

Quando o mpv disponibilizar tags confiáveis, use:

- título;
- artista;
- álbum;
- número da faixa, se suportado e válido;
- capa embutida ou externa apenas se houver um caminho/stream seguro.

Use fallbacks para nome do arquivo sem publicar strings vazias ou caminhos sensíveis desnecessários.

### Thumbnail/capa

- considere thumbnail opcional;
- não bloqueie UI, reprodução ou eventos do mpv para extrair imagem;
- não faça download arbitrário apenas para preencher capa;
- não exponha credenciais presentes em URLs;
- valide arquivo, formato e acesso;
- libere streams;
- falha de capa nunca pode derrubar a sessão;
- limpe a capa anterior ao trocar para item sem imagem.

Primeiro entregue metadados textuais confiáveis. Capa só entra quando a implementação for segura, pequena e testável.

---

## Timeline, duração e posição

Implemente timeline somente com valores válidos:

- início: zero;
- fim: duração conhecida e positiva;
- posição: limitada ao intervalo;
- taxa: coerente com o estado, quando a API exigir;
- não publique `NaN`, infinito, valores negativos ou posição maior que duração;
- mídia ao vivo/stream sem duração deve degradar sem erro.

Evite atualização excessiva. Prefira:

- eventos existentes;
- atualização ao carregar, pausar, buscar/seek e trocar mídia;
- se for indispensável um timer para progresso, use apenas um timer de baixa frequência, cancelável, sem sobreposição e ativo somente enquanto necessário.

Teste especialmente seek, pause, stream sem duração e troca rápida de playlist.

---

## Fullscreen

A regra do produto é obrigatória:

```text
Janela normal + mídia carregada
    => sessão SMTC habilitada

Entrou em fullscreen
    => SMTC desabilitado e controles externos não anunciados

Saiu de fullscreen
    => estado, botões, metadados e timeline sincronizados novamente
       e SMTC reabilitado

Fechou em fullscreen
    => nenhum evento, sessão ou recurso remanescente
```

Integre-se ao mecanismo real em `MainForm.Fullscreen.cs` e/ou aos eventos já expostos pelo player. Não detecte fullscreen por heurística visual, tamanho da janela ou polling.

Valide transições repetidas normal → fullscreen → normal, inclusive pausado, reproduzindo, com playlist e durante troca de mídia.

---

## Ciclo de vida, concorrência e descarte

A implementação deve ser segura e idempotente:

- inicializar no máximo uma integração por janela;
- não assinar o mesmo evento duas vezes;
- armazenar e remover todos os event handlers;
- cancelar timers e operações assíncronas;
- impedir callbacks depois de shutdown;
- liberar streams, referências COM/WinRT descartáveis e outros recursos conforme o contrato real;
- tolerar fechamento durante carregamento;
- tolerar troca rápida de mídia;
- evitar deadlock entre thread do libmpv, thread de UI e callback SMTC;
- registrar falhas relevantes usando o mecanismo de log existente, sem arquivo de log sempre ativo e sem dados sensíveis;
- não deixar o MPV.NET impedido de encerrar.

Use `IDisposable`/`IAsyncDisposable` apenas conforme necessário e faça o owner correto chamar o descarte no ciclo da janela/aplicativo.

---

## Migração de todos os projetos para o TFM 19041

### Projetos SDK-style

Aplique o TFM exato abaixo a todos os `*.csproj`, inclusive biblioteca principal, testes, WPF e extensão de exemplo:

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

Não deixe mistura de `net10.0`, `net10.0-windows7.0` e `net10.0-windows10.0.19041.0` entre projetos do fork.

Após a alteração:

- restaure dependências;
- verifique referências entre projetos;
- verifique analyzers e warnings de plataforma;
- confirme que os testes continuam executáveis;
- confirme que a extensão de exemplo compila e é carregável;
- confirme `win-x64`, single-file e self-contained;
- confirme que o TFM não reaparece hardcoded em scripts ou caminhos.

### WAP/MSIX

No `MpvNet.Pacote.wapproj`:

- alinhe o `TargetFramework` a `net10.0-windows10.0.19041.0`;
- preserve um `TargetPlatformVersion` de SDK que exista e seja apropriado para compilar/empacotar, atualmente 26100, se isso for necessário ao Desktop Bridge;
- altere o piso efetivamente suportado para `10.0.19041.0`;
- alinhe `TargetPlatformMinVersion`;
- alinhe `Package.appxmanifest` para `Windows.Desktop MinVersion="10.0.19041.0"`;
- revise `Windows.Universal` e `MaxVersionTested` com cuidado, sem reduzir desnecessariamente o SDK de compilação e sem declarar suporte falso;
- preserve `RuntimeBehavior="packagedClassicApp"`, `TrustLevel="mediumIL"`, identidade, associações, alias, versão e regras da Microsoft Store;
- não altere certificado ou dados de publicação;
- confirme que portátil/instalador continuam separados do MSIX.

### Manifesto Win32

Revise `src/MpvNet.Windows/app.manifest`:

- remova declarações obsoletas de compatibilidade com Windows 7, Windows 8 e Windows 8.1, pois o produto passará a ter piso Windows 10 19041;
- preserve Windows 10/11, PerMonitorV2, UTF-8, long paths, common controls e `asInvoker`;
- não peça elevação, `uiAccess` ou capacidades adicionais.

### CI, scripts e documentação

Atualize, quando necessário:

- `.github/workflows/dotnet-desktop.yml`;
- `.github/workflows/release-packages.yml`;
- scripts em `src/Tools/`;
- scripts de Store e Inno Setup;
- `docs/developer/build-release.md`;
- `docs/developer/windows-ui.md`;
- requisitos no README/manual/guia operacional;
- qualquer referência a Windows 7/8/8.1, `windows7.0`, `net10.0` genérico ou versão mínima antiga.

Os workflows já instalam `.NET 10.0.x`; preserve isso e acrescente validações de coerência do TFM se forem úteis e estáveis.

---

## Estratégia de testes

A lógica deve ser testável sem depender de uma sessão SMTC real no runner. Crie interfaces/adapters mínimos para simular o transporte do Windows.

### Testes automatizados mínimos

Inclua cobertura para:

1. mídia inexistente deixa controles desabilitados;
2. Playing mapeia para Playing;
3. Paused mapeia para Paused;
4. Stop/end limpa ou para a sessão conforme a regra definida;
5. Play do SMTC chama o comando correto uma vez;
6. Pause do SMTC chama o comando correto uma vez;
7. Next funciona no meio da playlist;
8. Next é ignorado/desabilitado no último item;
9. Previous funciona depois do primeiro item;
10. Previous é ignorado/desabilitado no primeiro item;
11. playlist com um item deixa Previous/Next desabilitados;
12. troca de item atualiza botões e metadados;
13. título vazio usa fallback;
14. troca para mídia sem capa limpa a capa anterior;
15. duração/posição inválidas não geram timeline inválida;
16. stream sem duração não lança exceção;
17. entrar em fullscreen desabilita o transporte sem pausar;
18. sair de fullscreen restaura o estado;
19. múltiplas transições fullscreen não duplicam assinaturas;
20. dispose remove handlers e ignora callbacks tardios;
21. inicialização falha do SMTC não impede reprodução;
22. comandos recebidos durante shutdown são ignorados;
23. áudio e vídeo usam tipo/metadados adequados;
24. URLs com credenciais não são publicadas como título.

Amplie `src/MpvNet.Tests` respeitando o padrão xUnit v3 atual. Não transforme testes unitários em dependentes de desktop interativo quando uma abstração resolver.

### Comandos mínimos por etapa

Ajuste conforme a realidade do repositório, mas execute no mínimo:

```powershell
dotnet --info
dotnet restore .\src\MpvNet.sln
dotnet build .\src\MpvNet.sln -c Debug
dotnet build .\src\MpvNet.sln -c Release
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --configuration Debug -- -noLogo
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --configuration Release -- -noLogo
.\lang\validate-po-files.ps1 -ValidateOnly
```

Quando o ambiente não possuir Desktop Bridge/MSIX, não finja sucesso: registre a limitação e execute todas as validações possíveis. Em ambiente com Visual Studio/targets adequados, execute também:

```powershell
msbuild .\src\MpvNet.Pacote\MpvNet.Pacote.wapproj /t:ValidateStorePackage /p:Configuration=Release /p:Platform=x64
```

Valide ainda:

- publish `win-x64` self-contained;
- publish single-file;
- scripts de conteúdo e dependências nativas;
- ZIP portátil;
- instalador, se Inno Setup estiver disponível;
- pacote Store, se Desktop Bridge e certificado de teste estiverem disponíveis;
- ausência de TFMs antigos com busca no repositório.

### Checklist manual obrigatório no Windows

Teste em Windows 10 19041 ou posterior e Windows 11 quando disponíveis:

- arquivo de vídeo local;
- arquivo de áudio local;
- URL/stream;
- mídia ao vivo sem duração;
- playlist de um item;
- playlist de vários itens;
- pasta com mídias;
- linha de comando;
- drag/drop;
- Play/Pause pelo Windows;
- teclas multimídia;
- Previous/Next;
- primeiro, intermediário e último item;
- seek;
- troca rápida de arquivo;
- título e metadados;
- mídia com e sem capa;
- tema claro/escuro;
- DPI e múltiplos monitores;
- normal → fullscreen → normal repetidamente;
- confirmar que a sessão/controles externos deixam de ser anunciados em fullscreen;
- fechar o aplicativo normal e em fullscreen;
- abrir novamente e confirmar ausência de sessão fantasma;
- testar com FluentFlyout instalado, se disponível, verificando detecção e comandos;
- confirmar que o player funciona normalmente sem FluentFlyout instalado.

Use também o smoke test existente:

```powershell
.\src\Tools\test-ui-smoke.ps1 `
  -ExecutablePath .\src\MpvNet.Windows\bin\Debug\win-x64\mpvnet.exe `
  -MediaPath C:\Videos\amostra.mp4
```

---

## Branch, commits e push por etapa

### Preparação obrigatória

Comece pela branch padrão `main` atualizada e por uma árvore limpa:

```powershell
git status --short
git switch main
git pull --ff-only
git status --short
git switch -c feature/windows-smtc-net10-19041
git push -u origin feature/windows-smtc-net10-19041
```

Se houver alterações locais anteriores, não as descarte e não as inclua automaticamente. Pare, registre o conflito de escopo e preserve o trabalho existente.

Antes de cada commit:

1. revise `git diff`;
2. execute os testes da etapa;
3. verifique `git status --short`;
4. confirme ausência de segredos, binários e arquivos alheios;
5. faça um commit pequeno;
6. faça push da branch;
7. registre SHA, arquivos, testes e resultado.

### Etapa 1 — baseline e migração de plataforma

- inventariar projetos, manifests, scripts, docs e CI;
- registrar baseline de build/testes antes da alteração;
- migrar todos os projetos para o TFM 19041;
- alinhar WAP/MSIX, manifests, scripts, CI e documentação de plataforma;
- eliminar referências incoerentes ao suporte antigo;
- validar Debug, Release, testes e, se disponível, Store.

Commit sugerido:

```text
build: target .NET 10 Windows 19041 across projects
```

Depois:

```powershell
git push
```

### Etapa 2 — contratos e adapter SMTC/interop

- criar o modelo/contratos testáveis;
- criar aquisição SMTC vinculada ao HWND;
- tratar disponibilidade e falhas;
- implementar ciclo de vida básico;
- criar testes unitários do mapeamento e do adapter fake.

Commit sugerido:

```text
feat(windows): add HWND-bound SMTC transport service
```

Execute testes e faça `git push`.

### Etapa 3 — comandos e sincronização do player

- conectar FileLoaded, pause, stop/end, playlist e shutdown;
- mapear Play/Pause/Previous/Next para os comandos existentes;
- habilitar botões dinamicamente;
- tratar concorrência e Dispatcher;
- ampliar testes.

Commit sugerido:

```text
feat(player): synchronize playback and playlist with SMTC
```

Execute testes e faça `git push`.

### Etapa 4 — metadados e timeline

- vídeo e áudio;
- fallbacks;
- duração/posição;
- seek e streams sem duração;
- thumbnail opcional segura;
- limpar dados antigos;
- ampliar testes.

Commit sugerido:

```text
feat(media): publish SMTC metadata and timeline
```

Execute testes e faça `git push`.

### Etapa 5 — fullscreen e descarte

- desabilitar SMTC em fullscreen;
- restaurar ao sair;
- garantir que reprodução não seja afetada;
- remover handlers/timers/streams no shutdown;
- testar repetição e callbacks tardios.

Commit sugerido:

```text
feat(windows): suspend SMTC controls in fullscreen
```

Execute testes e faça `git push`.

### Etapa 6 — CI, empacotamento e documentação final

- consolidar workflows e scripts;
- validar portátil, instalador e Store;
- atualizar docs existentes;
- documentar comportamento, requisito mínimo e limitações;
- executar suíte completa e checklist manual possível.

Commit sugerido:

```text
docs: document Windows media controls and platform baseline
```

Execute testes e faça `git push`.

### Etapa 7 — auditoria final

- buscar TODOs temporários, warnings novos, TFMs antigos e referências obsoletas;
- executar todos os testes novamente;
- revisar diff completo contra `main`;
- fazer commit somente se houver correção real;
- fazer push final.

Não crie commit vazio.

---

## Critérios de aceite

A tarefa só está concluída se todos os itens aplicáveis abaixo forem verdadeiros:

- [ ] branch `feature/windows-smtc-net10-19041` criada a partir de `main` atualizada;
- [ ] todos os commits foram enviados ao remoto;
- [ ] todos os `*.csproj` usam exatamente `net10.0-windows10.0.19041.0`;
- [ ] WAP/MSIX e manifests possuem piso 19041 coerente;
- [ ] não há suporte declarado a Windows 7/8/8.1;
- [ ] solução compila Debug e Release;
- [ ] testes xUnit v3 passam Debug e Release;
- [ ] integração SMTC é vinculada ao HWND;
- [ ] `GetForCurrentView()` não é usado;
- [ ] Play/Pause funcionam externamente;
- [ ] Previous/Next respeitam limites da playlist;
- [ ] estado interno atualiza o estado externo;
- [ ] áudio e vídeo publicam metadados coerentes;
- [ ] timeline não falha com stream ao vivo ou valores inválidos;
- [ ] fullscreen desabilita a exposição SMTC sem parar a reprodução;
- [ ] sair do fullscreen restaura a sessão corretamente;
- [ ] shutdown não deixa sessão fantasma nem handlers;
- [ ] falha do SMTC não impede o player de funcionar;
- [ ] nenhum código ou dependência do FluentFlyout foi copiado;
- [ ] funciona sem FluentFlyout e é detectável por ele quando instalado;
- [ ] publish `win-x64` continua válido;
- [ ] portátil/instalador/Store foram validados conforme ferramentas disponíveis;
- [ ] documentação existente foi atualizada;
- [ ] nenhum segredo ou artefato gerado foi commitado;
- [ ] relatório final contém commits, SHAs, testes, limitações e pendências reais.

---

## Fora do escopo

Não faça nesta branch:

- clone visual da barra do FluentFlyout;
- widget próprio embutido na taskbar;
- dependência obrigatória do FluentFlyout;
- cópia de código/assets GPL do FluentFlyout;
- reformulação ampla da UI;
- troca do OSC;
- mudança das regras da playlist;
- alteração de identidade, preço, assinatura ou versão do produto;
- upgrade indiscriminado de pacotes NuGet;
- mudança não relacionada em libmpv, FFmpeg, MediaInfo ou componentes de runtime;
- merge em `main`;
- release pública.

Se encontrar um bloqueio que exija ampliar o escopo, documente-o e pare antes da mudança expansiva.

---

## Relatório final obrigatório

Ao terminar, entregue:

1. resumo do comportamento implementado;
2. branch remota;
3. tabela de commits com SHA, mensagem e objetivo;
4. lista de arquivos alterados por etapa;
5. testes automatizados executados e resultados;
6. testes manuais executados e resultados;
7. resultado de Debug, Release, publish, portátil, instalador e Store;
8. confirmação do TFM em cada projeto;
9. confirmação da regra de fullscreen;
10. confirmação do funcionamento com e sem FluentFlyout;
11. warnings existentes antes e novos warnings;
12. limitações do ambiente, sem declarar como testado o que não foi;
13. riscos ou pendências remanescentes;
14. comparação final `main...feature/windows-smtc-net10-19041`;
15. confirmação de que tudo foi enviado com `git push`.

Não encerre apenas dizendo “implementado”. Apresente evidências reproduzíveis.
