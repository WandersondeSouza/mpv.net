# Mapa Inicial do Projeto mpv.net

## Objetivo

Este documento organiza o entendimento estrutural do projeto mpv.net com base na árvore local atual do fork.

---

# Visão geral

O projeto está organizado em uma solução principal dentro de `src/`:

```text
src/
  MpvNet.sln
  Directory.Build.props
  Directory.Packages.props
  MpvNet/
  MpvNet.Windows/
  NGettext.Wpf/
  MpvNet.Extension/
  Tools/
  Setup/
```

Responsabilidades principais:

1. `MpvNet`: núcleo de reprodução, comandos, configuração, input, integração com libmpv e extensões;
2. `MpvNet.Windows`: executável Windows, janela principal, WPF/WinForms, comandos de UI e integração com o sistema;
3. `NGettext.Wpf`: suporte de localização WPF, documentado em `docs/developer/localization-ptbr.md`;
4. `MpvNet.Extension/ExampleExtension`: exemplo de extensão .NET;
5. `Tools`: scripts de atualização/release;
6. `Setup`: empacotamento com Inno Setup.

---

# Projetos

| Projeto | Tipo | Target | Papel |
| --- | --- | --- | --- |
| `src/MpvNet/MpvNet.csproj` | Biblioteca | `net10.0` | Núcleo compartilhado, assembly `libmpvnet`. |
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | Aplicação Windows | `net10.0-windows7.0` | Executável `mpvnet.exe`, WPF + WinForms. |
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | Biblioteca | legado/packages.config | Integração NGettext com WPF. |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | Exemplo | extensão .NET | Demonstra extensão carregável. |

---

# Entry points reais

## Aplicação Windows

Arquivo: `src/MpvNet.Windows/Program.cs`

Responsabilidades:

- inicializar WinForms;
- configurar tradução;
- tratar exceções globais;
- processar registro de associações;
- iniciar `App.Init()` e `Theme.Init()`;
- aplicar controle de instância única;
- processar comandos de terminal;
- abrir `WinForms.MainForm`.

## Janela principal

Arquivo: `src/MpvNet.Windows/WinForms/MainForm.cs`

Responsabilidades:

- hospedar a área de vídeo via handle nativo;
- integrar eventos do player com a UI;
- controlar menu, fullscreen, janela e input visual;
- iniciar processamento de arquivos de linha de comando após criação da janela.

---

# Áreas principais

## Inicialização

Arquivos:

- `src/MpvNet.Windows/Program.cs`
- `src/MpvNet/App.cs`
- `src/MpvNet/CommandLine.cs`
- `src/MpvNet.Windows/WinForms/MainForm.cs`

Fluxo resumido:

1. `Program.Main`;
2. `App.Init`;
3. `Theme.Init`;
4. controle de instância;
5. `MainForm`;
6. `Player.Init`;
7. processamento pós-inicialização da linha de comando.

---

## Integração com mpv/libmpv

Arquivos:

- `src/MpvNet/Player.cs`
- `src/MpvNet/MpvClient.cs`
- `src/MpvNet/Native/LibMpv.cs`

Responsável por:

- criar e destruir contexto mpv;
- definir propriedades iniciais;
- chamar `mpv_initialize`;
- criar cliente `mpvnet`;
- enviar comandos;
- observar propriedades;
- receber eventos;
- sincronizar estado de reprodução.

---

## Configuração

Arquivos:

- `src/MpvNet/Player.cs`
- `src/MpvNet/App.cs`
- `src/MpvNet/Settings.cs`
- `src/MpvNet/InputConf.cs`
- `src/MpvNet.Windows/UI/Theme.cs`
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`

Arquivos de usuário:

- `mpv.conf`;
- `mpvnet.conf`;
- `input.conf`;
- `settings.xml`;
- `theme.conf`;
- `global-input.conf`.

---

## Comandos e atalhos

Arquivos:

- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/GuiCommand.cs`;
- `src/MpvNet/InputHelp.cs`;
- `src/MpvNet/InputConf.cs`;
- `src/MpvNet/CommandLine.cs`.

Comandos do mpv continuam sendo delegados ao libmpv. Comandos específicos do mpv.net são acionados principalmente por `script-message-to mpvnet`.

---

## Interface gráfica

Arquivos/pastas:

- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/WPF/`;
- `src/MpvNet.Windows/UI/`;
- `src/MpvNet.Windows/Native/`;
- `src/MpvNet.Windows/Help/`.

A UI mistura WinForms para a janela principal e WPF para janelas auxiliares como editores e About.

## Localização

Arquivos:

- `lang/source.pot`;
- `lang/po/*.po`;
- `lang/compile-mo-files.ps1`;
- `src/MpvNet.Windows/WPF/WpfTranslator.cs`;
- `src/MpvNet.Windows/Resources/editor_conf.txt`.

O fluxo gettext da interface e a inclusão de novos idiomas, incluindo `pt-BR`, estão documentados em `docs/developer/localization-ptbr.md`.

---

## Scripts e extensões

Arquivos:

- `src/MpvNet/ExtensionLoader.cs`;
- `src/MpvNet.Extension/ExampleExtension/`;
- `src/MpvNet/InputHelp.cs`;
- `src/MpvNet/CommandLine.cs`.

Pastas de configuração:

- `scripts`;
- `script-opts`;
- `extensions`.

---

## Build e release

Arquivos:

- `src/Tools/build-release-package.ps1`;
- `src/Tools/update-mpv-runtime.ps1`;
- `src/Setup/Inno/build-windows-installer.iss`;
- `src/MpvNet.Windows/MpvNet.Windows.csproj`.

O script de release atual publica x64, copia DLLs nativas x64, empacota ZIP com 7-Zip, gera instalador x64 via Inno Setup e cria release no GitHub com `gh`.

---

# Áreas críticas para manutenção

## Compatibilidade

Compatibilidade com mpv é prioridade. Evite alterar nomes de opções, comandos e comportamento de linha de comando sem migração.

## Fullscreen/UI

Mudanças podem impactar input, menu, OSC, DPI, múltiplos monitores e o handle nativo usado pelo libmpv.

## Configuração

Mudanças podem quebrar instalações normais, portáteis ou baseadas em `MPVNET_HOME`.

## Comandos

Comandos deprecated ainda podem ser usados por scripts ou configurações antigas. Não remover sem migração documentada.

---

# Pendências futuras

- Validar build real em ambiente Windows completo.
- Confirmar dependências nativas mínimas para execução e release.
- Documentar fluxo de eventos detalhado com base em `MpvClient.EventLoop`.
- Documentar fluxo completo de empacotamento após teste real.
- Criar tabela gerada automaticamente de comandos e opções.
