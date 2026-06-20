# Arquitetura do MPV.NET Media Player

## Objetivo

Este documento ajuda mantenedores, desenvolvedores e agentes de IA a entender a arquitetura geral do MPV.NET Media Player.

O foco é mapear responsabilidades reais do código atual, áreas críticas e fluxos que devem ser preservados.

Este arquivo substitui os antigos mapas separados de projeto, classes, startup e auditoria inicial. A regra aqui é manter a visão arquitetural em um único lugar e só separar um novo documento se houver necessidade real e contínua.

---

# Visão geral

O MPV.NET Media Player é um frontend Windows para o mpv/libmpv, baseado no mpv.net.

O projeto usa o motor do mpv para reprodução multimídia e implementa uma camada Windows com WinForms, WPF, comandos próprios, configuração compatível com mpv e integração com recursos do sistema.

Prioridades arquiteturais:

1. preservar compatibilidade com mpv/libmpv;
2. manter configuração baseada em arquivos simples;
3. preservar scripts, atalhos e comandos existentes;
4. isolar alterações de UI quando possível;
5. evitar refatorações amplas sem validação manual.

---

# Solução e camadas

```text
src/MpvNet.sln
  MpvNet/
  MpvNet.Windows/
  NGettext.Wpf/
  MpvNet.Extension/
```

## `MpvNet`

Núcleo da aplicação:

- integração com libmpv;
- comandos internos;
- linha de comando;
- resolução de configuração;
- `input.conf`;
- carregamento de extensões;
- estado do player.

Organização arquitetural adotada para novas extrações:

- `Infrastructure/RuntimeComponents` - catálogo, GitHub, metadados, staging,
  instalação e resolução dos componentes opcionais de runtime;
- `Native` - contratos P/Invoke e estruturas nativas;
- `Player.*.cs` - estado e coordenação do player, separados por capacidade;
- classes públicas antigas permanecem como fachadas quando uma responsabilidade
  é movida, preservando compatibilidade com extensões e consumidores existentes.

Os diretórios `Services`, `Configuration`, `Media`, `Models` e `Utilities`
devem ser criados apenas quando houver uma extração concreta. Sufixos como
`Service`, `Store`, `Provider`, `Resolver`, `Parser` e `Client` descrevem uma
responsabilidade real; não são aplicados mecanicamente a modelos.

Contratos de nomenclatura consolidados:

- `AppPaths` é o provedor de diretórios; `Folder` permanece como fachada
  obsoleta para compatibilidade;
- `TranslationProvider` mantém o tradutor ativo; `Translator` permanece como
  fachada obsoleta;
- `ExtensionService` carrega extensões gerenciadas; `ExtensionLoader` permanece
  como adaptador obsoleto;
- `SettingsStore` representa a persistência interna de `settings.xml`;
- `NamedValue` é o modelo descritivo para pares nome/valor. `StringPair`
  permanece suportado nos contratos públicos existentes para evitar quebra de
  extensões e chamadas que usam `List<StringPair>`.

## `MpvNet.Windows`

Frontend Windows:

- entry point;
- janela principal WinForms;
- janelas auxiliares WPF;
- comandos de UI;
- tema;
- hotkeys globais;
- associação de arquivos;
- integração com APIs nativas do Windows.

## `NGettext.Wpf`

Suporte de localização usado pelo frontend WPF.

## `MpvNet.Extension`

Exemplo de extensão .NET carregável.

---

# Mapa rápido de classes e pontos de entrada

Arquivos centrais para entender o fluxo sem abrir vários documentos:

- `src/MpvNet.Windows/Program.cs` - entry point da aplicação Windows;
- `src/MpvNet/App.cs` - inicialização, configuração e opções do frontend;
- `src/MpvNet.Windows/WinForms/MainForm.cs` - estado principal da janela WinForms;
- `src/MpvNet.Windows/WinForms/MainForm.*.cs` - responsabilidades separadas da janela principal por tema;
- `src/MpvNet/Integration/Mpv/Player.cs` - estado principal do player;
- `src/MpvNet/Integration/Mpv/Player.*.cs` - inicialização, eventos, ciclo de vida, carregamento de mídia e capacidades do player;
- `src/MpvNet/Integration/Mpv/MpvClient.cs` - wrapper de cliente e loop de eventos;
- `src/MpvNet/Native/LibMpv.cs` - P/Invoke e estruturas nativas;
- `src/MpvNet/Configuration/InputConf.cs` - leitura e migração do `input.conf`;
- `src/MpvNet/Configuration/Settings.cs` - persistência de estado do frontend;
- `src/MpvNet/Configuration/CommandLine.cs` - argumentos de linha de comando;
- `src/MpvNet.Windows/UI/Theme.cs` e `src/MpvNet.Windows/UI/GlobalHotkey.cs` - tema e hotkeys globais;
- `src/MpvNet.Windows/Commands/GuiCommand.cs` e `src/MpvNet/Command.cs` - comandos da UI e comandos internos.

---

# Fluxo de inicialização

Arquivo principal: `src/MpvNet.Windows/Program.cs`

Fluxo:

1. configura produto, tradução e handlers globais de exceção;
2. anexa console quando aplicável;
3. trata `--register-file-associations`;
4. chama `App.Init()`;
5. chama `Theme.Init()`;
6. cria `Mutex` baseado em `App.ConfPath`;
7. aplica regra de instância única, fila ou múltiplas instâncias;
8. processa comandos informativos de terminal;
9. inicia modo sem janela quando `--o=` está presente;
10. abre `WinForms.MainForm` no fluxo normal.

Arquivos relacionados:

- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/CommandLine.cs`;
- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`.

Fluxo resumido adicional:

1. `Program.Main` inicializa WinForms, tradução e handlers globais;
2. `App.Init()` resolve configuração e opções do frontend;
3. `Theme.Init()` carrega o tema;
4. a instância única é aplicada com base em `App.ConfPath`;
5. `Player.Init()` prepara libmpv e o estado inicial;
6. `MainForm` é criado;
7. arquivos/URLs informados pela linha de comando são processados depois da janela estar pronta.

---

# Integração com mpv/libmpv

Arquivos principais:

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/Integration/Mpv/MpvClient.cs`;
- `src/MpvNet/Native/LibMpv.cs`.

Responsabilidades:

- criar contexto `mpv_create`;
- registrar eventos;
- configurar propriedades iniciais;
- definir `config-dir`;
- carregar `input.conf` em memória;
- processar argumentos antes de `mpv_initialize`;
- chamar `mpv_initialize`;
- criar cliente `mpvnet`;
- iniciar loops de evento;
- observar propriedades como `pause`, `video-rotate` e `playlist-pos`;
- destruir handles no encerramento.

Essa camada é a área de maior risco do projeto.

Topologia atual do player:

- `src/MpvNet/Player.Initialization.cs` - sequência de inicialização do mpv/libmpv e criação do cliente `mpvnet`;
- `src/MpvNet/Player.ObservedProperties.cs` - propriedades observadas como `pause`, `video-rotate`, `playlist-pos` e `playlist`;
- `src/MpvNet/Player.Events.cs` - callbacks de eventos de arquivo, log, reconfiguração de vídeo e títulos Blu-ray;
- `src/MpvNet/Player.Lifecycle.cs` - `MainEventLoop`, shutdown e destruição dos handles;
- `src/MpvNet/Player.MediaLoading.cs` - carregamento de mídia, playlists, URLs, ISO/DVD/BD, pasta automática e normalização de playlist;
- `src/MpvNet/Player.Capabilities.cs` - perfis, decoders, protocolos, demuxers e criação de clientes adicionais.

---

# Interface gráfica

Arquivos principais:

- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/WPF/ConfWindow.xaml`;
- `src/MpvNet.Windows/WPF/InputWindow.xaml`;
- `src/MpvNet.Windows/WPF/Views/AboutWindow.xaml`;
- `src/MpvNet.Windows/WPF/Resources.xaml`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

A janela principal é WinForms. Janelas auxiliares e editores usam WPF. Essa combinação exige cuidado com ownership de janela, DPI, fullscreen e integração com o handle nativo usado pelo libmpv.

Topologia atual da janela principal:

- `src/MpvNet.Windows/WinForms/MainForm.ContextMenu.cs` - helpers de menu, faixas, capítulos e busca de itens;
- `src/MpvNet.Windows/WinForms/MainForm.Commands.cs` - handlers de comandos de UI acionados por `GuiCommand`;
- `src/MpvNet.Windows/WinForms/MainForm.Cursor.cs` - cursor e detecção de OSC;
- `src/MpvNet.Windows/WinForms/MainForm.DragDrop.cs` - entrada por drag/drop;
- `src/MpvNet.Windows/WinForms/MainForm.Fullscreen.cs` - transição de fullscreen e restauração de janela;
- `src/MpvNet.Windows/WinForms/MainForm.PlayerEvents.cs` - reação da UI a eventos do player.

---

# Sistema de configuração

Arquivos:

- `src/MpvNet/Integration/Mpv/Player.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Configuration/Settings.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

Pasta de configuração:

1. `MPVNET_HOME`;
2. `portable_config`;
3. `%APPDATA%\mpv.net`.

Arquivos:

- `mpv.conf`;
- `mpvnet.conf`;
- `input.conf`;
- `settings.xml`;
- `theme.conf`;
- `global-input.conf`.

---

# Sistema de comandos

Arquivos:

- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/Commands/GuiCommand.cs`;
- `src/MpvNet/Utilities/InputHelp.cs`;
- `src/MpvNet/Configuration/InputConf.cs`;
- `src/MpvNet/Configuration/CommandLine.cs`.

O projeto aceita comandos mpv diretamente e adiciona comandos próprios, normalmente chamados por:

```text
script-message-to mpvnet <comando>
```

Comandos marcados como deprecated ainda podem ser usados por configurações antigas e não devem ser removidos sem migração.

## Critério de auditoria

Se uma alteração exigir procurar a causa real em mais de um módulo, a investigação deve voltar para este arquivo e não ficar espalhada em páginas pequenas separadas.

---

# Scripts e extensões

Scripts Lua/JavaScript são responsabilidade do mpv e seguem a estrutura de configuração compatível:

- `scripts`;
- `script-opts`.

Extensões .NET são carregadas de:

```text
extensions
```

O carregamento é feito por `ExtensionLoader` após a janela principal informar que está carregada.

---

# Build e empacotamento

Arquivos:

- `src/MpvNet.Windows/MpvNet.Windows.csproj`;
- `src/Directory.Packages.props`;
- `src/Tools/build-release-package.ps1`;
- `src/Setup/Inno/build-windows-installer.iss`.

O executável é `mpvnet.exe`. O projeto da aplicação define `win-x64` como runtime padrão, e o script de release atual publica `win-x64`, copia DLLs nativas x64, cria ZIP portátil e gera instalador x64.

---

# Áreas consideradas sensíveis

## Integração com libmpv

Risco alto:

- reprodução;
- eventos;
- propriedades;
- threading;
- scripts;
- compatibilidade com mpv.

## Configuração

Risco alto:

- pasta de configuração;
- migrações automáticas;
- `input.conf`;
- `mpv.conf`;
- `mpvnet.conf`;
- modo portátil.

## UI/fullscreen

Risco médio/alto:

- DPI;
- fullscreen;
- múltiplos monitores;
- menu de contexto;
- foco;
- atalhos;
- tema claro/escuro.

## Release

Risco médio:

- caminhos fixos de ferramentas;
- dependências nativas;
- publicação Debug;
- ZIP portátil sem `portable_config`.

---

# Auditoria de modernização gradual - 2026-06-20

## Diagnóstico

- `RuntimeComponents` concentrava catálogo, acesso HTTP/GitHub, checksum,
  extração, persistência de metadados, instalação e resolução de caminhos.
- `MainPlayer` e `MainForm` continuam sendo agregadores de estado sensíveis;
  suas extrações devem ser precedidas por testes de caracterização.
- `ConfWindow`, `GuiCommand` e `InputHelp` misturam apresentação, parsing e
  coordenação, mas dependem de XAML e comandos textuais que impedem remoções
  baseadas somente em busca de referências C#.
- `MpvClient` expõe o contrato de baixo nível necessário ao libmpv. Sua API não
  deve ser escondida por uma camada genérica.
- `NGettext.Wpf`, `HandyControl`, arquivos gerados e artefatos de build ficam
  fora desta modernização.

## Classificação de risco

| Área | Risco | Estratégia |
| --- | --- | --- |
| Componentes de runtime | médio | fachada pública e serviços internos |
| Parsing e persistência de configuração | alto | caracterização antes da extração |
| Carregamento e eventos do player | alto | preservar ordem e contratos libmpv |
| Estado e eventos da janela | alto | manter UI nos partials e extrair apenas lógica não visual |
| Modelos e utilitários puros | baixo | mover quando houver benefício objetivo |

## Regras para as próximas fases

1. Uma fase deve compilar e passar nos testes antes da próxima.
2. APIs públicas existentes recebem fachada ou adaptador.
3. Código só é removido após considerar XAML, reflection, extensões e comandos.
4. Não será introduzido contêiner de injeção de dependências.
5. Documentação técnica será atualizada apenas para mudanças consolidadas.

## Extrações prioritárias consolidadas

- `RemotePlaylistService` concentra detecção, download temporário e timeout de
  playlists remotas; `MainPlayer` mantém os métodos públicos de compatibilidade
  e a decisão de reprodução.
- `GuiCommandArgumentParser` concentra validação de argumentos obrigatórios e
  números invariantes; `GuiCommand` mantém o registro e a execução dos comandos.
- `EditorConfigurationService` e `EditorConfigurationParser` concentram o
  parsing das definições do editor; `ConfWindow` mantém apenas estado e
  interação visual. `Conf` permanece como fachada obsoleta.

## Limpeza de redundância

- aliases e comandos depreciados continuam disponíveis quando fazem parte de
  configuração, extensão ou automação existente;
- usos internos do alias `Core` foram migrados para `Player`;
- inicializações intencionais por efeito colateral usam descarte explícito em
  vez de variáveis temporárias sem significado;
- o bootstrap de componentes resolve URL e digest do asset com uma única
  consulta de metadata por download;
- condições duplicadas e classes exclusivamente estáticas foram simplificadas.

---

<a id="relatorio-tecnico-inicial-2026-06-12"></a>

# Relatório técnico inicial - 2026-06-12

## Escopo analisado

Este diagnóstico consolida a leitura inicial do fork antes de novas mudanças
de estabilização. Foram considerados:

- regras de manutenção em `AGENTS.md`;
- documentação principal em `README.md`, `docs/manual.md` e
  `docs/guia-operacional.md`;
- documentação técnica em `docs/developer/`;
- superfície de código em `src/MpvNet`, `src/MpvNet.Windows`,
  `src/NGettext.Wpf`, `src/MpvNet.Tests` e `src/Tools`;
- artefatos de agentes e prompts em `.ai/`;
- fluxo de localização em `lang/source.pot` e `lang/po/*.po`.

O objetivo desta etapa não é refatorar o player. O objetivo é registrar o
estado técnico, separar riscos reais de melhorias possíveis e orientar mudanças
pequenas, verificáveis e compatíveis com mpv/libmpv.

## Diagnóstico resumido

- A arquitetura atual já tem camadas reconhecíveis: núcleo `MpvNet`, frontend
  `MpvNet.Windows`, suporte WPF/gettext em `NGettext.Wpf`, exemplo de extensão
  e projeto de pacote MSIX/WAP.
- O projeto já usa recursos modernos no build, como `LangVersion` 12.0,
  nullable reference types habilitado, pacotes NuGet centralizados e `win-x64`
  como runtime padrão.
- `Player.cs` e `MainForm.cs` continuam sendo pontos de estado central. A
  separação em partials reduziu o acoplamento visível, mas esses arquivos ainda
  devem ser tratados como áreas de alto risco.
- O harness `src/MpvNet.Tests/Program.cs` cobre parser de argumentos,
  playlists, títulos, configuração, idiomas, logs, limpeza temporária e
  políticas auxiliares de MediaInfo. Ele deve crescer junto de qualquer mudança
  nova nessas áreas.
- A localização está centralizada em `LanguageCatalog.cs`, com validação de
  paridade dos catálogos gettext contra `lang/source.pot`.
- O fluxo de release e dependências nativas está documentado em
  `docs/developer/build-release.md` e `docs/guia-operacional.md`, com scripts
  canônicos em `src/Tools/`.
- A documentação de agentes em `.ai/` é parte da superfície de manutenção e
  precisa permanecer alinhada ao mapa técnico real do repositório.

## Decisões de arquitetura

- Preservar `mpvnet.exe`, `%APPDATA%\mpv.net`, `MPVNET_HOME`,
  `portable_config`, `mpv.conf`, `mpvnet.conf` e `input.conf` como contratos de
  compatibilidade.
- Manter `src/MpvNet/Integration/Mpv/Player.cs` como estado principal do player e mover apenas
  responsabilidades bem delimitadas para partials ou classes auxiliares já
  justificadas por testes.
- Manter `src/MpvNet.Windows/WinForms/MainForm.cs` como estado principal da
  janela e isolar mudanças por tema nos partials `MainForm.*.cs`.
- Não substituir a lógica do mpv/libmpv por validações próprias do frontend. O
  frontend deve rejeitar apenas entradas claramente inválidas e deixar a decisão
  final de reprodução para o mpv/libmpv.
- Não criar normalizadores paralelos de idioma. Novas regras devem passar pelo
  catálogo central e pelos testes de fallback.
- Não criar novos documentos técnicos quando um documento existente puder
  receber a informação de forma clara e durável.

## Riscos técnicos atuais

- Alterações em eventos, callbacks e shutdown de `MpvClient`/`Player` podem
  afetar reprodução, scripts e encerramento do processo.
- Alterações em `MainForm` podem impactar fullscreen, DPI, menu de contexto,
  foco, atalhos, cursor/OSC e múltiplos monitores.
- Migrações automáticas em arquivos do usuário precisam ser conservadoras e
  manter backup quando alterarem configuração existente.
- Scripts de release dependem de fontes externas e ferramentas locais; qualquer
  mudança deve validar falha cedo para evitar pacote parcial.
- Atualizações de texto visível podem exigir sincronização gettext, compilação
  de `Locale` e validação de paridade.

## Plano de execução recomendado

1. Corrigir primeiro documentação e artefatos `.ai` que apontem para caminhos
   obsoletos ou fluxos inexistentes.
2. Auditar recursos e ciclo de vida em `MainForm`, `Player`, `MpvClient` e
   comandos de UI antes de alterar comportamento.
3. Para cada problema confirmado, criar uma mudança pequena com teste ou
   validação direta.
4. Rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`
   quando tocar parser, paths, playlists, títulos, logs, configuração, idioma
   ou MediaInfo.
5. Rodar `dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore
   /p:EnsureBuildAssets=false` como prova rápida para mudanças de código ou UI.
6. Para localização, rodar `.\lang\validate-po-files.ps1 -ValidateOnly`.
7. Para release, validar scripts, dependências nativas e conteúdo dos pacotes
   antes de publicar ou registrar a descricao da release no GitHub.

## Validação de base registrada

Na rodada inicial de 2026-06-12, antes de alterações de comportamento, a base
foi validada com:

```powershell
dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore /p:EnsureBuildAssets=false
.\lang\validate-po-files.ps1 -ValidateOnly
git diff --check
```

Resultado: testes, build rápido, validação gettext e checagem de whitespace
passaram. Revisões manuais completas de UI, pacote portátil, instalador e
workflow de release continuam pendentes conforme `docs/guia-operacional.md` e
`docs/developer/build-release.md`.

## Relatorio final da rodada segura - 2026-06-12

As etapas executadas nesta rodada mantiveram o escopo conservador definido para
o fork:

- documentacao e artefatos `.ai` foram alinhados ao fluxo atual de manutencao;
- este relatorio tecnico foi consolidado no documento de arquitetura existente;
- ciclo de vida, recursos e pontos de encerramento foram revisados sem
  refatoracao ampla;
- gravacoes de configuracao e arquivos gerados pelo aplicativo foram
  fortalecidas com escrita mais segura onde havia risco de arquivo parcial;
- integracao com mpv/libmpv foi mantida compativel, com ajustes pequenos e
  cobertos por testes;
- a cobertura de testes foi ampliada nos pontos tocados;
- gettext foi revalidado e a string visivel de criacao de arquivo de
  configuracao passou a usar catalogo de localizacao;
- o manifesto MSIX/WAP foi realinhado com a versao central
  `src/BuildVersion.props` (`7.1.3.15`).

Validacoes finais executadas na Etapa 8:

```powershell
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
.\lang\validate-po-files.ps1 -ValidateOnly
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore /p:EnsureBuildAssets=false
dotnet build .\src\MpvNet.sln -c Release -p:Platform=x64 --no-restore /p:EnsureBuildAssets=false
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease
git diff --check
```

Resultado da Etapa 8:

- testes automatizados passaram;
- validacao gettext passou com 850 entradas em todos os catalogos ativos;
- build rapido do projeto Windows passou sem avisos ou erros;
- build `Release|x64` da solucao passou sem avisos ou erros;
- o projeto MSIX/WAP foi ignorado no build de solucao desta maquina porque os
  targets Desktop Bridge/MSIX nao estavam instalados no local esperado;
- o pacote local sem publicacao no GitHub gerou:
  `artifacts\release\MPV.NET-Media-Player-v7.1.3.15-portable-x64\`,
  `artifacts\release\MPV.NET-Media-Player-v7.1.3.15-portable-x64.zip` e
  `artifacts\release\MPV.NET-Media-Player-v7.1.3.15-setup-x64.exe`;
- o script de release compilou `Locale`, validou dependencias nativas no
  publish, na pasta portatil e no ZIP, e concluiu o instalador Inno Setup;
- nenhuma release foi publicada no GitHub porque o comando foi executado com
  `-SkipGitHubRelease`.

Pendencias restantes:

- validar manualmente UI, fullscreen, menu, atalhos, temas, persistencia,
  arquivo local, URL/stream, playlist, pasta com midia, drag/drop,
  alternancia de faixas/legendas, cursor/OSC, comandos de janela e fechamento;
- validar o workflow manual `.github/workflows/release-packages.yml`;
- validar o pacote MSIX/WAP em ambiente com Desktop Bridge/MSIX instalado e
  credenciais de assinatura configuradas quando houver publicacao Store.

---

# Recomendações para agentes

1. Antes de alterar código, localizar a camada correta.
2. Preferir mudanças pequenas.
3. Preservar comandos, opções e arquivos existentes.
4. Atualizar documentação quando comportamento mudar.
5. Validar manualmente fullscreen, input e configuração quando tocar UI ou libmpv.
6. Separar documentação validada de hipóteses ainda pendentes.
7. Para mudanças em parsers, paths, playlist, título, logs, configuração ou políticas de MediaInfo, rodar `dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore`.
8. Para mudanças em UI/libmpv, além do build e dos testes automatizados, executar checklist manual com arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento.

