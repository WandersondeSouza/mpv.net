# Localização do mpv.net

## Objetivo

Este guia documenta como adicionar ou revisar traducoes da interface do mpv.net sem quebrar os idiomas existentes.

O mpv.net usa gettext para os textos de interface. A traducao fica fora dos arquivos `.resx` principais e e carregada em runtime a partir da pasta `Locale`.

## Fluxo atual confirmado

Arquivos e responsabilidades:

- `lang/source.pot`: template gettext gerado a partir dos textos fonte e base inglesa oficial.
- `lang/po/*.po`: arquivos de traducao apenas para idiomas traduzidos.
- `lang/compile-mo-files.ps1`: compila arquivos `.po` para `.mo`.
- `src/MpvNet.Windows/WPF/WpfTranslator.cs`: converte o valor de `language` em uma `CultureInfo`.
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

Os idiomas abaixo possuem mapeamento em `WpfTranslator.cs` e valor publico aceito por `language`. Idiomas traduzidos tambem possuem arquivo `.po` e pasta `Locale`; o ingles usa a base nativa em `source.pot`.

| Idioma | Valor de `language` | Arquivo PO | Pasta Locale | CultureInfo |
| --- | --- | --- | --- | --- |
| Bulgaro | `bulgarian` | `lang/po/bg.po` | `Locale/bg/LC_MESSAGES/` | `bg` |
| Chines simplificado | `chinese-china` | `lang/po/zh_CN.po` | `Locale/zh_CN/LC_MESSAGES/` | `zh-CN` |
| Ingles | `english` | nativo | nativo | `en` |
| Espanhol | `spanish` | `lang/po/es.po` | `Locale/es/LC_MESSAGES/` | `es` |
| Frances | `french` | `lang/po/fr.po` | `Locale/fr/LC_MESSAGES/` | `fr` |
| Alemao | `german` | `lang/po/de.po` | `Locale/de/LC_MESSAGES/` | `de` |
| Japones | `japanese` | `lang/po/ja.po` | `Locale/ja/LC_MESSAGES/` | `ja` |
| Coreano | `korean` | `lang/po/ko.po` | `Locale/ko/LC_MESSAGES/` | `ko` |
| Polones | `polish` | `lang/po/pl.po` | `Locale/pl/LC_MESSAGES/` | `pl` |
| Portugues do Brasil | `portuguese-brazil` | `lang/po/pt_BR.po` | `Locale/pt_BR/LC_MESSAGES/` | `pt-BR` |
| Portugues de Portugal | `portuguese-portugal` | `lang/po/pt_PT.po` | `Locale/pt_PT/LC_MESSAGES/` | `pt-PT` |
| Russo | `russian` | `lang/po/ru.po` | `Locale/ru/LC_MESSAGES/` | `ru` |
| Turco | `turkish` | `lang/po/tr.po` | `Locale/tr/LC_MESSAGES/` | `tr` |

O valor `system` continua selecionando o idioma do Windows quando houver mapeamento conhecido; caso contrario, o fallback continua sendo ingles. No editor de configuracao, `language=system` e exibido como o idioma efetivo detectado, mas continua sendo salvo como `system` se o usuario nao trocar manualmente o combo.

Quando `alang` esta definido, a interface usa o primeiro idioma suportado dessa lista de prioridade como preferencia de idioma. Essa escolha vem do valor declarado em `alang`, nao do idioma real da faixa de audio ou legenda selecionada pelo arquivo em reproducao. Se nenhum item de `alang` for suportado, a interface usa o idioma do Windows quando houver mapeamento conhecido; caso contrario, volta para ingles. A legenda (`slang`/`sid`) nao participa dessa escolha.

## Implementacao atual de portugues e espanhol

O fork possui traducoes gettext para portugues brasileiro (`language=portuguese-brazil`), portugues de Portugal (`language=portuguese-portugal`) e espanhol (`language=spanish`).

## Como adicionar ou revisar um idioma

1. Criar o arquivo `lang/po/<cultura>.po` a partir de `lang/source.pot`.

   Nao criar `lang/po/en.po`: ingles e nativo e deve permanecer representado pelo proprio `lang/source.pot`.

2. Ajustar o cabecalho do novo `.po` para UTF-8 e plural do portugues brasileiro:

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

5. Adicionar o idioma em `src/MpvNet.Windows/WPF/WpfTranslator.cs`:

```csharp
new("portuguese-brazil", "pt-BR", "pt"),
```

6. Quando necessario, ajustar o tratamento de `system` para regioes especificas, como `pt-BR` e `pt-PT`, sem mudar o comportamento dos outros idiomas.

7. Gerar os arquivos `.mo`:

```powershell
.\lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
```

8. Confirmar que o arquivo foi criado:

```text
src/MpvNet.Windows/bin/Debug/win-x64/Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

## O que preservar

- Nao renomear idiomas existentes.
- Nao mudar o padrao `language=system`.
- Nao criar `lang/po/en.po` sem uma decisao explicita de mudar o contrato de localizacao.
- Manter `lang/source.pot` como base inglesa oficial para paridade dos `.po`.
- Nao remover nem substituir a referencia historica a Transifex/upstream.
- Nao tratar `.resx` como fonte principal da traducao gettext da interface.
- Nao alterar o formato de `mpvnet.conf`; o usuario deve continuar podendo definir `language=<valor>`.

## Validacao recomendada

Validacao automatica minima:

```powershell
.\lang\validate-po-files.ps1 -ValidateOnly
.\lang\compile-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\pt_BR\LC_MESSAGES\mpvnet.mo
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\pt_PT\LC_MESSAGES\mpvnet.mo
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\es\LC_MESSAGES\mpvnet.mo
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore
```

O comando `.\lang\validate-po-files.ps1 -ValidateOnly` e a validacao principal de paridade: ele confirma que `lang/source.pot` nao tem duplicatas, que cada `.po` traduzido corresponde ao `source.pot` e que `lang/po/en.po` nao foi criado indevidamente.

Validacao manual:

- iniciar com `mpvnet.exe --language=portuguese-brazil`;
- iniciar com `mpvnet.exe --language=portuguese-portugal`;
- iniciar com `mpvnet.exe --language=spanish`;
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
