# Tarefa: implementar seleção automática entre libmpv normal e x86-64-v3 no MPV.NET

Trabalhe exclusivamente no repositório:

`WandersondeSouza/mpv.net`

Branch principal:

`main`

## Objetivo geral

Implementar, de forma segura, testável e compatível com as distribuições existentes, o suporte simultâneo a duas builds da libmpv:

- build x64 normal, destinada à maior compatibilidade possível;
- build `x86_64-v3`, otimizada para processadores modernos.

As duas versões devem:

1. ser baixadas durante a preparação das dependências;
2. ser mantidas simultaneamente;
3. ser copiadas para o output de desenvolvimento;
4. ser incluídas na publicação self-contained;
5. ser incluídas no ZIP portátil;
6. ser incluídas no instalador executável;
7. ser incluídas no pacote MSIX/Microsoft Store;
8. ser validadas nos artefatos finais;
9. ser selecionadas automaticamente na inicialização do player;
10. possuir fallback seguro para a versão normal.

Nenhuma funcionalidade de reprodução, interface, configuração, associação de arquivos, cache, streaming, atualização, localização ou empacotamento que não seja necessária para este trabalho deve ser alterada.

Não faça uma refatoração ampla do projeto.

---

# Regra principal de compatibilidade

A versão normal deve ser sempre obrigatória e funcionar como fallback.

Use os nomes de distribuição:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

Significado:

- `libmpv-2.dll`: build normal x64, com maior compatibilidade.
- `libmpv-2-v3.dll`: build otimizada para `x86_64-v3`.

Não renomeie a versão normal para `libmpv-2-normal.dll`. A versão normal deve continuar com o nome oficial para preservar compatibilidade com o comportamento atual e permitir fallback pelo carregamento convencional.

---

# Antes de alterar o código

Faça primeiro uma investigação completa e registre um resumo técnico contendo:

1. onde a libmpv é baixada;
2. de qual release e asset ela é baixada;
3. como a variante é escolhida atualmente;
4. onde a DLL é extraída;
5. onde ela é copiada no build Debug;
6. onde ela é copiada no build Release;
7. onde ela é copiada no `dotnet publish`;
8. como é formada a versão portátil;
9. como é formado o instalador Inno Setup;
10. como é formado o pacote WAP/MSIX;
11. onde estão as declarações P/Invoke da libmpv;
12. qual assembly contém essas declarações;
13. em que ponto acontece a primeira chamada nativa;
14. em que ponto o resolvedor da DLL deve ser registrado;
15. quais testes e scripts de validação já existem.

Inspecione, no mínimo:

```text
src/Tools/prepare-native-dependencies.ps1
src/Tools/prepare-build-output.ps1
src/Tools/build-release-package.ps1
src/Tools/generate-portable-zip.ps1
src/Tools/generate-installer-exe.ps1
src/Tools/publish-store-package.ps1
src/Tools/validate-native-dependencies.ps1
src/Tools/validate-package-contents.ps1
src/Tools/test-mpv-build-variants.ps1
src/MpvNet.Windows/MpvNet.Windows.csproj
src/MpvNet.Pacote/MpvNet.Pacote.wapproj
src/MpvNet.Pacote/Package.appxmanifest
src/Setup/Inno/inno-setup.iss
.github/workflows
```

Localize também todas as ocorrências de:

```text
libmpv-2
DllImport
LibraryImport
NativeLibrary
mpv_create
mpv_initialize
MpvBuildVariant
MPVNET_MPV_BUILD_VARIANT
```

Não faça alterações antes de entender o fluxo completo.

---

# Etapa 1 — ajustar o download das duas variantes

Modifique o fluxo de preparação das dependências para baixar e manter simultaneamente:

```text
mpv-dev-x86_64-<data>-git-<commit>.7z
mpv-dev-x86_64-v3-<data>-git-<commit>.7z
```

A origem atualmente utilizada deve ser preservada, salvo se estiver comprovadamente inválida:

```text
https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest
```

Os padrões dos assets devem continuar sendo estritos, evitando escolher arquivos incorretos.

Para a versão normal:

```regex
^mpv-dev-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$
```

Para a versão v3:

```regex
^mpv-dev-x86_64-v3-[0-9]{8}-git-[0-9a-z]+\.7z$
```

Garanta que o padrão da versão normal não aceite acidentalmente a variante `x86_64-v3`.

## Requisitos de cache

Preserve a política atual de cache e o parâmetro `MaxCacheAgeDays`.

Entretanto, cada variante deve possuir seu próprio arquivo no cache. O cache da versão normal não pode ser confundido com o da v3. A atualização de uma variante não pode apagar ou invalidar indevidamente a outra.

## Extração

Extraia cada archive em um diretório isolado, por exemplo:

```text
artifacts/native-dependencies/extract/libmpv-normal
artifacts/native-dependencies/extract/libmpv-v3
```

Nunca extraia as duas variantes no mesmo diretório.

Após a extração:

- copie a normal como `libmpv-2.dll`;
- copie a v3 como `libmpv-2-v3.dll`.

## Metadados

Substitua o marcador único atual por um manifesto descritivo, preferencialmente JSON, por exemplo `libmpv-builds.json`.

Estrutura sugerida:

```json
{
  "schemaVersion": 1,
  "source": "shinchiro/mpv-winbuild-cmake",
  "normal": {
    "file": "libmpv-2.dll",
    "asset": "mpv-dev-x86_64-YYYYMMDD-git-abcdef.7z",
    "sha256": "...",
    "downloadedAtUtc": "..."
  },
  "x86_64-v3": {
    "file": "libmpv-2-v3.dll",
    "asset": "mpv-dev-x86_64-v3-YYYYMMDD-git-abcdef.7z",
    "sha256": "...",
    "downloadedAtUtc": "..."
  }
}
```

Não dependa desse arquivo para o funcionamento básico do player. Ele é metadado de diagnóstico e validação.

## Mesma versão lógica

Sempre que possível, garanta que as duas variantes pertençam à mesma publicação upstream e ao mesmo conjunto de data/commit.

Se a release mais recente não possuir as duas variantes correspondentes:

- não monte silenciosamente uma combinação inconsistente;
- apresente erro claro durante o processo de release;
- informe os nomes dos assets encontrados.

---

# Etapa 2 — validação dos binários baixados

Preserve a validação PE x64 já existente e amplie-a.

Para cada DLL:

1. verificar existência;
2. verificar tamanho maior que zero;
3. verificar assinatura PE;
4. verificar `Machine = 0x8664`;
5. calcular SHA-256;
6. verificar se as duas DLLs não são byte a byte idênticas;
7. verificar se as duas exportam as funções essenciais da libmpv.

Validar pelo menos estas exportações:

```text
mpv_client_api_version
mpv_create
mpv_initialize
mpv_command
mpv_command_string
mpv_get_property
mpv_set_property
mpv_wait_event
mpv_terminate_destroy
```

Use uma técnica confiável disponível no ambiente Windows, como leitura da tabela de exports PE, `dumpbin /exports` quando disponível ou outra solução local robusta.

Não introduza dependência obrigatória de uma ferramenta externa apenas para iniciar o aplicativo. A validação de exports deve acontecer no build/release ou nos testes, não durante toda inicialização normal do player.

---

# Etapa 3 — detecção de CPU compatível com x86-64-v3

Crie uma classe pequena, isolada e testável para detectar compatibilidade.

Use as APIs do .NET em `System.Runtime.Intrinsics.X86`.

Verifique inicialmente:

```csharp
Environment.Is64BitProcess
RuntimeInformation.ProcessArchitecture == Architecture.X64
```

Depois verifique os conjuntos de instruções necessários. No mínimo:

```csharp
Sse3.IsSupported
Ssse3.IsSupported
Sse41.IsSupported
Sse42.IsSupported
Popcnt.IsSupported
Avx.IsSupported
Avx2.IsSupported
Fma.IsSupported
Bmi1.IsSupported
Bmi2.IsSupported
Lzcnt.IsSupported
```

Analise a definição formal de `x86-64-v3` antes de concluir a lista final.

Não use somente o ano ou modelo comercial da CPU. Não use WMI como mecanismo principal. Não execute uma instrução AVX2 apenas para descobrir se ela causa exceção. Não dependa apenas de `CPUID` manual se as APIs oficiais do .NET cobrirem corretamente a necessidade.

Crie uma abstração testável, como:

```csharp
internal interface ICpuFeatureProvider
{
    bool IsX64Process { get; }
    bool IsX64Architecture { get; }
    bool SupportsX64V3 { get; }
}
```

Ou estrutura equivalente que permita simular CPUs compatíveis e incompatíveis nos testes. A implementação de produção pode consultar diretamente os intrinsics do .NET.

---

# Etapa 4 — resolução e carregamento da DLL

Antes da primeira chamada a qualquer função da libmpv, registre um resolvedor nativo com:

```csharp
NativeLibrary.SetDllImportResolver
```

O resolvedor deve ser registrado no assembly que realmente contém as declarações `[DllImport]` ou `[LibraryImport]`. Não suponha que seja necessariamente o assembly executável.

Use algo semelhante a:

```csharp
typeof(TipoQueContemOsImports).Assembly
```

## Regra de seleção

A lógica deve ser:

```text
Se o processo não for x64:
    produzir erro coerente, pois a distribuição atual é x64.

Se a CPU for compatível com x86-64-v3:
    tentar carregar libmpv-2-v3.dll por caminho absoluto.

Se a v3 não existir ou NativeLibrary.TryLoad falhar:
    registrar o motivo;
    tentar libmpv-2.dll.

Se a CPU não for compatível:
    carregar diretamente libmpv-2.dll.

Se a normal também falhar:
    lançar erro claro e acionável.
```

## Caminho seguro

Resolva as DLLs a partir de `AppContext.BaseDirectory` e use caminho absoluto.

Não use o diretório de trabalho atual. Não modifique globalmente o `PATH`. Não copie nem substitua DLLs durante a inicialização. Não renomeie fisicamente a DLL v3 para `libmpv-2.dll` em runtime. Não grave dentro da pasta instalada.

Isso é especialmente importante para Program Files, instalador Inno Setup, MSIX, Microsoft Store, múltiplas instâncias do aplicativo e atualizações em andamento.

## Nome lógico dos imports

Preserve um único nome lógico nas declarações P/Invoke:

```text
libmpv-2
```

O resolvedor deve interceptar apenas os nomes esperados, por exemplo `libmpv-2` e `libmpv-2.dll`. Para qualquer outra biblioteca, retorne `IntPtr.Zero`, permitindo que o carregamento normal do .NET continue funcionando.

## Inicialização única

A inicialização do resolvedor deve ser:

- executada uma única vez;
- thread-safe;
- anterior a qualquer inicializador estático que possa chamar a libmpv;
- anterior à criação do contexto mpv;
- anterior a `mpv_create`;
- anterior a `mpv_initialize`.

Considere `Interlocked`, `Lazy<T>`, `lock` ou mecanismo equivalente. Não registre dois resolvers no mesmo assembly.

## Diagnóstico

Exponha internamente informações como:

```text
CPU compatível com x86-64-v3: sim/não
DLL preferida
DLL efetivamente carregada
caminho carregado
fallback utilizado: sim/não
motivo do fallback
```

Registre essas informações no sistema de log já existente. Não mostre mensagens invasivas ao usuário quando o fallback para a normal funcionar. Mostre erro ao usuário apenas se nenhuma DLL puder ser carregada. Não registre dados pessoais.

---

# Etapa 5 — fallback real para falha de carregamento

O fallback não deve depender somente da detecção da CPU.

Mesmo em uma CPU compatível, a v3 pode falhar por arquivo ausente, arquivo corrompido, dependência transitiva ausente, bloqueio do Windows, erro de empacotamento, incompatibilidade inesperada ou atualização incompleta.

Portanto, detecção positiva de CPU significa apenas que a v3 pode ser tentada. Ela não significa que o carregamento está garantido.

Use `NativeLibrary.TryLoad` e somente selecione a v3 se ela for realmente carregada. Se a v3 falhar, tente a normal no mesmo processo antes de desistir. Capture e registre informações úteis, mas não esconda a falha final da DLL normal.

---

# Etapa 6 — compatibilidade com MSIX e Microsoft Store

O pacote da Microsoft Store deve conter as duas DLLs no diretório da aplicação:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

Remova a regra que torna `x86_64-v3` a única variante padrão do pacote Store. O pacote Store deve ser universal dentro do escopo x64 suportado pelo aplicativo. Não publique um pacote Store que exija x86-64-v3 para iniciar.

O carregamento deve funcionar no ambiente empacotado usando arquivos somente leitura instalados com o pacote.

Não tente substituir arquivos do pacote, escrever na pasta de instalação, baixar a v3 na primeira execução, depender de permissões elevadas, solicitar `broadFileSystemAccess`, mudar `RuntimeBehavior`, mudar `TrustLevel`, alterar a identidade da Store, alterar o Publisher, alterar associações de arquivos ou alterar App Execution Alias.

Preserve o modelo atual:

```text
packagedClassicApp
mediumIL
```

A seleção deve ocorrer apenas entre as duas DLLs já presentes no pacote.

---

# Etapa 7 — output de desenvolvimento

Após um build normal de:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

a pasta ao lado de `mpvnet.exe` deve conter:

```text
libmpv-2.dll
libmpv-2-v3.dll
MediaInfo.dll
```

O build Debug e o build Release devem preparar as duas variantes por padrão.

Caso seja necessário manter um modo rápido para desenvolvedores, crie uma propriedade explícita, por exemplo `IncludeBothMpvVariants=true`. Mas a configuração usada para release, portátil, instalador e Store deve sempre incluir ambas.

Evite manter `MpvBuildVariant` como seletor exclusivo no fluxo de distribuição. Caso o parâmetro antigo precise ser preservado por compatibilidade de scripts, documente-o como modo de desenvolvimento/teste e não como modo padrão de release.

---

# Etapa 8 — publicação self-contained

Valide o resultado de:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj `
    --self-contained true `
    --configuration Release `
    --runtime win-x64 `
    /p:IncludeNativeLibrariesForSelfExtract=false
```

O diretório publicado deve conter fisicamente:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

Não embuta essas DLLs dentro do single-file de forma que o resolvedor não consiga encontrá-las por caminho absoluto.

Preserve `PublishSingleFile=true` e `IncludeNativeLibrariesForSelfExtract=false`, salvo se a investigação demonstrar uma necessidade técnica concreta de mudança. Se precisar alterar alguma propriedade de publish, explique claramente a razão e valide todos os formatos de distribuição.

---

# Etapa 9 — ZIP portátil

Modifique a geração e a validação do ZIP portátil.

O ZIP final deve conter no mesmo diretório de `mpvnet.exe`:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

O script de geração deve falhar quando faltar a normal, faltar a v3, uma delas estiver vazia, uma delas não for PE x64, as duas forem idênticas ou faltarem exports essenciais.

Depois de gerar o ZIP:

1. abra ou extraia o ZIP para diretório temporário;
2. valide as duas DLLs dentro do artefato;
3. execute um smoke test do aplicativo extraído;
4. registre o nome das DLLs encontradas;
5. registre hashes SHA-256.

Não valide apenas a pasta intermediária. Valide o ZIP final.

---

# Etapa 10 — instalador executável Inno Setup

Atualize `src/Setup/Inno/inno-setup.iss` e os scripts relacionados.

O instalador deve incluir `libmpv-2.dll` e `libmpv-2-v3.dll` na mesma pasta de instalação de `mpvnet.exe`.

Não instale a v3 em uma subpasta diferente, salvo se o resolvedor for explicitamente projetado e testado para isso.

O fluxo deve falhar antes da compilação do instalador se qualquer uma das duas estiver ausente.

Após gerar o instalador:

1. faça instalação silenciosa em diretório temporário ou ambiente de teste;
2. confirme a presença das duas DLLs;
3. execute smoke test;
4. desinstale ou limpe o ambiente de teste;
5. não altere associações do sistema durante testes automatizados, quando houver parâmetro para evitá-las.

Preserve todas as regras atuais do instalador que não estejam relacionadas às duas DLLs.

---

# Etapa 11 — pacote Microsoft Store

Atualize `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` e `src/Tools/publish-store-package.ps1`.

O payload final do pacote deve conter:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

A validação deve inspecionar `.msix`, `.msixupload`, `.appx`, `.appxupload`, bundles e pacotes aninhados dentro do bundle.

Não considere válido apenas porque as DLLs estavam no diretório `msixpublish`. Abra o pacote final como archive e confirme que ambas estão no payload real da aplicação.

Preserve as validações atuais de assinatura, versão, revisão zero no manifesto, identidade, publisher, assets, `Scripts/osc.lua` e `mpvnet.exe`. Não altere os dados da identidade do pacote.

---

# Etapa 12 — testes unitários da seleção

Crie testes automatizados para a política de escolha.

A lógica de seleção não deve depender diretamente de intrinsics estáticos dentro do método que decide o arquivo. Separe coleta das características reais da CPU de decisão de qual DLL tentar.

Cubra no mínimo:

1. CPU compatível e ambas existem: preferir `libmpv-2-v3.dll`.
2. CPU incompatível e ambas existem: usar `libmpv-2.dll`; a v3 não deve ser tentada.
3. CPU compatível, v3 ausente: usar `libmpv-2.dll` e registrar fallback.
4. CPU compatível, v3 inválida: tentar v3, falhar, usar normal e registrar fallback.
5. CPU compatível, v3 carrega: não tentar carregar a normal.
6. Normal ausente: CPU incompatível deve gerar erro claro; CPU compatível e v3 funcional pode iniciar, mas o empacotamento deve ser considerado inválido; qualquer distribuição oficial deve falhar na validação por ausência da normal.
7. Ambas ausentes: `DllNotFoundException` ou exceção específica equivalente, com mensagem útil.
8. Ambas inválidas: erro final contendo informações das duas tentativas.
9. Arquitetura diferente de x64: não selecionar v3 e produzir erro coerente com a distribuição x64.
10. Inicialização chamada duas vezes: não registrar resolver novamente nem lançar exceção por resolver duplicado.
11. Biblioteca não relacionada: retornar `IntPtr.Zero`.
12. Caminhos: confirmar uso de `AppContext.BaseDirectory`, nunca `Environment.CurrentDirectory`.

---

# Etapa 13 — testes dos requisitos x86-64-v3

Crie testes para cada requisito individual da detecção.

Simule combinações onde apenas uma capacidade esteja ausente:

```text
AVX ausente
AVX2 ausente
FMA ausente
BMI1 ausente
BMI2 ausente
LZCNT ausente
POPCNT ausente
SSE4.1 ausente
SSE4.2 ausente
SSSE3 ausente
```

Em cada caso, o resultado deve ser incompatível com v3.

Teste também todas as capacidades presentes, com resultado esperado compatível.

Não tente alterar o resultado das propriedades estáticas reais do .NET. Use abstração ou objeto de dados para a lógica testável.

---

# Etapa 14 — testes dos scripts PowerShell

Amplie ou substitua cuidadosamente `test-mpv-build-variants.ps1`.

O teste deve preparar as duas DLLs no mesmo diretório e validar:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

Não crie mais diretórios finais separados por variante como única validação. Ainda é permitido usar diretórios separados durante a extração e análise.

Adicione testes dos scripts com arquivos artificiais ou mocks quando possível, evitando downloads reais em todos os testes.

Separe testes rápidos e determinísticos de testes de integração que acessam a internet. Os testes rápidos devem funcionar offline. Os testes online devem ser explicitamente identificados, por exemplo com `-Integration` ou `-Online`.

Nunca faça um teste unitário depender obrigatoriamente da release mais recente do GitHub.

---

# Etapa 15 — smoke test nativo

Crie uma ferramenta ou modo de teste mínimo que:

1. inicialize o resolvedor;
2. carregue a DLL selecionada;
3. invoque `mpv_client_api_version`;
4. invoque `mpv_create`;
5. confirme retorno diferente de zero;
6. destrua corretamente o contexto;
7. encerre sem abrir a interface completa.

Não reproduza mídia nesse teste unitário. Crie separadamente um teste manual de reprodução.

O smoke test deve registrar:

```text
CPU detectada
compatibilidade v3
DLL selecionada
versão da API libmpv
resultado de mpv_create
```

Caso a infraestrutura atual não permita executar o P/Invoke isoladamente, implemente um argumento interno de diagnóstico, por exemplo `--diagnose-libmpv`. Evite expor opções confusas ao usuário comum. Documente como opção técnica.

---

# Etapa 16 — teste em CPU sem x86-64-v3

Como a máquina de desenvolvimento provavelmente possui AVX2, o comportamento em CPU antiga precisa ser testável sem depender apenas do hardware real.

Adicione uma substituição exclusiva para testes ou desenvolvimento, por exemplo:

```text
MPVNET_FORCE_LIBMPV_VARIANT=normal
MPVNET_FORCE_LIBMPV_VARIANT=x86_64-v3
MPVNET_FORCE_LIBMPV_VARIANT=auto
```

Regras:

- padrão: `auto`;
- `normal`: força a normal;
- `x86_64-v3`: somente para diagnóstico e deve rejeitar CPU incompatível, salvo em teste com provider simulado;
- valores inválidos: registrar aviso e usar `auto`;
- não transformar essa opção em preferência de usuário na interface;
- não persistir automaticamente;
- não utilizar essa variável para mascarar pacote incorreto.

Considere ainda uma variável separada apenas para simular incompatibilidade em testes automatizados. Ela não deve ser documentada como configuração normal do produto.

---

# Etapa 17 — teste manual de reprodução

Depois dos testes automatizados, execute testes manuais nas duas variantes.

## Forçando normal

Validar abertura do player, arquivo de vídeo local, arquivo de áudio local, playlist, URL HTTP ou HTTPS, busca, pausa, áudio, legendas e fechamento do player.

## Automático em CPU compatível com v3

Confirmar no log que `libmpv-2-v3.dll` foi carregada e repetir os testes básicos.

## Fallback

Renomeie temporariamente ou simule falha da v3. Confirme que a v3 falhou, a normal foi carregada e o player iniciou. Restaurar o ambiente ao final.

Não faça commit de DLL renomeada, arquivos temporários ou logs.

---

# Etapa 18 — validação de todos os artefatos

A release somente pode ser considerada válida quando os seguintes destinos contiverem as duas DLLs:

```text
output Debug
output Release
diretório dotnet publish
diretório portátil
ZIP portátil final
diretório usado pelo Inno Setup
instalação criada pelo instalador
diretório msixpublish
pacote MSIX/AppX interno
bundle ou upload final da Microsoft Store
```

Para cada destino, validar existência, tamanho, PE x64, SHA-256, exports essenciais e nomes corretos.

A validação deve falhar com mensagem clara indicando tipo do artefato, caminho e DLL ausente ou inválida.

---

# Etapa 19 — atualização dos validadores existentes

Atualize `validate-native-dependencies.ps1`, `validate-package-contents.ps1` e qualquer validador relacionado.

A lista obrigatória deve incluir:

```text
libmpv-2.dll
libmpv-2-v3.dll
```

Não remova validações existentes de `mpvnet.exe`, `Scripts/osc.lua`, `MediaInfo.dll` e DLLs do runtime.

Evite duplicar a mesma lógica de validação em vários scripts. Crie funções compartilhadas ou um script auxiliar quando isso reduzir duplicação sem ampliar excessivamente o escopo.

---

# Etapa 20 — documentação

Atualize a documentação real do repositório, especialmente:

```text
docs/developer/build-release.md
docs/developer/mpv-integration.md
README.md
```

Somente onde for necessário.

Documente:

1. diferença entre normal e x86-64-v3;
2. que ambas acompanham todas as distribuições x64;
3. que a seleção é automática;
4. que a normal é o fallback obrigatório;
5. quais instruções são verificadas;
6. onde conferir a DLL selecionada no log;
7. como executar o diagnóstico;
8. como executar testes;
9. como forçar a normal em ambiente de desenvolvimento;
10. que o pacote Microsoft Store não exige CPU v3 para iniciar.

Não afirme ganho percentual de desempenho sem benchmark real. Não prometa que a v3 sempre será mais rápida em toda reprodução. Explique que o ganho depende do conteúdo, decodificação, filtros, drivers e demais partes do pipeline.

---

# Etapa 21 — segurança e robustez

A implementação deve:

- usar caminhos absolutos;
- não carregar DLL de diretório controlado pelo usuário;
- não pesquisar primeiro no diretório de trabalho;
- não modificar o `PATH`;
- não baixar libmpv durante a inicialização comum;
- não executar DLL sem validação no fluxo de build;
- não sobrescrever arquivo instalado;
- não usar diretório temporário para carregamento normal;
- não ignorar falhas de carregamento;
- não capturar `Exception` genérica sem necessidade;
- não ocultar a falha final;
- não registrar segredos ou tokens;
- não incluir `GH_TOKEN` em logs;
- preservar compatibilidade com instalação MSIX.

Analise também a possibilidade de DLL hijacking no comportamento atual e restrinja a resolução ao diretório oficial da aplicação.

Não altere o carregamento das outras bibliotecas nativas sem necessidade.

---

# Etapa 22 — desempenho

A detecção da CPU e a decisão devem ocorrer uma única vez por processo.

Não verifique os intrinsics a cada chamada P/Invoke. Não calcule hash das DLLs em toda inicialização normal, salvo se já houver uma política explícita de integridade em runtime.

Hashes e exports devem ser verificados principalmente no build, testes e empacotamento.

O carregamento normal deve adicionar custo desprezível à inicialização.

---

# Etapa 23 — compatibilidade com extensões

Verifique se extensões .NET ou componentes do projeto fazem P/Invoke direto para `libmpv-2.dll`.

Caso façam:

- preserve a versão normal com esse nome;
- documente que imports externos não passam necessariamente pelo resolver do assembly principal;
- não quebre extensões existentes;
- não force extensões a conhecer `libmpv-2-v3.dll`.

A otimização automática deve beneficiar o player principal sem sacrificar a compatibilidade do ecossistema.

---

# Etapa 24 — sequência de implementação e commits

Não implemente tudo em um único commit.

Use esta sequência:

## Commit 1 — testes e contrato do download

```text
test: define dual libmpv dependency contract
```

Inclua testes que inicialmente demonstrem a ausência do suporte simultâneo.

## Commit 2 — download das duas variantes

```text
build: prepare normal and x86-64-v3 libmpv builds
```

Implemente download, cache, extração, nomes e manifesto. Execute os testes antes do commit.

## Commit 3 — detecção e seleção

```text
feat: select optimized libmpv build at startup
```

Implemente detecção de CPU, política de escolha, resolver e fallback. Execute testes unitários e smoke test.

## Commit 4 — build e portable

```text
build: include both libmpv builds in portable output
```

Atualize output, publish e ZIP portátil. Valide o ZIP final.

## Commit 5 — instalador

```text
build: package both libmpv builds in installer
```

Atualize Inno Setup e valide instalação.

## Commit 6 — Microsoft Store

```text
build: package both libmpv builds in Microsoft Store artifact
```

Atualize WAP/MSIX e valide o pacote interno e bundle.

## Commit 7 — validações de release

```text
test: validate dual libmpv files in release artifacts
```

Amplie validadores e testes de integração.

## Commit 8 — documentação

```text
docs: document automatic libmpv variant selection
```

Atualize apenas a documentação necessária.

Não faça commit quando os testes relevantes estiverem falhando.

---

# Etapa 25 — comandos mínimos de validação

Execute, conforme os projetos reais encontrados:

```powershell
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln -c Debug
dotnet build src\MpvNet.sln -c Release
```

Execute todos os projetos de teste encontrados.

Execute também os scripts de teste relacionados à libmpv.

Valide uma publicação:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj `
    --self-contained true `
    --configuration Release `
    --runtime win-x64 `
    /p:IncludeNativeLibrariesForSelfExtract=false
```

Gere e valide ZIP portátil, instalador e pacote Microsoft Store.

Quando o ambiente não possuir Inno Setup, Desktop Bridge ou certificado:

- não finja que o teste passou;
- execute todas as validações possíveis;
- informe exatamente o que ficou pendente;
- não enfraqueça os scripts para fazê-los passar artificialmente.

---

# Critérios de aceite

O trabalho somente estará completo quando:

- [ ] as duas variantes forem baixadas;
- [ ] os archives forem armazenados separadamente;
- [ ] a versão normal for `libmpv-2.dll`;
- [ ] a v3 for `libmpv-2-v3.dll`;
- [ ] ambas forem PE x64 válidas;
- [ ] ambas exportarem a API essencial;
- [ ] ambas pertencerem à mesma release upstream;
- [ ] o output Debug contiver ambas;
- [ ] o output Release contiver ambas;
- [ ] o publish self-contained contiver ambas;
- [ ] o ZIP portátil final contiver ambas;
- [ ] o instalador contiver ambas;
- [ ] a instalação final contiver ambas;
- [ ] o pacote Microsoft Store contiver ambas;
- [ ] bundles aninhados forem inspecionados;
- [ ] CPU compatível preferir a v3;
- [ ] CPU incompatível usar a normal;
- [ ] falha da v3 causar fallback para a normal;
- [ ] nenhuma DLL causar erro claro;
- [ ] o resolvedor for registrado antes de `mpv_create`;
- [ ] o resolvedor estiver no assembly correto;
- [ ] outras bibliotecas não forem interceptadas;
- [ ] a seleção ocorrer uma única vez;
- [ ] houver logs suficientes para diagnóstico;
- [ ] houver testes unitários da política;
- [ ] houver teste de cada requisito de CPU;
- [ ] houver smoke test nativo;
- [ ] houver teste de conteúdo de cada artefato;
- [ ] a Microsoft Store não exigir x86-64-v3;
- [ ] extensões existentes continuarem compatíveis;
- [ ] toda documentação necessária estiver atualizada;
- [ ] cada etapa tiver sido testada antes do commit.

---

# Restrições finais

Não:

- altere recursos não relacionados;
- mude a identidade da Store;
- mude o certificado sem necessidade;
- altere associações de arquivos;
- altere traduções sem relação com a tarefa;
- altere a interface do player;
- crie uma preferência visual para escolher DLL;
- baixe libmpv na inicialização do usuário;
- substitua DLL instalada em runtime;
- elimine a versão normal;
- publique apenas a v3;
- considere a detecção da CPU suficiente sem testar `TryLoad`;
- faça um único commit gigante;
- marque testes como concluídos sem executá-los;
- reduza validações existentes para facilitar o build.

Ao terminar, apresente:

1. diagnóstico do comportamento anterior;
2. arquivos alterados;
3. arquitetura implementada;
4. regra exata da detecção;
5. regra exata de fallback;
6. conteúdo verificado em cada artefato;
7. testes executados;
8. resultados;
9. testes não executados e motivo;
10. commits criados;
11. riscos residuais;
12. instruções para teste manual em uma CPU antiga e em uma CPU compatível com v3.
