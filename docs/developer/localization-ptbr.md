# Localizacao e novo idioma pt-BR

## Objetivo

Este guia documenta como adicionar uma nova traducao da interface do mpv.net sem quebrar os idiomas existentes. O exemplo principal e o portugues brasileiro (`pt-BR`), mas o mesmo fluxo vale para outros idiomas.

O mpv.net usa gettext para os textos de interface. A traducao fica fora dos arquivos `.resx` principais e e carregada em runtime a partir da pasta `Locale`.

## Fluxo atual confirmado

Arquivos e responsabilidades:

- `lang/source.pot`: template gettext gerado a partir dos textos fonte.
- `lang/po/*.po`: arquivos de traducao por idioma.
- `lang/create-mo-files.ps1`: compila arquivos `.po` para `.mo`.
- `src/MpvNet.Windows/WPF/WpfTranslator.cs`: converte o valor de `language` em uma `CultureInfo`.
- `src/MpvNet.Windows/Resources/editor_conf.txt`: lista os valores aceitos pelo editor de configuracao para a opcao `language`.
- `src/Tools/release-mpv.net.ps1`: gera ou reaproveita a pasta `Locale` durante o fluxo de release.

O formato final esperado pelo runtime e:

```text
Locale/<cultura>/LC_MESSAGES/mpvnet.mo
```

Exemplo para portugues brasileiro:

```text
Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

## Implementacao atual de `pt-BR`

O fork ja possui uma traducao inicial em `lang/po/pt_BR.po`, exposta como `language=portuguese-brazil`.

## Como adicionar ou revisar `pt-BR`

1. Criar `lang/po/pt_BR.po` a partir de `lang/source.pot`.

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

O nome recomendado e `portuguese-brazil`, seguindo o estilo dos valores existentes como `chinese-china`.

5. Adicionar o idioma em `src/MpvNet.Windows/WPF/WpfTranslator.cs`:

```csharp
new("portuguese-brazil", "pt-BR", "pt"),
```

6. Ajustar o tratamento de `system` para que Windows em `pt-BR` resolva para `portuguese-brazil`, sem mudar o comportamento dos outros idiomas.

7. Gerar os arquivos `.mo`:

```powershell
.\lang\create-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
```

8. Confirmar que o arquivo foi criado:

```text
src/MpvNet.Windows/bin/Debug/win-x64/Locale/pt_BR/LC_MESSAGES/mpvnet.mo
```

## O que preservar

- Nao renomear idiomas existentes.
- Nao mudar o padrao `language=system`.
- Nao remover nem substituir a referencia historica a Transifex/upstream.
- Nao tratar `.resx` como fonte principal da traducao gettext da interface.
- Nao alterar o formato de `mpvnet.conf`; o usuario deve continuar podendo definir `language=<valor>`.

## Validacao recomendada

Validacao automatica minima:

```powershell
.\lang\create-mo-files.ps1 .\src\MpvNet.Windows\bin\Debug\win-x64
Test-Path .\src\MpvNet.Windows\bin\Debug\win-x64\Locale\pt_BR\LC_MESSAGES\mpvnet.mo
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj --no-restore
```

Validacao manual:

- iniciar com `mpvnet.exe --language=portuguese-brazil`;
- iniciar com `mpvnet.conf` contendo `language=portuguese-brazil`;
- confirmar que um idioma invalido continua caindo para ingles;
- confirmar que idiomas existentes, como `german` e `chinese-china`, continuam funcionando;
- em release, confirmar que o ZIP contem `Locale/pt_BR/LC_MESSAGES/mpvnet.mo`.

## Riscos

O maior risco e desalinhamento entre o nome publico da opcao, o nome do arquivo `.po`, o nome da pasta `Locale` e a `CultureInfo` usada pelo WPF. Para `pt-BR`, o contrato recomendado e:

| Item | Valor |
| --- | --- |
| Opcao em `mpvnet.conf` | `language=portuguese-brazil` |
| Arquivo PO | `lang/po/pt_BR.po` |
| Pasta Locale | `Locale/pt_BR/LC_MESSAGES/` |
| CultureInfo | `pt-BR` |
