# Resultado da modernização para C# 14

Execução de 11/11/2026 na branch `codex/modernize-csharp14-net10`, a partir
de `master` em `da1838c5`. Ambiente Windows 11 Pro x64 build 26300,
Intel Core i5-8400, SDK .NET 10.0.404 e MSBuild do Visual Studio 18.12.2.
O [baseline](csharp14-modernization-baseline.md) registra os diagnósticos
antes das alterações.

## Alterações por etapa

| Etapa | Commit | Mudança |
| --- | --- | --- |
| A | `b7cf02e` | Inventário, builds/testes C# 12, avisos, inspeção da ABI, medição inicial e riscos. |
| B | `eeb734e` | `global.json` seleciona a série estável 10.0. `src/Directory.Build.props` fixa C# 14.0 com nullable, estilo e AnalysisLevel 10. |
| C1 | `9c95dcc` | `CommandLine.Arguments` usa `field` no getter lazy. O parser privado declara `NotNullWhen(true)`, dispensando três operadores `!`. Os testes de CLI e startup passaram em 331 casos. |
| C3–C5 | `c24c762`, `f6dc950` | `MpvClient` unifica as leituras normal e OSD com seus formatos e diagnósticos. A desalocação com `mpv_free` ocorre em `finally` dentro do gate. `LibMpv` usa o decodificador UTF-8 do runtime e dispensa o buffer temporário por string. `UnmanagedStringArray` registra cada ponteiro antes da cópia para permitir limpeza parcial. |
| C4 e D | `21da0a4` | O teste de shutdown aguarda o descarte sem cruzar threads com o lease do `ReaderWriterLockSlim`. A suíte ganhou casos de UTF-8 e de propriedade nativa, sem acrescentar loops de eventos ao novo teste. |

Os cinco `.csproj` foram avaliados com `NETCoreSdkVersion=10.0.404`,
`TargetFramework=net10.0-windows10.0.19041.0`, `LangVersion=14.0`,
`Nullable=enable`, `EnableNETAnalyzers=true`, `AnalysisLevel=10`,
`EnforceCodeStyleInBuild=true` e `RuntimeIdentifier=win-x64`. MSBuild.exe
confirmou o mesmo TFM, SDK e linguagem no projeto Windows usado pelo WAP.
O wrapper Desktop Bridge avalia `net451` internamente e usa restore UAP
para o grafo de embalagem. O payload C# do pacote vem do projeto Windows
.NET 10 e o mínimo do manifesto continua 10.0.19041.0.

O lote preserva as declarações Cdecl, os layouts, a resolução de variantes
e a superfície pública. Os adaptadores obsoletos continuam disponíveis
para consumidores externos. A revisão de async, UI e mídia não justificou
mudar a ordem de shutdown, dispatch da UI, hooks de cache, recuperação de
live, seleção de idioma ou o formato da configuração. As dependências e
workflows conservaram suas versões. A atualização xUnit 4.0.3 foi
avaliada pelo changelog. As correções do runner e de relatórios não
resolvem a exceção observada dentro da DLL. [Release do xUnit 4.0.3](https://xunit.net/releases/v3/4.0.3).

## Avisos e análise

Contagem de diagnósticos emitidos por configuração, sem repetir as linhas
do resumo ou somar a compilação duplicada de projetos.

| Diagnóstico | Debug antes → depois | Release antes → depois | Arquivo |
| --- | ---: | ---: | --- |
| xUnit1031 | 2 → 0 | 2 → 0 | `MpvNet.Tests/CoreCoverageTests.cs` |
| NU1701 | 1 → 1 | 1 → 1 | `MpvNet.Pacote.wapproj`, restore UAP de Xaml.Behaviors.Wpf |
| **Total da solução** | **3 → 1** | **3 → 1** | |

O build gerenciado passou de 2 para 0 avisos. Não houve ID de build novo.
O `NU1701` segue visível no WAP e os builds do executável gerenciado não
o emitem. A revisão da alteração verificou ownership, limites de ABI e
ausência de novas chamadas externas ou dependências. A comparação
delimitada de `dotnet format whitespace --verify-no-changes --no-restore
--include` reduziu os diagnósticos em `MpvClient.cs` de 152 para 124,
removeu três diagnósticos CHARSET no núcleo e 59 ENDOFLINE nos testes.
Os 124 diagnósticos de indentação são anteriores e ficam fora das linhas
editadas. O verificador do núcleo retorna 2 por eles. Nos testes ele
retornou 0. `git diff --check` retornou 0.

## Validação executada

| Verificação | Comando ou procedimento | Resultado |
| --- | --- | --- |
| Solução e bundle MSIX | `MSBuild.exe src/MpvNet.sln -restore -t:Build -p:Configuration=Debug -p:Platform=x64 -p:AppxPackageSigningEnabled=false -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Always -p:AppxPackageDir=$out -p:UseSharedCompilation=false -m:1`. Release com `-t:Rebuild`. | Exit 0 em Debug e Release. Bundle x64 sem assinatura em ambas. |
| Extensão | `dotnet build src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj -c $cfg -p:Platform=x64 -p:UseSharedCompilation=false -m:1` | Exit 0, zero avisos em Debug e Release. |
| xUnit v3 e ArchUnitNET | `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-build --no-restore -c $cfg -p:Platform=x64 -- -noLogo -longRunning 15 -reporter verbose -result-xml $xml` | Release automático 1.096/1.096 (57,516 s), Debug com `MPVNET_FORCE_LIBMPV_VARIANT=normal` 1.096/1.096 (57,095 s). As duas regras ArchUnitNET passaram nas duas execuções. |
| Debug automático | Mesmo comando com seleção automática de DLL. | Execução interrompida após `DivideByZeroException` em `mpv_wait_event` e ausência de progresso em `NativePlaylistTitleTests.YtDlpBootstrapUpdatesOnlyTheAutomaticPathWithoutReloadingThePlaylist`. A pilha também apareceu no baseline, antes das alterações. |
| Interop, lifecycle, cache e playlist | Filtro de classes do runner xUnit nas variantes `normal` e `x86_64-v3`. | 97/97 em cada variante no lote. Depois do isolamento do novo teste síncrono, `LibMpvInteropTests` passou em 6/6 por variante. Testes de lifecycle passaram em 11/11. |
| ABI e fallback real | `dotnet mpvnet.dll --diagnose-libmpv` em cópia de teste, com seleção normal, v3 e automática com a v3 ausente e inválida. | API 2.6 (`0x20006`), criação e destruição do contexto em ambas as variantes. Seleção da normal nos dois cenários de fallback. |
| Baseline Windows | `src/Tools/validate-windows-baseline.ps1` | Exit 0, TFM e mínimo MSIX 19041 conferidos. |
| Dependências | `dotnet list src/MpvNet.Tests/MpvNet.Tests.csproj package --vulnerable --include-transitive --no-restore` | Exit 0, sem advisories no grafo gerenciado consultado. O restore WAP também não emitiu NU190x. |
| Catálogos | `lang/validate-po-files.ps1 -ValidateOnly`, `python lang/test-language-expansion.py`, `src/Tools/test-localization-package.ps1` | Exit 0, 19 catálogos e 807 chaves por PO sem ausências, duplicações ou vazios. |
| Pacotes | `test-libmpv-build-contract.ps1`, `validate-native-dependencies.ps1`, `validate-package-contents.ps1` e `build-release-package.ps1 src $out -SkipGitHubRelease` | Exit 0. Bundle MSIX Release e ZIP portátil x64 com conteúdo validado. Instalador Inno Setup 7.1.41.0 x64 compilado a partir do publish validado. O portátil passou em `mpvnet.com --diagnose-libmpv`. |

O MSIX Release antes/depois preservou os 533 caminhos do pacote principal
x64. SHA-256 idêntico em 23 assets, incluindo `libmpv-2.dll` e
`libmpv-2-v3.dll` da revisão `bcde6b93cc`, MediaInfo 26.10.0.0,
`Scripts/osc.lua` e 19 catálogos `.mo`. A configuração de runtime da
saída Release declara `net10.0` com runtime 10.0.15.
No grafo gerenciado, o pacote Xaml.Behaviors resolve
`lib/net10.0-windows7.0/Microsoft.Xaml.Behaviors.dll`. No grafo WAP,
o aviso UAP histórico permanece.

Os logs de build, `warningsonly`, XML xUnit, medições e pacotes locais
ficaram em `%TEMP%\mpvnet-csharp14-da1838c5`. As verificações locais
usaram `DOTNET_ROOT` e `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` apontando
para a extração do SDK 10 e `MPVNET_LOCAL_APPDATA` isolado.

## Desempenho e smoke

Os mesmos binários Debug x64 e a mesma revisão libmpv do baseline
foram exercitados com perfil temporário, seleção automática e áudio
nulo. Foram cinco processos por cenário e versão, com as métricas
do [baseline](csharp14-modernization-baseline.md#cobertura-e-medição).
Quatro pares adicionais alternaram a ordem de execução e criaram um
perfil novo por amostra. Os números são medianas, exceto a primeira linha.

| Medida | C# 12 | C# 14 |
| --- | ---: | ---: |
| Primeiro processo observado | 1.561,7 ms | 2.161,5 ms |
| Inicialização com perfil novo, pares alternados | 771,8 ms | 765,9 ms |
| Inicialização com perfil reaproveitado | 674,3 ms | 675,0 ms |
| Abertura de MP4 | 937,3 ms | 880,2 ms |
| Carregamento de playlist com 1.000 entradas | 1.725,5 ms | 1.727,6 ms |
| Troca de item na playlist | 39,6 ms | 38,5 ms |
| Fechamento sem mídia, perfil reaproveitado | 645,3 ms | 641,5 ms |
| Fechamento com MP4 | 2.369,3 ms | 2.403,7 ms |
| Fechamento após troca de item | 790,4 ms | 794,0 ms |
| Bytes privados sem mídia, perfil reaproveitado | 65,8 MiB | 65,8 MiB |
| Bytes privados com MP4 | 78,6 MiB | 73,5 MiB |
| Bytes privados com playlist | 89,2 MiB | 89,2 MiB |
| Working set sem mídia, perfil reaproveitado | 135,1 MiB | 135,6 MiB |
| Working set com MP4 | 165,4 MiB | 160,5 MiB |
| Working set com playlist | 179,0 MiB | 181,0 MiB |

A diferença isolada na primeira inicialização não se repetiu com perfis
novos em ordem alternada. O ensaio não controlou o estado do cache de
disco entre as primeiras execuções nem mediu um boot frio do Windows.
As medianas comparáveis não indicaram regressão material neste conjunto.
O consumo é do processo inteiro. As amostras não medem retenção de memória
em sessões longas nem isolam o efeito da refatoração UTF-8.

O smoke IPC passou nos oito cenários antes e depois. Cobriu MP4 Unicode,
MKV com duas faixas de áudio e legenda interna, SRT externa, MP3, FLAC,
M3U, HTTP MP4 e HLS VOD/live sintético em loopback. Confirmou avanço
da reprodução, pausa, seek, seleção de faixa/legenda, volume, troca
de item e saída normal. A suíte nativa verificou a precedência das
opções explícitas do mpv sobre o cache automático.

O primeiro roteiro de smoke enviava `quit` assim que o IPC nativo
respondia, antes da criação do cliente `mpvnet`, e provocou a mensagem
de inicialização. O roteiro foi corrigido para aguardar
`user-data/frontend/name` antes de operar. Os smokes completos usaram
o sinal corrigido.

No YouTube, com yt-dlp 2026.11.04.232659 e perfil isolado, o player
carregou um vídeo público de 177 s, selecionou uma faixa de áudio e
capturou dois quadros distintos a 0,500 s e 2,208 s. Para um vídeo
indisponível, o hook reportou exit 1 e o player retornou ao estado ocioso.
O teste curto não caracteriza congelamento intermitente em sessões longas.

## Limitações e reversão

O i5-8400 permite executar as duas DLLs e provocar fallback removendo ou
invalidando a v3 em uma cópia temporária. O caminho de CPU sem AVX2 tem
teste de seleção simulado. Falta a validação em CPU física desse perfil.
O caso intermitente em `mpv_wait_event` na execução automática Debug
já ocorria em C# 12. Ele continua aberto e não foi atribuído à linguagem,
à escolha de CPU ou ao novo teste. O escopo local não mede sessões longas
de cache ou a divisão entre heap gerenciado e memória nativa.

A checagem visual cobriu os temas escuro e claro, o menu em português
e em árabe com escrita RTL, o botão de tela cheia, a saída com Esc,
o atalho Espaço e o fechamento pela janela. Os testes automatizados
cobrem SMTC, OSC, atalhos e idioma. A inspeção visual de SMTC/taskbar,
drag/drop e transição de DPI entre monitores ficou pendente, assim
como a confirmação em Windows 10 físico. A validação de pacote local
cobre o conteúdo do MSIX sem assinatura. Instalação/atualização
assinada e certificação do Partner Center dependem de ambiente de Store.

Antes da integração, a revisão pode permanecer no PR. Se a mudança já
tiver sido integrada com esses commits, reverta `f6dc950`, `21da0a4`,
`c24c762`, `9c95dcc` e `eeb734e` nessa ordem, ou reverta o commit
agregado se o PR for integrado por squash. Recompile com o SDK .NET 10
e repita os testes x64. O baseline e este relatório podem permanecer
como registro histórico.
