# Baseline da modernização para C# 14

Auditoria de 11/11/2026, antes das alterações de produção. Base `master` no
commit `da1838c5e608b9f4d1cf674c021fb000da123b25`, com árvore limpa.

## Ambiente e configuração encontrada

- Windows 11 Pro x64, build 26300, Intel Core i5-8400, sessão gráfica local.
- O `dotnet --info` e `dotnet --list-sdks` iniciais mostraram somente o SDK
  11.0.100 no PATH e runtimes de desktop 10.0.15 e 11.0.0. A raiz não tinha
  `global.json`. Para comparar a mesma cadeia de ferramentas, foi extraído
  o SDK oficial 10.0.404 x64 em uma pasta temporária e conferido o SHA-512
  do ZIP contra os metadados da Microsoft. Nesse ambiente, `--list-sdks`
  retornou apenas 10.0.404, com Roslyn 5.9.0-1.26529.119 e MSBuild 18.9.15.
  O MSBuild do Visual Studio 2026 com Desktop Bridge era 18.12.2.
- `src/Directory.Build.props` declarava C# 12.0, nullable, analisadores,
  estilo no build e `AnalysisLevel=10`. Núcleo, Windows, localização,
  testes e extensão avaliavam `net10.0-windows10.0.19041.0`; a solução
  compila em Debug/Release x64. A extensão tem solução própria.
- O WAP declara o mesmo TFM e mínimo 19041 no manifesto. Os targets
  Desktop Bridge restauram o grafo de empacotamento como UAP. O
  `.wapproj` não compila C# diretamente e referencia o executável Windows
  com as propriedades do projeto C#.
- Dependências diretas da base: CommunityToolkit.Mvvm 8.4.2, NGettext 0.6.7,
  Microsoft.Xaml.Behaviors.Wpf 1.1.161, Windows SDK BuildTools
  10.0.28000.2957, xUnit v3 4.0.2, runner 4.0.1 e ArchUnitNET 0.13.5.
  A consulta NuGet em Windows, NGettext.Wpf e testes encontrou xUnit 4.0.3
  como atualização direta da suíte. O Dependabot monitora NuGet e Actions.

## Comandos e resultados iniciais

Executados na raiz com o SDK 10 no PATH e `DOTNET_ROOT` apontando para a
extração. Para MSBuild.exe, `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` apontou
para a mesma extração. `MPVNET_LOCAL_APPDATA` isolou os dados dos testes.
Para cada configuração, `$cfg` é `Debug` ou `Release`.

| Verificação | Comando ou procedimento | Evidência |
| --- | --- | --- |
| Solução x64 e WAP | `MSBuild.exe src/MpvNet.sln -restore -t:Rebuild -p:Configuration=$cfg -p:Platform=x64 -p:AppxPackageSigningEnabled=false -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Always -p:AppxPackageDir=$out -p:UseSharedCompilation=false -m:1` | Debug e Release, exit 0, com MSIX bundles x64 sem assinatura. |
| Build gerenciado | `dotnet build src/MpvNet.Tests/MpvNet.Tests.csproj -c $cfg -t:Rebuild -p:Platform=x64 -p:UseSharedCompilation=false -m:1` | Debug e Release, exit 0. O projeto de teste referencia e compila o Windows, núcleo e NGettext.Wpf. |
| Extensão | `dotnet build src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj -c $cfg -p:Platform=x64` | Debug e Release, exit 0. |
| xUnit v3 | `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-build --no-restore -c $cfg -p:Platform=x64 -- -noLogo` | Release, exit 0, 1.094/1.094. Primeira execução Debug interrompida após travar com erro registrado em `mpv_wait_event`. Reexecução Debug com variante normal e relatório XML: 1.094 casos, 1.091 aprovados e 3 falhas. |
| Contratos do Windows | `src/Tools/validate-windows-baseline.ps1` | Exit 0. |
| Advisory NuGet | `dotnet list src/MpvNet.Tests/MpvNet.Tests.csproj package --vulnerable --include-transitive` | Exit 0, sem advisories para o grafo consultado. |
| Formatação | `dotnet format whitespace <projeto> --verify-no-changes --no-restore --include <arquivos do lote>` | Exit 2 na base: 152 WHITESPACE em MpvClient.cs, 3 CHARSET no núcleo e 59 ENDOFLINE em LibMpvInteropTests.cs. |
| Pacote | `validate-package-contents.ps1` e `validate-native-dependencies.ps1` no bundle Debug | Payload, exports e hashes das duas DLLs, MediaInfo 26.10.0.0, OSC e os 19 catálogos presentes. |

As falhas Debug com a variante normal foram
`AudioArtworkApplicationTests.PlayerArtworkDoesNotBlockPlaybackOrOverrideExplicitVideoSettings`
(caso de WAV com arte desativada, fonte esperada `none`, obtida `null`),
`GitHubReleaseClientTests.SlowTransferContinuesWhileBytesKeepArriving`
(timeout de inatividade de 0,3 s) e
`StartupLocalizationTests.CommandLineLanguageAppliesToTheInitialNativeMessage`
(timeout de conexão ao IPC no caso zh-CN). Havia outros processos de build e
smoke ativos durante essa execução. A causa das três falhas permanece em
aberto. O episódio do smoke que enviou `quit` antes de o frontend publicar
o sinal de prontidão é separado dessas falhas da suíte.

### Avisos por configuração, sem somar builds repetidos

| ID | Arquivo/projeto | Solução Debug | Solução Release | Gerenciado por configuração |
| --- | --- | ---: | ---: | ---: |
| xUnit1031 | `CoreCoverageTests.cs:590,592` | 2 | 2 | 2 |
| NU1701 | `MpvNet.Pacote.wapproj`, Xaml.Behaviors.Wpf no restore UAP | 1 | 1 | 0 |
| **Total** | | **3** | **3** | **2** |

Os builds completos não emitiram avisos CS de nullable, obsolescência ou
async. Para o lote, o gate é zero diagnósticos novos de build. Compare os
logs `-flp:logfile=<arquivo>;warningsonly` por projeto, ID e local da
origem, sem contar novamente as linhas de resumo ou as recompilações do
projeto WPF. A verificação de formatação é limitada aos arquivos tocados.

## Inspeção e prioridades

| Prioridade | Evidência | Impacto, confiança e custo |
| --- | --- | --- |
| P1 | `Directory.Build.props:12` e ausência de `global.json` | A estação selecionou SDK 11 apesar do TFM 10. Alta confiança, custo baixo. Limitar a resolução à série estável 10.0 e usar C# 14 centralmente. |
| P1 | `MpvClient.cs:568–622,695–719` e `UnmanagedStringArray.cs:22–27` | Duas leituras de propriedade só liberam o buffer se a conversão concluir. Um ponteiro de comando é registrado depois da cópia, de modo que a limpeza parcial não o encontra caso a cópia falhe. Alta confiança estática, custo baixo. |
| P2 | `LibMpv.cs:279–293` | Decodificação UTF-8 faz leitura byte a byte e cópia intermediária. A API estável do runtime usa o mesmo decodificador. Alta confiança para ponteiros válidos da ABI, custo baixo com teste de fronteira. |
| P2 | `CommandLine.cs:6–26,38–48,259–288` | O campo privado serve só ao getter lazy. C# 14 permite aproximar o cache da propriedade via `field`. `NotNullWhen(true)` expressa o contrato do parser privado e dispensa três operadores `!`. Alta confiança, custo baixo. |
| P2 | `CoreCoverageTests.cs:575–594` | Dois waits bloqueantes reportados. A aquisição e a liberação de `ReaderWriterLockSlim` devem ocorrer no mesmo thread, portanto um `await` direto dentro do lease seria incorreto. Alta confiança, custo baixo com sincronização explícita. |
| P2 | `MpvNet.Pacote.wapproj`, restore Desktop Bridge | NU1701 histórico no grafo UAP. O build gerenciado usa seu próprio target; pacote local gerado e validado. Confiança alta no diagnóstico, custo médio para trocar versão/empacotamento e provar compatibilidade da Store. |

Nenhum P0 confirmado. As medições de tamanho apontaram `MainForm.cs` (1.305
linhas), `MpvClient.cs` (853), `Player.MediaLoading.cs` (780) e
`RuntimeComponentService.cs` (621), além do interop WPF e dos diálogos.
Vários módulos já têm partials por responsabilidade. Os adaptadores
`Global.Player`, `Translator`, `ExtensionLoader`, `Folder`,
`TaskHelp`, `Conf` e as APIs de compatibilidade de NGettext.Wpf marcados
como obsoletos exigem compatibilidade com consumidores externos, bindings e
extensões. A revisão estática também considerou o manifesto e comandos/scripts
por nome. Não há remoções propostas a partir de busca textual isolada.

A busca em produção não encontrou declarações `async void`. Foram
inspecionados o `Task.Run` e a espera síncrona em `Player.State.cs` e
`Player.Lifecycle.cs`, o caminho de shutdown da janela em `MainForm.cs`,
os gates de cache/armazenamento, o timeout de processos em
`OnlineMediaDiagnostics.cs`, o `HttpClient` compartilhado em
`GitHubReleaseClient.cs` e o cancelamento de thumbnail em
`WindowsSystemMediaTransportService.cs`. A espera do player precede a
liberação dos handles. As exceções de cancelamento esperado, os fallbacks e
as continuations que removem tarefas precisam preservar esse contrato.

`.editorconfig` usa sugestões para estilo, mantém `CA1822` desativado,
`WFO1000` silencioso e exclui código gerado/HandyControl conforme as regras
atuais. `CA1401` preserva as entradas públicas da ABI, `SYSLIB1054` mantém
o marshalling estabelecido e os pragmas de LibMpv mantêm nomes C e strings
UTF-8 explícitas. `CA2000` no setup de SMTC indica transferência de
ownership ao controlador. Há uma justificativa pendente para aliases de
enumeração em `InteropValues.cs:955`. Alterar essas regras globalmente
mudaria o baseline e exigiria uma revisão própria.

## Inventário de interoperabilidade x64

`LibMpv.cs` tem 32 declarações DllImport, todas Cdecl e encaminhadas pelo
resolvedor do assembly para uma DLL por processo.

| Operação | Entradas |
| --- | --- |
| Contextos | `mpv_client_api_version`, `mpv_create`, `mpv_create_client`, `mpv_initialize`, `mpv_destroy`, `mpv_terminate_destroy` |
| Comandos e erro | `mpv_command`, `mpv_command_async`, `mpv_command_string`, `mpv_command_ret`, `mpv_error_string` |
| Opções e propriedades | `mpv_set_option`, `mpv_set_option_string`, cinco formas de `mpv_get_property` (ponteiro, int, long, double, nó), três de `mpv_set_property` e `mpv_set_property_string` |
| Observação e eventos | `mpv_request_log_messages`, `mpv_observe_property`, `mpv_unobserve_property`, `mpv_wait_event`, `mpv_request_event`, `mpv_hook_add`, `mpv_hook_continue` |
| Desalocação | `mpv_free`, duas formas de `mpv_free_node_contents` |

Strings C usam `LPUTF8Str` ou bytes UTF-8 com terminador. Os vetores de
comando são gerenciados por `UnmanagedStringArray` e liberados com
`FreeHGlobal`. As strings de propriedade retornadas pertencem ao chamador
e requerem `mpv_free`. Resultados em nó requerem
`mpv_free_node_contents` após sucesso e a estrutura externa continua sob
ownership de quem a alocou. Buffers de eventos e mensagens de erro são
emprestados. O loop faz snapshot antes de avançar para o próximo evento;
callbacks são gerenciados e despachados fora do lock. Não há GCHandle de
callback nem SafeHandle nesses imports. O gate do cliente controla o
descarte dos handles. `mpv_event` e `mpv_event_property` têm 24 bytes em
x64. `mpv_node` tem união no offset 0, formato no 8 e tamanho 16.
`mpv_event_end_file` representa apenas o prefixo de razão e erro.
Os valores públicos de eventos depreciados continuam parte do contrato.

## Cobertura e medição

A suíte já exercita ABI e seleção de libmpv, lifecycle e confiabilidade,
contratos ArchUnitNET, cache nativo/política/sessão, live recovery,
diagnósticos de streaming, SMTC, OSC, playlists, entradas e localização,
inclusive subprocessos de startup. Lacunas do lote: UTF-8 malformado e
terminação NUL em buffers, equivalência entre os leitores de propriedade
e regressões físicas de CPU sem AVX2. Percentuais de cobertura, duplicação e
complexidade não foram medidos.

O laboratório usou o executável Debug x64, a revisão libmpv `bcde6b93cc`,
seleção automática, configuração isolada, áudio nulo e mídia sintética de
8 s, H.264/AAC, 320×180. O tempo até prontidão termina na publicação de
`user-data/frontend/name` via pipe IPC, a abertura termina ao detectar
duração, a troca de item termina com o caminho e as quatro faixas corretas,
e o fechamento termina na saída normal após `quit`. A playlist contém mil
caminhos distintos no disco. As cinco execuções por cenário são processos
novos. Não houve limpeza do cache de disco do Windows. O working set e os
bytes privados são do processo inteiro, sem atribuição gerenciada/nativa.

| Cenário | Medida da base |
| --- | --- |
| Inicialização sem mídia | Primeira execução 729,0 ms, mediana das quatro seguintes 699,6 ms. Medianas das cinco: fechamento 656,0 ms, bytes privados 65,8 MiB e working set 135,2 MiB. |
| Abertura de MP4 | Mediana 910,3 ms, bytes privados 88,2 MiB. |
| Playlist de 1.000 entradas e troca | Medianas de 1.792,8 ms para carregar e 42,2 ms para a troca, bytes privados 96,4 MiB. |

A GUI pode ser executada neste ambiente. Validação visual abrangente,
CPU física sem AVX2, reprodução externa prolongada, assinatura,
instalação/atualização e certificação de Store permanecem fora desta
evidência inicial. O inventário de `.ai/prompts/` e a documentação técnica
atual já registram implementação de lifecycle, variantes, idiomas, SMTC,
arte de áudio, playlists e cache por item. O lote aproveita essas
implementações e a cobertura existente.

Referências de contrato: [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14),
[resolução de SDK](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json),
[API C do mpv](https://github.com/mpv-player/mpv/blob/bcde6b93cc/include/mpv/client.h)
e [Marshal do .NET 10.0.15](https://github.com/dotnet/runtime/blob/v10.0.15/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/Marshal.cs).
