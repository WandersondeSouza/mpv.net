# Localização do MPV.NET Media Player

## Tópicos

- [Idiomas disponíveis](#idiomas-disponíveis)
- [Variantes regionais e fallback](#normalização-e-fallback)
- [Interface, áudio e legenda](#interface-áudio-e-legenda)
- [Adicionar ou revisar um idioma](#como-adicionar-ou-revisar-um-idioma)
- [Extrair e validar os catálogos](#extração-do-catálogo-fonte)

## Objetivo

Este guia documenta como adicionar ou revisar traduções da interface do MPV.NET Media Player sem quebrar os idiomas existentes.

O mpv.net usa gettext para os textos de interface. O player carrega os catálogos de tradução da pasta `Locale` durante a execução.

## Fluxo atual confirmado

Arquivos e responsabilidades:

- `lang/source.pot`: template gettext gerado a partir dos textos-fonte e base inglesa oficial.
- `lang/po/*.po`: catálogos dos 19 idiomas traduzidos.
- `lang/compile-mo-files.ps1`: compila arquivos `.po` para `.mo`.
- `tools/localization/checks/*.py`: utilitários de leitura para detectar duplicatas e inconsistências.
- `tools/localization/cleanup/*.py`: utilitários que limpam ou reescrevem arquivos de tradução.
- `tools/localization/reference/msgattrib_help.txt`: referência textual do `msgattrib` usada na manutenção do gettext.
- `src/MpvNet/Services/LanguageCatalog.cs`: centraliza catálogo de idiomas, normalização, fallback e seleção auxiliar de idiomas de mídia.
- `src/MpvNet.Windows/WPF/WpfTranslator.cs`: aplica o idioma de interface resolvido pelo serviço central em uma `CultureInfo`.
- `src/MpvNet.Windows/Resources/editor_conf.txt`: lista os valores aceitos pelo editor de configuração para a opção `language`.
- `src/Tools/build-release-package.ps1`: recompila os catálogos da pasta `Locale` para o pacote.

O inglês é o idioma-fonte do mpv.net. Por isso, não existe `lang/po/en.po` nem `Locale/en/LC_MESSAGES/mpvnet.mo` gerado pelo fluxo normal. Quando a interface está em inglês, o gettext usa os próprios `msgid` em inglês como texto final.

Use `lang/source.pot` como base. Mantenha as mesmas entradas ativas em cada `.po`, com uma tradução para cada chave.

Na inicialização, `App.Init` lê `mpvnet.conf` e aplica os argumentos `--language`
antes de gerar menus e a mensagem inicial do mpv. O último argumento prevalece
sobre o arquivo de configuração; aliases e fallback usam o resolvedor existente.
Assim, a mensagem para soltar arquivos acompanha o idioma solicitado desde a
abertura. As opções explícitas `osd-msg1` em `mpv.conf` ou na linha de comando
continuam prevalecendo sobre a mensagem padrão traduzida.

O player procura os catálogos compilados no caminho abaixo.

```text
Locale/<cultura>/LC_MESSAGES/mpvnet.mo
```

Exemplo para português brasileiro:

```text
Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

<a name="implementacao-atual"></a>
<a name="idiomas-disponiveis"></a>

## Idiomas disponíveis

Os 20 idiomas abaixo possuem mapeamento em `LanguageCatalog.cs` e valor público aceito por `language`. Os 19 idiomas traduzidos possuem arquivo `.po` e pasta `Locale`; o inglês usa a base nativa em `source.pot`.

| Idioma | Valor de `language` | Arquivo PO | Pasta Locale | CultureInfo |
| --- | --- | --- | --- | --- |
| Búlgaro | `bulgarian` | `lang/po/bg.po` | `Locale/bg/LC_MESSAGES/` | `bg` |
| Chinês simplificado | `chinese-china` | `lang/po/zh_CN.po` | `Locale/zh_CN/LC_MESSAGES/` | `zh-CN` |
| 繁體中文（台灣） | `chinese-taiwan` | `lang/po/zh_TW.po` | `Locale/zh_TW/LC_MESSAGES/` | `zh-TW` |
| 繁體中文（香港） | `chinese-hongkong` | `lang/po/zh_HK.po` | `Locale/zh_HK/LC_MESSAGES/` | `zh-HK` |
| हिन्दी | `hindi` | `lang/po/hi_IN.po` | `Locale/hi_IN/LC_MESSAGES/` | `hi-IN` |
| العربية | `arabic` | `lang/po/ar_SA.po` | `Locale/ar_SA/LC_MESSAGES/` | `ar-SA` |
| Українська | `ukrainian` | `lang/po/uk_UA.po` | `Locale/uk_UA/LC_MESSAGES/` | `uk-UA` |
| Bahasa Indonesia | `indonesian` | `lang/po/id_ID.po` | `Locale/id_ID/LC_MESSAGES/` | `id-ID` |
| Inglês | `english` | nativo | nativo | `en` |
| Espanhol | `spanish` | `lang/po/es.po` | `Locale/es/LC_MESSAGES/` | `es` |
| Francês | `french` | `lang/po/fr.po` | `Locale/fr/LC_MESSAGES/` | `fr` |
| Alemão | `german` | `lang/po/de.po` | `Locale/de/LC_MESSAGES/` | `de` |
| Italiano | `italian` | `lang/po/it.po` | `Locale/it/LC_MESSAGES/` | `it` |
| Japonês | `japanese` | `lang/po/ja.po` | `Locale/ja/LC_MESSAGES/` | `ja` |
| Coreano | `korean` | `lang/po/ko.po` | `Locale/ko/LC_MESSAGES/` | `ko` |
| Polonês | `polish` | `lang/po/pl.po` | `Locale/pl/LC_MESSAGES/` | `pl` |
| Português do Brasil | `portuguese-brazil` | `lang/po/pt_BR.po` | `Locale/pt_BR/LC_MESSAGES/` | `pt-BR` |
| Português de Portugal | `portuguese-portugal` | `lang/po/pt_PT.po` | `Locale/pt_PT/LC_MESSAGES/` | `pt-PT` |
| Russo | `russian` | `lang/po/ru.po` | `Locale/ru/LC_MESSAGES/` | `ru` |
| Turco | `turkish` | `lang/po/tr.po` | `Locale/tr/LC_MESSAGES/` | `tr` |

Em configurações antigas, `system` é tratado como seleção pelo idioma do Windows. Na inicialização, uma opção explícita tem prioridade. Sem ela, o player resolve o idioma a partir da cultura de interface do Windows. A falta de correspondência leva ao inglês. O editor mostra os valores públicos suportados e apresenta a opção efetiva ao abrir uma configuração antiga com `system`.

Quando `alang` está definido, a interface usa o primeiro idioma suportado dessa lista de prioridade como preferência de idioma. Essa escolha vem do valor declarado em `alang`, não do idioma real da faixa de áudio ou legenda selecionada pelo arquivo em reprodução. Se nenhum item de `alang` for suportado, o resolvedor usa a cultura de interface da thread naquele momento, com fallback para inglês. A legenda (`slang`/`sid`) não participa dessa escolha.

## Arquitetura central de idiomas

O código de idioma fica centralizado em `src/MpvNet/Services/LanguageCatalog.cs`:

- `LanguageNormalizer`: normaliza códigos ISO 639-1, ISO 639-2, nomes comuns e valores BCP 47 para um formato interno consistente, como `pt-BR`, `es-MX`, `zh-CN` e `sr-Cyrl`.
- `LanguageFallbackResolver`: gera a ordem de fallback reutilizável para interface, áudio e legenda.
- `LocalizationService`: resolve o idioma de inicialização, escolhas manuais e listas vindas de `alang` para o nome público aceito pelo mpv.net.
- `MediaLanguageService`: monta prioridades e compara faixas de audio/legenda com as mesmas regras de normalização e fallback.

Não criar normalizadores ou fallbacks paralelos em `WpfTranslator`, `Player.cs` ou controles de configuração. Novos idiomas devem entrar pelo catálogo central e, quando forem idiomas de interface traduzidos, também pelo bloco `language` em `editor_conf.txt` e pelos catálogos gettext.

<a name="normalizacao-e-fallback"></a>

## Normalização e fallback

A normalização aceita códigos e nomes como `eng`, `en_US`, `Portuguese`, `Português do Brasil`, `spa`, `fr_CA`, `zho`, `Simplified Chinese`, `jpn`, `ger` e `Italian`, retornando valores BCP 47 quando possível.

Para a interface, `LocalizationCultureResolver` aplica primeiro o mapa oficial
de culturas suportadas pelo projeto. Variantes regionais não ganham arquivos
gettext próprios; elas apontam para o idioma base já traduzido.

A [lista de idiomas](#idiomas-disponíveis) identifica as culturas-base e os
catálogos. As variantes abaixo usam essa mesma lista.

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
nativos dos seis novos idiomas também são aceitos pelo resolvedor como aliases.
No seletor, os nomes dos 20 idiomas seguem o idioma ativo da interface, usando
os catálogos gettext; inglês usa nomes legíveis do texto-fonte. Ao trocar o
idioma, a lista e o texto selecionado são atualizados junto com o editor aberto.
Os valores gravados, como `portuguese-brazil`, continuam literais.

A resolução segue esta ordem.

1. cultura base oficial resolvida pelo mapa acima;
2. idioma base genérico quando a família tem apenas um idioma oficial no projeto, como `de`, `en`, `es`, `fr`, `it`, `ja`, `ko`, `pl`, `ru` e `tr`;
3. idioma padrão, hoje inglês, quando o chamador permitir fallback padrão;
4. texto nativo/fonte caso o gettext não encontre catálogo.

Variantes com escrita diferente exigem cuidado. O fallback central não cruza
automaticamente `zh-CN` com `zh-TW` nem `sr-Cyrl` com `sr-Latn`. Português
brasileiro e português de Portugal também permanecem separados: `pt-BR` não
cai para `pt-PT`, e `pt-PT` não cai para `pt-BR`.

<a name="interface-audio-e-legenda"></a>

## Interface, áudio e legenda

Em `mpvnet.conf`, use `language` para definir a interface. O
`mpv.conf` usa `alang` para ordenar as preferências de áudio e
`slang` para as de legenda. Depois da inicialização, um `alang`
não vazio também pode selecionar a interface, conforme o
[catálogo de idiomas](#idiomas-disponíveis).

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

Os testes com catálogos de exemplo cobrem contextos e todas as formas plurais
dos idiomas adicionais. O OSD inicial, os erros, os atalhos e os comandos
compartilham o catálogo de interface.

Configure a interface, o áudio e as legendas separadamente.

- Interface: `mpvnet.conf`, opção `language`, com valores como `english`, `portuguese-brazil` e `portuguese-portugal`.
- Áudio preferido: `mpv.conf`, opção nativa do mpv `alang`.
- Legenda preferida: `mpv.conf`, opção nativa do mpv `slang`.
- Seleção manual em runtime: propriedades nativas `aid` e `sid`.
- Carregamento automático de legendas externas: opção nativa `sub-auto`.

O mpv seleciona as faixas com `alang`, `slang`, `aid`, `sid`,
`sub-auto` e `track-auto-selection`, e publica as faixas em `track-list`.
O frontend normaliza códigos, resolve aliases e exibe os metadados.

Exemplo de preferências de faixa em `mpv.conf`.

```ini
alang=por,pt-BR,eng
slang=pt-BR,por,eng
sub-auto=exact
```

## Como adicionar ou revisar um idioma

1. Crie o arquivo `lang/po/<cultura>.po` a partir de `lang/source.pot`.

   Mantenha o inglês no `lang/source.pot`. O fluxo normal usa os `msgid` diretamente.

2. Preencha os metadados. O exemplo usa português brasileiro. Ajuste
   `Language`, `Language-Team` e `Plural-Forms` para o idioma escolhido.

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

3. Traduza os `msgstr` e preserve os `msgid`.

4. Adicione o valor público ao bloco `language` em `src/MpvNet.Windows/Resources/editor_conf.txt`.

   ```text
   option = portuguese-brazil
   ```

   O nome deve seguir o estilo dos valores existentes, como `chinese-china`, `portuguese-brazil` e `portuguese-portugal`.

5. Registre o idioma na tabela do [catálogo central](../../src/MpvNet/Services/LanguageCatalog.cs).
   Informe o valor público de `language`, a cultura BCP 47, a pasta do
   catálogo gettext e os aliases de entrada. Para um idioma traduzido,
   indique a presença do catálogo no campo `HasGettextCatalog`.

6. Ajuste o fallback central para as regiões específicas quando necessário.
   Preserve o comportamento dos demais idiomas.

   Atualize também `Package.appxmanifest` (culturas declaradas) e o payload
   explícito de `MpvNet.Pacote.wapproj`. `validate-package-contents.ps1` exige um
   `.mo` não vazio para cada `.po`, inclusive em ZIP/MSIX aninhados.

7. Gere os arquivos `.mo` na raiz do repositório.

   ```powershell
   .\lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
   ```

8. Confira se o arquivo foi criado no diretório do idioma.

   ```text
   src/MpvNet.Windows/bin/Debug/win-x64/Locale/pt_BR/LC_MESSAGES/mpvnet.mo
   ```

## Como adicionar uma nova variante regional

1. Não criar novo arquivo `.po` nem pasta `Locale` se a variante usa texto igual
   ao idioma base já existente.
2. Adicionar a variante em `LocalizationCultureResolver` apontando para a
   cultura base oficial.
3. Ampliar os testes de idioma em `src/MpvNet.Tests/LocalizationExpansionTests.cs`
   e os casos existentes em `Program.cs` quando aplicável, cobrindo a resolução
   da variante e o fallback final.
4. Atualize a tabela de variantes.

Exemplo: para mapear uma nova variante de espanhol, adicionar a cultura ao mapa
central apontando para `es`; não criar `lang/po/es_MX.po`.

## O que preservar

- Não renomear idiomas existentes.
- Não reintroduzir `language=system`.
- Não criar `lang/po/en.po` sem uma decisão explícita de mudar o contrato de localização.
- Manter `lang/source.pot` como base inglesa oficial para paridade dos `.po`.
- Não remover nem substituir a referência histórica a Transifex/upstream.
- Não tratar `.resx` como fonte principal da tradução gettext da interface.
- Não alterar o formato de `mpvnet.conf`; o usuário deve continuar podendo definir `language=<valor>`.
- Não misturar `language` com preferência de áudio ou legenda; para mídia, preservar `alang` e `slang` do mpv.

<a name="extracao-do-catalogo-fonte"></a>

## Extração do catálogo-fonte

O script `lang/update-gettext-catalogs.ps1` gera a lista de fontes em um arquivo
temporário, sempre com caminhos relativos ao repositório. Não versionar novamente
`lang/cs-files.txt`: listas persistidas acumulam caminhos absolutos da máquina e
fontes geradas. A extração ignora `bin`, `obj`, `artifacts`, `AppPackages`,
`.venv`, o projeto de testes e `Resources.Designer.cs`, mas inclui os textos
declarados por `AddMenuItem(...)`, XAML e `editor_conf.txt`.

<a name="validacao-recomendada"></a>

## Validação recomendada

Execute os comandos abaixo na raiz do repositório.

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

O comando `.\lang\validate-po-files.ps1 -ValidateOnly` verifica a paridade. Ele confirma que `lang/source.pot` não tem duplicatas, que cada `.po` traduzido corresponde ao `source.pot` e que `lang/po/en.po` não foi criado indevidamente.

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

### Verificação manual

- iniciar com `mpvnet.exe --language=portuguese-brazil`;
- iniciar com `mpvnet.exe --language=portuguese-portugal`;
- iniciar com `mpvnet.exe --language=spanish`;
- iniciar com `mpvnet.exe --language=italian`;
- iniciar com `mpvnet.conf` contendo `language=portuguese-brazil`;
- confirmar que um idioma inválido continua caindo para inglês;
- confirmar que idiomas existentes, como `german` e `chinese-china`, continuam funcionando;
- em release, confirmar que o ZIP contém os arquivos `.mo` esperados em `Locale/<cultura>/LC_MESSAGES/mpvnet.mo`.

## Riscos

Um valor divergente entre o catálogo, a pasta de saída e o WPF pode fazer a interface voltar ao texto-fonte. Confira os campos de `pt-BR`.

| Item | Valor |
| --- | --- |
| Opção em `mpvnet.conf` | `language=portuguese-brazil` |
| Arquivo PO | `lang/po/pt_BR.po` |
| Pasta Locale | `Locale/pt_BR/LC_MESSAGES/` |
| CultureInfo | `pt-BR` |

<a name="auditoria-de-traducao"></a>

## Auditoria de tradução

Execute as verificações de paridade e idioma.

```powershell
.\lang\validate-po-files.ps1 -ValidateOnly
python .\lang\audit-catalog-languages.py --fail-on-candidates
```

O primeiro comando verifica a [paridade](#validação-recomendada). Preserve os placeholders dos `msgid` e encaminhe termos ambíguos para revisão humana.

`audit-catalog-languages.py` complementa a paridade com heurísticas para prosa
inglesa idêntica, rótulos comuns sem tradução e fragmentos ingleses repetidos.
Um resultado sem candidatos reduz omissões óbvias, mas não certifica a qualidade
estilística nem substitui revisão por falantes do idioma.

<a name="inventário-da-expansão"></a>

## Registro inicial da expansão de seis idiomas — 2026-10-27

Cada novo `.po` continha **775 entradas ativas**, além do cabeçalho; nenhuma
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

- `lang/source.pot` e os 19 catálogos `.po` ficaram com 775 entradas únicas e sem chaves ausentes, extras, duplicadas ou vazias. Os seis novos catálogos também passaram pela verificação de tokens, metadados, plurais e consultas reais aos arquivos MO. As 762 entradas anteriores dos 13 catálogos existentes foram comparadas com a branch de origem; nenhuma tradução anterior foi alterada.
- `python lang/test-language-expansion.py`: 3 testes aprovados. `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-restore`: 787 testes aprovados, nenhum erro ou falha. Build normal, publicação Release, ZIP portátil e instalador foram gerados; os validadores de conteúdo e dependências nativas passaram.
- O teste de execução cobriu os seis idiomas com arquivo local, fluxo HTTP em loopback, pausa, busca, tela cheia, troca de faixas de audio/legenda, playlist e encerramento limpo. A verificação visual conferiu RTL em árabe e as telas em hindi e chinês tradicional taiwanês, em tema claro e escuro.
- O MSIX sem assinatura contém os 19 catálogos e declara os 20 idiomas. O manifesto e a versão Store foram validados em conjunto; cada novo catálogo extraído do ZIP e do MSIX corresponde as 775 entradas do PO.
- Artefatos e registros locais desta validação ficam em `artifacts/language-expansion/`; essa pasta não faz parte do commit.

A rodada não incluiu instalação ou atualização de um MSIX assinado, certificação ou publicação na Store. A revisão visual não percorreu toda a matriz de DPI, monitores, foco e drag-and-drop, e as traduções ainda precisam de uma revisão independente por falantes nativos.

## Revisão final dos catálogos e das imagens da Store — 2026-10-27

- `lang/source.pot` e os 19 arquivos `.po` tinham 780 entradas ativas, sem chaves ausentes, extras, duplicadas ou vazias. A auditoria de idioma terminou sem candidatos conhecidos de prosa inglesa, rótulos comuns sem tradução ou fragmentos ingleses recorrentes. Nomes de marcas, parâmetros do mpv, comandos, placeholders e valores técnicos literais foram preservados.
- Foram revisadas 1.248 traduções existentes em 14 catálogos. Cinco rótulos novos da interface foram acrescentados ao template e traduzidos nos 19 catálogos, totalizando 95 novas traduções.
- `App.Init` aplica `--language` antes de construir a interface inicial; as mensagens da tela ociosa e do OSC seguem o catálogo ativo. Os filtros da caixa de abertura também usam rótulos traduzidos, mantendo os padrões de extensões.
- `src/MpvNet.Tests/MpvNet.Tests.csproj` copia as DLLs nativas e os recursos de runtime para a saída efetiva dos testes e interrompe o build se `libmpv-2.dll` estiver ausente. Isso cobre a falha de execução por DLL ausente na pasta `MpvNet.Tests`.
- `python lang/test-language-expansion.py`: 6 testes aprovados. `lang/validate-po-files.ps1 -ValidateOnly`: os 19 catálogos passaram. `dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj -c Release -p:Platform=x64 --no-restore`: 843 testes aprovados, sem falhas ou erros.
- A interface foi aberta com os seis novos idiomas. As capturas `Player00.png`, `Player01.png` e `Player02.png` registram, respectivamente, a tela inicial, o menu traduzido e um quadro do vídeo fornecido. Os banners quadrado e vertical também foram localizados para cada pasta; os arquivos de metadados mantêm os títulos dos campos em português.

A auditoria automática identificou apenas omissões prováveis. A revisão editorial independente por falantes nativos ficou fora dessa rodada. A validação de interface descrita acima cobre esses seis idiomas e as telas capturadas; não equivale à matriz completa de DPI, monitores, foco e drag-and-drop.

## Cache automático de streaming — 08/11/2026

O editor inclui um único Sim/Não de `automatic-network-cache`. O identificador
persistido permanece literal; o rótulo **Automatic streaming cache** e a ajuda
são traduzidos nos 19 catálogos. As três chaves novas foram acrescentadas sem
alterar traduções anteriores: o template e os catálogos passaram a ter 807 entradas ativas naquela revisão.
Inglês continua usando o texto-fonte. A troca de idioma reutiliza a reconstrução
de rótulos existente, sem substituir os valores editados. O toggle e a ajuda
foram verificados visualmente em português, nos temas claro/escuro, com migração
legada e gravação em configuração isolada. Isso não valida todos os idiomas,
DPIs, RTL ou múltiplos monitores; os contratos de catálogo continuam automatizados.

Os seis testes de `lang/test-language-expansion.py`, a compilação dos 19 MOs e
`src/Tools/test-localization-package.ps1` passaram. O teste de pacote verifica
pasta, ZIP, contêiner Store aninhado e rejeição de catálogos ausentes/vazios;
isso não comprova instalação ou certificação na Microsoft Store.
