# Auditoria editorial da documentação do MPV.NET

Auditoria de 15/11/2026 na branch `master`, com árvore inicialmente limpa no
commit `a5502bebd1b2b1f58f08258165ee3f93d09cc44e`. Escopo executado conforme
o [prompt editorial](../../../.ai/prompts/documentation-editorial-modernization.md).

## A — Inventário e diagnóstico

| Documento ou grupo | Seções e assuntos examinados |
| --- | --- |
| [README](../../../README.md) e [política de segurança](../../../SECURITY.md) | Índice, download, Windows mínimo, CPU, branch suportada e canal de relato privado. |
| [Manual](../../manual.md) | Instalação, formatos e associações, SMTC, capas, configuração, playlists, linha de comando, idiomas, streaming, cache, OSC, modo portátil e suporte. |
| [Atalhos](../../ATALHOS.md) e [configuração rápida](../../CONFIGURACAO.md) | Bindings, diagnóstico, precedência da pasta, arquivos, menu e estrutura portátil. |
| [Guia operacional](../../guia-operacional.md) e [logs](../../logging.md) | Build, teste, empacotamento, diagnósticos, retenção, privacidade e limites da validação local. |
| [Arquitetura](../architecture.md) e [configuração interna](../configuration.md) | Projetos, pontos de entrada, ciclo de vida, paths, componentes, opções e migrações. |
| [Build e release](../build-release.md) | SDK, C#, Windows, x64, ferramentas, scripts, versões, ZIP, instalador, WAP, assinatura, WACK e workflows. |
| [Integração com mpv/libmpv](../mpv-integration.md) | ABI e variantes, dispositivos ópticos, playlists, eventos, integração Windows, yt-dlp, cache, recuperação e medições. |
| [Localização](../localization.md) | Gettext, 20 idiomas, aliases, variantes, RTL/LTR, preferências de áudio e legenda, catálogo e empacotamento. |
| [Interface Windows](../windows-ui.md) | Topologia, tela cheia, taskbar, SMTC, OSC, arte de áudio, temas, DPI e smoke test. |
| [Baseline C# 14](csharp14-modernization-baseline.md) e [resultado C# 14](csharp14-modernization-result.md) | Configuração à época, commits, testes, medições e reversão. Os registros continuam associados às revisões que foram testadas. |
| [Qualidade](../../quality/README.md) e [baseline de recursos](../../quality/reports/2026-11-13-baseline.md) | Processo de auditoria, diagnóstico datado e links para as evidências. |
| [README do WAP](../../../src/MpvNet.Pacote/README.md), [notas do NGettext.Wpf](../../../src/NGettext.Wpf/Releasing.md) e [ferramentas de localização](../../../tools/localization/README.md) | Requisitos, procedimento upstream de NuGet e utilitários gettext. |
| [AGENTS.md](../../../AGENTS.md), [.github](../../../.github/), [.ai](../../../.ai/) e [.agents](../../../.agents/) | Regras de manutenção, templates, branch do CI, publicação, segurança, escopo e links. `docs/tasks/` e `docs/prompts/` são registros de planejamento usados como referência. |

A triagem inicial analisou 20 documentos e 52 Markdown de referência. Ela
encontrou 175 ocorrências de links locais válidos, dois blocos C# na
documentação de uso e técnica, três páginas com mais de um H1 e um erro de
sintaxe nos 51 blocos PowerShell da linha de base.

| Impacto | Evidência anterior | Confronto e correção |
| --- | --- | --- |
| Alto | `SECURITY.md:7` informava `main` como branch suportada. | `refs/remotes/origin/HEAD` aponta para `origin/master`. O código público e os gatilhos de CI também usam `master`. A política agora indica `master` e mantém os canais privados. [Repositório](https://github.com/WandersondeSouza/mpv.net) · [CI desktop](../../../.github/workflows/dotnet-desktop.yml) |
| Alto | `build-release.md:482` usava `-Version <versao>` em uma cerca PowerShell, que gerava erro sintático no operador `<`. | O exemplo usa `Read-Host` e passa o valor ao parâmetro `-Version`. O texto explica `-StoreVersion` e os efeitos dos fluxos de publicação. [Script de versão](../../../src/Tools/set-release-version.ps1) |
| Alto | `manual.md`, em **Linha de comando**, indicava 180, 30 e 10 s como tetos atuais. `configuration.md` atribuía a política ao comando `loadfile`. | A referência técnica mantém os valores atuais de 240, 90 e 70 s e os hooks `on_load` e `on_preloaded`, com refinamento em `file-loaded`. A revisão de 180/30/10 s ficou no histórico. [Política](../../../src/MpvNet/Media/MediaInput.cs) · [Registro dos hooks](../../../src/MpvNet/Integration/Mpv/Player.Initialization.cs) |
| Médio | Em **Controles de mídia do Windows**, o manual dizia que a busca de capas online começava desligada. A busca de posição omitia o caso restrito ao cache. | A busca de capas vem ativada para áudio local elegível. O SMTC exige `seekable`, rejeita `partially-seekable` e verifica a duração. Permaneceram as opções de privacidade, cache e seleção de capa. [Padrões do aplicativo](../../../src/MpvNet/App.cs) · [Snapshot SMTC](../../../src/MpvNet.Windows/WinForms/MainForm.MediaTransport.cs) |
| Médio | `logging.md` mostrava saídas de diagnóstico com nomes de pacote padrão e só descrevia o caminho desktop. | O script inclui `-diagnostic` antes de `-portable-x64` ou `-setup-x64`. A localização dos logs também depende do MSIX ou de `MPVNET_LOCAL_APPDATA`. [Script de release](../../../src/Tools/build-release-package.ps1) · [AppPaths](../../../src/MpvNet/Infrastructure/Folder.cs) |
| Médio | A configuração técnica dizia que `--diagnose-components` não escrevia. O staging era descrito como descartado por inteiro. | `--diagnose-paths` termina antes de criar pastas. `--diagnose-components` executa após a preparação local, sem iniciar downloads. O texto diferencia as gerações de staging das transferências parciais retomáveis. [Ponto de entrada](../../../src/MpvNet.Windows/Program.cs) · [Downloads](../../../src/MpvNet/Infrastructure/RuntimeComponents/RuntimeComponentDownloads.cs) |
| Médio | `localization.md:283` e `manual.md:622` ensinavam procedimentos com exemplos de código-fonte C#. | Um procedimento passou a apontar para o catálogo e o outro ganhou uma chamada PowerShell. Os argumentos e os campos do catálogo continuam documentados. |
| Médio | O quadro do WAP sugeria equivalência entre o alvo declarado no projeto de pacote e o alvo efetivo do executável. O nome do script sugeria envio à Store. | O guia identifica o payload .NET 10 do projeto Windows, o SDK exigido pelo WAP e o mínimo do manifesto. O script gera e inspeciona o pacote localmente. [Projeto WAP](../../../src/MpvNet.Pacote/MpvNet.Pacote.wapproj) · [Manifesto](../../../src/MpvNet.Pacote/Package.appxmanifest) · [Script Store](../../../src/Tools/publish-store-package.ps1) |
| Baixo | `build-release.md`, `mpv-integration.md` e `windows-ui.md` repetiam H1. Localização, logs, manual e build tinham prosa como “validacao”, “traducoes”, “compativeis” e “Retencao”. | Revisão de acentuação, concordância, voz, cercas e níveis de títulos. As âncoras antigas sem acento continuam disponíveis por aliases. |
| Baixo | Manutenção de pacotes, variantes de CPU, idiomas e cache repetiam comandos ou tabelas em várias páginas. Validações datadas apareciam ao lado de regras atuais. | Cada referência passou a apontar para a seção que mantém o detalhe. As medições continuam nas páginas das respectivas áreas, em registros de validação. |

## B — Revisão documentada

| Informação | Decisão editorial e conteúdo preservado |
| --- | --- |
| Uso e entrada de mídia | O manual concentra instalação, controles, capas, CLI, disco/playlist, retomada e troubleshooting. A lista de extensões ficou junto da instalação e manteve as quatro listas padrão, as diferenças entre instalador e MSIX e o limite do suporte baseado em extensão. |
| Usuário e implementação de streaming | O manual mantém as opções usuais, a política de histórico de URLs, autenticação, riscos de cookies e o diagnóstico. A integração é a referência da tabela de cache, dos hooks, precedência, adaptação e reconexão. A configuração técnica mantém as origens, hash, staging, prazo, retomada e fallback dos componentes. |
| Idiomas | O catálogo técnico reúne uma tabela dos 20 idiomas e outra de variantes. O manual usa um comando reutilizável e uma tabela de códigos de CLI. Permaneceram os aliases, gettext, o fallback, `alang`/`slang`, RTL/LTR e a formatação regional. |
| Build e distribuição | O guia operacional reúne os comandos rotineiros para pacotes locais. A referência de build mantém SDK, scripts, ordem das etapas, validação do payload, versionamento, assinatura, WACK e publicação. O guia de logs mantém os comandos com `-EnableFileLogging`. O README do WAP aponta para a referência do pacote. |
| Configuração e atalhos | Configuração rápida mantém precedência e layout de `portable_config`. A configuração técnica registra os caminhos de dados e opções de migração. A referência de atalhos mantém as tabelas, o diagnóstico efetivo e os conflitos do exemplo J/K/L. |
| Histórico | Resultados de mídia, hardware, serviços e cache foram reunidos em [Registros de validação](../mpv-integration.md#registros-de-validação). Em interface, o smoke test está junto do checklist e os ensaios SMTC/capas seguem datados. Os números da expansão de idiomas ficaram associados à rodada correspondente. Baselines de C# e qualidade preservam a evidência original. As notas inglesas do NGettext.Wpf identificam o procedimento NuGet upstream. |

### Exemplos C# removidos

| Local original | Substituição |
| --- | --- |
| `docs/manual.md:622`, integração com `ProcessStartInfo.ArgumentList` | [Chamada PowerShell](../../manual.md#integração-por-parâmetros-vídeo-título-e-idioma) com argumentos separados, diretório do executável, nova instância, `--alang=`, idioma com fallback para inglês, título e mídia. |
| `docs/developer/localization.md:283`, registro de `portuguese-brazil` | Instrução para registrar valor público, cultura BCP 47, pasta gettext e aliases no [catálogo central](../../../src/MpvNet/Services/LanguageCatalog.cs). Registrar um idioma exige editar o catálogo e atualizar os recursos relacionados. |

Os exemplos INI, gettext, `.conf`, opções do mpv, caminhos de código e
identificadores de API permanecem nas seções em que são necessários. Os
trechos de C# em prompts e planos históricos são referências desses artefatos.

## C — Validação executada

| Verificação | Método e resultado |
| --- | --- |
| Diff | `git diff --check` passou. A revisão incluiu o arquivo novo e conferiu arquivos rastreados, não rastreados e área de staging. Somente Markdown integra o lote. |
| Markdown, UTF-8 e links | Python 3.14.4, com `markdown-it-py` 4.2.0 no modo CommonMark e tabelas, verificou decodificação UTF-8, H1 único, níveis, cercas, destinos e fragmentos. Foram avaliados 73 arquivos Markdown, incluindo as referências. Foram resolvidas 262 ocorrências de links locais e intra-repositório, incluindo os fragmentos. |
| Âncoras anteriores | Os títulos de navegação foram comparados a `HEAD`. As mudanças ortográficas têm aliases. O único fragmento que deixou de existir era de um parágrafo sobre Desktop Bridge interpretado como título Setext pelo separador `---`. A seção de validação do pacote mantém o acesso ao assunto. |
| Caminhos e branch | Cruzamento de caminhos literais com o checkout e de `master` com o remote HEAD e os workflows. `lang/cs-files.txt` é citado como arquivo antigo que não deve ser recriado. `Packaging.Distribution.props` é local e vem de `Packaging.Distribution.props.example`. Caminhos com variáveis, exemplos de mídia, saídas de build e artefatos históricos foram conferidos pelo contexto. |
| Exemplos C# | A leitura dos blocos com e sem rótulo e a busca por sintaxe C# não encontraram exemplos de código-fonte na documentação de uso ou técnica. |
| PowerShell | O parser do PowerShell 7.6.9 aceitou os 44 blocos. A AST permitiu conferir 25 chamadas a scripts do repositório e 27 parâmetros nomeados nas declarações dos scripts. Um processo local capturou os argumentos da amostra do manual em dois casos, com espaços e Unicode, aspas no título, URL com query e fallback para inglês. Ambos preservaram os cinco argumentos e restauraram o diretório de trabalho. |
| Windows, framework e versão | `.\src\Tools\validate-windows-baseline.ps1` passou. Os projetos declaram `net10.0-windows10.0.19041.0` e o manifesto MSIX tem mínimo 19041. A avaliação com `dotnet msbuild -getProperty` confirmou SDK 10.0.404, C# 14.0, RID `win-x64`, caminhos de saída Debug/Release e logging desativado em Release. `global.json` seleciona .NET 10 estável. A revisão configura a versão desktop 7.1.42.0 e a versão Store 7.1.41.0. Esses valores descrevem o checkout. |
| Catálogos, extensões e cache | `.\lang\validate-po-files.ps1 -ValidateOnly` passou com 807 entradas únicas em cada um dos 19 catálogos, sem ausências, extras, duplicatas ou vazios. A comparação entre manual, catálogo e manifesto conferiu os 20 códigos de idioma. A checagem das listas de extensão confirmou 50 de vídeo, 40 de áudio, 8 de playlist e 14 de imagem. Os três grupos de cache coincidem com `MediaInput.cs`. |

O manual do mpv serviu para conferir opções nativas como
`cover-art-file`. A documentação do extrator ficou como referência para
instruções de cookies e requisitos EJS sujeitos a alterações. [Manual do mpv](https://mpv.io/manual/master/) · [Cookies de YouTube](https://github.com/yt-dlp/yt-dlp/wiki/Extractors#exporting-youtube-cookies) · [EJS](https://github.com/yt-dlp/yt-dlp/wiki/EJS)

## D — Resultado e limitações

O manual concentra as tarefas do usuário e oferece um comando reutilizável
para integração. A leitura técnica encontra o comportamento vigente antes das
medições datadas. Os riscos principais eram perder opções, enfraquecer
restrições de compatibilidade, transformar evidência antiga em garantia atual
ou quebrar links com a correção de títulos. A revisão reteve as configurações
e os registros necessários e verificou os destinos locais.

A auditoria não executou build, suíte xUnit, player ou scripts de empacotamento
e publicação. A reprodução externa e prolongada, a matriz de CPU/DPI,
a instalação/atualização de MSIX assinado e a certificação Store continuam
dependentes de teste específico. O exemplo do manual foi validado na passagem
dos argumentos, sem abrir a mídia. Os links externos foram consultados
pontualmente para conferir contratos, sem varredura de disponibilidade.
