# Localização do MPV.NET Media Player

## Objetivo

Este guia documenta como adicionar ou revisar traducoes da interface do MPV.NET Media Player sem quebrar os idiomas existentes.

O mpv.net usa gettext para os textos de interface. A traducao fica fora dos arquivos `.resx` principais e e carregada em runtime a partir da pasta `Locale`.

## Fluxo atual confirmado

Arquivos e responsabilidades:

- `lang/source.pot`: template gettext gerado a partir dos textos fonte e base inglesa oficial.
- `lang/po/*.po`: arquivos de traducao apenas para idiomas traduzidos.
- `lang/compile-mo-files.ps1`: compila arquivos `.po` para `.mo`.
- `tools/localization/checks/*.py`: utilitarios de leitura para detectar duplicatas e inconsistencias.
- `tools/localization/cleanup/*.py`: utilitarios que limpam ou reescrevem arquivos de traducao.
- `tools/localization/reference/msgattrib_help.txt`: referencia textual do `msgattrib` usada na manutencao do gettext.
- `src/MpvNet/Services/LanguageCatalog.cs`: centraliza catalogo de idiomas, normalizacao, fallback e selecao auxiliar de idiomas de midia.
- `src/MpvNet.Windows/WPF/WpfTranslator.cs`: aplica o idioma de interface resolvido pelo servico central em uma `CultureInfo`.
- `src/MpvNet.Windows/Resources/editor_conf.txt`: lista os valores aceitos pelo editor de configuracao para a opcao `language`.
- `src/Tools/build-release-package.ps1`: gera ou reaproveita a pasta `Locale` durante o fluxo de release.

O ingles e o idioma-fonte nativo do mpv.net. Por isso, nao existe `lang/po/en.po` nem `Locale/en/LC_MESSAGES/mpvnet.mo` gerado pelo fluxo normal. Quando a interface esta em ingles, o gettext usa os proprios `msgid` em ingles como texto final.

O arquivo `lang/source.pot` deve ser tratado como a base inglesa oficial. Todo arquivo `.po` de idioma traduzido deve ter as mesmas entradas ativas do `source.pot`, sem entradas ausentes, extras, duplicadas ou vazias. A validacao de paridade compara cada `lang/po/*.po` contra esse arquivo.

O formato final esperado pelo runtime e:

```text
Locale/<cultura>/LC_MESSAGES/mpvnet.mo
```

Exemplo para portugues brasileiro:

```text
Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

## Idiomas disponiveis

Os 20 idiomas abaixo possuem mapeamento em `LanguageCatalog.cs` e valor publico aceito por `language`. Os 19 idiomas traduzidos possuem arquivo `.po` e pasta `Locale`; o ingles usa a base nativa em `source.pot`.

| Idioma | Valor de `language` | Arquivo PO | Pasta Locale | CultureInfo |
| --- | --- | --- | --- | --- |
| Bulgaro | `bulgarian` | `lang/po/bg.po` | `Locale/bg/LC_MESSAGES/` | `bg` |
| Chines simplificado | `chinese-china` | `lang/po/zh_CN.po` | `Locale/zh_CN/LC_MESSAGES/` | `zh-CN` |
| 繁體中文（台灣） | `chinese-taiwan` | `lang/po/zh_TW.po` | `Locale/zh_TW/LC_MESSAGES/` | `zh-TW` |
| 繁體中文（香港） | `chinese-hongkong` | `lang/po/zh_HK.po` | `Locale/zh_HK/LC_MESSAGES/` | `zh-HK` |
| हिन्दी | `hindi` | `lang/po/hi_IN.po` | `Locale/hi_IN/LC_MESSAGES/` | `hi-IN` |
| العربية | `arabic` | `lang/po/ar_SA.po` | `Locale/ar_SA/LC_MESSAGES/` | `ar-SA` |
| Українська | `ukrainian` | `lang/po/uk_UA.po` | `Locale/uk_UA/LC_MESSAGES/` | `uk-UA` |
| Bahasa Indonesia | `indonesian` | `lang/po/id_ID.po` | `Locale/id_ID/LC_MESSAGES/` | `id-ID` |
| Ingles | `english` | nativo | nativo | `en` |
| Espanhol | `spanish` | `lang/po/es.po` | `Locale/es/LC_MESSAGES/` | `es` |
| Frances | `french` | `lang/po/fr.po` | `Locale/fr/LC_MESSAGES/` | `fr` |
| Alemao | `german` | `lang/po/de.po` | `Locale/de/LC_MESSAGES/` | `de` |
| Italiano | `italian` | `lang/po/it.po` | `Locale/it/LC_MESSAGES/` | `it` |
| Japones | `japanese` | `lang/po/ja.po` | `Locale/ja/LC_MESSAGES/` | `ja` |
| Coreano | `korean` | `lang/po/ko.po` | `Locale/ko/LC_MESSAGES/` | `ko` |
| Polones | `polish` | `lang/po/pl.po` | `Locale/pl/LC_MESSAGES/` | `pl` |
| Portugues do Brasil | `portuguese-brazil` | `lang/po/pt_BR.po` | `Locale/pt_BR/LC_MESSAGES/` | `pt-BR` |
| Portugues de Portugal | `portuguese-portugal` | `lang/po/pt_PT.po` | `Locale/pt_PT/LC_MESSAGES/` | `pt-PT` |
| Russo | `russian` | `lang/po/ru.po` | `Locale/ru/LC_MESSAGES/` | `ru` |
| Turco | `turkish` | `lang/po/tr.po` | `Locale/tr/LC_MESSAGES/` | `tr` |

O valor `system` nao faz mais parte do contrato da interface. O idioma de inicializacao vem do idioma do Windows quando houver mapeamento suportado ou do valor explicitamente passado por parametro/configuracao; se nao houver suporte, o fallback continua sendo ingles. No editor de configuracao, o combo de idioma deve mostrar apenas valores publicos suportados e carregar configs antigas com `system` como o idioma efetivo de startup.

Quando `alang` esta definido, a interface usa o primeiro idioma suportado dessa lista de prioridade como preferencia de idioma. Essa escolha vem do valor declarado em `alang`, nao do idioma real da faixa de audio ou legenda selecionada pelo arquivo em reproducao. Se nenhum item de `alang` for suportado, a interface usa o idioma de inicializacao; se ele nao for suportado, volta para ingles. A legenda (`slang`/`sid`) nao participa dessa escolha.

## Arquitetura central de idiomas

O codigo de idioma fica centralizado em `src/MpvNet/Services/LanguageCatalog.cs`:

- `LanguageNormalizer`: normaliza codigos ISO 639-1, ISO 639-2, nomes comuns e valores BCP 47 para um formato interno consistente, como `pt-BR`, `es-MX`, `zh-CN` e `sr-Cyrl`.
- `LanguageFallbackResolver`: gera a ordem de fallback reutilizavel para interface, audio e legenda.
- `LocalizationService`: resolve o idioma de inicializacao, escolhas manuais e listas vindas de `alang` para o nome publico aceito pelo mpv.net.
- `MediaLanguageService`: monta prioridades e compara faixas de audio/legenda com as mesmas regras de normalizacao e fallback.

Nao criar normalizadores ou fallbacks paralelos em `WpfTranslator`, `Player.cs` ou controles de configuracao. Novos idiomas devem entrar pelo catalogo central e, quando forem idiomas de interface traduzidos, tambem pelo bloco `language` em `editor_conf.txt` e pelos catalogos gettext.

## Normalizacao e fallback

A normalizacao aceita codigos e nomes como `eng`, `en_US`, `Portuguese`, `Português do Brasil`, `spa`, `fr_CA`, `zho`, `Simplified Chinese`, `jpn`, `ger` e `Italian`, retornando valores BCP 47 quando possivel.

Para a interface, `LocalizationCultureResolver` aplica primeiro o mapa oficial
de culturas suportadas pelo projeto. Variantes regionais nao ganham arquivos
gettext proprios; elas apontam para o idioma base ja traduzido.

Idiomas base oficiais:

| Cultura base | Idioma |
| --- | --- |
| `bg` | Bulgaro |
| `de` | Alemao |
| `en` | Ingles nativo |
| `es` | Espanhol |
| `fr` | Frances |
| `it` | Italiano |
| `ja` | Japones |
| `ko` | Coreano |
| `pl` | Polones |
| `pt-BR` | Portugues do Brasil |
| `pt-PT` | Portugues de Portugal |
| `ru` | Russo |
| `tr` | Turco |
| `zh-CN` | Chines simplificado |
| `zh-TW` | Chines tradicional de Taiwan |
| `zh-HK` | Chines tradicional de Hong Kong |
| `hi-IN` | Hindi |
| `ar-SA` | Arabe |
| `uk-UA` | Ucraniano |
| `id-ID` | Indonesio |

Mapeamento de variantes:

| Cultura recebida | Cultura usada |
| --- | --- |
| `bg-BG` | `bg` |
| `de-DE`, `de-AT`, `de-CH`, `de-LI`, `de-LU` | `de` |
| `en-US`, `en-GB`, `en-AU`, `en-CA`, `en-NZ`, `en-IE`, `en-IN`, `en-ZA`, `en-SG`, `en-HK` | `en` |
| `es-ES`, `es-MX`, `es-AR`, `es-CL`, `es-CO`, `es-PE`, `es-UY`, `es-VE`, `es-EC`, `es-BO`, `es-PY`, `es-CR`, `es-DO`, `es-GT`, `es-HN`, `es-NI`, `es-PA`, `es-PR`, `es-SV`, `es-US` | `es` |
| `fr-FR`, `fr-CA`, `fr-BE`, `fr-CH`, `fr-LU`, `fr-MC` | `fr` |
| `it-IT`, `it-CH` | `it` |
| `ja-JP` | `ja` |
| `ko-KR` | `ko` |
| `pl-PL` | `pl` |
| `pt-BR` | `pt-BR` |
| `pt-PT`, `pt-AO`, `pt-MZ`, `pt-CV`, `pt-GW`, `pt-ST`, `pt-TL`, `pt-MO` | `pt-PT` |
| `ru-RU`, `ru-BY`, `ru-KZ`, `ru-KG`, `ru-MD` | `ru` |
| `tr-TR`, `tr-CY` | `tr` |
| `zh`, `zh-CN`, `zh-SG`, `zh-Hans`, `zh-Hans-TW` | `zh-CN` |
| `zh-TW`, `zh-Hant`, `zh-Hant-CN` | `zh-TW` |
| `zh-HK`, `zh-MO`, `zh-Hant-HK`, `zh-Hant-MO` | `zh-HK` |
| `hi`, `hi-IN`, `hin` | `hi-IN` |
| `ar`, `ar-SA`, `ar-EG`, `ar-AE`, `ara` | `ar-SA` (somente texto) |
| `uk`, `uk-UA`, `ukr` | `uk-UA` |
| `id`, `id-ID`, `ind` | `id-ID` |

A escrita chinesa explícita tem precedência sobre a região. Não inferir chinês
simplificado de uma variante tradicional quando o catálogo solicitado estiver
ausente. Outros idiomas/variantes sem suporte usam o inglês nativo. Nomes
nativos dos seis novos idiomas também são aceitos pelo resolvedor e exibidos no
seletor independentemente do idioma ativo.

A ordem geral de fallback e:

1. cultura base oficial resolvida pelo mapa acima;
2. idioma base generico quando a familia tem apenas um idioma oficial no projeto, como `de`, `en`, `es`, `fr`, `it`, `ja`, `ko`, `pl`, `ru` e `tr`;
3. idioma padrao, hoje ingles, quando o chamador permitir fallback padrao;
4. texto nativo/fonte caso o gettext nao encontre catalogo.

Variantes com escrita diferente exigem cuidado. O fallback central nao cruza
automaticamente `zh-CN` com `zh-TW` nem `sr-Cyrl` com `sr-Latn`. Portugues
brasileiro e portugues de Portugal tambem permanecem separados: `pt-BR` nao
cai para `pt-PT`, e `pt-PT` nao cai para `pt-BR`.

## Interface, audio e legenda

### Escrita, direção e região

Os seis catálogos adicionais usam o inglês como referência, com revisão de
termos técnicos e variantes: por exemplo, `播放清單`/`播放列表`,
`軟體`/`軟件` e `義大利語`/`意大利語`. Os parâmetros e nomes de filtros do
mpv permanecem literais; não se traduzem valores gravados no arquivo de
configuração nem títulos fornecidos pelo usuário.

`CurrentUICulture` seleciona o texto e os plurais. `CurrentCulture`, o
`FormatProvider` do NGettext e o `Language` de formatação do WPF preservam a
região do usuário: texto árabe `ar-SA` não transforma números/calendário de
`ar-EG` ou `ar-AE` em formatação saudita.

`InterfaceLayout` aplica recursos dinâmicos RTL/LTR a janelas, menus e diálogos.
Campos de caminhos, comandos, URLs e atalhos continuam LTR; isoladores bidi são
usados somente em cópias de exibição. A família composta `Global User Interface`
usa o fallback tipográfico do Windows para CJK, devanágari, árabe e cirílico.
O frontend não espelha vídeo nem muda o renderizador de legendas do mpv.

O seletor atualiza os textos do editor aberto, a árvore, menus, título acessível
e taskbar, sem substituir os valores editados. A gravação conserva um alias
preexistente, como `language=zh-Hant`, quando a seleção não mudou. Ao reabrir,
uma categoria salva no idioma anterior volta à categoria geral traduzida se
não existir no idioma atual. Reiniciar continua sendo recomendado para aplicar
todas as opções; a seleção de áudio/legenda permanece a cargo do mpv.

### Inventário da expansão

Cada novo `.po` contém **775 entradas ativas**, além do cabeçalho; nenhuma
entrada ausente, extra, duplicada, vazia ou fuzzy. As 762 entradas anteriores
foram mantidas. O inventário acrescentou nomes dos seis idiomas e textos antes
ausentes do template: clipboard indisponível, faixa sem título, repositório,
categoria `Cache`, descrição de histórico de URLs e os identificadores técnicos
`save-network-urls-in-recent` e `video`. Os catálogos anteriores receberam as
mesmas chaves; suas traduções existentes foram preservadas.

| Catálogo | Entradas | Formas plurais |
| --- | ---: | ---: |
| `zh_TW.po` | 775 | 1 |
| `zh_HK.po` | 775 | 1 |
| `hi_IN.po` | 775 | 2 |
| `ar_SA.po` | 775 | 6 |
| `uk_UA.po` | 775 | 3 |
| `id_ID.po` | 775 | 1 |

O template atual não tem mensagens com `msgid_plural`; fixtures verificam
contextos e todas as formas desses idiomas para manter a compilação preparada
para novos plurais. O player não exibe uma mensagem própria de loading que
precise de um novo catálogo; OSD inicial, falhas, atalhos e comandos compartilham
o catálogo de interface.

Idioma da interface e idioma de midia sao configuracoes diferentes:

- Interface: `mpvnet.conf`, opcao `language`, com valores como `english`, `portuguese-brazil` e `portuguese-portugal`.
- Audio preferido: `mpv.conf`, opcao nativa do mpv `alang`.
- Legenda preferida: `mpv.conf`, opcao nativa do mpv `slang`.
- Selecao manual em runtime: propriedades nativas `aid` e `sid`.
- Carregamento automatico de legendas externas: opcao nativa `sub-auto`.

O mpv/libmpv continua sendo a autoridade final para selecionar audio e legenda por `alang`, `slang`, `aid`, `sid`, `track-list`, `sub-auto` e `track-auto-selection`. O mpv.net usa logica propria apenas para normalizar, comparar, resolver fallback e exibir metadados de faixas sem duplicar o seletor de faixas do mpv.

Exemplos de configuracao em `mpv.conf`:

```ini
alang=por,pt-BR,eng
slang=pt-BR,por,eng
sub-auto=exact
```

Use `language` apenas para a interface. Nao usar `language` para escolher audio ou legenda.

## Implementacao atual

O fork possui traducoes gettext para os idiomas listados na tabela acima. Ingles continua nativo e nao usa arquivo `.po`.

## Como adicionar ou revisar um idioma

1. Criar o arquivo `lang/po/<cultura>.po` a partir de `lang/source.pot`.

   Nao criar `lang/po/en.po`: ingles e nativo e deve permanecer representado pelo proprio `lang/source.pot`.

2. Preencher os metadados do catálogo para o idioma. O exemplo abaixo é
   específico para português brasileiro; para outro idioma, ajuste `Language`,
   `Language-Team` e `Plural-Forms` segundo a regra daquela língua:

```po
msgid ""
msgstr ""
"Project-Id-Version: mpv.net\n"
"Report-Msgid-Bugs-To: \n"
"POT-Creation-Date: 2025-10-06 00:24+0200\n"
"PO-Revision-Date: YEAR-MO-DA HO:MI+ZONE\n"
"Last-Translator: FULL NAME <EMAIL@ADDRESS>\n"
"Language-Team: Portuguese (Brazil)\n"
"Language: pt_BR\n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Content-Transfer-Encoding: 8bit\n"
"Plural-Forms: nplurals=2; plural=(n > 1);\n"
```

3. Traduzir os `msgstr` sem alterar os `msgid`.

4. Adicionar o valor publico ao bloco `language` em `src/MpvNet.Windows/Resources/editor_conf.txt`:

```text
option = portuguese-brazil
```

O nome deve seguir o estilo dos valores existentes, como `chinese-china`, `portuguese-brazil` e `portuguese-portugal`.

5. Adicionar o idioma em `src/MpvNet/Services/LanguageCatalog.cs`:

```csharp
new("portuguese-brazil", "pt-BR", "pt_BR", true, "por-br", "pt-br", "pt_br", "brazilian portuguese", "português do brasil"),
```

6. Quando necessario, ajustar o fallback central para regioes especificas, sem mudar o comportamento dos outros idiomas.

   Atualizar também `Package.appxmanifest` (culturas declaradas) e o payload
   explícito de `MpvNet.Pacote.wapproj`. `validate-package-contents.ps1` exige um
   `.mo` não vazio para cada `.po`, inclusive em ZIP/MSIX aninhados.

7. Gerar os arquivos `.mo`:

```powershell
.\lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
```

8. Confirmar que o arquivo foi criado:

```text
src/MpvNet.Windows/bin/Debug/win-x64/Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

## Como adicionar uma nova variante regional

1. Nao criar novo arquivo `.po` nem pasta `Locale` se a variante usa texto igual
   ao idioma base ja existente.
2. Adicionar a variante em `LocalizationCultureResolver` apontando para a
   cultura base oficial.
3. Ampliar os testes de idioma em `src/MpvNet.Tests/LocalizationExpansionTests.cs`
   e os casos existentes em `Program.cs` quando aplicável, cobrindo a resolução
   da variante e o fallback final.
4. Atualizar esta tabela de variantes.

Exemplo: para mapear uma nova variante de espanhol, adicionar a cultura ao mapa
central apontando para `es`; nao criar `lang/po/es_MX.po`.

## O que preservar

- Nao renomear idiomas existentes.
- Nao reintroduzir `language=system`.
- Nao criar `lang/po/en.po` sem uma decisao explicita de mudar o contrato de localizacao.
- Manter `lang/source.pot` como base inglesa oficial para paridade dos `.po`.
- Nao remover nem substituir a referencia historica a Transifex/upstream.
- Nao tratar `.resx` como fonte principal da traducao gettext da interface.
- Nao alterar o formato de `mpvnet.conf`; o usuario deve continuar podendo definir `language=<valor>`.
- Nao misturar `language` com preferencia de audio ou legenda; para midia, preservar `alang` e `slang` do mpv.

## Extracao do catalogo-fonte

O script `lang/update-gettext-catalogs.ps1` gera a lista de fontes em um arquivo
temporario, sempre com caminhos relativos ao repositorio. Nao versionar novamente
`lang/cs-files.txt`: listas persistidas acumulam caminhos absolutos da maquina e
fontes geradas. A extracao ignora `bin`, `obj`, `artifacts`, `AppPackages`,
`.venv`, o projeto de testes e `Resources.Designer.cs`, mas inclui os textos
declarados por `AddMenuItem(...)`, XAML e `editor_conf.txt`.

## Validacao recomendada

Validacao automatica minima:

```powershell
.\lang\validate-po-files.ps1 -ValidateOnly
python .\lang\test-language-expansion.py
.\lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
python .\lang\verify-language-expansion.py --mo-directory .\src\MpvNet.Windows\bin\Debug\win-x64
& .\src\Tools\test-localization-package.ps1
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\pt_BR\LC_MESSAGES\mpvnet.mo
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\pt_PT\LC_MESSAGES\mpvnet.mo
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\es\LC_MESSAGES\mpvnet.mo
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\it\LC_MESSAGES\mpvnet.mo
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore
```

O comando `.\lang\validate-po-files.ps1 -ValidateOnly` e a validacao principal de paridade: ele confirma que `lang/source.pot` nao tem duplicatas, que cada `.po` traduzido corresponde ao `source.pot` e que `lang/po/en.po` nao foi criado indevidamente.

Os testes cobrem os 20 idiomas base, aliases, culturas do Windows, `alang`,
persistência, fallback, separação das variantes chinesas/portuguesas, carregamento
real no NGettext, erro/OSD, contextos e placeholders. A expansão também possui
testes STA de editor aberto, retorno de idioma, RTL/LTR, valores não salvos,
formatação `ar-EG` e glifos CJK/devanágari/árabes/cirílicos.

`verify-language-expansion.py` verifica todas as chaves dos seis novos catálogos,
flags fuzzy, codificação, tokens, números, escapes, cabeçalhos e consultas reais
nos binários GNU MO. Também testa a compilação Python offline; os comprimentos
das strings MO excluem o terminador NUL. `compile-mo-files.ps1` usa `msgfmt
--check` quando disponível e aborta em erros, sem ignorar catálogos inválidos.
O fluxo de release recompila as fontes atuais em vez de reutilizar um `Locale`
incompleto. `update-source-pot-fallback.py` mantém a distinção de maiúsculas e
minúsculas exigida pelo gettext, por exemplo `Cache` e `cache`.

Validacao manual:

- iniciar com `mpvnet.exe --language=portuguese-brazil`;
- iniciar com `mpvnet.exe --language=portuguese-portugal`;
- iniciar com `mpvnet.exe --language=spanish`;
- iniciar com `mpvnet.exe --language=italian`;
- iniciar com `mpvnet.conf` contendo `language=portuguese-brazil`;
- confirmar que um idioma invalido continua caindo para ingles;
- confirmar que idiomas existentes, como `german` e `chinese-china`, continuam funcionando;
- em release, confirmar que o ZIP contem os arquivos `.mo` esperados em `Locale/<cultura>/LC_MESSAGES/mpvnet.mo`.

## Riscos

O maior risco e desalinhamento entre o nome publico da opcao, o nome do arquivo `.po`, o nome da pasta `Locale` e a `CultureInfo` usada pelo WPF. Para `pt-BR`, o contrato e:

| Item | Valor |
| --- | --- |
| Opcao em `mpvnet.conf` | `language=portuguese-brazil` |
| Arquivo PO | `lang/po/pt_BR.po` |
| Pasta Locale | `Locale/pt_BR/LC_MESSAGES/` |
| CultureInfo | `pt-BR` |

## Auditoria de traducao

A auditoria automatizada principal e:

```powershell
.\lang\validate-po-files.ps1 -ValidateOnly
```

Ela compara todos os `lang/po/*.po` com `lang/source.pot`, rejeita `lang/po/en.po`, detecta chaves ausentes, extras, duplicadas e textos vazios. Placeholders devem permanecer iguais aos `msgid`; quando houver duvida, nao inventar traducao automaticamente. Marcar para revisao humana ou manter fallback seguro.

## Resultado da expansao de seis idiomas — 2026-10-27

- `lang/source.pot` e os 19 catalogos `.po` ficaram com 775 entradas unicas e sem chaves ausentes, extras, duplicadas ou vazias. Os seis novos catalogos tambem passaram pela verificacao de tokens, metadados, plurais e consultas reais aos arquivos MO. As 762 entradas anteriores dos 13 catalogos existentes foram comparadas com a branch de origem; nenhuma traducao anterior foi alterada.
- `python lang/test-language-expansion.py`: 3 testes aprovados. `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-restore`: 787 testes aprovados, nenhum erro ou falha. Build normal, publicacao Release, ZIP portatil e instalador foram gerados; os validadores de conteudo e dependencias nativas passaram.
- O teste de execucao cobriu os seis idiomas com arquivo local, fluxo HTTP em loopback, pausa, busca, tela cheia, troca de faixas de audio/legenda, playlist e encerramento limpo. A verificacao visual conferiu RTL em arabe e as telas em hindi e chines tradicional taiwanes, em tema claro e escuro.
- O MSIX sem assinatura contem os 19 catalogos e declara os 20 idiomas. O manifesto e a versao Store foram validados em conjunto; cada novo catalogo extraido do ZIP e do MSIX corresponde as 775 entradas do PO.
- Artefatos e registros locais desta validacao ficam em `artifacts/language-expansion/`; essa pasta nao faz parte do commit.

Esta validacao nao incluiu instalacao/atualizacao real de um MSIX assinado nem certificacao ou publicacao na Store. A revisao visual nao percorreu toda a matriz de DPI, monitores, foco e drag-and-drop, e as traducoes ainda precisam de uma revisao independente por falantes nativos.

